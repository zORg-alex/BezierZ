using System.Collections;
using UnityEngine;
using UnityEditor;
using System;
using Utility;
using RectEx;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(OtherCurve))]
public partial class OtherCurvePropertyDrawer : PropertyDrawer
{
	private OtherCurve curve;
	private UnityEngine.Object targetObject;
	private Transform targetTransform;
	private bool targetIsGameObject;

	public static int zzz;
	public int z;

	public OtherCurvePropertyDrawer()
	{
		z = zzz++;
	}

	[NonSerialized]
	private static OtherCurvePropertyDrawer currentlyEditedPropertyDrawer;
	private Tool lastTool;

	private Event current;
	private bool IsCurrentlyEditedDrawer => currentlyEditedPropertyDrawer == this;
	private OtherCurve currentlyEditedCurve;
	private Action FinishCurrentEditorAction;
	private void FinishCurrentEditorActionInvoke() => FinishCurrentEditorAction?.Invoke();
	private void FinishCurrentEditorActionInvoke(Scene s) => FinishCurrentEditorAction?.Invoke();
	private bool isInEditMode { get => curve?._isInEditMode ?? false; set { if (curve != null) curve._isInEditMode = value; } }
	private bool isMouseOver;
	private static Dictionary<OtherCurve, Action> _ActivePreviewSubscriptions = new Dictionary<OtherCurve, Action>();

	private Texture2D isOpenTexture;
	private Texture2D isClosedTexture;
	private Texture2D isOpenClosedTexture => curve.IsClosed ? isClosedTexture : isOpenTexture;
	private Texture2D EyeOpenTexture;
	private Texture2D EyeClosedTexture;
	public Texture2D PreviewTexture => curve._previewOn ? EyeOpenTexture : EyeClosedTexture;
	private bool initialized;
	private string editButtonText => isInEditMode ? "Stop Editing" : "Edit";

	public override bool CanCacheInspectorGUI(SerializedProperty property) => false;

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + EditorHeight(property.GetValue<OtherCurve>());

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


		if (current.type == EventType.Repaint)
		{
			var mouseOverProperty = position.Contains(current.mousePosition);
			if (curve._isMouseOverProperty && !mouseOverProperty)
				OnMouseLeaveProperty();
			if (!curve._isMouseOverProperty && mouseOverProperty)
				OnMouseEnterProperty();
			curve._isMouseOverProperty = mouseOverProperty;
		}

		var posDivided = position.CutFromTop(EditorGUIUtility.singleLineHeight);
		var rects = EditorGUI.PrefixLabel(posDivided[0], label).Row(new float[] { 1, 0 }, new float[] { 0, 24 });
		GUI.Label(position.CutFromLeft(10)[0].MoveLeft(), z.ToString());//////////////////////////
		if (isInEditMode) GUI.color = Color.red * .6666f + Color.white * .3333f;
		if (GUI.Button(rects[0], new GUIContent(editButtonText)))
		{
			OnEditPressed();
		}
		GUI.color = c;

		var previewOn = GUI.Toggle(rects[1], curve._previewOn, PreviewTexture, "Button");
		if (curve._previewOn != previewOn)
		{
			curve._previewOn = previewOn;
			OnPreviewChanged();
		}

		if (curve._isInEditMode)
			DrawEditor(posDivided[1]);

		GUI.Label(posDivided[1], $"                          , curve is in Edit mode {curve._isInEditMode}");

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
		if (curve._isInEditMode)
			StartEditor();
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
		if (currentlyEditedPropertyDrawer?.currentlyEditedCurve?._isInEditMode ?? false)
			currentlyEditedPropertyDrawer.FinishEditor(currentlyEditedPropertyDrawer.currentlyEditedCurve);
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
		var capturedCurve = curve;
		FinishCurrentEditorAction = () =>
			FinishEditor(capturedCurve);
		Selection.selectionChanged += FinishCurrentEditorActionInvoke;
		EditorSceneManager.sceneClosed += FinishCurrentEditorActionInvoke;
		AssemblyReloadEvents.beforeAssemblyReload += FinishCurrentEditorActionInvoke;
		SceneView.duringSceneGui += OnEditorSceneView;
	}

	private void UnsubscribeFromEditorEvents()
	{
		Selection.selectionChanged -= FinishCurrentEditorActionInvoke;
		EditorSceneManager.sceneClosed -= FinishCurrentEditorActionInvoke;
		AssemblyReloadEvents.beforeAssemblyReload -= FinishCurrentEditorActionInvoke;
		SceneView.duringSceneGui -= OnEditorSceneView;
		FinishCurrentEditorAction = null;
	}

	private void CallAllSceneViewRepaint()
	{
		foreach (SceneView sv in SceneView.sceneViews)
			sv.Repaint();
	}

	void OnMouseEnterProperty()
	{
		if (!curve._previewOn)
		{
			SceneView.duringSceneGui += OnPreview;
			CallAllSceneViewRepaint();
		}
	}
	void OnMouseLeaveProperty()
	{
		if (!curve._previewOn)
		{
			SceneView.duringSceneGui -= OnPreview;
			CallAllSceneViewRepaint();
		}
	}
	void StopPreview()
	{
		curve._previewOn = false;
		OnPreviewChanged();
	}
	void OnPreviewChanged(Scene s) => OnPreviewChanged();
	void OnPreviewChanged()
	{
		//In case mouse over subscribed remove duplicate
		SceneView.duringSceneGui -= OnPreview;
		_ActivePreviewSubscriptions.GetValueOrDefault(curve)?.Invoke();

		if (curve._previewOn)
		{
			Selection.selectionChanged += UnsubscribePreviewIfNotOn;
			EditorSceneManager.sceneClosed += UnsubscribePreview;
			AssemblyReloadEvents.beforeAssemblyReload += UnsubscribePreview;
			SceneView.duringSceneGui += OnPreview;
			_ActivePreviewSubscriptions.Add(curve, UnsubscribePreview);
		}
	}
	void UnsubscribePreview(Scene s) => UnsubscribePreviewIfNotOn(true);
	void UnsubscribePreview() => UnsubscribePreviewIfNotOn(true);
	void UnsubscribePreviewIfNotOn() => UnsubscribePreviewIfNotOn(false);
	void UnsubscribePreviewIfNotOn(bool force = false)
	{
		if (force || !curve._previewOn)
		{
			Selection.selectionChanged -= UnsubscribePreviewIfNotOn;
			EditorSceneManager.sceneClosed -= UnsubscribePreview;
			AssemblyReloadEvents.beforeAssemblyReload -= UnsubscribePreview;
			SceneView.duringSceneGui -= OnPreview;
			_ActivePreviewSubscriptions.Remove(curve);
		}
	}

	void OnPreview(SceneView sv)
	{
		DrawCurve();
	}

	private void OnEditorSceneView(SceneView sv) => OnEditorSceneView();

	/// <summary>
	/// Draws Curve Editor handles and overlay
	/// </summary>
	void OnEditorSceneView()
	{
		DrawCurveAndPoints();
	}

}
