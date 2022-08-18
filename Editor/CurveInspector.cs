using UnityEngine;
using UnityEditor;
using System;
using RectEx;
using UnityEditor.Toolbars;
using System.Linq;
using Utility.Editor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using Utility;

namespace BezierCurveZ
{
	/// <summary>
	/// <see cref="Curve"/> Property Drawer. Allows curve editing
	/// </summary>
	[CustomPropertyDrawer(typeof(Curve))]
	public class CurvePropertyDrawer : PropertyDrawer
	{
		private UnityEngine.Object targetObject;
		private Transform targetTransform;
		private bool targetIsGameObject;
		private Curve curve;

		private bool _initialized;
		private Texture2D rotationMinimizationTexture;
		private Texture2D lookUpTexture;
		private Texture2D isOpenTexture;
		private Texture2D isClosedTexture;

		[NonSerialized]
		private static CurvePropertyDrawer currentlyEditedPropertyDrawer;
		private bool _isInEditMode;
		public bool IsEditing { get => _isInEditMode; private set { _isInEditMode = value; if (value) currentlyEditedPropertyDrawer = this; } }
		private bool isMouseOver;
		private bool previewAlways;

		#region editor behaviour
		private bool IsCurrentlyEditedDrawer => currentlyEditedPropertyDrawer == this;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
			IsCurrentlyEditedDrawer ? EditorGUIUtility.singleLineHeight + 2 + editorHeight : EditorGUIUtility.singleLineHeight;

		private void Initialize(SerializedProperty property)
		{
			if (EditorGUIUtility.isProSkin)
			{
				rotationMinimizationTexture = Resources.Load<Texture2D>("Bezier.RM_d");
				lookUpTexture = Resources.Load<Texture2D>("Bezier.LookUp_d");
				isOpenTexture = Resources.Load<Texture2D>("Bezier.IsOpen_d");
				isClosedTexture = Resources.Load<Texture2D>("Bezier.IsClosed_d");
			}
			else
			{
				rotationMinimizationTexture = Resources.Load<Texture2D>("Bezier.RM");
				lookUpTexture = Resources.Load<Texture2D>("Bezier.LookUp");
				isOpenTexture = Resources.Load<Texture2D>("Bezier.IsOpen");
				isClosedTexture = Resources.Load<Texture2D>("Bezier.IsClosed");
			}
			_initialized = true;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (!_initialized) Initialize(property);

			current = Event.current;

			if (targetObject == null)
			{
				targetObject = property.serializedObject.targetObject;
				if (targetObject is Component c)
				{
					targetTransform = c.transform;
					targetIsGameObject = true;
				}
			}
			//curve = fieldInfo.GetValue(targetObject) as Curve;
			curve = property.GetValue<Curve>();

			var rootRows = IsCurrentlyEditedDrawer ?
				position.Column(new float[] { 0f, 1f }, new float[] { EditorGUIUtility.singleLineHeight, 0f }) :
				new Rect[] { position };
			var firstRow = rootRows[0].Row(new float[] { 0f, 0f, 0f, 1f }, new float[] { EditorGUIUtility.labelWidth, 100f, 22f, 0f });
			EditorGUI.LabelField(firstRow[0], label);
			if (!IsCurrentlyEditedDrawer && GUI.Button(firstRow[1], "Edit"))
			{
				StartEditor();
			}
			else if (IsCurrentlyEditedDrawer && current.type != EventType.Layout)
			{
				DrawCurveEditor(rootRows[1], property);
				var c = GUI.color;
				GUI.color = Color.white / 2 + Color.red / 2;
				if (GUI.Button(firstRow[1], "Finish Editing"))
				{
					FinishEditor();
				}
				GUI.color = c;
			}

			SetUpPreview(GUI.Toggle(firstRow[2], previewAlways, GUIContent.none));

			if (!_isInEditMode)
			{
				CheckIfMouseIsOver(position);
			}

			//// Notify of undo/redo that might modify the path
			//if (current.type == EventType.ValidateCommand && current.commandName == "UndoRedoPerformed")
			//{
			//	//data.PathModifiedByUndo();
			//}
		}

		/// <summary>
		/// Manages Preview subscription on previewAlways value
		/// </summary>
		/// <param name="newPreviewValue">new value from toggle button</param>
		private void SetUpPreview(bool newPreviewValue)
		{
			if (newPreviewValue != previewAlways)
			{
				previewAlways = newPreviewValue;
				SceneView.duringSceneGui -= _OnScenePreviewInternal;
				if (previewAlways)
				{
					SceneView.duringSceneGui += _OnScenePreviewInternal;
					Selection.selectionChanged += UnsubscribeSetUpPreview;
				}
			}

			void UnsubscribeSetUpPreview()
			{
				Selection.selectionChanged -= UnsubscribeSetUpPreview;
				SceneView.duringSceneGui -= _OnScenePreviewInternal;
			}
		}

