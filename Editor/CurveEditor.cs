using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using BezierCurveZ;
using System;
using System.Collections.Generic;
using static Codice.CM.Common.CmCallContext;
using RectEx;
using System.Linq;
using Utility.Editor;
using Debug = UnityEngine.Debug;
using Sirenix.Utilities;

namespace BezierCurveZ.Editor
{
	public class CurveEditor : SubscribableEditor<Curve>
	{
		private static CurveEditor instance;
		public static CurveEditor Instance
		{
			[DebuggerStepperBoundary]
			get
			{
				if (instance == null) instance = new CurveEditor();
				return instance;
			}
		}
		//TODO remove
		Curve curve => field;

		private Transform targetTransform;
		private bool targetIsGameObject;
		private UnityEngine.Object targetObject;

		private List<int> selectedPointIdexes = new List<int>();
		private List<Point> selectedPoints = new List<Point>();
		private bool updateClosestPoint = true;
		private int closestIndex;
		private int closestControlIndex;
		private Tool currentInternalTool;
		private bool selectHandlesOnly;
		private Point closestPoint;

		private Color CurveColor = new Color(.3f, 1f, .3f);
		private Color NormalColor = new Color(1f, .5f, .5f);
		private Color UpColor = new Color(.5f, 1f, .5f);
		private Color ForwardColor = new Color(.5f, .5f, 1f);
		private Color HandleColor = new Color(.8f, .8f, .8f);
		private Color SelecrionRectColor = new Color(.5f, .5f, 1f);
		private Color CutColor = new Color(1f, .5f, .8f);

		private bool primaryMouseDragging;
		private bool showPointGUI;
		private bool mouseCaptured;
		public bool snapKeyDown;
		private bool selectingMultiple;
		private int controlID;
		private bool openContext;
		private bool cutInitiated;
		private Vector3 cutPoint;
		private Vector2 mouseDownPosition;
		private Vector2 rightMouseDownPosition;
		private DateTime mouseDownDateTime;
		private Vector3 editedPosition;
		private Quaternion editedRotation;
		private Vector3 editedScale;
		private float snapDistance = .01f;

		public override void Start(Curve curve, SerializedProperty property)
		{
			base.Start(curve, property);
			instance = this;
			targetObject = property.serializedObject.targetObject;
			if ((Component)targetObject is Component c)
			{
				targetTransform = c.transform;
				targetIsGameObject = true;
			}
		}
		public override void Stop()
		{
			base.Stop();
			instance = null;
		}

		public override void OnSceneGUI()
		{
			var current = Event.current;
			controlID = GUIUtility.GetControlID(932795649, FocusType.Passive);
			//current = Event.current;
			if (current.type == EventType.Layout)
				//Magic thing to stop mouse from selecting other objects
				HandleUtility.AddDefaultControl(controlID);

			//Update curve if Undo performed
			if (current.type == EventType.ValidateCommand && current.commandName == "UndoRedoPerformed")
			{
				curve.BumpVersion();
				CallAllSceneViewRepaint();
			}

			if (updateClosestPoint && !selectingMultiple && !mouseCaptured)
				UpdateClosestPoint(current);
			ProcessInput(current);
			DrawStuff(current);
		}

