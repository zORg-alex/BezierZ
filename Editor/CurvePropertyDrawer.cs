using UnityEngine;
using UnityEditor;
using Utility;
using RectEx;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;

namespace BezierCurveZ.Editor
{
	[CustomPropertyDrawer(typeof(Curve))]
	public class CurvePropertyDrawer : PropertyDrawer
	{
		private bool initialized;
		private Event current;
		private Transform targetTransform;
		private static Dictionary<Curve, PreviewCallbacks> _ActivePreviewSubscriptions = new Dictionary<Curve, PreviewCallbacks>();

		GUIContent editButtonText(bool edit) => edit ? new GUIContent("Stop") : new GUIContent("Edit");
		private static Texture2D isOpenTexture;
		private static Texture2D isClosedTexture;
		private Texture2D isOpenClosedTexture(bool closed) => closed ? isClosedTexture : isOpenTexture;
		private static Texture2D EyeOpenTexture;
		private static Texture2D EyeClosedTexture;
		public Texture2D PreviewTexture(bool preview) => preview ? EyeOpenTexture : EyeClosedTexture;

		private GUIContent _maxAngleErrorLabel = new GUIContent("Max Ang");
		private GUIContent _minDistLabel = new GUIContent("Min Dist");
		private GUIContent _accuracyLabel = new GUIContent("Acc");

		GUIContent[] interpolationOptions = new GUIContent[] { new GUIContent("Rotation Minimization"), new GUIContent("Linear Interpolation"), new GUIContent("Smooth Interpolation"), new GUIContent("CatmullRom Additive Interpolation") };


		private void Initialize()
		{
			LoadTextures();
			initialized = true;
		}

		private void LoadTextures()
		{
			var path = AssetDatabase.GetAssetPath(Textures.instance.textures);
			var textures = TextureObfuscator.UnpackTextures(path);


			var suff = string.Empty;
			if (EditorGUIUtility.isProSkin) suff = "_d";
			isOpenTexture = textures.FirstOrDefault(t=>t.name == "Bezier.IsOpen" + suff);
			isClosedTexture = textures.FirstOrDefault(t => t.name == "Bezier.IsClosed" + suff);
			EyeOpenTexture = textures.FirstOrDefault(t => t.name == "Bezier.EyeOpen" + suff);
			EyeClosedTexture = textures.FirstOrDefault(t => t.name == "Bezier.EyeClosed" + suff);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (!initialized) Initialize();
			var c = GUI.color;
			current = Event.current;
			var curve = property.GetValue<Curve>();

			if (curve == null)
			{
				UnityEditor.EditorGUI.LabelField(position, label, "null");
				return;
			}

			UnityEditor.EditorGUI.BeginProperty(position, label, property);

			var posDivided = position.CutFromTop(EditorGUIUtility.singleLineHeight);
			var firstLineButtonRects = posDivided[0].Row(new float[] { 0, 1, 0 }, new float[] { EditorGUIUtility.labelWidth, 0, 24 });

			UnityEditor.EditorGUI.LabelField(firstLineButtonRects[0], label);

			if (curve.IsInEditMode) GUI.color = new Color(1, .3f, .3f);
			var edited = GUI.Toggle(firstLineButtonRects[1], curve.IsInEditMode, new GUIContent(editButtonText(curve.IsInEditMode)), "Button");
			if (edited != curve.IsInEditMode)
			{
				curve.IsInEditMode = edited;
				if (edited)
				{
					//OnPreviewOff(curve);
					CurveEditor.Instance.Start(curve, property);
				}
				else
				{
					//OnPreviewOn(curve, property);
					CurveEditor.Instance.Stop();
				}
			}
			GUI.color = c;

			//Restore preview
			if (curve.PreviewOn && !_ActivePreviewSubscriptions.ContainsKey(curve))
				OnPreviewOn(curve, property);
			//Hover preview
			if (current.type == EventType.Repaint)
			{
				var mouseOverProperty = position.Contains(current.mousePosition);
				if (curve.IsMouseOverProperty && !mouseOverProperty && !curve.PreviewOn && !curve.IsInEditMode)
					OnPreviewOff(curve);
				if (!curve.IsMouseOverProperty && mouseOverProperty && !curve.PreviewOn)
					OnPreviewOn(curve, property);
				RepaintSceneViews();
				curve.IsMouseOverProperty = mouseOverProperty;
			}
			//Thanks to hover preview it will always be on when clicking
			curve.PreviewOn = GUI.Toggle(firstLineButtonRects[2], curve.PreviewOn, PreviewTexture(curve.PreviewOn), "Button");

			if (curve.IsInEditMode)
			{
				EditorGUI(posDivided[1], curve, property.serializedObject.targetObject);
			}

			UnityEditor.EditorGUI.EndProperty();
			GUI.color = c;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var curve = property.GetValue<Curve>();
			return EditorGUIUtility.singleLineHeight + EditorHeight(curve);
		}

		private float EditorHeight(Curve curve) => curve.IsInEditMode ? 64 + 2 * 3 : 0;

