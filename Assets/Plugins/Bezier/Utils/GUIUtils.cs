using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Utility.Editor {
	public static class GUIUtils {

		public static void DrawCircle(Vector2 center, float radius, bool filled = false, int quality = 12) {
			var points = new Vector3[quality + 1];
			for (int i = 0; i <= quality; i++) {
				points[i] = new Vector2(center.x + Mathf.Sin(Mathf.PI * 2 * i / quality) * radius, center.y + Mathf.Cos(Mathf.PI * 2 * i / quality) * radius);
			}
			if (filled) {
				Handles.DrawAAConvexPolygon(points);
			} else {
				Handles.DrawAAPolyLine(points);
			}
		}

		public static void DrawCircle(Vector3 center, Vector3 normal, float radius, bool filled = false, int quality = 12, Vector3 startFrom = default)
		{
			if (startFrom == default) startFrom = Vector3.up;
			Vector3 from = Vector3.Cross(normal, Vector3.Cross(normal, startFrom)).normalized;
			Quaternion q = Quaternion.AngleAxis(360f / quality, normal);
			var points = new Vector3[quality + 1];
			for (int i = 0; i <= quality; i++)
			{
				points[i] = Handles.matrix * (center + from * radius);
				from = q * from;
			}
			if (filled)
			{
				Handles.DrawAAConvexPolygon(points);
			}
			else
			{
				Handles.DrawAAPolyLine(points);
			}
		}

		public static void DrawRectangle(Vector3 position, Quaternion rotation, Vector2 size)
		{
			Vector3 vector = rotation * new Vector3(size.x, 0f, 0f);
			Vector3 vector2 = rotation * new Vector3(0f, size.y, 0f);
			Vector3[] points = new Vector3[5];
			points[0] = position + vector + vector2;
			points[1] = position + vector - vector2;
			points[2] = position - vector - vector2;
			points[3] = position - vector + vector2;
			points[4] = position + vector + vector2;
			Handles.DrawAAPolyLine(points);
		}
	}
}