		private void ProcessInput(Event current)
		{

			if (GetKeyDown(KeyCode.Delete))
			{
				current.Use();
				if (closestControlIndex != -1 || selectedPointIdexes.Count > 0)
				{
					Undo.RecordObject(targetObject, "Delete Points");
					if (selectedPointIdexes.Count == 0)
						curve.DissolveEP(curve.GetSegmentIndex(closestControlIndex));
					else
					{
						curve.RemoveMany(selectedPointIdexes);
						selectedPointIdexes.Clear();
						selectedPoints.Clear();
					}
				}
			}
			if (current.type == EventType.MouseDrag && current.button == 0)
				primaryMouseDragging = true;
			else if (GetMouseUp(0))
				primaryMouseDragging = false;

			if (GetKeyUp(KeyCode.Q))
			{
				showPointGUI = !showPointGUI;
			}
			else if (showPointGUI && (GetMouseDown(2) || GetKeyDown(KeyCode.Escape) ||
				currentInternalTool == Tool.Move || currentInternalTool == Tool.Rotate || currentInternalTool == Tool.Scale))
			{
				showPointGUI = false;
			}

			if (GetKeyDown(KeyCode.C))
			{
				selectHandlesOnly = true;
				current.Use();
			}
			else if (GetKeyUp(KeyCode.C))
			{
				selectHandlesOnly = false; ;
				current.Use();
			}

			if (GetKeyDown(KeyCode.S) && primaryMouseDragging)
			{
				snapKeyDown = true;
				current.Use();
			}
			else if (GetKeyUp(KeyCode.S) && snapKeyDown)
			{
				snapKeyDown = false;
				current.Use();
			}

			//Rect selection + Shift/Ctrl click
			if (selectingMultiple && GUIUtility.hotControl == 0 && current.type == EventType.MouseDrag)
				WhileSelectingMultiple(current);
			else if (selectingMultiple && GUIUtility.hotControl == 0 && mouseDownPosition == current.mousePosition)
			{
				selectedPointIdexes.Clear();
				selectedPoints.Clear();
				WhileSelectingMultiple(current);
			}
			if (GetMouseDown(0))
			{
				mouseDownPosition = current.mousePosition;
				selectingMultiple = true;
			}
			if ((selectingMultiple && current.type == EventType.MouseMove) || (GetMouseUp(0) || GUIUtility.hotControl != 0))
			{
				selectingMultiple = false;
				mouseDownPosition = Vector2.zero;
			}

			//Context menu
			if (openContext && current.type == EventType.Repaint)
			{
				openContext = false;
				ContextMenuOpen(current);
			}
			if (GetMouseDown(1) && closestIndex != -1)
			{
				rightMouseDownPosition = current.mousePosition;
				mouseDownDateTime = DateTime.Now;
			}
			else if (GetMouseUp(1))
				if (!(mouseDownDateTime.AddSeconds(1f) < DateTime.Now || closestIndex == -1 || (rightMouseDownPosition - current.mousePosition).magnitude > 5f))
				{
					openContext = true;
					CallAllSceneViewRepaint();
				}


			//Cut/Extrude
			if (GetKeyDown(KeyCode.V))
			{
				if (PositionHandleIds_copy.@default.Has(GUIUtility.hotControl))
				{
					StartExtruding();
				}
				else
				{
					cutInitiated = true;
					GUIUtility.hotControl = controlID;
				};
				current.Use();
			}
			else if (GetKeyUp(KeyCode.V))
			{
				current.Use();
			}
			else if (cutInitiated && GetMouseDown(0))
			{
				cutInitiated = false;
				Cut();
			}
			else if (cutInitiated && (GetKeyDown(KeyCode.Escape) || GetMouseDown(1)))
			{
				cutInitiated = false;
			}



			bool GetKeyDown(KeyCode key) => current.type == EventType.KeyDown && current.keyCode == key;
			bool GetKeyUp(KeyCode key) => current.type == EventType.KeyUp && current.keyCode == key;
			bool GetMouseDown(int button) => current.type == EventType.MouseDown && current.button == button;
			bool GetMouseUp(int button) => current.type == EventType.MouseUp && current.button == button;
		}

		private void DrawStuff(Event current)
		{
			DrawCurve();
			DrawCurveHandles();
			DrawTools(current);
		}

