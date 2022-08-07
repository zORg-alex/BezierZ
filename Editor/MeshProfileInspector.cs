using System.Reflection;
using UnityEditor;
using UnityEngine;
using Utility;
using Utility.Editor;
#if UNITY_EDITOR
#endif

namespace MeshGeneration
{
#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(MeshProfile))]
	public class MeshProfileInspector : PropertyDrawer {
		private UnityEngine.Object targetObject;
		private FieldInfo propField;
		private MeshProfile value => (MeshProfile)propField.GetValue(targetObject);

		private bool isUnfolded;
		private Vector2 pointDragStart;
		private Vector2 selectedPoint;
		private int selectedIndex = -1;
		private Vector2 hoveredPoint;
		private int hoveredIndex = -1;

		private float lastClickTime;
		private int consecutiveClickNum;

		/* Some resize stuff */
		private bool initiated;
		private static readonly int k_ResizePanelControlID = "MeshProfileInspectorResize".GetHashCode();
		private static readonly int k_PanelControlID = "MeshProfileInspector".GetHashCode();
		private Vector2 m_lastMousePos;
		private Vector2 m_DragDistance;
		private float m_DragStartSide;
		/* Some resize stuff END */

		private void Init(SerializedProperty property) {
			if (initiated) return;
			targetObject = (UnityEngine.Object)property.serializedObject.targetObject;
			propField = property.serializedObject.targetObject.GetType().GetField(property.name);
			if (graphRect.height < 10 || graphRect.width < 10) {
				graphRect.size = Vector2.one * 256;
			}
			initiated = true;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => (isUnfolded ? graphRect.height + 4 : 0) + 22f;

		private static Rect graphRect = new Rect(Vector2.zero, Vector2.one * 256);

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			Init(property);

			position.width = Screen.width;
			var labelRect = new Rect(position.position, new Vector2(position.width, 22f));
			var foldRect = new Rect(position.position + Vector2.up * 22f, new Vector2(graphRect.width + 4, graphRect.height + 4));
			var size = graphRect.size;
			graphRect.xMin = position.xMin + 2;
			graphRect.yMin = position.yMin + 24;
			graphRect.size = size;
			graphRect.width = Mathf.Min(graphRect.width, position.width + 4);

			isUnfolded = EditorGUI.Foldout(labelRect, isUnfolded, label);
			if (isUnfolded) {
				GUI.Box(foldRect, GUIContent.none, EditorStyles.helpBox);
				DrawResizeHandle();
				DrawGraph();

				Event current = Event.current;
				int hotControl = GUIUtility.hotControl;
				Rect dragRegion = new Rect(foldRect.xMax - 10, foldRect.yMax - 10, 10, 10);

				HandleResize(current, hotControl, dragRegion);
				HandleGraphInput(current, hotControl, graphRect);
			}
		}

		private void HandleGraphInput(Event current, int hotControl, Rect graphRect) {
			int controlID = GUIUtility.GetControlID(k_PanelControlID, FocusType.Passive);
			var points = value.GetPoints();
			var scaledPoints = points.SelectArray(p => TransformPoint(p * new Vector2(1, -1)));
			switch (current.GetTypeForControl(controlID)) {
				case EventType.MouseDown:
					if (!graphRect.Contains(current.mousePosition)) return;
					if (current.button == 0) {
						bool onPoint = DetectPoint(current, points, scaledPoints);
						pointDragStart = current.mousePosition;

						var relativeMousePos = (InverseTransformVector(current.mousePosition - graphRect.position) - Vector2.one) * new Vector2(1, -1);
						var clickedLineInd = GetLineClicked(relativeMousePos, out bool clickedOnLine);
						if (!onPoint && (points.Length < 2 || clickedOnLine)) {
							value.AddAt(clickedLineInd + 1, relativeMousePos);
						} else if (!onPoint)
							return;

						if (lastClickTime + .3f > Time.time)
							consecutiveClickNum++;
						else
							consecutiveClickNum = 1;
						lastClickTime = Time.time;

						m_DragDistance = default;
						//Handle DoubleClick

						GUIUtility.hotControl = controlID;
						current.Use();
					} else if (current.button == 1) {
						var onPoint = DetectPoint(current, points, scaledPoints);

						value.RemoveAt(selectedIndex);
						selectedIndex = -1;
						selectedPoint = default;

						GUIUtility.hotControl = controlID;
						current.Use();
					}
					break;
				case EventType.MouseUp:
					if (hotControl == controlID) {
						selectedPoint = value[selectedIndex];

						GUIUtility.hotControl = 0;
						current.Use();
					}
					break;
				case EventType.MouseDrag:
					this.m_DragDistance = current.mousePosition - pointDragStart;
					this.m_lastMousePos = current.mousePosition;

					value[selectedIndex] = selectedPoint + InverseTransformVector(m_DragDistance * new Vector2(1, -1));

					current.Use();
					break;
					//case EventType.MouseMove:
					//	var hovering = DetectPoint(current, points, scaledPoints, false);
					//	if (hoveredIndex == -1) break;
					//	if (hovering)
					//		current.Use();
					//	else {
					//		hoveredIndex = -1;
					//		hoveredPoint = default;
					//		current.Use();
					//	}
					//	break;
			}
		}

