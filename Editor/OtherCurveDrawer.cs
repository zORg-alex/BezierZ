using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System;
using Utility;
using RectEx;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(OtherCurve))]
public class OtherCurveDrawer : PropertyDrawer
{
	private OtherCurve curve;
	private UnityEngine.Object targetObject;
	private Transform targetTransform;
	private bool targetIsGameObject;
	Vector3 TransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.TransformPoint(v) : v;
	Vector3 InverseTransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformPoint(v) : v;
	Vector3 TransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.TransformDirection(v) : v;
	Vector3 InverseTransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformDirection(v) : v;
	Vector3 TransformVector(Vector3 v) => targetIsGameObject ? targetTransform.TransformVector(v) : v;
	Vector3 InverseTransformVector(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformVector(v) : v;
	Matrix4x4 localToWorldMatrix => targetIsGameObject ? targetTransform.localToWorldMatrix : Matrix4x4.identity;
	Quaternion TransformRotation => targetIsGameObject ? targetTransform.rotation : Quaternion.identity;

	[NonSerialized]
	private static OtherCurveDrawer currentlyEditedPropertyDrawer;
	private Tool lastTool;

	private Event current;
	private bool IsCurrentlyEditedDrawer => currentlyEditedPropertyDrawer == this;
	private OtherCurve currentlyEditedCurve;
	private bool isInEditMode { get => curve?._isInEditMode ?? false; set { if (curve != null) curve._isInEditMode = value; } }
	private bool isMouseOver;

	private Texture2D isOpenTexture;
	private Texture2D isClosedTexture;
	private Texture2D isOpenClosedTexture => curve.IsClosed ? isClosedTexture : isOpenTexture;
	private Texture2D EyeOpenTexture;
	private Texture2D EyeClosedTexture;
	public Texture2D PreviewTexture => curve._previewOn ? EyeOpenTexture : EyeClosedTexture;
	private bool initialized;
	private bool _isMouseOverProperty;
	private Color CurveColor = Color.green * .6666f + Color.white * .3333f;
	private Color NormalColor = Color.red * .5f + Color.white * .5f;
	private Color UpColor = Color.green * .5f + Color.white * .5f;
	private Color ForwardColor = Color.blue * .5f + Color.white * .5f;
	private Color HandleColor = Color.white * .6666f;

	private string editButtonText => isInEditMode ? "Stop Editing" : "Edit";

	public Action<SceneView> OnSceneGUI { get; private set; }

	public override bool CanCacheInspectorGUI(SerializedProperty property) => false;

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => PropertyHeight(property.GetValue<OtherCurve>());
	private float PropertyHeight(OtherCurve curve) => EditorGUIUtility.singleLineHeight + (curve._isInEditMode ? EditorGUIUtility.singleLineHeight + 32 + 6 : 0);

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		current = Event.current;
		//Should fix array properties bug
		GetObjects(property);
		if (!initialized) Initialize(property);

		EditorGUI.BeginProperty(position, label, property);
		var il = EditorGUI.indentLevel;
		EditorGUI.indentLevel = 0;
		var c = GUI.color;

		var mouseOverProperty = position.Contains(current.mousePosition);
		if (_isMouseOverProperty && !mouseOverProperty)
			OnMouseLeaveProperty();
		if (!_isMouseOverProperty && mouseOverProperty)
			OnMouseEnterProperty();
		_isMouseOverProperty = mouseOverProperty;

		var firstLine = position.FirstLine();
		var rects = EditorGUI.PrefixLabel(firstLine, label).Row(new float[] { 1, 0 }, new float[] { 0, 24 });
		if (isInEditMode) GUI.color = Color.red *.6666f + Color.white *.3333f;
		if (GUI.Button(rects[0], new GUIContent(editButtonText)))
		{
			OnEditPressed();
		}
		GUI.color = c;

		var previewOn = GUI.Toggle(rects[1], curve._previewOn, PreviewTexture, "Button");
		if (curve._previewOn != previewOn) {
			curve._previewOn = previewOn;
			OnPreviewChanged();
		}

		EditorGUI.EndProperty();
		EditorGUI.indentLevel = il;
		GUI.color = c;
	}

	private void GetObjects(SerializedProperty property)
	{
		if (targetObject == null)
		{
			targetObject = property.serializedObject.targetObject;
			if (targetObject is Component c)
			{
				targetTransform = c.transform;
				targetIsGameObject = true;
			}
		}
		curve = property.GetValue<OtherCurve>();
	}

	private void Initialize(SerializedProperty property)
	{
		if (EditorGUIUtility.isProSkin)
		{
			isOpenTexture = Resources.Load<Texture2D>("Bezier.IsOpen_d");
			isClosedTexture = Resources.Load<Texture2D>("Bezier.IsClosed_d");
			EyeOpenTexture = Resources.Load<Texture2D>("Bezier.EyeOpen_d");
			EyeClosedTexture = Resources.Load<Texture2D>("Bezier.EyeClosed_d");
		}
		else
		{
			isOpenTexture = Resources.Load<Texture2D>("Bezier.IsOpen");
			isClosedTexture = Resources.Load<Texture2D>("Bezier.IsClosed");
			EyeOpenTexture = Resources.Load<Texture2D>("Bezier.EyeOpen");
			EyeClosedTexture = Resources.Load<Texture2D>("Bezier.EyeClosed");
		}
		if (curve._previewOn)
			OnPreviewChanged();
		initialized = true;
	}