		private void EditorGUI(Rect position, Curve curve, UnityEngine.Object targetObject)
		{
			var line = position.Row(new float[] { 0, 1, 1 }, new float[] { 64, 0, 0 }, 4);
			var detStack = line[1].Column(3);
			var interpStack = line[2].Column(3);

			var lw = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = Mathf.Max(
				EditorStyles.label.CalcSize(_maxAngleErrorLabel).x,
				EditorStyles.label.CalcSize(_minDistLabel).x,
				EditorStyles.label.CalcSize(_accuracyLabel).x
				);
			UnityEditor.EditorGUI.BeginChangeCheck();
			var maxerr = Mathf.Clamp(
				UnityEditor.EditorGUI.FloatField(detStack[0], _maxAngleErrorLabel, curve.InterpolationMaxAngleError)
				, 0, 180);
			var mindist = Mathf.Max(CurveInterpolation.MinSplitDistance,
				UnityEditor.EditorGUI.FloatField(detStack[1], _minDistLabel, curve.InterpolationMinDistance)
				);
			var acc = (int)Mathf.Clamp(
				UnityEditor.EditorGUI.IntField(detStack[2], _accuracyLabel, curve.InterpolationAccuracy)
				, 1, 1000);
			var tens = 0f;
			if (curve.IterpolationOptionsInd == InterpolationMethod.CatmullRomAdditive)
				tens = Mathf.Max(
					UnityEditor.EditorGUI.FloatField(interpStack[1], "tension", curve.InterpolationCapmullRomTension)
					, 0.01f);
			EditorGUIUtility.labelWidth = lw;
			if (UnityEditor.EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(targetObject, $"Interpolation options changed on curve");
				curve.InterpolationMaxAngleError = maxerr;
				curve.InterpolationMinDistance = mindist;
				curve.InterpolationAccuracy = acc;
				curve.InterpolationCapmullRomTension = tens;
				RepaintSceneViews();
			}

			if (GUI.Button(line[0], isOpenClosedTexture(curve.IsClosed)))
			{
				Undo.RecordObject(targetObject, $"IsClosed changed on curve");
				curve.SetIsClosed(!curve.IsClosed);
				RepaintSceneViews();
			}

			var ops = UnityEditor.EditorGUI.Popup(interpStack[0], (int)curve.IterpolationOptionsInd, interpolationOptions);
			if (ops != (int)curve.IterpolationOptionsInd)
			{
				Undo.RecordObject(targetObject, $"Interpolation options changed on curve");
				curve.IterpolationOptionsInd = (InterpolationMethod)ops;
				RepaintSceneViews();
			}
		}

		private void OnPreview(Curve curve)
		{
			if (targetTransform.IsNullOrDestroyed() || !CheckPropertyIsOK(curve))
			{
				OnPreviewOff(curve);
				return;
			}
			var c = Handles.color;
			var m = Handles.matrix;
			Handles.color = new Color(.25f, 1f, .25f);
			Handles.matrix = targetTransform.localToWorldMatrix;

			Handles.DrawAAPolyLine((curve.IsInEditMode || curve.IsMouseOverProperty) ? 2 : 1, curve.VertexDataPoints);

			Handles.matrix = m;
			Handles.color = c;
		}

		private bool CheckPropertyIsOK(Curve curve)
		{
			var ok = _ActivePreviewSubscriptions.TryGetValue(curve, out var sub);
			if (ok) ok = sub.isExisting;
			return ok;
		}

		private void OnPreviewOff(Curve curve)
		{
			var c = _ActivePreviewSubscriptions.GetValueOrDefault(curve);
			if (c == null)
				return;
			Selection.selectionChanged -= c.UnsubscribePreview;
			EditorSceneManager.sceneClosed -= c.UnsubscribePreview;
			AssemblyReloadEvents.beforeAssemblyReload -= c.UnsubscribePreview;
			SceneView.duringSceneGui -= c.OnPreview;
			_ActivePreviewSubscriptions.Remove(curve);
			//Debug.Log("OnPreviewOff");
		}

		private void OnPreviewOn(Curve curve, SerializedProperty property)
		{
			var c = _ActivePreviewSubscriptions.GetValueOrDefault(curve);
			if (c != null)
				return;
			targetTransform = ((Component)property.serializedObject.targetObject).transform;
			var capturedCurve = curve;
			c = new PreviewCallbacks(capturedCurve, OnPreviewOff, OnPreview, property);
			Selection.selectionChanged += c.UnsubscribePreview;
			EditorSceneManager.sceneClosed += c.UnsubscribePreview;
			AssemblyReloadEvents.beforeAssemblyReload += c.UnsubscribePreview;
			SceneView.duringSceneGui += c.OnPreview;
			_ActivePreviewSubscriptions.Add(capturedCurve, c);
			//Debug.Log("OnPreviewOn");
		}

		private void RepaintSceneViews()
		{
			foreach (SceneView sv in SceneView.sceneViews)
			{
				sv.Repaint();
			}
		}

	}
}