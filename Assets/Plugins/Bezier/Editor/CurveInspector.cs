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
		private Curve curve;
		[NonSerialized]
		private static CurvePropertyDrawer currentPropertyDrawer;
		private bool isInEditMode;
		private bool isMouseOver;
		private bool previewAlways;
		private Vector3 closestPoint;
		private int closestIndex;

		#region editor behaviour
		private bool IsCurrentlyEditedDrawer => currentPropertyDrawer == this;

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
			IsCurrentlyEditedDrawer ? EditorGUIUtility.singleLineHeight + 2 + editorHeight : EditorGUIUtility.singleLineHeight;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (targetObject == null)
				targetObject = (Component)property.serializedObject.targetObject;
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

		private void DrawCurveEditor(Rect position)
		{
			var lines = position.Column(4);
			var firstLine = lines[0].Row(2);
			curve.MaxAngleError = EditorGUI.FloatField(firstLine[0], "MaxAngleError", curve.MaxAngleError);
			curve.MinSamplingDistance = EditorGUI.FloatField(firstLine[1], "MinSamplingDistance", curve.MinSamplingDistance);
		}

		private void OnSceneGUI(SceneView scene)
		{
			if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "UndoRedoPerformed")
				curve.Update();
			Input();

			DrawCurveAndPoints();
			DrawHandles();
		}

		private void Input()
		{
			if (Event.current.type == EventType.MouseMove)
			{
				Vector2 mousePos = new Vector2(Event.current.mousePosition.x, Screen.height - Event.current.mousePosition.y);
				var minDist = float.MaxValue;
				closestIndex = -1;
				for (int i = 0; i < curve.Points.Count; i++)
				{
					Vector3 point = curve.Points[i];
					var dist = Camera.current.WorldToScreenPoint(Handles.matrix * point).DistanceTo(mousePos);
					if (dist < minDist) { minDist = dist; closestIndex = i; }
				}
				closestPoint = curve.Points[closestIndex];
			}
			if (Event.current.type == EventType.MouseUp)
			{

			}
		}

		private void DrawHandles()
		{
			EditorGUI.BeginChangeCheck();

			var m = Handles.matrix;
			Handles.matrix = targetObject.transform.localToWorldMatrix;
			var c = Handles.color;
			Handles.color = Color.white.MultiplyAlpha(.66f);

			//TODO pick point, and edit it, stop picking while mouse down

			Handles.color = Color.white.MultiplyAlpha(.66f);
			for (int i = 0; i < curve.Points.Count; i++)
			{
				var point = curve.Points[i];
				GUIUtils.DrawCircle(point, -Camera.current.transform.forward, .1f * HandleUtility.GetHandleSize(point));
			}
			//var newPoint = Handles.DoPositionHandle(closestPoint, CurveEditorTransformOrientation.rotation);
			var newPoint = Handles.PositionHandle(closestPoint, CurveEditorTransformOrientation.rotation);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(targetObject, "Point position changed");
				curve.SetPoint(closestIndex, newPoint);
			}

			Handles.matrix = m;
			Handles.color = c;
		}

		private void DrawCurveAndPoints(bool Highlight = false)
		{
			var m = Handles.matrix;
			Handles.matrix = targetObject.transform.localToWorldMatrix;
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