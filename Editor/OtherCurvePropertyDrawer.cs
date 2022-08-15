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

    private UnityEngine.Object targetObject;
    private OtherCurve curve;
    private static Action FinishCurrentEditorAction;
    private static void FinishCurrentEditor() => FinishCurrentEditorAction();
    private static void FinishCurrentEditor(Scene s) => FinishCurrentEditorAction();

    private static Dictionary<OtherCurve, Action> _ActivePreviewSubscriptions = new Dictionary<OtherCurve, Action>();
	private Event current;
	private Transform targetTransform;
	private bool targetIsGameObject;

    private static Texture2D isOpenTexture;
    private static Texture2D isClosedTexture;
    private Texture2D isOpenClosedTexture => curve.IsClosed ? isClosedTexture : isOpenTexture;
    private static Texture2D EyeOpenTexture;
    private static Texture2D EyeClosedTexture;
    public Texture2D PreviewTexture => curve._previewOn ? EyeOpenTexture : EyeClosedTexture;
    private static bool initialized;

	private string EditButtonText(bool editing) => editing ? "Stop Edit" : "Edit";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        GetObjects(property);
        return EditorGUIUtility.singleLineHeight + EditorHeight(curve);
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
        initialized = true;
	}

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var c = GUI.color;
        current = Event.current;
        GetObjects(property);
        if (!initialized) Initialize(property);
        EditorGUI.BeginProperty(position, label, property);

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
        var firstLineButtonRects = posDivided[0].Row(new float[] { 0, 1, 0 }, new float[] { EditorGUIUtility.labelWidth, 0, 24 });

        EditorGUI.LabelField(firstLineButtonRects[0], label);

        if (curve._isInEditMode) GUI.color = Color.red * .6666f + Color.white * .3333f;
        var edited = GUI.Toggle(firstLineButtonRects[1], curve._isInEditMode, new GUIContent(EditButtonText(curve._isInEditMode)), "Button");
        if (edited != curve._isInEditMode)
		{
            curve._isInEditMode = edited;
            EditPressed();
		}
        GUI.color = c;

        var previewOn = GUI.Toggle(firstLineButtonRects[2], curve._previewOn, PreviewTexture, "Button");
        if (curve._previewOn != previewOn)
        {
            curve._previewOn = previewOn;
            OnPreviewChanged();
        }

        if (curve._isInEditMode)
            DrawEditor(posDivided[1]);

        EditorGUI.EndProperty();
        GUI.color = c;
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
    private void EditPressed()
    {
        if (curve._isInEditMode)
            StartEditor();
        else
            FinishEditor(curve);
    }

    private void StartEditor()
    {
        var capturedThing = curve;
        FinishCurrentEditorAction?.Invoke();
        FinishCurrentEditorAction = () => {
            FinishEditor(capturedThing);
            EditorUtility.SetDirty(targetObject);
            FinishCurrentEditorAction = null;
        };
        capturedThing._isInEditMode = true;
        Selection.selectionChanged += FinishCurrentEditor;
        EditorSceneManager.sceneClosed += FinishCurrentEditor;
        AssemblyReloadEvents.beforeAssemblyReload += FinishCurrentEditor;
        SceneView.duringSceneGui += OnEditorSceneView;
    }

    private void FinishEditor(OtherCurve curve)
    {
        Selection.selectionChanged -= FinishCurrentEditor;
        EditorSceneManager.sceneClosed -= FinishCurrentEditor;
        AssemblyReloadEvents.beforeAssemblyReload -= FinishCurrentEditor;
        SceneView.duringSceneGui -= OnEditorSceneView;
        curve._isInEditMode = false;
        FinishCurrentEditorAction = null;
    }

    private void OnEditorSceneView(SceneView obj)
    {
        DrawSceneEditor();
    }
}