		/// <summary>
		/// Subscribe Preview to SceneView.duringSceneGui on hover and unsubscribe on leave and redraw scene.
		/// _PreviewHandlesInternal will unsubscribe if property will get edited itself.
		/// </summary>
		private void CheckIfMouseIsOver(Rect position)
		{
			if (!IsCurrentlyEditedDrawer && position.Contains(current.mousePosition))
			{
				if (!isMouseOver)
				{
					isMouseOver = true;
					SceneView.duringSceneGui -= _OnScenePreviewInternal;
					SceneView.duringSceneGui += _OnScenePreviewInternal;
					Selection.selectionChanged += UnsubscribeSetUpPreview;
					CallAllSceneViewRepaint();
				}
			}
			else if (isMouseOver)
			{
				isMouseOver = false;
				if (!previewAlways)
					SceneView.duringSceneGui -= _OnScenePreviewInternal;
				CallAllSceneViewRepaint();
			}
		}

		void UnsubscribeSetUpPreview()
		{
			Selection.selectionChanged -= UnsubscribeSetUpPreview;
			SceneView.duringSceneGui -= _OnScenePreviewInternal;
		}

		private void StartEditor()
		{
			_isInEditMode = true;
			currentlyEditedPropertyDrawer?.FinishEditor();
			Selection.selectionChanged += FinishEditor;
			EditorSceneManager.sceneClosed += FinishEditor;
			AssemblyReloadEvents.beforeAssemblyReload += FinishEditor;
			SceneView.duringSceneGui += OnSceneGUI;
			CallAllSceneViewRepaint();
			currentlyEditedPropertyDrawer = this;
			CurveEditorOverlay.Show();
			lastTool = Tools.current;
			Tools.current = Tools.current == Tool.Move || Tools.current == Tool.Rotate ? Tools.current : Tool.None;
		}

		private void FinishEditor(UnityEngine.SceneManagement.Scene scene) => FinishEditor();

		private void FinishEditor()
		{
			_isInEditMode = false;
			Selection.selectionChanged -= FinishEditor;
			EditorSceneManager.sceneClosed -= FinishEditor;
			AssemblyReloadEvents.beforeAssemblyReload -= FinishEditor;
			SceneView.duringSceneGui -= OnSceneGUI;
			CallAllSceneViewRepaint();
			currentlyEditedPropertyDrawer = null;
			CurveEditorOverlay.Hide();
			Tools.current = lastTool;
		}
		#endregion

		/// <summary>
		/// Draws preview
		/// Unsubscribes from SceneView.duringSceneGui if Editor is on
		/// </summary>
		/// <param name="scene"></param>
		void _OnScenePreviewInternal(SceneView scene)
		{
			if (IsCurrentlyEditedDrawer)
			{
				SceneView.duringSceneGui -= _OnScenePreviewInternal;
				return;
			}
			PropertyOnScenePreview();
		}

		private void PropertyOnScenePreview()
		{
			DrawCurveAndPoints(isMouseOver);
		}

		private void CallAllSceneViewRepaint()
		{
			foreach (var scene in SceneView.sceneViews)
				((SceneView)scene).Repaint();
		}

