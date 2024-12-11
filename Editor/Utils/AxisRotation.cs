using UnityEditor;
using UnityEngine;

namespace BezierZUtility.Editor
{
	public class AxisRotation
	{
		private static readonly Color s_DimmingColor = new Color(0f, 0f, 0f, 0.078f);
		private static Vector3 lockedPosition;
		private static Vector2 s_CurrentMousePosition;
		private static Transform[] ignoreRaySnapObjects;

		public static Quaternion Do(int id, Quaternion rotation, Vector3 position, Vector3 axis, float size)
		{
			return Do(id, rotation, position, axis, size, drawCircle: true);
		}

		internal static Quaternion Do(int id, Quaternion rotation, Vector3 position, Vector3 axis, float size, bool drawCircle)
		{
			Vector3 vector = Handles.matrix.MultiplyPoint(position);
			Matrix4x4 matrix = Handles.matrix;
			Event current = Event.current;
			switch (current.GetTypeForControl(id))
			{
				case EventType.MouseMove:
				case EventType.Layout:
					Handles.matrix = Matrix4x4.identity;
					HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(vector, size) + 5f);
					Handles.matrix = matrix;
					break;
				case EventType.MouseDown:
					if (HandleUtility.nearestControl == id && current.button == 0 && !current.alt)
					{
						GUIUtility.hotControl = id;
						lockedPosition = position;
						s_CurrentMousePosition = current.mousePosition;
						ignoreRaySnapObjects = null;
						current.Use();
						EditorGUIUtility.SetWantsMouseJumping(1);
					}

					break;
				case EventType.MouseDrag:
					if (GUIUtility.hotControl != id)
					{
						break;
					}

					if (EditorGUI.actionKey && current.shift)
					{
						if (ignoreRaySnapObjects == null)
						{
							ignoreRaySnapObjects = Selection.GetTransforms(SelectionMode.Deep | SelectionMode.Editable);
						}

						object obj = HandleUtility.RaySnap(HandleUtility.GUIPointToWorldRay(current.mousePosition));
						if (obj != null)
						{
							Quaternion quaternion = Quaternion.LookRotation(((RaycastHit)obj).point - position);
							if (Tools.pivotRotation == PivotRotation.Global)
							{
								Transform activeTransform = Selection.activeTransform;
								if ((bool)activeTransform)
								{
									Quaternion quaternion2 = Quaternion.Inverse(activeTransform.rotation) * rotation;
									quaternion *= quaternion2;
								}
							}

							rotation = quaternion;
						}
					}
					else
					{
						s_CurrentMousePosition += current.delta;
						rotation *= Quaternion.AngleAxis(current.delta.x, axis);
					}

					GUI.changed = true;
					current.Use();
					break;
				case EventType.MouseUp:
					if (GUIUtility.hotControl == id && (current.button == 0 || current.button == 2))
					{
						//Tools.UnlockHandlePosition();
						GUIUtility.hotControl = 0;
						current.Use();
						EditorGUIUtility.SetWantsMouseJumping(0);
					}

					break;
				case EventType.KeyDown:
					if (current.keyCode == KeyCode.Escape && GUIUtility.hotControl == id)
					{
						//Tools.UnlockHandlePosition();
						EditorGUIUtility.SetWantsMouseJumping(0);
					}

					break;
				case EventType.Repaint:
					{
						SetupHandleColor(id, current, out var prevColor, out var thickness);
						bool flag = id == GUIUtility.hotControl;
						bool flag2 = IsHovering(id, current);
						Handles.matrix = Matrix4x4.identity;
						if (drawCircle)
						{
							Handles.DrawWireDisc(vector, rotation * Vector3.forward, size, thickness);
						}

						if (flag2 || flag)
						{
							Handles.color = s_DimmingColor;
							Handles.DrawSolidDisc(vector, rotation * Vector3.forward, size);
						}

						Handles.matrix = matrix;
						Handles.color = prevColor;
						break;
					}
			}

			return rotation;
		}


		static void SetupHandleColor(int controlID, Event evt, out Color prevColor, out float thickness)
		{
			prevColor = Handles.color;
			thickness = Handles.lineThickness;
			if (controlID == GUIUtility.hotControl)
			{
				Handles.color = Handles.selectedColor;
			}
			else if (IsHovering(controlID, evt))
			{
				Color color = Handles.color * new Color(1f, 1f, 1f, 1.33f);
				color.r = Mathf.Clamp01(color.r);
				color.g = Mathf.Clamp01(color.g);
				color.b = Mathf.Clamp01(color.b);
				color.a = Mathf.Clamp01(color.a);
				Handles.color = color;
				thickness += 1f;
			}

		}

		static bool IsHovering(int controlID, Event evt) => controlID == HandleUtility.nearestControl && GUIUtility.hotControl == 0 && !Tools.viewToolActive;
	}
}