	private void IsClosedChanged(bool value)
	{
		curve.IsClosed = value;
	}

	private void OnEditPressed()
	{
		if (!isInEditMode)
			StartEditor();
		else
			FinishThisEditor();
	}

	private void StartEditor()
	{
		if (currentlyEditedPropertyDrawer != this && (currentlyEditedCurve?._isInEditMode ?? false))
			currentlyEditedPropertyDrawer.FinishEditor(currentlyEditedCurve);
		isInEditMode = true;
		currentlyEditedPropertyDrawer = this;
		currentlyEditedCurve = curve;
		SubscribeToEditorEvents();
		CallAllSceneViewRepaint();
		lastTool = Tools.current;
		Tools.current = Tools.current == Tool.Move || Tools.current == Tool.Rotate ? Tools.current : Tool.None;
	}

	private void FinishThisEditor(Scene s) => FinishEditor(curve);
	private void FinishThisEditor() => FinishEditor(curve);
	private void FinishEditor(OtherCurve curve)
	{
		curve._isInEditMode = false;
		currentlyEditedPropertyDrawer = null;
		currentlyEditedCurve = null;
		UnsubscribeFromEditorEvents();
		CallAllSceneViewRepaint();
	}

	private void SubscribeToEditorEvents()
	{
		Selection.selectionChanged += FinishThisEditor;
		EditorSceneManager.sceneClosed += FinishThisEditor;
		AssemblyReloadEvents.beforeAssemblyReload += FinishThisEditor;
		SceneView.duringSceneGui += OnSceneGUI;
	}

	private void UnsubscribeFromEditorEvents()
	{
		Selection.selectionChanged -= FinishThisEditor;
		EditorSceneManager.sceneClosed -= FinishThisEditor;
		AssemblyReloadEvents.beforeAssemblyReload -= FinishThisEditor;
		SceneView.duringSceneGui -= OnSceneGUI;
	}

	private void CallAllSceneViewRepaint()
	{
		foreach (SceneView sv in SceneView.sceneViews)
			sv?.Repaint();
	}

	void OnMouseEnterProperty()
	{
		if (!curve._previewOn)
			SceneView.duringSceneGui += OnPreview;
	}
	void OnMouseLeaveProperty()
	{
		if (!curve._previewOn)
			SceneView.duringSceneGui -= OnPreview;
	}
	void PreviewStopped()
	{
		curve._previewOn = false;
		OnPreviewChanged();
	}
	void OnPreviewChanged(Scene s) => OnPreviewChanged();
	void OnPreviewChanged()
	{
		Selection.selectionChanged -= OnPreviewChanged;
		EditorSceneManager.sceneClosed -= OnPreviewChanged;
		AssemblyReloadEvents.beforeAssemblyReload -= OnPreviewChanged;
		SceneView.duringSceneGui -= OnPreview;
		if (curve._previewOn)
		{
			Selection.selectionChanged += OnPreviewChanged;
			EditorSceneManager.sceneClosed += OnPreviewChanged;
			AssemblyReloadEvents.beforeAssemblyReload += OnPreviewChanged;
			SceneView.duringSceneGui += OnPreview;
		}
	}

	void OnPreview(SceneView sv)
	{
		DrawCurve();
	}

	/// <summary>
	/// Draws OnMouseOver curve preview and base part of Editor draw call
	/// </summary>
	private void DrawCurve()
	{
		var c = Handles.color;
		foreach (var segment in curve.Segments)
		{
			Handles.color = CurveColor;
			Handles.DrawBezier(segment[0], segment[3], segment[1], segment[2], CurveColor, null, isMouseOver ? 2f : 1f);
			Handles.color = HandleColor;
			if (isInEditMode)
			{
				Handles.DrawAAPolyLine(segment[0], segment[1]);
				Handles.DrawAAPolyLine(segment[2], segment[3]);
			}
		}

		Handles.color = Color.red / 2 + Color.white / 2;
		//foreach (var vert in curve.VertexData)
		//{
		//	Handles.DrawAAPolyLine(vert.point, vert.point + vert.normal * .2f);
		//	//Handles.Label(vert.point, $"{vert.length}, {vert.time}");
		//}
		//DrawCurveFromVertexData(curve.VertexData);
		Handles.color = c;
	}

	/// <summary>
	/// Draw two sided curve
	/// </summary>
	/// <param name="vertexData"></param>
	/// <exception cref="NotImplementedException"></exception>
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
			Handles.color = towardCamera ? Color.green : Color.red *.6666f + Color.green *.3333f;
			Handles.DrawAAPolyLine((towardCamera ? 4 : 2), vertices.ToArray());
		}
	}

	/// <summary>
	/// Draws Curve Editor handles and overlay
	/// </summary>
	void OnEditorSceneView(SceneView sv)
	{

	}
}
