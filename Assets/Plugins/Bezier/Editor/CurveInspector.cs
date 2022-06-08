using UnityEngine;
using UnityEditor;
using System;
using RectEx;
using UnityEditor.Toolbars;
using System.Linq;
using Utility.Editor;

namespace BezierCurveZ
{
	/// <summary>
	/// <see cref="Curve"/> Property Drawer. Allows curve editing
	/// </summary>
	[CustomPropertyDrawer(typeof(Curve))]
	public class CurvePropertyDrawer : PropertyDrawer
	{
		private Component targetObject;
		private Transform targetTransform;

		private Curve curve;
		[NonSerialized]
		private static CurvePropertyDrawer currentPropertyDrawer;
		private bool isInEditMode;
		private bool isMouseOver;
		private bool previewAlways;
		private Vector3 closestPosition;
		private Curve.Point closestPoint;
		private Vector3 editedPosition;
		private int closestIndex;

		#region editor behaviour
		private bool IsCurrentlyEditedDrawer => currentPropertyDrawer == this;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
			IsCurrentlyEditedDrawer ? EditorGUIUtility.singleLineHeight + 2 + editorHeight : EditorGUIUtility.singleLineHeight;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (targetObject == null)
			{
				targetObject = (Component)property.serializedObject.targetObject;
				targetTransform = targetObject.transform;
			}
			curve = fieldInfo.GetValue(targetObject) as Curve;

			var rootRows = IsCurrentlyEditedDrawer ?
				position.Column(new float[] { 0f, 1f }, new float[] { EditorGUIUtility.singleLineHeight, 0f }) :
				new Rect[] { position };
			var firstRow = rootRows[0].Row(new float[] { 0f, 0f, 0f, 1f }, new float[] { EditorGUIUtility.labelWidth, 100f, 22f, 0f });
			EditorGUI.LabelField(firstRow[0], label);
			if (!IsCurrentlyEditedDrawer && GUI.Button(firstRow[1], "Edit"))
			{
				StartEditor();
			}
			else if (IsCurrentlyEditedDrawer && Event.current.type != EventType.Layout)
			{
				DrawCurveEditor(rootRows[1]);
				var c = GUI.color;
				GUI.color = Color.white / 2 + Color.red / 2;
				if (GUI.Button(firstRow[1], "Finish Editing"))
				{
					FinishEditor();
				}
				GUI.color = c;
			}

			SetUpPreview(GUI.Toggle(firstRow[2], previewAlways, GUIContent.none));

			if (!isInEditMode)
			{
				CheckIfMouseIsOver(position);
			}

			// Notify of undo/redo that might modify the path
			if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "UndoRedoPerformed")
			{
				//data.PathModifiedByUndo();
			}
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
			if (!IsCurrentlyEditedDrawer && position.Contains(Event.current.mousePosition))
			{
				if (!isMouseOver)
				{
					isMouseOver = true;
					SceneView.duringSceneGui -= _OnScenePreviewInternal;
					SceneView.duringSceneGui += _OnScenePreviewInternal;
					Selection.selectionChanged = UnsubscribeSetUpPreview;
					CallSceneRedraw();
				}
			}
			else if (isMouseOver)
			{
				isMouseOver = false;
				if (!previewAlways)
					SceneView.duringSceneGui -= _OnScenePreviewInternal;
				CallSceneRedraw();
			}

