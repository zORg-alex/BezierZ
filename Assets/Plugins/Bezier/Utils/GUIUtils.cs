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
			Vector3 from = Vector3.Cross(normal, Vector3.Cross(normal, startFrom));
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
	}
}