		private float editorHeight = EditorGUIUtility.singleLineHeight * 2 + 64 + 8;
		private void DrawCurveEditor(Rect position, SerializedProperty property)
		{
			if (position.height == 0) return;
			EditorGUI.HelpBox(position, "", MessageType.None);
			position = position.Extend(-6,-4);
			var lines = position.Column(
				new float[] { 0f,0f,1f},
				new float[] { EditorGUIUtility.singleLineHeight, 64, 0f });
			var firstLine = lines[0].Row(2, 10);
			EditorGUI.BeginChangeCheck();
			EditorGUI.PropertyField(firstLine[0],property.FindPropertyRelative("_maxAngleError"));
			EditorGUI.PropertyField(firstLine[1], property.FindPropertyRelative("_minSamplingDistance"));
			if (EditorGUI.EndChangeCheck())
			{
				curve.Update(force: true);
			}
			var bigButtons = lines[1].Row(new float[] { 0, 0, 1 }, new float[] { 64, 64, 0 });
			EditorGUI.BeginChangeCheck();
			var rm = GUI.Toggle(bigButtons[0], curve.UseRotations, new GUIContent(curve.UseRotations ? rotationMinimizationTexture : lookUpTexture,
				"Use Rotations and interpolate them through curve\nLookup, will have stable rotations, but will fail on vertical segments"
				), "Button");
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(targetObject, "Curve Use Rotations changed");
				curve.UseRotations = rm;
				CallAllSceneViewRepaint();
			}
			EditorGUI.BeginChangeCheck();
			var closed = GUI.Toggle(bigButtons[1], curve.IsClosed, new GUIContent(curve.IsClosed ? isClosedTexture : isOpenTexture,
				"IsClosed, curve loops\nIsOpen curve has start and end"
				), "Button");
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(targetObject, "Curve Is Closed changed");
				curve.CloseCurve(closed);
				CallAllSceneViewRepaint();
			}
		}

		private Tool lastTool;
		private Tool currentInternalTool;
		private float mouse1PressedTime;
		private GenericMenu contextMenu;
		private Vector2 mouse1Position;
		private bool drawContextMenu;
		private bool cuttingInitialized;
		private int extruding;
		private bool isExtruding => extruding > 0;
		private bool startedExtruding => extruding == 1;
		private bool extrusionRightDirection;
		private Vector3 closestPointToMouseOnCurve;
		private int controlID;
		private Event current;
		private bool selectHandlesOnly;
		/// <summary>
		/// Hovered point
		/// </summary>
		private Curve.BezierPoint closestPoint;
		/// <summary>
		/// World position of <see cref="closestPoint"/>
		/// </summary>
		private Vector3 editedPosition;
		/// <summary>
		/// Index of <see cref="closestPoint"/>
		/// </summary>
		private int closestIndex;
		private int closestControlIndex;
		//private int closestHandleIndex;
		private bool sKeyDown;
		private bool mouse0DragRect;
		private Vector2 mouse0DownPosition;
		private List<int> selectedPointIdexes = new List<int>();
		private Quaternion toolRotation;
		private bool showPointGUI;
		private bool captureMouse;
		private Curve backupCurve;
		private int backupClosestIndex;
		private Vector3 backupEditedPosition;

		private void OnSceneGUI(SceneView scene)
		{
			controlID = GUIUtility.GetControlID(932795648, FocusType.Passive);
			current = Event.current;
			if (current.type == EventType.Layout)
				//Magic thing to stop mouse from selecting other objects
				HandleUtility.AddDefaultControl(controlID);

			if (current.type == EventType.ValidateCommand && current.commandName == "UndoRedoPerformed")
				curve.Update(true);
			EditorGUI.BeginChangeCheck();
			Input();

			DrawCurveAndPoints();
			DrawHandles();
		}

		private void Input()
		{

			if (Tools.current == Tool.Move || Tools.current == Tool.Rotate)
			{
				currentInternalTool = Tools.current;
				Tools.current = Tool.None;
			}


			if (current.type == EventType.Repaint || current.type == EventType.Layout)
				return;

			//show Edit GUI
			if (GetKeyDown(KeyCode.Q) && GUIUtility.hotControl == 0)
			{
				showPointGUI = !showPointGUI;
				if (!showPointGUI)
				{
					captureMouse = false;
					Tools.current = currentInternalTool;
				}
				else
					currentInternalTool = Tools.current;
			}
			//Cancel showing Edit GUI
			else if (showPointGUI && (GetKeyDown(KeyCode.Escape) || GetMouseDown(1)))
			{
				showPointGUI = false;
				captureMouse = false;
				Tools.current = currentInternalTool;
				//Don't want to continue to trigger other things, like Context menu
				return;
			}

			//Messy part
			//Cancel Extrude before capture returns
			bool vUp = GetKeyUp(KeyCode.V);
			if (vUp && isExtruding)
			{
				extruding = 0;
				captureMouse = false;
				return;
			}

			//If captureMouse is on, don't start anything else
			if (captureMouse)
				return;

			//Cut/Extrude
			if (vUp && !isExtruding)
			{
				if (!curve.IsClosed && closestIndex == 0)
				{
					Undo.RecordObject(targetObject, $"Added new point to a curve");
					curve.AddPointAtStart(curve.Points[closestIndex]);
					closestIndex = 0;
					closestPoint = curve.Points[closestIndex];
				}
				else if (!curve.IsClosed && closestIndex == curve.Points.Count - 1)
				{
					Undo.RecordObject(targetObject, $"Added new point to a curve");
					curve.AddPointAtEnd(curve.Points[closestIndex]);
					closestIndex++;
					closestPoint = curve.Points[closestIndex];
				}
				else
				{
					cuttingInitialized = true;
					GUIUtility.hotControl = controlID;
				}
			}
			//V down on dragging point
			else if (GetKeyDown(KeyCode.V) && curve.IsControlPoint(closestIndex) && PositionHandleIds_copy.@default.Has(GUIUtility.hotControl))
			{
				//Create new point and assign it as closest and set a priority to it, since next frame it might not be closest
				BackupCurve();
				extruding = 1;
				captureMouse = true;
			}
			//Cancel Cut
			else if (cuttingInitialized && (GetMouseDown(1) || GetKeyDown(KeyCode.Escape)))
			{
				cuttingInitialized = false;
				GUIUtility.hotControl = 0;
				current.Use();
			}
			//Apply cut
			else if (cuttingInitialized && GetMouseDown(0))
			{
				cuttingInitialized = false;
				GUIUtility.hotControl = 0;
				Undo.RecordObject(targetObject, $"Curve split at {closestPointToMouseOnCurve}");
				curve.SplitAt(InverseTransformPoint(closestPointToMouseOnCurve));
			}

			//ControlPoint mode selection Dropdown menu
			#region CP Mode Dropdown
			//Cancel Dropdown
			if (/*drawContextMenu && */(current.type == EventType.Layout || GetMouseDown(0) || GetKeyDown(KeyCode.Escape)))
			{
				//drawContextMenu = false;
			}
			if (!showPointGUI && GetMouseDown(1) && closestControlIndex != -1)
			{
				mouse1Position = current.mousePosition;
				mouse1PressedTime = Time.time;
			}
			//Open Context Menu for control points mode if mouse psition is close to press position
			else if (closestControlIndex != -1 && mouse1PressedTime > 0 && Time.time - mouse1PressedTime < .5f &&
				GetMouseUp(1) && (mouse1Position - current.mousePosition).magnitude < 5f)
			{
				mouse1PressedTime = 0;
				current.Use();

				contextMenu = new GenericMenu();
				foreach (var val in Curve.BezierPoint.AllModes)
				{
					var mode = val;
					contextMenu.AddItem(new GUIContent(mode.ToString()),
						closestPoint.mode == mode, () =>
						{
							EditorGUI.BeginChangeCheck();
							if (selectedPointIdexes.Count > 0)
								foreach (var ind in selectedPointIdexes)
								{
									curve.SetPointMode(ind, mode);
								}
							else
								curve.SetPointMode(closestControlIndex, mode);
							//drawContextMenu = false;
							Undo.RecordObject(targetObject, $"Curve {closestControlIndex} point mode changed to {mode}");
						});
				}
				contextMenu.DropDown(new Rect(mouse1Position, Vector2.zero));

				//drawContextMenu = true;
			}
			#endregion

			//Delete selected point
			if (GetKeyDown(KeyCode.Delete))
			{
				SelectClosestPointToMouse(current);
				if (selectedPointIdexes.Count > 1)
				{
					Undo.RecordObject(targetObject, "Delete selected points");
					curve.RemoveMany(selectedPointIdexes);
					selectedPointIdexes.Clear();
					SelectClosestPointToMouse(current);
				}
				else if (selectedPointIdexes.Count == 1 || closestIndex != -1)
				{
					var ind = selectedPointIdexes.Count == 1 ? selectedPointIdexes[0] : closestIndex;
					if (curve.IsControlPoint(ind))
					{
						Undo.RecordObject(targetObject, "Delete closest point");
						curve.RemoveAt(ind);
						SelectClosestPointToMouse(current);
					}
				}
				current.Use();
			}

			//Selection Rect
			if (GetMouseDown(0) && GUIUtility.hotControl == 0)
			{
				mouse0DownPosition = current.mousePosition;
			}
			//Add/Remove points in rect if
			if (current.type == EventType.MouseDrag && GUIUtility.hotControl == 0 && current.button == 0)
			{
				mouse0DragRect = true;
				CallAllSceneViewRepaint();

				if(!current.shift && ! EditorGUI.actionKey)
					selectedPointIdexes.Clear();

				Rect rect = GetRectFromTwoPonts(mouse0DownPosition, current.mousePosition);
				for (int i = 0; i < curve.Points.Count; i++)
				{
					Curve.BezierPoint point = curve.Points[i];
					if (point.type != Curve.BezierPoint.Type.Control) continue;

					if (rect.Contains(SceneView.currentDrawingSceneView.camera.WorldToScreenPoint(point)))
					{
						if (!EditorGUI.actionKey && !selectedPointIdexes.Contains(i))
							selectedPointIdexes.Add(i);
						else if (EditorGUI.actionKey)
							selectedPointIdexes.Remove(i);
					}
				}
			}
			else if (GetMouseUp(0) && mouse0DragRect && GUIUtility.hotControl == 0)
			{
				mouse0DragRect = false;
				CallAllSceneViewRepaint();
			}
			else if (GetMouseUp(0) && !mouse0DragRect)
			{
				selectedPointIdexes.Clear();
				CallAllSceneViewRepaint();
			}

			//Cancel if any rotation
			else if (GetMouseUp(0))
				toolRotation = GetToolRotation(curve.GetSegmentIndex(closestIndex));

			//Handle alternative selection mode
			if (GetKeyDown(KeyCode.C))
			{
				current.Use();
				selectHandlesOnly = true;
				SelectClosestPointToMouse(current);

			}
			else if (selectHandlesOnly && GetKeyUp(KeyCode.C))
			{
				selectHandlesOnly = false;
				CallAllSceneViewRepaint();
			}

			if (current.type == EventType.MouseMove)
			{
				//Trigger repainting if it is needed by tool
				if (cuttingInitialized)
					CallAllSceneViewRepaint();

				closestPointToMouseOnCurve = HandleUtility.ClosestPointToPolyLine(curve.Vertices.SelectArray(v => TransformPoint(v)));

				SelectClosestPointToMouse(current);
			}
			if (GetKeyDown(KeyCode.S))
				sKeyDown = true;
			else if (GetKeyUp(KeyCode.S)) sKeyDown = false;

			bool GetKeyDown(KeyCode key) => current.type == EventType.KeyDown && current.keyCode == key;
			bool GetKeyUp(KeyCode key) => current.type == EventType.KeyUp && current.keyCode == key;
			bool GetMouseDown(int button) => current.type == EventType.MouseDown && current.button == button;
			bool GetMouseUp(int button) => current.type == EventType.MouseUp && current.button == button;
		}

		Vector3 TransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.TransformPoint(v) : v;
		Vector3 InverseTransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformPoint(v) : v;
		Vector3 TransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.TransformDirection(v) : v;
		Vector3 InverseTransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformDirection(v) : v;
		Vector3 TransformVector(Vector3 v) => targetIsGameObject ? targetTransform.TransformVector(v) : v;
		Vector3 InverseTransformVector(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformVector(v) : v;
		Matrix4x4 localToWorldMatrix => targetIsGameObject ? targetTransform.localToWorldMatrix : Matrix4x4.identity;
		Quaternion TransformRotation => targetIsGameObject ? targetTransform.rotation : Quaternion.identity;

		private void BackupCurve()
		{
			backupCurve = curve.Copy();
			backupClosestIndex = closestIndex;
			backupEditedPosition = editedPosition;
		}

		private Rect GetRectFromTwoPonts(Vector2 a, Vector2 b)
		{
			var aa = HandleUtility.GUIPointToScreenPixelCoordinate(a);
			var u = HandleUtility.GUIPointToScreenPixelCoordinate(b);
			var x0 = aa.x.Min(u.x);
			var x1 = aa.x.Max(u.x);
			var y0 = aa.y.Min(u.y);
			var y1 = aa.y.Max(u.y);

			var rect = Rect.MinMaxRect(x0, y0, x1, y1);
			return rect;
		}

		private void SnapPointToCurvePoints(ref Vector3 pos, int index)
		{
			var senseDist = HandleUtility.GetHandleSize(TransformPoint(pos)) * .2f;
			if (!current.shift && sKeyDown)
			{
				var c = Handles.color;
				var m = Handles.matrix;
				Handles.color = Color.yellow;
				Handles.matrix = localToWorldMatrix;

				var minDist = float.MaxValue;
				var minAxis = 0;
				var minPoint = Vector3.zero;
				Func<int,bool> predicate = i => i != index;
				if (curve.IsControlPoint(index))
					predicate = i => i < index - 1 || i > index + 1;
				foreach (var point in curve.Points.Where((p,i)=>predicate(i)))
				{
					var dist = DistanceToAxis(pos, point, senseDist, out int axis);
					if (dist < minDist && dist < senseDist)
					{
						minPoint = point;
						minDist = dist;
						minAxis = axis;
					}
					if (dist < senseDist)
					{
						switch (axis)
						{
							case 0:
								Handles.DrawAAPolyLine(point, new Vector3(pos.x, point.point.y, point.point.z));
								break;
							case 1:
								Handles.DrawAAPolyLine(point, new Vector3(point.point.x, pos.y, point.point.z));
								break;
							case 2:
								Handles.DrawAAPolyLine(point, new Vector3(point.point.x, point.point.y, pos.z));
								break;
							default:
								break;
						}
					}
				}
				if (minDist < senseDist)
				{
					pos = minAxis switch
					{
						0 => new Vector3(pos.x, minPoint.y, minPoint.z),
						1 => new Vector3(minPoint.x, pos.y, minPoint.z),
						2 => new Vector3(minPoint.x, minPoint.y, pos.z),
						_ => pos
					};
				}

				Handles.color = c;
				Handles.matrix = m;
			}

			static float DistanceToAxis(Vector3 vector1, Vector3 vector2, float senseDistance, out int axis)
			{
				float x = (vector1.x - vector2.x).Abs();
				float y = (vector1.y - vector2.y).Abs();
				float z = (vector1.z - vector2.z).Abs();

				if (x < senseDistance && y < senseDistance)
				{
					axis = 2;
					return (float)Math.Sqrt(x * x + y * y);
				}
				else if (x < senseDistance && z < senseDistance)
				{
					axis = 1;
					return (float)Math.Sqrt(x * x + z * z);
				}
				else if (y < senseDistance && z < senseDistance)
				{
					axis = 0;
					return (float)Mathf.Sqrt(y * y + z * z);
				}
				axis = -1;
				return (float)Math.Sqrt(x * x + y * y + z * z);
			}
		}

		private void SelectClosestPointToMouse(Event current)
		{
			if (captureMouse) return;
			//Just a reminder: GUI coordinates starts from top-left of actual view
			//Register closest point to mouse
			Vector2 mousePos = current.mousePosition;
			var minDist = float.MaxValue;
			var minCPDist = float.MaxValue;
			var minHDist = float.MaxValue;
			closestIndex = -1;
			closestControlIndex = -1;
			//closestHandleIndex = -1;
			for (int i = 0; i < curve.Points.Count; i++)
			{
				Vector3 point = curve.Points[i];
				//Skip Control Points while altSelectionMode
				if ((selectHandlesOnly && curve.IsControlPoint(i)) || (currentInternalTool == Tool.Rotate && !curve.IsControlPoint(i)))
					continue;

				var dist = HandleUtility.WorldToGUIPoint(TransformPoint(curve.Points[i])).DistanceTo(mousePos);
				if (dist <= minDist + .01f)
				{
					minDist = dist;
					closestIndex = i;
				}
				if (curve.IsControlPoint(i))
				{
					if (dist <= minCPDist + .01f)
					{
						minCPDist = dist;
						closestControlIndex = i;
					}
				}
				else
				if (dist <= minHDist + .1f)
				{
					minHDist = dist;
					//closestHandleIndex = i;
				}
			}
			if (curve.Points[closestControlIndex].mode == Curve.BezierPoint.Mode.Linear)
				closestIndex = closestControlIndex;

			closestPoint = curve.Points[closestIndex];
			editedPosition = TransformPoint(closestPoint.point);
			if (GUIUtility.hotControl == 0)
				toolRotation = GetToolRotation(curve.GetSegmentIndex(closestIndex));
			if (minDist > 100)
				closestIndex = -1;
		}

		private void DrawHandles()
		{
			var m = Handles.matrix;
			//Handles.matrix = targetTransform.localToWorldMatrix;
			Handles.matrix = Matrix4x4.identity;
			var c = Handles.color;
			Handles.color = Color.white.MultiplyAlpha(.66f);
			var cam = Camera.current;

			//Draw points
			for (int i = 0; i < curve.Points.Count; i++)
			{
				var globalPointPos = TransformPoint(curve.Points[i]);
				bool isControlPoint = curve.IsControlPoint(i);
				float size = HandleUtility.GetHandleSize(globalPointPos) * (isControlPoint ? .2f : .15f);
				var segInd = curve.GetSegmentIndex(i);

				var selected = selectedPointIdexes.Contains(i);
				float width = selected ? 3f : 1f;
				Handles.color = selected ? Color.white : Color.white.MultiplyAlpha(.66f);

				if (isControlPoint)
				{
					GUIUtils.DrawCircle(globalPointPos, (cam.transform.position - globalPointPos).normalized, size, width: width);
					//Handles.Label(globalPointPos, $"{i}, ind={i} segind={curve.GetSegmentIndex(i)}");
					//DrawAxes(.3f, globalPointPos, curve.GetCPRotation(segInd) * targetTransform.rotation);
					Handles.DrawAAPolyLine(globalPointPos, globalPointPos + curve.GetCPRotation(segInd) * TransformRotation * Vector3.up * .25f);
				}
				else
				{
					//float time = curve.Points[i].type == Curve.BezierPoint.Type.RightHandle ? 0f : 1f;
					GUIUtils.DrawRectangle(globalPointPos,
						Quaternion.LookRotation(cam.transform.position - globalPointPos, TransformDirection(curve.GetCPTangent(segInd))),
						Vector2.one * size, width);
				}
			}

			//Draw tool
			if (closestIndex != -1 && !showPointGUI)
			{
				Vector3 pos = default;
				if (currentInternalTool == Tool.Move)
				{
					var rotation = Tools.pivotRotation == PivotRotation.Local ? curve.Points[closestIndex].rotation * TransformRotation : Tools.handleRotation;
					if (!current.shift)
					{
						pos = Handles.PositionHandle(editedPosition, rotation);
						SnapPointToCurvePoints(ref pos, closestIndex);
					}
					else
						pos = Handles.FreeMoveHandle(editedPosition, rotation, HandleUtility.GetHandleSize(editedPosition) * .2f, Vector3.one * .2f, Handles.RectangleHandleCap);

					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(targetObject, "Point position changed");

						//TODO Extrude mechanics. Here we know movement direction, so we can start extruding in right direction and change if needed
						//DoExtrusionWhileMoveTool(pos);

						if (selectedPointIdexes.Count == 0)
						{
							curve.SetPoint(closestIndex, InverseTransformPoint(pos));
						}
						else
						{
							var delta = InverseTransformPoint(pos) - curve.Points[closestIndex];
							foreach (var ind in selectedPointIdexes)
							{
								var point = curve.Points[ind];
								curve.SetPoint(ind, point + delta);
							}
						}
						editedPosition = TransformPoint(curve.Points[closestIndex]);
					}
				}
				else if (currentInternalTool == Tool.Rotate && curve.IsControlPoint(closestIndex))
				{
					int segmentIndex = curve.GetSegmentIndex(closestIndex);
					if (Tools.pivotRotation == PivotRotation.Local)
						toolRotation = GetToolRotation(segmentIndex);
					float handleSize = HandleUtility.GetHandleSize(editedPosition);

					var rot = Handles.DoRotationHandle(toolRotation, editedPosition);

					if (EditorGUI.EndChangeCheck())
					{
						Undo.RecordObject(targetObject, "Point rotation changed");
						var diff = rot * toolRotation.Inverted();
						if (selectedPointIdexes.Count == 0)
						{
							curve.SetCPRotationWithHandles(segmentIndex, diff, additive: true);
						}
						else
						{
							foreach (var ind in selectedPointIdexes)
							{
								segmentIndex = curve.GetSegmentIndex(ind);
								var rotatedRelativeEditedPoint = diff * (TransformPoint(curve.Points[ind]) - editedPosition);
								curve.SetPoint(ind, closestPoint + rotatedRelativeEditedPoint);
								curve.SetCPRotationWithHandles(segmentIndex, diff, additive: true);
							}
						}
						toolRotation = rot;
					}
					DrawAxes(handleSize, editedPosition, curve.GetCPRotation(curve.GetSegmentIndex(closestIndex)));
				}
			}
			else if (showPointGUI && closestIndex != -1 && current.type != EventType.Layout)
			{
				Handles.BeginGUI();
				var guiRect = new Rect(HandleUtility.WorldToGUIPoint(editedPosition), new Vector3(300, EditorGUIUtility.singleLineHeight * 3 + 8));
				captureMouse = guiRect.Contains(current.mousePosition);
				GUI.Box(guiRect, GUIContent.none);

				//Draw position line
				var line = guiRect.FirstLine(EditorGUIUtility.singleLineHeight + 4).Extend(-2);
				GUI.Label(line.MoveLeftFor(30), "pos");
				EditorGUI.BeginChangeCheck();
				var pos = EditorGUI.Vector3Field(line, GUIContent.none, closestPoint.point);
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Set position");
					curve.SetPoint(closestIndex, pos);
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
					curve.SetCPRotationWithHandles(curve.GetSegmentIndex(closestIndex), Quaternion.Euler(rot), index: closestIndex, isRotationAlligned: false);
					closestPoint = curve.Points[closestIndex];
				}
				//Draw Modes dropdown
				line = line.MoveDown();
				GUI.Label(line.MoveLeftFor(30), "mode");
				GUI.enabled = curve.IsControlPoint(closestIndex);
				EditorGUI.BeginChangeCheck();
				var modeId = EditorGUI.Popup(line, Curve.BezierPoint.AllModes.IndexOf(closestPoint.mode), Curve.BezierPoint.AllModes.SelectArray(m => m.ToString()));
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Set mode");
					curve.SetPointMode(closestIndex, Curve.BezierPoint.AllModes[modeId]);
					closestPoint = curve.Points[closestIndex];
				}
				GUI.enabled = true;
				Handles.EndGUI();

				Quaternion r = cam.transform.rotation;
				float h = HandleUtility.GetHandleSize(editedPosition) * .2f;
				Handles.color = Color.white / 5 * 3;
				Handles.DrawAAConvexPolygon(editedPosition, editedPosition + r * Vector3.right * h, editedPosition + r * Vector3.down * h);
			}

			//DrawClosestPoint on cut
			if (cuttingInitialized)
			{
				Handles.color = Color.red / 2 + Color.white / 2;
				Handles.DrawSolidDisc(closestPointToMouseOnCurve, -cam.transform.forward, .1f * HandleUtility.GetHandleSize(closestPointToMouseOnCurve));
			}

			//DrawSelectionRect
			if (mouse0DragRect)
			{
				var mpos = current.mousePosition;
				var rect = new Vector3[] {
					HandleUtility.GUIPointToWorldRay(mpos).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(new Vector2(mpos.x, mouse0DownPosition.y)).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(mouse0DownPosition).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(new Vector2(mouse0DownPosition.x, mpos.y)).GetPoint(.1f),
					HandleUtility.GUIPointToWorldRay(mpos).GetPoint(.1f)
				};
				Handles.color = Color.gray / 3;
				Handles.DrawAAConvexPolygon(rect);
				Handles.color = Color.gray / 3 + Color.white / 3;
				Handles.DrawAAPolyLine(rect);
			}

			Handles.matrix = m;
			Handles.color = c;
		}

		private void DoExtrusionWhileMoveTool(Vector3 newPosition)
		{
			//Check if direction changed and reset process
			if (extruding > 1)
			{
				var newDirection = Vector3.Dot(newPosition - editedPosition, closestPoint.rotation * Vector3.forward) > 0;
				if (newDirection != extrusionRightDirection)
				{
					curve.CopyFrom(backupCurve);
					extruding = 1;
				}
			}
			//First step
			if (startedExtruding)
			{
				extrusionRightDirection = Vector3.Dot(newPosition - editedPosition, closestPoint.rotation * Vector3.forward) > 0;

				curve.SplitAt(curve.GetSegmentIndex(closestControlIndex) + (extrusionRightDirection ? 0 : -1), extrusionRightDirection ? 0f : 1f);

				if (extrusionRightDirection)
					closestIndex--;
				closestPoint = curve.Points[closestIndex];
				closestControlIndex = closestIndex;
				extruding++;
			}
		}

		/// <summary>
		/// Curve preview and in editor draw cycle
		/// </summary>
		/// <param name="Highlight"></param>
		private void DrawCurveAndPoints(bool Highlight = false)
		{
			var m = Handles.matrix;
			Handles.matrix = localToWorldMatrix;
			var c = Handles.color;
			Handles.color = Color.white.MultiplyAlpha(.66f);
			DrawCurveFromVertexData(curve.VertexData);
			foreach (var seg in curve.Segments)
			{
				//Handles.DrawBezier(seg[0], seg[3], seg[1], seg[2], Color.green, null, Highlight ? 3 : 2);
				Handles.DrawAAPolyLine(seg[0], seg[1]);
				Handles.DrawAAPolyLine(seg[2], seg[3]);
			}

			Handles.color = Color.red / 2 + Color.white / 2;
			foreach (var vert in curve.VertexData)
			{
				Handles.DrawAAPolyLine(vert.point, vert.point + vert.normal * .2f);
				//Handles.Label(vert.point, $"{vert.length}, {vert.time}");
			}

			Handles.matrix = m;
			Handles.color = c;
		}

		/// <summary>
		/// Draw two sided curve
		/// </summary>
		/// <param name="vertexData"></param>
		/// <exception cref="NotImplementedException"></exception>
		private void DrawCurveFromVertexData(IEnumerable<BezierCurveVertexData.VertexData> vertexData)
		{
			var c = Handles.color;
			var vertices = vertexData.Select(v=>v.point).Take(1).ToList();
			Transform camTransform = Camera.current.transform;
			var upDotCamera = Vector3.Dot(vertexData.FirstOrDefault().up, camTransform.forward);
			foreach (var v in vertexData.Skip(1))
			{
				vertices.Add(v.point);
				var newDot = Vector3.Dot(v.up, camTransform.forward);
				if (upDotCamera != newDot)
				{
					DrawVertices(vertices, upDotCamera > 0);

					vertices.Clear();
					vertices.Add(v.point);
					upDotCamera = newDot;
				}
			}
			DrawVertices(vertices, upDotCamera > 0);

			Handles.color = c;

			static void DrawVertices(List<Vector3> vertices, bool towardCamera)
			{
				Handles.color = towardCamera ? Color.red / 3 * 2 + Color.green / 3 : Color.green;
				Handles.DrawAAPolyLine((towardCamera ? 4 : 2), vertices.ToArray());
			}
		}

		private Quaternion GetToolRotation(int segmentInd) => Tools.pivotRotation switch
		{
			PivotRotation.Global => Tools.handleRotation,
			PivotRotation.Local => TransformRotation * curve.GetCPRotation(segmentInd),
			_ => Quaternion.identity
		};
		//toolRotation = targetTransform.rotation* curve.GetCPRotation(segmentIndex);
		private void DrawAxes(float handleSize, Vector3 position, Quaternion rotation)
		{
			var c = Handles.color;
			Handles.color = Color.red;
			Handles.DrawAAPolyLine(position, position + rotation * Vector3.right * handleSize);
			Handles.color = Color.green;
			Handles.DrawAAPolyLine(position, position + rotation * Vector3.up * handleSize);
			Handles.color = Color.blue;
			Handles.DrawAAPolyLine(position, position + rotation * Vector3.forward * handleSize);
			Handles.color = c;
		}

	}
}