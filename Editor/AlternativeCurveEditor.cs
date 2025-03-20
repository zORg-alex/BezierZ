using BezierZUtility;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
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

		private bool _selectEndpointsOnly => _internalTool == Tool.Rotate || _selectAllPoints.Count > 1;

		public override void Start(Curve curve, SerializedProperty property)
		{
			base.Start(curve, property);
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

			CurveEditorUtility.CurveTools(current, _internalTool, _curve, _targetObject,
				_selectEndPoints.Count > 1, _selectEndPoints.Indexes.Select(i => i * 3), _closestIndex,
				_localToWorldMatrix, _worldToLocalMatrix);
			_blockClosePointsUpdate = GUIUtility.hotControl != 0;

			if (!current.IsRepaint()) return;

			CurveEditorUtility.DrawPoints(_curve, EndpointsSelected, _localToWorldMatrix, _handleSize);



			bool EndpointsSelected(int i) => _selectEndPoints.Indexes.Contains(i / 3);
		}

		private void ProcessInputs(Event current)
		{
			if (current.IsKeyDown(KeyCode.C))
				_selectHandlesOnly = true;
			else if (current.IsKeyUp(KeyCode.C))
				_selectHandlesOnly = false;
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