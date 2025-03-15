using BezierZUtility;
using BezierZUtility.Editor;
using RectEx;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

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

			_selectEndPoints.DoSelectionRect(current, _curve.UniqueEndPoints.Select(p=>TransformPoint(p)), _controlID);

			if (!current.IsRepaint()) return;
			DrawPoints();
		}

		private void DrawPoints()
		{
			var cam = Camera.current;
			var camPos = cam.transform.position;
			Handles.color = Color.white * .8f;
			for (int i = 0; i < _curve.PointCount; i++)
			{
				var point = _curve.Points[i];
				var pos = TransformPoint(point);
				float size = HandleUtility.GetHandleSize(pos) * _handleSize;
				if (point.IsEndPoint)
				{
					GUIUtils.DrawCircle(pos, pos - camPos, size, _selectEndPoints.Indexes.Contains(i / 3), 1.5f, 24);
					Handles.Label(pos, (i == _curve.PointCount - 1 && _curve.IsClosed ? "     / " : "  ") + i.ToString());
				}
				else
				{
					Point endpoint = point.isRightHandle ? _curve.Points[i - 1] : _curve.Points[i + 1];
					Handles.DrawAAPolyLine(1.5f, GetHandleShapePoints(
						pos, pos - camPos,
						_localToWorldMatrix.rotation * endpoint.forward,
						size));
					Handles.DrawAAPolyLine(1.5f, TransformPoint(endpoint), pos);
				}
			}
			Handles.color = Color.white;

			Vector3[] GetHandleShapePoints(Vector3 pos, Vector3 normal, Vector3 forward, float size)
			{
				var cross = normal.normalized.Cross(forward).normalized;
				Vector3 d1 = (cross + forward) * size;
				Vector3 d2 = (cross - forward) * size;
				return new Vector3[] { pos - d1, pos - d2, pos + d1, pos + d2, pos - d1 };
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

	public class SelectRectContext
	{
		private Vector2 _mouseDownPos;

		public IEnumerable<int> Indexes => _oldIndexes.Concat(_newIndexes);
		private List<int> _oldIndexes = new();
		private List<int> _newIndexes = new();

		public void DoSelectionRect(Event current, IEnumerable<Vector3> points, int controlId)
		{
			if (current.IsMouseDown(0))
			{
				_mouseDownPos = current.mousePosition;
				if (!current.shift) _oldIndexes.Clear();
			}
			if (current.IsMouseUp(0)) {
				_mouseDownPos = default;
				_oldIndexes.AddRange(_newIndexes);
				_newIndexes.Clear();
			}

			if (!current.IsRepaint() || _mouseDownPos == default) return;
			var rect = new Rect(_mouseDownPos, current.mousePosition - _mouseDownPos).Abs();

			Handles.BeginGUI();
			EditorStyles.selectionRect.Draw(rect, GUIContent.none, controlId);
			Handles.EndGUI();

			_newIndexes.Clear();
			rect = rect.Extend(15, 15);
			int i = 0;
			foreach(var point in points)
			{
				if (rect.Contains(HandleUtility.WorldToGUIPoint(point)))
				{
					if (current.control)
						_newIndexes.Remove(i);
					else
						_newIndexes.Add(i);
				}
				i++;
			}
		}
	}
	public static class EventExtensions
	{
		public static bool IsMouseDown(this Event e, int button) => e.type == EventType.MouseDown && e.button == button;
		public static bool IsMouseUp(this Event e, int button) => e.type == EventType.MouseUp && e.button == button;
		public static bool IsMouseDrag(this Event e, int button) => e.type == EventType.MouseDrag && e.button == button;
		public static bool IsKeyDown(this Event e, KeyCode key) => e.type == EventType.KeyDown && e.keyCode == key;
		public static bool IsKeyUp(this Event e, KeyCode key) => e.type == EventType.KeyUp && e.keyCode == key;
		public static bool IsRepaint(this Event e) => e.type == EventType.Repaint;
		public static bool IsLayout(this Event e) => e.type == EventType.Layout;
	}
}