		private int GetLineClicked(Vector2 position, out bool clickedOnLine) {
			float minDist = float.MaxValue;
			int minDistInd = 0;
			for (int i = 0; i < value.Length; i++) {
				var p1 = value[i];
				var p2 = value.GetLoopPoint(i + 1);
				var pointOnLine = Utils.FindNearestPointOnFiniteLine(p1, p2, position);
				var dist = position.DistanceTo(pointOnLine);
				if (dist < minDist) {
					minDist = dist;
					minDistInd = i;
				}
			}

			clickedOnLine = minDist < 3f;
			return clickedOnLine ? minDistInd : 0;
		}
		private bool DetectPoint(Event current, Vector2[] points, Vector2[] scaledPoints, bool selectMode = true) {
			bool gotPoint = false;
			for (int i = 0; i < scaledPoints.Length; i++) {
				gotPoint |= new Rect(scaledPoints[i] - new Vector2(5, 5), new Vector2(10, 10)).Contains(current.mousePosition);
				if (gotPoint && selectMode) {
					selectedIndex = i;
					selectedPoint = points[i];
					return gotPoint;
				} else if (gotPoint) {
					hoveredIndex = i;
					hoveredPoint = points[i];
					return gotPoint;
				}
			}
			if (selectMode) {
				selectedPoint = default;
				selectedIndex = -1;
			} else {
				hoveredPoint = default;
				hoveredIndex = -1;
			}

			return gotPoint;
		}

		private void HandleResize(Event current, int hotControl, Rect dragRegion) {

			int controlID = GUIUtility.GetControlID(k_ResizePanelControlID, FocusType.Passive);
			switch (current.GetTypeForControl(controlID)) {
				case EventType.MouseDown:
					if (current.button == 0) {
						var canDrag = dragRegion.Contains(current.mousePosition);
						if (!canDrag)
							return;

						//record in screenspace, not GUI space so that the resizing is consistent even if the cursor leaves the window
						this.m_lastMousePos = GUIUtility.GUIToScreenPoint(current.mousePosition);
						this.m_DragDistance = Vector2.zero;
						this.m_DragStartSide = graphRect.width;

						GUIUtility.hotControl = controlID;
						current.Use();
					}
					break;
				case EventType.MouseUp:
					if (hotControl == controlID) {
						GUIUtility.hotControl = 0;
						current.Use();
					}
					break;
				case EventType.MouseDrag:
					if (hotControl == controlID) {
						var mouse_screen = GUIUtility.GUIToScreenPoint(current.mousePosition);
						this.m_DragDistance += mouse_screen - this.m_lastMousePos;
						this.m_lastMousePos = mouse_screen;
						ClipGraphRectSize();

						current.Use();
					}
					break;
				case EventType.KeyDown:
					if (hotControl == controlID && current.keyCode == KeyCode.Escape) {
						ClipGraphRectSize();

						GUIUtility.hotControl = 0;
						current.Use();
					}
					break;
			}
		}

		private void ClipGraphRectSize() {
			graphRect.size = Vector2.Max(
				Vector2.one * 128,
				Vector2.Min(
					Vector2.one * (m_DragStartSide + Mathf.Max(m_DragDistance.x, m_DragDistance.y)),
					Vector2.one * (Screen.width - 40)));
		}

		private void DrawGraph() {
			var value = (MeshProfile)propField.GetValue(targetObject);
			var c = Handles.color;
			Handles.color = Color.gray.MultiplyAlpha(.5f);
			Handles.DrawAAPolyLine(new Vector3[] { new Vector3(graphRect.min.x, graphRect.center.y), new Vector3(graphRect.max.x, graphRect.center.y) });
			Handles.DrawAAPolyLine(new Vector3[] { new Vector3(graphRect.center.x, graphRect.min.y), new Vector3(graphRect.center.x, graphRect.max.y) });
			Handles.color = c;
			var points = value.GetPoints();
			for (int i = 0; i < points.Length; i++) {
				Vector2 p = points[i] * new Vector2(1, -1);
				var pos = new Rect(TransformPoint(p), new Vector2(40, 18));
				GUI.Label(pos, new GUIContent(i.ToString()));
				var scaledPoint = TransformPoint(p);
				GUIUtils.DrawCircle(scaledPoint, 3, true);
				if (hoveredIndex == i)
					GUIUtils.DrawCircle(scaledPoint, 5, true);
				if (selectedIndex == i)
					GUIUtils.DrawCircle(scaledPoint, 5);

			}
			Handles.DrawAAPolyLine(value.GetLoopedPoints().SelectArray(p=>(Vector3)TransformPoint(p * new Vector2(1,-1))));
		}

		/// <summary>
		/// -1..1 to screen position
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		private static Vector2 TransformPoint(Vector2 p) {
			return graphRect.position + (p + Vector2.one) * (graphRect.width / 2);
		}

		/// <summary>
		/// Screen position to -1..1
		/// </summary>
		/// <param name="p"></param>
		/// <returns></returns>
		private static Vector2 InverseTransformVector(Vector2 p) {
			return p * (2/graphRect.width);
		}

		private static void DrawResizeHandle() {
			var c = Handles.color;
			Handles.color = Color.black.MultiplyAlpha(.75f);
			GUIUtils.DrawCircle(new Vector2(graphRect.xMax - 3, graphRect.yMax - 3), 1.5f, true);
			GUIUtils.DrawCircle(new Vector2(graphRect.xMax - 10, graphRect.yMax - 3), 1.5f, true);
			GUIUtils.DrawCircle(new Vector2(graphRect.xMax - 3, graphRect.yMax - 10), 1.5f, true);
			Handles.color = c;
		}
	}
#endif
}