using UnityEngine;
using UnityEditor;
using Utility;
using RectEx;
using System;
using UnityEditor.SceneManagement;
using BezierCurveZ;
using NUnit.Framework;
using System.Collections.Generic;
using static OtherCurvePropertyDrawer;
using System.Security.Cryptography;

[CustomPropertyDrawer(typeof(OtherCurve))]
public class OtherCurvePropertyDrawer_: PropertyDrawer{
	private bool initialized;
	private Event current;
	private Transform targetTransform;
	private static Dictionary<OtherCurve, PreviewCallbacks> _ActivePreviewSubscriptions = new Dictionary<OtherCurve, PreviewCallbacks>();

	string editButtonText(bool edit) => edit ? "Stop" : "Edit";
	private static Texture2D isOpenTexture;
	private static Texture2D isClosedTexture;
	private Texture2D isOpenClosedTexture(bool closed) => closed ? isClosedTexture : isOpenTexture;
	private static Texture2D EyeOpenTexture;
	private static Texture2D EyeClosedTexture;
	public Texture2D PreviewTexture(bool preview) => preview ? EyeOpenTexture : EyeClosedTexture;
	private void Initialize()
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
		if (!initialized) Initialize();
		var c = GUI.color;
		current = Event.current;
		var curve = property.GetValue<OtherCurve>();

		if (curve == null)
		{
			EditorGUI.LabelField(position, label, "null");
			return;
		}

		EditorGUI.BeginProperty(position, label, property);

		var posDivided = position.CutFromTop(EditorGUIUtility.singleLineHeight);
		var firstLineButtonRects = posDivided[0].Row(new float[] { 0, 1, 0 }, new float[] { EditorGUIUtility.labelWidth, 0, 24 });

		EditorGUI.LabelField(firstLineButtonRects[0], label);

		if (curve._isInEditMode) GUI.color = new Color(1, .3f, .3f);
		var edited = GUI.Toggle(firstLineButtonRects[1], curve._isInEditMode, new GUIContent(editButtonText(curve._isInEditMode)), "Button");
		if (edited != curve._isInEditMode)
		{
			curve._isInEditMode = edited;
			if (edited)
				OtherCurveEditor.Instance.Start(curve, property);
			else
				OtherCurveEditor.Instance.Stop();
		}
		GUI.color = c;

		//Restore preview
		if (curve._previewOn && !_ActivePreviewSubscriptions.ContainsKey(curve))
			OnPreviewOn(curve, property);
		//Hover preview
		if (current.type == EventType.Repaint)
		{
			var mouseOverProperty = position.Contains(current.mousePosition);
			if (curve._isMouseOverProperty && !mouseOverProperty && !curve._previewOn && !curve._isInEditMode)
				OnPreviewOff(curve);
			if (!curve._isMouseOverProperty && mouseOverProperty && !curve._previewOn)
				OnPreviewOn(curve, property);
			RepaintSceneViews();
			curve._isMouseOverProperty = mouseOverProperty;
		}
		//Thanks to hover preview it will always be on when clicking
		curve._previewOn = GUI.Toggle(firstLineButtonRects[2], curve._previewOn, PreviewTexture(curve._previewOn), "Button");

		EditorGUI.EndProperty();
		GUI.color = c;
	}

	private void OnPreview(OtherCurve curve)
	{
		var c = Handles.color;
		var m = Handles.matrix;
		Handles.color = new Color(.25f, 1f, .25f);
		Handles.matrix = targetTransform.localToWorldMatrix;

		Handles.DrawAAPolyLine((curve._isInEditMode || curve._isMouseOverProperty) ? 2 : 1, curve.VertexDataPoints);

		Handles.matrix = m;
		Handles.color = c;
	}

	private void OnPreviewOff(OtherCurve curve)
	{
		var c = _ActivePreviewSubscriptions.GetValueOrDefault(curve);
		if (c == null)
			return;
		Selection.selectionChanged -= c.UnsubscribePreviewIfNotOn;
		EditorSceneManager.sceneClosed -= c.UnsubscribePreview;
		AssemblyReloadEvents.beforeAssemblyReload -= c.UnsubscribePreview;
		SceneView.duringSceneGui -= c.OnPreview;
		_ActivePreviewSubscriptions.Remove(curve);
	}

	private void OnPreviewOn(OtherCurve curve, SerializedProperty property)
	{
		var c = _ActivePreviewSubscriptions.GetValueOrDefault(curve);
		if (c != null)
			return;
		targetTransform = ((Component)property.serializedObject.targetObject).transform;
		var capturedCurve = curve;
		c = new PreviewCallbacks(capturedCurve, OnPreviewOff, OnPreview, property);
		Selection.selectionChanged += c.UnsubscribePreviewIfNotOn;
		EditorSceneManager.sceneClosed += c.UnsubscribePreview;
		AssemblyReloadEvents.beforeAssemblyReload += c.UnsubscribePreview;
		SceneView.duringSceneGui += c.OnPreview;
		_ActivePreviewSubscriptions.Add(capturedCurve, c);
	}

	private void RepaintSceneViews()
	{
		foreach (SceneView sv in SceneView.sceneViews)
		{
			sv.Repaint();
		}
	}

}
