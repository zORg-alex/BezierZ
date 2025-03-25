using BezierZUtility;
using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BezierCurveZ.Editor
{
	public class AlternativeCurveEditor : SubscribableEditor<Curve>
	{
		private static AlternativeCurveEditor _instance;
		public static AlternativeCurveEditor Instance { [DebuggerStepThrough] get => Utils.SingletonGetNew(ref _instance); }

		private Object _targetObject;
		private Transform _targetTransform;
		private bool _targetIsGameObject;
		private int _controlID;
		private Curve _curve;
		private Matrix4x4 _worldToLocalMatrix;
		private Matrix4x4 _localToWorldMatrix;
		private SelectRectContext _selectEndPoints;
		private SelectRectContext _selectAllPoints;
		private float _handleSize = .2f;
		private Tool _internalTool;

		private int _closestIndex;
		private bool _selectHandlesOnly;
		private bool _blockClosePointsUpdate;
		private bool _isMoved;
		private bool _isCutting;
		private Vector3 _moveOriginalPosition;
		private Vector3 _cutPoint;

		private bool _selectEndpointsOnly => _internalTool == Tool.Rotate || _selectAllPoints.Count > 1;

		public override void Start(Curve curve, SerializedProperty property)
		{
			base.Start(curve, property);
			_internalTool = Tools.current;
			curve.IsInAlternateEditMode = true;
			_instance = this;
			_targetObject = property.serializedObject.targetObject;
			if ((Component)_targetObject is Component c)
			{
				_targetTransform = c.transform;
				_targetIsGameObject = true;
			}
			_curve = curve;
			_worldToLocalMatrix = _targetTransform.worldToLocalMatrix;
			_localToWorldMatrix = _targetTransform.localToWorldMatrix;
			_selectEndPoints = new();
			_selectAllPoints = new();
		}

		public override void Stop()
		{
			if (field != null)
				field.IsInAlternateEditMode = false;
			base.Stop();
			_instance = null;
			Tools.current = _internalTool;
		}

		public override void OnSceneGUI()
		{
			var current = Event.current;
			_controlID = GUIUtility.GetControlID(932795649, FocusType.Passive);
			if (current.type == EventType.Layout)
				//Magic thing to stop mouse from selecting other objects
				HandleUtility.AddDefaultControl(_controlID);
			//Prevent moving screen with ViewTool instead of selecting, prevent modifying parent Transform
			if (Tools.current != Tool.None)
			{
				_internalTool = Tools.current;
				Tools.current = Tool.None;
			}

			if (!_selectEndPoints.IsSelecting() && !_blockClosePointsUpdate)
				CurveEditorUtility.ProcessClosestPoints(current, _curve, _selectEndpointsOnly, _selectHandlesOnly,
					ref _closestIndex, _localToWorldMatrix);
			ProcessInputs(current);

			if (_closestIndex == -1)
				_selectEndPoints.DoSelectionRect(current, _curve.UniqueEndPoints.Select(p => TransformPoint(p)), _controlID);
			else
				_selectEndPoints.Cancel();

			CurveEditorUtility.DrawPoints(_curve, EndpointIsSelected, _localToWorldMatrix, _handleSize);

			CurveEditorUtility.CurveTools(current, _internalTool, _curve, _targetObject,
				_selectEndPoints.Count > 1, _selectEndPoints.Indexes.Select(i => i * 3), _closestIndex,
				ref _moveOriginalPosition,
				_localToWorldMatrix, _worldToLocalMatrix);
			_blockClosePointsUpdate = GUIUtility.hotControl != 0;
			_isMoved = GUIUtility.hotControl != 0 && _internalTool == Tool.Move;
			if (_isCutting)
				WhileCutting(current);

			if (!current.IsRepaint()) return;



			bool EndpointIsSelected(int i) => _selectEndPoints.Indexes.Contains(i / 3);
		}

		private void ProcessInputs(Event current)
		{
			if (current.IsKeyDown(KeyCode.Delete))
			{
				Undo.RecordObject(_targetObject, "Curve Delete");
				current.Use();
				if (_selectEndPoints.Count > 1)
				{
					//var orderedEnumerable = _selectEndPoints.Indexes.OrderBy(i => -i).ToList();
					//foreach (var ind in orderedEnumerable)
					//	_curve.DissolveEP(ind);
					_curve.RemoveMany(_selectEndPoints.Indexes.Select(i => i * 3));
				}
				else if (_closestIndex >= 0)
					_curve.Remove(_closestIndex);

				_closestIndex = -1;
			}
			if (current.IsKeyUp(KeyCode.Delete))
				current.Use();

			if (current.IsKeyDown(KeyCode.C))
				_selectHandlesOnly = true;
			else if (current.IsKeyUp(KeyCode.C))
				_selectHandlesOnly = false;

			if (current.IsKeyDown(KeyCode.V))
				StartCutting();
			if (_isCutting && (current.IsMouseDown(1) || current.IsKeyDown(KeyCode.Escape)))
				CancelCutting();
			if (_isCutting && current.IsMouseDown(0))
				FinishCutting();


			void StartCutting()
			{
				if (_isMoved)
					Extrude();
				else
					_isCutting = true;
			}
			void FinishCutting()
			{
				Undo.RecordObject(_targetObject, "Curve Cut");
				_curve.SplitCurveAt(InverseTransformPoint(_cutPoint));
			}
			void Extrude()
			{
				Undo.RecordObject(_targetObject, "Curve Extrude");
				Point closestPoint = _curve.Points[_closestIndex];
				var dir = closestPoint.forward.Dot(closestPoint - _moveOriginalPosition);

				int segmentIndex = _curve.GetSegmentIndex(_closestIndex);
				_curve.SetPointPosition(_closestIndex, closestPoint);
				if (_curve.IsClosed) segmentIndex %= _curve.SegmentCount;

				if (segmentIndex == _curve.SegmentCount)
				{
					_curve.SplitCurveAt(segmentIndex - 1, 1f);
					_curve.UpdatePosition(_closestIndex - 1);
					_closestIndex += 3;
				}
				else
				{
					_curve.SplitCurveAt(segmentIndex, 0f);
					_curve.UpdatePosition(_closestIndex + 4);
				}
				closestPoint = _curve.Points[_closestIndex];
			}

			void CancelCutting() => _isCutting = false;
		}

		private void WhileCutting(Event current)
		{
			if (current.IsMouseMove())
			{
				_cutPoint = HandleUtility.ClosestPointToPolyLine(_curve.VertexDataPoints.SelectArray(v => TransformPoint(v)));
			}
			else if (current.IsRepaint())
			{
				Handles.color = new Color(.7f, .3f, .2f);
				Handles.DrawSolidDisc(_cutPoint, _cutPoint - Camera.current.transform.position, HandleUtility.GetHandleSize(_cutPoint) * .1f);
				Handles.color = Color.white;
			}
		}

		private Vector3 InverseTransformPoint(Vector3 position) => _worldToLocalMatrix.MultiplyPoint3x4(position);
		private Vector3 TransformPoint(Vector3 position) => _localToWorldMatrix.MultiplyPoint3x4(position);

		private Vector3[] GetEndpointsPositions(Curve curve)
		{
			var _endPoints = new Vector3[_curve.EndPointCount];
			int i = 0;
			foreach (var p in _curve.EndPoints)
				_endPoints[i++] = p.position;
			return _endPoints;
		}
	}
}