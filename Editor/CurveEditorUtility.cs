using BezierZUtility;
using BezierZUtility.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BezierCurveZ.Editor
{
	internal static class CurveEditorUtility
	{
		internal static void ProcessClosestPoints(Event current, Curve curve, bool ignoreHandles, bool ignoreEndpoints,
			ref int closestIndex, Matrix4x4 localToWorldMatrix)
		{
			Vector2 mousePos = current.mousePosition;
			//distance to point in screen pixels when tools will show up
			var minDist = 100f;
			closestIndex = -1;
			int len = curve.PointCount - (curve.IsClosed ? 3 : 0);
			for (int i = 0; i < len; i++)
			{
				var point = curve.Points[i];
				if ((ignoreHandles && !point.IsEndPoint) || (ignoreEndpoints && point.IsEndPoint) || point.IsLinear) continue;

				var dist = HandleUtility.WorldToGUIPoint(Transform(point)).DistanceTo(mousePos);
				if (dist < minDist)
				{
					minDist = dist;
					closestIndex = i;
				}
			}
			Vector3 Transform(Vector3 pos) => localToWorldMatrix.MultiplyPoint3x4(pos);
		}
		internal static void CurveTools(Event current, Tool tool, Curve curve, UnityEngine.Object targetObject,
			bool multipleSelected, IEnumerable<int> selectedIndexes,
			int closestIndex, ref Vector3 moveOriginalPosition, Matrix4x4 localToWorldMatrix, Matrix4x4 worldToLocalMatrix)
		{
			if (!hasClosestPoint() || (multipleSelected && !selectedIndexes.Contains(closestIndex))) return;
			Point point = closestPoint();
			var editedPosition = localToWorldMatrix.MultiplyPoint3x4(point);
			var editedRotation = point.IsEndPoint ? localToWorldMatrix.rotation * point.rotation : curve.GetClosestEndPoint(closestIndex).rotation;

			if (tool == Tool.Move)
			{
				var wasMouseDown = current.IsMouseDown(0);
				EditorGUI.BeginChangeCheck();
				var pos = Vector3.zero;
				if (current.shift)
					pos = Handles.FreeMoveHandle(editedPosition, HandleUtility.GetHandleSize(editedPosition) * .16f, Vector3.one * .2f, Handles.RectangleHandleCap);
				else
					pos = Handles.DoPositionHandle(editedPosition, editedRotation);

				if (wasMouseDown && GUIUtility.hotControl != 0)
					moveOriginalPosition = editedPosition;

				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Point position changed");

					if (!multipleSelected)
						curve.SetPointPosition(closestIndex, worldToLocalMatrix.MultiplyPoint3x4(pos));
					else
					{
						var delta = worldToLocalMatrix.MultiplyVector(pos - editedPosition);
						foreach (var ind in selectedIndexes)
							curve.SetPointPosition(ind, curve.Points[ind] + delta);
					}
					return;
				}
			}
			else if (tool == Tool.Rotate)
			{
				EditorGUI.BeginChangeCheck();
				float handleSize = HandleUtility.GetHandleSize(editedPosition);
				//get delta rotation in curve space
				var delta = worldToLocalMatrix.rotation.Inverted() * editedRotation.Inverted() *
					Handles.DoRotationHandle(editedRotation, editedPosition);

				if (multipleSelected)
					foreach (var ind in selectedIndexes)
					{
						float size = HandleUtility.GetHandleSize(TransformPoint(curve.Points[ind]));
						GUIUtils.DrawAxes(TransformPoint(curve.Points[ind]), localToWorldMatrix.rotation * curve.Points[ind].rotation, size, 3f);
					}
				else
					GUIUtils.DrawAxes(editedPosition, localToWorldMatrix.rotation * curve.Points[closestIndex].rotation, handleSize, 3f);

				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Point rotation changed");

					if (multipleSelected)
					{
						Vector3 localEditedPosition = InverseTransformPoint(editedPosition);
						foreach (var ind in selectedIndexes)
						{
							var pos = delta * (curve.Points[ind] - localEditedPosition) + localEditedPosition;
							curve.SetPointPosition(ind, pos);
							curve.AddEPRotation(curve.GetSegmentIndex(ind), delta);
						}
					}
					else
					{
						curve.AddEPRotation(curve.GetSegmentIndex(closestIndex), delta);
					}
					return;
				}
			}

			return;

			bool hasClosestPoint() => closestIndex != -1;
			Point closestPoint() => hasClosestPoint() ? curve.Points[closestIndex] : default;
			Vector3 InverseTransformPoint(Vector3 position) => worldToLocalMatrix.MultiplyPoint3x4(position);
			Vector3 TransformPoint(Vector3 position) => localToWorldMatrix.MultiplyPoint3x4(position);

		}

		/// <summary>
		/// Draws Endpoints and Handles
		/// </summary>
		internal static void DrawPoints(Curve curve, Func<int, bool> isSelected, Matrix4x4 localToWorldMatrix, float handleSize)
		{
			var cam = Camera.current;
			var camPos = cam.transform.position;
			Handles.color = Color.white * .8f;
			for (int i = 0; i < curve.PointCount; i++)
			{
				var point = curve.Points[i];
				var pos = localToWorldMatrix.MultiplyPoint3x4(point);
				float size = HandleUtility.GetHandleSize(pos) * handleSize;
				if (point.IsEndPoint)
				{
					GUIUtils.DrawCircle(pos, pos - camPos, size, isSelected(i), 1.5f, 24);
					Handles.Label(pos, (i == curve.PointCount - 1 && curve.IsClosed ? "     / " : "  ") + i.ToString());
				}
				else
				{
					Point endpoint = point.isRightHandle ? curve.Points[i - 1] : curve.Points[i + 1];
					Handles.DrawAAPolyLine(1.5f, GetHandleShapePoints(
						pos, pos - camPos,
						localToWorldMatrix.rotation * endpoint.forward,
						size));
					Handles.DrawAAPolyLine(1.5f, localToWorldMatrix.MultiplyPoint3x4(endpoint), pos);
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
		private static void RepaintSceneViews()
		{
			foreach (SceneView sv in SceneView.sceneViews)
			{
				sv.Repaint();
			}
		}
	}
}