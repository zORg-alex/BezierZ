using BezierZUtility;
using BezierZUtility.Editor;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
			var minDist = 50f;
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
		internal static bool CurveTools(Event current, Tool tool, Curve curve, UnityEngine.Object targetObject,
			bool multipleSelected, IEnumerable<int> selectedIndexes,
			int _closestIndex, Matrix4x4 localToWorldMatrix, Matrix4x4 worldToLocalMatrix)
		{
			if (!hasClosestPoint()) return false;
			Point point = closestPoint();
			var editedPosition = localToWorldMatrix.MultiplyPoint3x4(point);
			var editedRotation = localToWorldMatrix.rotation * point.rotation;

			if (tool == Tool.Move)
			{
				EditorGUI.BeginChangeCheck();
				var pos = Vector3.zero;
				if (current.shift)
					pos = Handles.FreeMoveHandle(editedPosition, HandleUtility.GetHandleSize(editedPosition) * .16f, Vector3.one * .2f, Handles.RectangleHandleCap);
				else
					pos = Handles.DoPositionHandle(editedPosition, editedRotation);

				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Point position changed");

					if (!multipleSelected)
						curve.SetPointPosition(_closestIndex, worldToLocalMatrix.MultiplyPoint3x4(pos));
					else
					{
						var delta = worldToLocalMatrix.MultiplyVector(pos - editedPosition);
						foreach (var ind in selectedIndexes)
							curve.SetPointPosition(ind, curve.Points[ind] + delta);
					}
					return true;
				}
			}

			return false;

			bool hasClosestPoint() => _closestIndex != -1;
			Point closestPoint() => hasClosestPoint() ? curve.Points[_closestIndex] : default;
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
	}
}