			void UnsubscribeSetUpPreview()
			{
				Selection.selectionChanged -= UnsubscribeSetUpPreview;
				SceneView.duringSceneGui -= _OnScenePreviewInternal;
			}
		}

		private void StartEditor()
		{
			isInEditMode = true;
			currentPropertyDrawer?.FinishEditor();
			Selection.selectionChanged += FinishEditor;
			AssemblyReloadEvents.beforeAssemblyReload += FinishEditor;
			SceneView.duringSceneGui += OnSceneGUI;
			CallSceneRedraw();
			currentPropertyDrawer = this;
			CurveEditorOverlay.Show();
			lastTool = Tools.current;
			Tools.current = Tool.None;
		}

		private void FinishEditor()
		{
			isInEditMode = false;
			Selection.selectionChanged -= FinishEditor;
			AssemblyReloadEvents.beforeAssemblyReload -= FinishEditor;
			SceneView.duringSceneGui -= OnSceneGUI;
			CallSceneRedraw();
			currentPropertyDrawer = null;
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

		private void CallSceneRedraw()
		{
			foreach (var scene in SceneView.sceneViews)
				((SceneView)scene).Repaint();
		}

		private float editorHeight = EditorGUIUtility.singleLineHeight * 4;
		private Tool lastTool;
		private float mouse1PressedTime;
		private GenericMenu contextMenu;
		private Vector2 mouse1Position;
		private bool drawContextMenu;
		private bool cuttingInitialized;
		private Vector3 closestPointToMouseOnCurve;
		private int controlID;
		private bool altSelectionMode;

		private void DrawCurveEditor(Rect position)
		{
			var lines = position.Column(4);
			var firstLine = lines[0].Row(2);
			curve.MaxAngleError = EditorGUI.FloatField(firstLine[0], "MaxAngleError", curve.MaxAngleError);
			curve.MinSamplingDistance = EditorGUI.FloatField(firstLine[1], "MinSamplingDistance", curve.MinSamplingDistance);
		}

		private void OnSceneGUI(SceneView scene)
		{
			controlID = GUIUtility.GetControlID(932795648, FocusType.Passive);
			if (Event.current.type == EventType.Layout)
				//Magic thing to stop mouse from selecting other objects
				HandleUtility.AddDefaultControl(controlID);

			if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "UndoRedoPerformed")
				curve.Update();
			Input();

			DrawCurveAndPoints();
			DrawHandles();
		}

		private void Input()
		{
			Event current = Event.current;

			//ControlPoint mode selection Dropdown menu
			#region CP Mode Dropdown
			//Cancel Dropdown
			if (drawContextMenu && (current.type == EventType.Layout || GetMouseDown(0) || GetKeyDown(KeyCode.Escape)))
			{
				drawContextMenu = false;
			}
			//Cut/Extrude
			if (GetKeyUp(KeyCode.V))
			{
				if (closestIndex == 0) curve.AddPointAtStart(curve.Points[closestIndex]);
				else if (closestIndex == curve.Points.Count - 1)
				{
					curve.AddPointAtEnd(curve.Points[closestIndex]);
					closestIndex++;
					closestPoint = curve.Points[closestIndex];
				} else 
				{
					cuttingInitialized = true;
					GUIUtility.hotControl = controlID;
				}
			}
			//Cancel Cut
			if ((GetMouseDown(1) || GetKeyDown(KeyCode.Escape)) && cuttingInitialized)
			{
				cuttingInitialized = false;
				GUIUtility.hotControl = 0;
			}
			else if (GetMouseDown(1) && closestPoint.type == Curve.Point.Type.Control && closestIndex != -1)
			{
				mouse1Position = current.mousePosition;
				mouse1PressedTime = Time.time;
			}
			//Open Context Menu for control points
			else if (!drawContextMenu && closestIndex != -1 && mouse1PressedTime > 0 && Time.time - mouse1PressedTime < .5f &&
				GetMouseUp(1) && (mouse1Position - current.mousePosition).magnitude < 5f)
			{
				mouse1PressedTime = 0;
				current.Use();

				contextMenu = new GenericMenu();
				foreach (var val in Curve.Point.AllModes)
				{
					var mode = val;
					contextMenu.AddItem(new GUIContent(mode.ToString()),
						closestPoint.mode == mode, () => {
							curve.SetPointMode(closestIndex, mode);
							drawContextMenu = false;
						});
				}
				contextMenu.DropDown(new Rect(mouse1Position, Vector2.zero));

				drawContextMenu = true;
			} else if (cuttingInitialized && GetMouseDown(0))
			{
				cuttingInitialized = false;
				GUIUtility.hotControl = 0;
				curve.SplitAt(closestPointToMouseOnCurve);
			}
			#endregion

			//Handle selection mode
			if (GetKeyDown(KeyCode.C))
			{
				current.Use();
				altSelectionMode = true;
				SelectClosestPointToMouse(current);

			} else if (altSelectionMode && GetKeyUp(KeyCode.C))
			{
				altSelectionMode = false;
			} else if (altSelectionMode && current.type == EventType.MouseDrag)
			{
				GUIUtility.hotControl = controlID;
			}

			if (current.type == EventType.MouseMove)
			{
				//Trigger repainting if it is needed by tool
				if (cuttingInitialized) SceneView.currentDrawingSceneView.Repaint();

				closestPointToMouseOnCurve = HandleUtility.ClosestPointToPolyLine(curve.Vertices.SelectArray(v => targetTransform.TransformPoint(v)));

				SelectClosestPointToMouse(current);
			}

			//if (current.type == EventType.MouseDown)
			//	current.Use();

			bool GetKeyDown(KeyCode key) => current.type == EventType.KeyDown && current.keyCode == key;
			bool GetKeyUp(KeyCode key) => current.type == EventType.KeyUp && current.keyCode == key;
			bool GetMouseDown(int button) => current.type == EventType.MouseDown && current.button == button;
			bool GetMouseUp(int button) => current.type == EventType.MouseUp && current.button == button;
		}

		private void SelectClosestPointToMouse(Event current)
		{
			//Just a reminder: GUI coordinates starts from top-left of actual view
			//Register closest point to mouse
			Vector2 mousePos = current.mousePosition;
			var minDist = float.MaxValue;
			closestIndex = -1;
			for (int i = 0; i < curve.Points.Count; i++)
			{
				Vector3 point = curve.Points[i];
				//Skip Control Points while altSelectionMode
				if (altSelectionMode && curve.Points[i].type == Curve.Point.Type.Control) continue;

				var dist = HandleUtility.WorldToGUIPoint(targetTransform.TransformPoint(curve.Points[i])).DistanceTo(mousePos);
				if (dist < minDist) { minDist = dist; closestIndex = i; }
			}
			closestPoint = curve.Points[closestIndex];
			editedPosition = targetTransform.TransformPoint(closestPoint.point);
			if (minDist > 100)
				closestIndex = -1;
		}

		private void DrawHandles()
		{
			EditorGUI.BeginChangeCheck();
			var m = Handles.matrix;
			//Handles.matrix = targetTransform.localToWorldMatrix;
			Handles.matrix = Matrix4x4.identity;
			var c = Handles.color;
			Handles.color = Color.white.MultiplyAlpha(.66f);

			//Draw GUI
			Handles.BeginGUI();
			GUI.Label(new Rect(Event.current.mousePosition, new Vector2(200, 22)), "hotControl " + GUIUtility.hotControl);

			Handles.EndGUI();

			//Draw points
			for (int i = 0; i < curve.Points.Count; i++)
			{
				var globalPointPos = transform(curve.Points[i]);
				var dir = curve.Points[i].type == Curve.Point.Type.Control ? getHandleFromDirection(i) : Vector3.up;
				var normal = curve.Points[i].type == Curve.Point.Type.Control ? -Camera.current.transform.forward : Vector3.Cross(dir, Vector3.Cross(dir, globalPointPos - Camera.current.transform.position)).normalized;
				GUIUtils.DrawCircle(globalPointPos, normal, .2f * HandleUtility.GetHandleSize(globalPointPos),
					false, curve.Points[i].type == Curve.Point.Type.Control ? 12 : 4, dir);
			}

			//Draw tool
			if (closestIndex != -1)
			{
				editedPosition = Handles.PositionHandle(editedPosition, CurveEditorTransformOrientation.rotation);

				//Do undo
				if (EditorGUI.EndChangeCheck())
				{
					Undo.RecordObject(targetObject, "Point position changed");
					curve.SetPoint(closestIndex, targetTransform.InverseTransformPoint(editedPosition));
				}
			}

			//DrawClosestPoint
			if (cuttingInitialized)
			{
				Handles.color = Color.red / 2 + Color.white / 2;
				Handles.DrawSolidDisc(closestPointToMouseOnCurve, -Camera.current.transform.forward, .1f * HandleUtility.GetHandleSize(closestPointToMouseOnCurve));
			}

			Handles.matrix = m;
			Handles.color = c;

			//Local methods

			Vector3 transform(Vector3 pos) => targetTransform.TransformPoint(pos);

			Vector3 getHandleFromDirection(int i) =>
				curve.Points[i].type == Curve.Point.Type.LeftHandle ? targetTransform.TransformDirection(curve.Points[i+1] - curve.Points[i]) :
				curve.Points[i].type == Curve.Point.Type.RightHandle ? targetTransform.TransformDirection(curve.Points[i-1] - curve.Points[i]) : default;
		}

		private void DrawCurveAndPoints(bool Highlight = false)
		{
			var m = Handles.matrix;
			Handles.matrix = targetTransform.localToWorldMatrix;
			var c = Handles.color;
			Handles.color = Color.white.MultiplyAlpha(.66f);
			foreach (var seg in curve.Segments)
			{
				Handles.DrawBezier(seg[0], seg[3], seg[1], seg[2], Color.green, null, Highlight ? 3 : 2);
				Handles.DrawAAPolyLine(seg[0], seg[1]);
				Handles.DrawAAPolyLine(seg[2], seg[3]);
			}

			Handles.color = Color.red / 2 + Color.white / 2;
			foreach (var vert in curve.VertexData)
			{
				//Handles.DrawSolidDisc(vert.point, -Camera.current.transform.forward, HandleUtility.GetHandleSize(vert.point) * .05f);
				Handles.DrawAAPolyLine(vert.point, vert.point + vert.rotation * Vector3.right * .2f);
			}

			Handles.matrix = m;
			Handles.color = c;
		}
	}
}