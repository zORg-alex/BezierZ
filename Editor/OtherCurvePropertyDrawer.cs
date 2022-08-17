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
    private OtherCurve propValue;
    private OtherCurve mouseOverCurve;
    private static Action FinishCurrentEditorAction;
    private static void FinishCurrentEditor() => FinishCurrentEditorAction();
    private static void FinishCurrentEditor(Scene s) => FinishCurrentEditorAction();

    private static Dictionary<OtherCurve, PreviewCallbacks> _ActivePreviewSubscriptions = new Dictionary<OtherCurve, PreviewCallbacks>();
	private Event current;
	private Transform targetTransform;
	private bool targetIsGameObject;

    private static Texture2D isOpenTexture;
    private static Texture2D isClosedTexture;
    private Texture2D isOpenClosedTexture => propValue.IsClosed ? isClosedTexture : isOpenTexture;
    private static Texture2D EyeOpenTexture;
    private static Texture2D EyeClosedTexture;
    public Texture2D PreviewTexture => propValue._previewOn ? EyeOpenTexture : EyeClosedTexture;
    private static bool initialized;

	private string EditButtonText(bool editing) => editing ? "Stop Edit" : "Edit";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        GetObjects(property);
        return EditorGUIUtility.singleLineHeight + EditorHeight(propValue);
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
        propValue = property.GetValue<OtherCurve>();
		OnPreviewChanged();
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

        if (propValue == null)
        {
            EditorGUI.LabelField(position, label, "null");
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

		if (current.type == EventType.Repaint)
		{
			var mouseOverProperty = position.Contains(current.mousePosition);
			if (propValue._isMouseOverProperty && !mouseOverProperty)
				OnMouseLeaveProperty();
			if (!propValue._isMouseOverProperty && mouseOverProperty)
				OnMouseEnterProperty();
            propValue._isMouseOverProperty = mouseOverProperty;
		}

		var posDivided = position.CutFromTop(EditorGUIUtility.singleLineHeight);
        var firstLineButtonRects = posDivided[0].Row(new float[] { 0, 1, 0 }, new float[] { EditorGUIUtility.labelWidth, 0, 24 });

        EditorGUI.LabelField(firstLineButtonRects[0], label);

        if (propValue._isInEditMode) GUI.color = Color.red * .6666f + Color.white * .3333f;
        var edited = GUI.Toggle(firstLineButtonRects[1], propValue._isInEditMode, new GUIContent(EditButtonText(propValue._isInEditMode)), "Button");
        if (edited != propValue._isInEditMode)
		{
            propValue._isInEditMode = edited;
            EditPressed();
		}
        GUI.color = c;

        var previewOn = GUI.Toggle(firstLineButtonRects[2], propValue._previewOn, PreviewTexture, "Button");
        if (propValue._previewOn != previewOn)
        {
            propValue._previewOn = previewOn;
            OnPreviewChanged();
        }

        if (propValue._isInEditMode)
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
        if (!propValue._previewOn)
        {
            mouseOverCurve = propValue;
            SceneView.duringSceneGui += MouseOverPreview;
        }
        CallAllSceneViewRepaint();
    }
	void MouseOverPreview(SceneView sv)
	{
        if (!_ActivePreviewSubscriptions.ContainsKey(mouseOverCurve))
            OnPreview(mouseOverCurve);
	}

	void OnMouseLeaveProperty()
    {
        SceneView.duringSceneGui -= MouseOverPreview;
        CallAllSceneViewRepaint();
    }
    //void OnPreviewChanged(Scene s) => OnPreviewChanged();
    void OnPreviewChanged()
    {
        //In case mouse over subscribed remove duplicate
        UnsubscribePreview(propValue);

        if (propValue._previewOn)
        {
            var capturedCurve = propValue;
            PreviewCallbacks c = new PreviewCallbacks(capturedCurve, this);
            Selection.selectionChanged += c.UnsubscribePreviewIfNotOn;
            EditorSceneManager.sceneClosed += c.UnsubscribePreview;
            AssemblyReloadEvents.beforeAssemblyReload += c.UnsubscribePreview;
            SceneView.duringSceneGui += c.OnPreview;
			_ActivePreviewSubscriptions.Add(capturedCurve, c);
        }
        CallAllSceneViewRepaint();
    }

    void UnsubscribePreview(OtherCurve curve) => UnsubscribePreviewIfNotOn(curve, true);
    void UnsubscribePreviewIfNotOn(OtherCurve curve) => UnsubscribePreviewIfNotOn(curve, false);
    void UnsubscribePreviewIfNotOn(OtherCurve curve, bool force = false)
    {
        if (force || !curve._previewOn)
        {
            var c = _ActivePreviewSubscriptions.GetValueOrDefault(curve);
            if (c == null) return;
            Selection.selectionChanged -= c.UnsubscribePreviewIfNotOn;
            EditorSceneManager.sceneClosed -= c.UnsubscribePreview;
            AssemblyReloadEvents.beforeAssemblyReload -= c.UnsubscribePreview;
			SceneView.duringSceneGui -= c.OnPreview;
			_ActivePreviewSubscriptions.Remove(curve);
        }
    }

    void OnPreview(OtherCurve curve)
    {
        var m = Handles.matrix;
        Handles.matrix = localToWorldMatrix;
        DrawCurve(curve);
        Handles.matrix = m;
    }
    private void EditPressed()
    {
        if (propValue._isInEditMode)
            StartEditor();
        else
            FinishEditor(propValue);
        CallAllSceneViewRepaint();
    }

    private void StartEditor()
    {
        curve = propValue;
        var capturedThing = propValue;
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
        EditorStarted();
    }

    private void FinishEditor(OtherCurve curve)
    {
        Selection.selectionChanged -= FinishCurrentEditor;
        EditorSceneManager.sceneClosed -= FinishCurrentEditor;
        AssemblyReloadEvents.beforeAssemblyReload -= FinishCurrentEditor;
        SceneView.duringSceneGui -= OnEditorSceneView;
        curve._isInEditMode = false;
        FinishCurrentEditorAction = null;
        EditorFinished();
        this.curve = null;
    }

	private void OnEditorSceneView(SceneView obj)
    {
        DrawSceneEditor();
    }
    internal class PreviewCallbacks
    {
		public PreviewCallbacks(OtherCurve curve, OtherCurvePropertyDrawer drawer)
		{
			this.curve = curve;
			this.drawer = drawer;
		}
        public OtherCurve curve;
        public OtherCurvePropertyDrawer drawer;
        public void OnPreview(SceneView s) => drawer.OnPreview(curve);
        public void OnPreview() => drawer.OnPreview(curve);
        public void UnsubscribePreview() => drawer.UnsubscribePreview(curve);
        public void UnsubscribePreview(Scene s) => drawer.UnsubscribePreview(curve);
        public void UnsubscribePreviewIfNotOn(bool force) => drawer.UnsubscribePreviewIfNotOn(curve, force);
        public void UnsubscribePreviewIfNotOn() => drawer.UnsubscribePreviewIfNotOn(curve, false);

		public override bool Equals(object obj)
		{
            var c = (PreviewCallbacks)obj;
            return curve.Equals(c.curve) && drawer.Equals(c.drawer);
		}
		public override int GetHashCode() => base.GetHashCode();
	}
}
