using UnityEngine;

namespace BezierCurveZ.Editor
{
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