		/// <summary>
		/// Draws tool handles in global space
		/// </summary>
		private void DrawTools(Event current)
		{
			if (Tools.current != Tool.None)
			{
				currentInternalTool = Tools.current;
			}
			Tools.current = Tool.None;
			var c = Handles.color;

			if (selectingMultiple && GUIUtility.hotControl == 0)
			{
				if (current.type == EventType.Repaint)
				{
					var mousePos = current.mousePosition;
					var rect = new Vector3[] {
					HandleUtility.GUIPointToWorldRay(mouseDownPosition).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(new Vector2(mouseDownPosition.x, mousePos.y)).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(mousePos).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(new Vector2(mousePos.x, mouseDownPosition.y)).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(mouseDownPosition).GetPoint(.1f),
				};
					Handles.color = SelecrionRectColor * .5f;
					Handles.DrawAAConvexPolygon(rect);
					Handles.color = SelecrionRectColor;
					Handles.DrawAAPolyLine(rect);
					Handles.color = c;
				}
			}

			if (cutInitiated && current.type == EventType.MouseMove)
			{
				cutPoint = HandleUtility.ClosestPointToPolyLine(curve.VertexDataPoints.SelectArray(v => TransformPoint(v)));
			}
			else if (cutInitiated && current.type == EventType.Repaint)
			{
				Handles.color = CutColor;
				Handles.DrawSolidDisc(cutPoint, cutPoint - Camera.current.transform.position, HandleUtility.GetHandleSize(cutPoint) * .1f);
				Handles.color = c;
			}
			if (closestIndex == -1)
				return;
			else if (currentInternalTool == Tool.Move)
			{
				EditorGUI.BeginChangeCheck();
				var pos = Vector3.zero;
				if (!current.shift)
				{
					pos = Handles.PositionHandle(editedPosition, toolRotation);
					if (snapKeyDown)
						ProcessSnapping(ref pos);
				}
				else
				{
#if UNITY_2022
					pos = Handles.FreeMoveHandle(editedPosition, HandleUtility.GetHandleSize(editedPosition) * .16f, Vector3.one * .2f, Handles.RectangleHandleCap);
#else

					pos = Handles.FreeMoveHandle(editedPosition, editedRotation, HandleUtility.GetHandleSize(editedPosition) * .16f, Vector3.one * .2f, Handles.RectangleHandleCap);
#endif
				}



				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Point position changed");

					if (selectedPointIdexes.Count == 0)
					{
						var ind = closestPoint.IsLinear ? closestControlIndex : closestIndex;

						curve.SetPointPosition(ind, InverseTransformPoint(pos));
						editedPosition = TransformPoint(curve.Points[closestIndex]);
					}
					else
					{
						var delta = InverseTransformVector(pos - editedPosition);
						foreach (var ind in selectedPointIdexes)
						{
							var point = curve.Points[ind];
							curve.SetPointPosition(ind, point + delta);
						}
						editedPosition += TransformVector(delta);
					}
				}
			}
			else if (currentInternalTool == Tool.Rotate)
			{
				EditorGUI.BeginChangeCheck();
				int segmentIndex = curve.GetSegmentIndex(closestControlIndex);
				float handleSize = HandleUtility.GetHandleSize(editedPosition);

				var r = Handles.DoRotationHandle(editedRotation, editedPosition).normalized;
				var delta = r * editedRotation.Inverted();
				delta.ToAngleAxis(out var dangle, out var daxis);
				var pointDelta = Quaternion.AngleAxis(dangle, InverseTransformVector(daxis));
				editedRotation = r;

				if (selectedPointIdexes.Count == 0)
					GUIUtils.DrawAxes(editedPosition, localToWorldMatrix.rotation * curve.Points[closestControlIndex].rotation, handleSize, 3f);
				else
					foreach (var ind in selectedPointIdexes)
					{
						float size = HandleUtility.GetHandleSize(TransformPoint(curve.Points[ind]));
						GUIUtils.DrawAxes(TransformPoint(curve.Points[ind]), localToWorldMatrix.rotation * curve.Points[ind].rotation, size, 3f);
					}

				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Point Roation changed");

					if (selectedPointIdexes.Count == 0)
					{
						var ind = closestControlIndex;

						curve.AddEPRotation(segmentIndex, pointDelta);
					}
					else
					{
						Vector3 localEditedPosition = InverseTransformPoint(editedPosition);
						foreach (var ind in selectedPointIdexes)
						{
							var pos = pointDelta * (curve.Points[ind] - localEditedPosition) + localEditedPosition;
							curve.SetPointPosition(ind, pos);
							curve.AddEPRotation(curve.GetSegmentIndex(ind), pointDelta);
						}
					}
				}
			}
			else if (currentInternalTool == Tool.Scale)
			{
				EditorGUI.BeginChangeCheck();
				int segmentIndex = curve.GetSegmentIndex(closestControlIndex);
				float handleSize = HandleUtility.GetHandleSize(editedPosition);

				var scale = Vector3.zero;


				var center = selectedPointIdexes.Count > 0 ? selectedPointIdexes.Sum(i => curve.Points[i].position) / curve.PointCount : editedPosition;
				scale = Handles.DoScaleHandle(editedScale/*curve.Points[closestControlIndex].scale*/,
					Tools.pivotMode == PivotMode.Center ? center : editedPosition,
					editedRotation, handleSize);


				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Point Scale changed");

					if (selectedPointIdexes.Count == 0)
					{
						var ind = closestControlIndex;

						curve.SetEPScale(segmentIndex, scale);
					}
					else
					{
						if (Tools.pivotMode == PivotMode.Center)
						{
							var scaleDiff = DivideVectorsComponentwise(scale, editedScale);

							for (int i = 0; i < selectedPointIdexes.Count; i++)
							{
								var ind = selectedPointIdexes[i];
								var point = selectedPoints[i];
								Vector3 loPos = (point - center);
								Vector3 scaledPos = center + MultiplyVectorsComponentwise(loPos, scaleDiff);
								curve.SetPointPosition(ind, scaledPos);
								curve.SetEPScale(ind, MultiplyVectorsComponentwise(scaleDiff, point.scale));
							}
						}
						else
						{
							var scaleDiff = new Vector3(scale.x / editedScale.x, scale.y / editedScale.y, scale.z / editedScale.z);

							for (int i = 0; i < selectedPointIdexes.Count; i++)
							{
								var ind = selectedPointIdexes[i];
								var point = selectedPoints[i];
								curve.SetEPScale(ind, MultiplyVectorsComponentwise(scaleDiff, point.scale));
							}
						}
					}
				}
			}
			else if (showPointGUI && closestIndex != -1 && current.type != EventType.Layout)
			{
				//////////////////////////////
				//			Q Menu			//
				//////////////////////////////
				Handles.BeginGUI();
				var guiRect = new Rect(HandleUtility.WorldToGUIPoint(editedPosition), new Vector3(300, EditorGUIUtility.singleLineHeight * 4 + 8));
				mouseCaptured = guiRect.Contains(current.mousePosition);
				GUI.Box(guiRect, GUIContent.none);

				//Draw position line
				var line = guiRect.FirstLine(EditorGUIUtility.singleLineHeight + 4).Extend(-2);
				GUI.Label(line.MoveLeftFor(30), "pos");
				EditorGUI.BeginChangeCheck();
				var pos = EditorGUI.Vector3Field(line, GUIContent.none, closestPoint);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Set position");
					curve.SetPointPosition(closestIndex, pos);
					closestPoint = curve.Points[closestIndex];
				}

				//Draw Eulers Angles line
				line = line.MoveDown();
				GUI.Label(line.MoveLeftFor(30), "rot");
				EditorGUI.BeginChangeCheck();
				var rot = EditorGUI.Vector3Field(line, GUIContent.none, closestPoint.rotation.eulerAngles);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Set rotation");
					curve.SetEPRotation(curve.GetSegmentIndex(closestIndex), Quaternion.Euler(rot));
					closestPoint = curve.Points[closestIndex];
				}

				//Draw scale 
				line = line.MoveDown();
				GUI.Label(line.MoveLeftFor(30), "sca");
				EditorGUI.BeginChangeCheck();
				var sca = EditorGUI.Vector3Field(line, GUIContent.none, closestPoint.scale);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Set scale");
					curve.SetEPScale(curve.GetSegmentIndex(closestIndex), sca);
					closestPoint = curve.Points[closestIndex];
				}

				//Draw Modes dropdown
				line = line.MoveDown();
				GUI.Label(line.MoveLeftFor(30), "mode");
				GUI.enabled = curve.IsEndPoint(closestIndex);
				EditorGUI.BeginChangeCheck();
				var modeId = EditorGUI.Popup(line, Point.AllModes.IndexOf(closestPoint.mode), Point.AllModes.SelectArray(m => m.ToString()));
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Set mode");
					curve.SetPointMode(closestIndex, Point.AllModes[modeId]);
					closestPoint = curve.Points[closestIndex];
				}
				GUI.enabled = true;
				Handles.EndGUI();

				Quaternion r = Camera.current.transform.rotation;
				float h = HandleUtility.GetHandleSize(editedPosition) * .2f;
				Handles.color = Color.white / 5 * 3;
				Handles.DrawAAConvexPolygon(editedPosition, editedPosition + r * Vector3.right * h, editedPosition + r * Vector3.down * h);
			}

			static Vector3 MultiplyVectorsComponentwise(Vector3 a, Vector3 b)
			{
				return new Vector3(b.x * a.x, b.y * a.y, b.z * a.z);
			}

			Vector3 DivideVectorsComponentwise(Vector3 a, Vector3 b)
			{
				return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
			}
		}
		private void DrawCurve()
		{
			var c = Handles.color;
			var m = Handles.matrix;
			Handles.matrix = localToWorldMatrix;
			//foreach (var segment in curve.Segments)
			//{
			//	Handles.color = CurveColor;
			//	Handles.DrawBezier(segment[0], segment[3], segment[1], segment[2], CurveColor, null, curve._isMouseOverProperty ? 2.5f : 1.5f);
			//}

			Handles.color = Color.red / 2 + Color.white / 2;
			foreach (var vert in curve.VertexData)
			{
				Handles.DrawAAPolyLine(vert, vert + vert.normal * .2f * vert.Scale.x);
				//Handles.Label(vert.point, $"{vert.length}, {vert.time}");
			}
			foreach (int i in curve.EndPointIndexes)
			{
				Point point = curve.Points[i];
				GUIUtils.DrawAxes(point, point.rotation, .1f, 3);
			}
			DrawCurveFromVertexData(curve.VertexData.Select(v => (v.Position, v.up)));

			Handles.color = c;
			Handles.matrix = m;
		}
		/// <summary>
		/// Draw two sided curve
		/// </summary>
		/// <param name="vertexData"></param>
		private void DrawCurveFromVertexData(IEnumerable<(Vector3 position, Vector3 up)> vertexData)
		{
			var c = Handles.color;
			var m = Handles.matrix;
			Handles.matrix = localToWorldMatrix;
			var vertices = vertexData.Take(1).Select(v => v.position).ToList();
			Vector3 campos = InverseTransformPoint(Camera.current.transform.position);
			var upDotCamera = Vector3.Dot(vertexData.FirstOrDefault().up, vertexData.FirstOrDefault().position - campos);
			foreach (var v in vertexData.Skip(1))
			{
				vertices.Add(v.position);
				var newDot = Vector3.Dot(v.up, v.position - campos);
				if (upDotCamera != newDot)
				{
					DrawVertices(vertices, !(upDotCamera > 0));

					vertices.Clear();
					vertices.Add(v.position);
					upDotCamera = newDot;
				}
			}
			DrawVertices(vertices, upDotCamera > 0);

			Handles.color = c;
			Handles.matrix = m;

			static void DrawVertices(List<Vector3> vertices, bool towardCamera)
			{
				Handles.color = towardCamera ? Color.green : new Color(.6f, .3f, 0);
				Handles.DrawAAPolyLine((towardCamera ? 2f : 3f), vertices.ToArray());
			}
		}

		/// <summary>
		/// Draws curve handles in local space
		/// </summary>
		void DrawCurveHandles()
		{
			var c = Handles.color;
			var m = Handles.matrix;
			Handles.matrix = localToWorldMatrix;
			Handles.color = HandleColor;
			foreach (var segment in curve.Segments)
			{
				Handles.DrawAAPolyLine(segment[0], segment[1]);
				Handles.DrawAAPolyLine(segment[2], segment[3]);
			}
			var cam = Camera.current;
			Vector3 camLocalPos = InverseTransformPoint(cam.transform.position);
			for (int i = 0; i < curve.PointCount; i++)
			{
				var point = curve.Points[i];
				float size = HandleUtility.GetHandleSize(point) * .2f;
				bool isSelected = selectedPointIdexes.Contains(i);
				Handles.color = HandleColor * (isSelected ? 1 : .5f);
				if (point.type == Point.Type.Control)
				{
					GUIUtils.DrawCircle(point, point - camLocalPos, size, isSelected, 1.5f, 24);
					Handles.Label(point, (i == curve.PointCount - 1 && curve.IsClosed ? "     / " : "  ") + i.ToString());
				}
				else
				{
					Handles.DrawAAPolyLine(1.5f, GetHandleShapePoints(point, point - camLocalPos, size));
				}
			}
			Handles.color = c;
			Handles.matrix = m;

			Vector3[] GetHandleShapePoints(Point point, Vector3 normal, float size)
			{
				var cross = normal.normalized.Cross(point.forward).normalized;
				Vector3 d1 = (cross + point.forward) * size;
				Vector3 d2 = (cross - point.forward) * size;
				return new Vector3[] { point - d1, point - d2, point + d1, point + d2, point - d1 };
			}
		}

		private void UpdateClosestPoint(Event current)
		{
			if (GUIUtility.hotControl != 0) return;
			//Just a reminder: IMGUI coordinates starts from top-left of actual view
			Vector2 mousePos = current.mousePosition;
			var minDist = float.MaxValue;
			var minHDist = float.MaxValue;
			closestIndex = -1;
			closestControlIndex = -1;
			for (int i = 0; i < curve.PointCount; i++)
			{
				bool selectControlsOnly = currentInternalTool == Tool.Rotate;
				bool isContolPoint = curve.IsEndPoint(i);
				if ((selectHandlesOnly && isContolPoint) || selectControlsOnly && !isContolPoint)
					continue;

				var dist = HandleUtility.WorldToGUIPoint(TransformPoint(curve.Points[i])).DistanceTo(mousePos);
				if (dist <= minDist + .01f)
				{
					minDist = dist;
					closestIndex = i;
				}
				else
				if (dist <= minHDist + .1f)
				{
					minHDist = dist;
					//closestHandleIndex = i;
				}
			}
			if (closestIndex == -1)
			{
				closestPoint = default;

			}
			if (curve.IsClosed && closestIndex == curve.LastPointInd) closestIndex = 0;
			closestPoint = curve.Points[closestIndex];
			var cind = closestIndex + (closestPoint.isRightHandle ? -1 : closestPoint.isLeftHandle ? 1 : 0);
			closestControlIndex = cind;
			//closestEndPoint = curve.Points[cind];
			if (closestPoint.mode == Point.Mode.Linear)
			{
				editedPosition = TransformPoint(curve.Points[cind]);
			}
			else
			{
				editedPosition = TransformPoint(closestPoint.position);
			}
			editedRotation = toolRotation;
			editedScale = closestPoint.scale;
			if (minDist > 100)
				closestIndex = -1;
		}

		private void WhileSelectingMultiple(Event current)
		{
			if (GUIUtility.hotControl != 0)
			{
				selectingMultiple = false;
				return;
			}
			if (!(current.type == EventType.Repaint || current.type == EventType.MouseDrag || current.type == EventType.MouseDown))
				return;
			if (!current.shift && !EditorGUI.actionKey)
			{
				selectedPointIdexes.Clear();
				selectedPoints.Clear();
			}

			//Extend selection rect just enough to include points touching, this will also add clicked points
			var rect = new Rect(mouseDownPosition, current.mousePosition - mouseDownPosition).Abs().Extend(18, 18);
			for (int i = 0; i < curve.Points.Count; i++)
			{
				var point = curve.Points[i];
				if (!point.IsEndPoint || (curve.IsClosed && i == curve.LastPointInd)) continue;

				if (rect.Contains(HandleUtility.WorldToGUIPoint(TransformPoint(point))))
				{
					if (EditorGUI.actionKey)
					{
						var ind =  selectedPointIdexes.IndexOf(i);
						selectedPointIdexes.RemoveAt(ind);
						selectedPoints.RemoveAt(ind);
					}
					else if (!selectedPointIdexes.Contains(i))
					{
						selectedPointIdexes.Add(i);
						selectedPoints.Add(point);
					}
				}
			}

			CallAllSceneViewRepaint();
		}

		private void CallAllSceneViewRepaint()
		{
			foreach (SceneView sv in SceneView.sceneViews)
				sv.Repaint();
		}

		private void ContextMenuOpen(Event current)
		{
			var contextMenu = new GenericMenu();
			foreach (var mode in Point.AllModes)
			{
				var capturedMode = mode;
				contextMenu.AddItem(new GUIContent(mode.ToString()), curve.Points[closestIndex].mode == capturedMode, () =>
				{
					EditorGUI.BeginChangeCheck();
					if (selectedPointIdexes.Count > 0)
						foreach (var ind in selectedPointIdexes)
						{
							curve.SetPointMode(ind, capturedMode);
						}
					else
						curve.SetPointMode(closestIndex, capturedMode);
					Undo.RecordObject(targetObject, $"Curve {closestControlIndex} point mode changed to {mode}");
				});
			}

			Vector2 mousePosition = current.mousePosition;
			contextMenu.DropDown(new Rect(mousePosition, Vector2.zero));
		}

		private void StartExtruding()
		{
			Undo.RecordObject(targetObject, "Curve Extrude");
			int segmentIndex = curve.GetSegmentIndex(closestIndex);
			//BackupCurve();
			curve.SetPointPosition(closestIndex, closestPoint);
			if (curve.GetEPTangentFromPoints(segmentIndex).Dot(TransformPoint(closestPoint) - editedPosition) < 0)
			{
				if (curve.IsClosed) segmentIndex %= curve.SegmentCount;
				if (segmentIndex == curve.SegmentCount)
				{
					curve.SplitCurveAt(segmentIndex - 1, 1f);
					closestIndex += 3;
				}
				else
				{
					curve.SplitCurveAt(segmentIndex, 0f);
				}
			}
			else
			{
				var prevSegmentIndex = segmentIndex - 1;
				if (curve.IsClosed) prevSegmentIndex = (curve.SegmentCount + prevSegmentIndex) % curve.SegmentCount;
				if (prevSegmentIndex > 0 || curve.SegmentCount == segmentIndex)
					curve.SplitCurveAt(prevSegmentIndex, 1f);
				else
				{
					curve.SplitCurveAt(segmentIndex, 0f);
					closestIndex += 3;
				}
			}
			closestPoint = curve.Points[closestIndex];
		}

		private void Cut()
		{
			Undo.RecordObject(targetObject, "Curve Cut");
			curve.SplitCurveAt(InverseTransformPoint(cutPoint));
		}

		private void ProcessSnapping(ref Vector3 pos)
		{
			var invRot = toolRotation.Inverted();
			var m = Matrix4x4.TRS(pos, toolRotation, Vector3.one);
			var im = m.inverse;
			var dist = curve.Points.Select(p => im.MultiplyPoint3x4(TransformPoint(p)));
			var dd = new List<(int i, Vector3 v, float dist)>();
			{
				int i = 0;
				foreach (var d in dist)
				{
					if (i != closestIndex && ((d.x.Abs() < snapDistance ? 1 : 0) + (d.y.Abs() < snapDistance ? 1 : 0) + (d.z.Abs() < snapDistance ? 1 : 0) > 1))
						dd.Add((i, trimDistanceMaximum(d, snapDistance), d.x.Abs() + d.y.Abs() + d.z.Abs()));
					i++;
				}
			}
			dd.Sort((a, b) => a.dist.CompareTo(b.dist));

			Handles.color = Color.yellow * .8f;
			foreach (var d in dd)
			{
				Handles.DrawAAPolyLine(TransformPoint(curve.Points[d.i]), pos);
			}

			if (dd.Count == 0) return;

			pos = m.MultiplyPoint3x4(dd.First().v);

			Vector3 trimDistanceMaximum(Vector3 v, float minDist)
			{
				if (v.x.Abs() > minDist) return new Vector3(0, v.y, v.z);
				if (v.y.Abs() > minDist) return new Vector3(v.x, 0, v.z);
				if (v.z.Abs() > minDist) return new Vector3(v.x, v.y, 0);
				return v;
			}
		}
		/// <summary>
		/// Transforms position from local space to world space.
		/// </summary>
		Vector3 TransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.TransformPoint(v) : v;
		/// <summary>
		/// Transforms position from world space to local space.
		/// </summary>
		Vector3 InverseTransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformPoint(v) : v;
		Vector3 TransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.TransformDirection(v) : v;
		Vector3 InverseTransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformDirection(v) : v;
		Vector3 TransformVector(Vector3 v) => targetIsGameObject ? targetTransform.TransformVector(v) : v;
		Vector3 InverseTransformVector(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformVector(v) : v;
		Matrix4x4 localToWorldMatrix => targetIsGameObject ? targetTransform.localToWorldMatrix : Matrix4x4.identity;
		Matrix4x4 worldToLocalMatrix => targetIsGameObject ? targetTransform.worldToLocalMatrix : Matrix4x4.identity;
		Quaternion TransformRotation => targetIsGameObject ? targetTransform.rotation : Quaternion.identity;
		private Quaternion toolRotation => Tools.pivotRotation == PivotRotation.Local ? localToWorldMatrix.rotation * closestPoint.rotation : Quaternion.identity;


	} 
}