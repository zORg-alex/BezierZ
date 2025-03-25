using UnityEngine;
using UnityEditor;
using BezierZUtility;
using RectEx;
using UnityEditor.SceneManagement;
using System.Linq;
using UnityEditorInternal;
using System;
using System.Reflection;
using System.Collections.Generic;
using Sirenix.Utilities;

namespace BezierCurveZ.Editor
{
	[CustomPropertyDrawer(typeof(Curve))]
	public class CurvePropertyDrawer : PropertyDrawer
	{
		private bool initialized;
		private Event current;
		private Transform targetTransform;
		private static Dictionary<Curve, PreviewCallbacks> _ActivePreviewSubscriptions = new Dictionary<Curve, PreviewCallbacks>();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="edit"></param>
		/// <returns></returns>
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
		private GUIStyle boxStyle;

		private ReorderableList _contraintList;
		GUIContent _constraintsLabel = new GUIContent("Constraints");
		private SerializedObject _serializedObject;

		private void Initialize(SerializedProperty property)
		{
			_serializedObject = property.serializedObject;
			LoadTextures();
			boxStyle = EditorStyles.helpBox;
			initialized = true;
			CreateConstraintList(property);
		}

		private void LoadTextures()
		{
			var path = AssetDatabase.GetAssetPath(Textures.instance.textures);
			var textures = TextureObfuscator.UnpackTextures(path);
			var suffix = string.Empty;
			if (EditorGUIUtility.isProSkin) suffix = "_d";

			isOpenTexture = textures.FirstOrDefault(t=>t.name == "Bezier.IsOpen" + suffix);
			isClosedTexture = textures.FirstOrDefault(t => t.name == "Bezier.IsClosed" + suffix);
			EyeOpenTexture = textures.FirstOrDefault(t => t.name == "Bezier.EyeOpen" + suffix);
			EyeClosedTexture = textures.FirstOrDefault(t => t.name == "Bezier.EyeClosed" + suffix);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			if (!initialized) Initialize(property);
			var c = GUI.color;
			current = Event.current;
			var curve = property.GetValue<Curve>();

			if (curve == null)
			{
				UnityEditor.EditorGUI.LabelField(position, label);
				return;
			}

			UnityEditor.EditorGUI.BeginProperty(position, label, property);

			var posDivided = position.CutFromTop(EditorGUIUtility.singleLineHeight);
			var firstLineButtonRects = posDivided[0].Row(new float[] { 0, 1, 1, 0 }, new float[] { EditorGUIUtility.labelWidth, 0, 0, 24 });

			UnityEditor.EditorGUI.LabelField(firstLineButtonRects[0], label);

			if (curve.IsInEditMode) GUI.color = new Color(1, .3f, .3f);
			var edited = GUI.Toggle(firstLineButtonRects[1], curve.IsInEditMode, new GUIContent(editButtonText(curve.IsInEditMode)), "Button");
			if (edited != curve.IsInEditMode)
			{
				curve.IsInEditMode = edited;
				if (edited)
				{
					AlternativeCurveEditor.Instance.Stop();
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

			if (curve.IsInAlternateEditMode) GUI.color = new Color(1, .3f, .3f);
			edited = GUI.Toggle(firstLineButtonRects[2], curve.IsInAlternateEditMode, new GUIContent(editButtonText(curve.IsInAlternateEditMode)), "Button");
			if (edited != curve.IsInAlternateEditMode)
			{
				curve.IsInAlternateEditMode = edited;
				if (edited)
				{
					CurveEditor.Instance.Stop();
					AlternativeCurveEditor.Instance.Start(curve, property);
				}
				else
				{
					AlternativeCurveEditor.Instance.Stop();
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
				if (curve.IsMouseOverProperty && !mouseOverProperty && !curve.PreviewOn && !curve.IsInAnyEditMode)
					OnPreviewOff(curve);
				if (!curve.IsMouseOverProperty && mouseOverProperty && !curve.PreviewOn)
					OnPreviewOn(curve, property);
				RepaintSceneViews();
				curve.IsMouseOverProperty = mouseOverProperty;
			}
			//Thanks to hover preview it will always be on when clicking
			curve.PreviewOn = GUI.Toggle(firstLineButtonRects[3], curve.PreviewOn, PreviewTexture(curve.PreviewOn), "Button");

			if (curve.IsInEditMode)
			{
				GUI.Box(posDivided[1].Extend(0,-4), GUIContent.none, boxStyle);
				CurveEditorGUI(property, posDivided[1].Extend(-4, -8), curve, property.serializedObject.targetObject);
			}

			UnityEditor.EditorGUI.EndProperty();
			GUI.color = c;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			var curve = property.GetValue<Curve>();
			return EditorGUIUtility.singleLineHeight +
				CurveEditorHeight(property, curve);
		}

		private float CurveEditorHeight(SerializedProperty property, Curve curve) => (curve != null && curve.IsInEditMode) ? 64 + 2 * 3 + 8 +
				 _contraintList.GetHeight()	: 0;

		private void CurveEditorGUI(SerializedProperty property, Rect position, Curve curve, UnityEngine.Object targetObject)
		{
			SerializedProperty constraintsProperty = property.FindPropertyRelative("_constraints");
			var constraintsHeight = _contraintList.GetHeight();
			var mainColumn = position.CutFromBottom(constraintsHeight);
			var line = mainColumn[0].Row(new float[] { 0, 1, 1 }, new float[] { 64, 0, 0 }, 4);
			var detStack = line[1].Column(3);
			var interpStack = line[2].Column(3);

			if (GUI.Button(line[0], isOpenClosedTexture(curve.IsClosed)))
			{
				Undo.RecordObject(targetObject, $"IsClosed changed on curve");
				curve.SetIsClosed(!curve.IsClosed);
				RepaintSceneViews();
			}

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
			if (curve.InterpolationMethod == InterpolationMethod.CatmullRomAdditive)
				tens = Mathf.Max(
					UnityEditor.EditorGUI.FloatField(interpStack[1], "Tension", curve.InterpolationCapmullRomTension)
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

			var ops = UnityEditor.EditorGUI.Popup(interpStack[0], (int)curve.InterpolationMethod, interpolationOptions);
			if (ops != (int)curve.InterpolationMethod)
			{
				Undo.RecordObject(targetObject, $"Interpolation options changed on curve");
				curve.InterpolationMethod = (InterpolationMethod)ops;
				RepaintSceneViews();
			}

			Rect contraintsRect = mainColumn[1].Extend(-12, 0, -2, 0);
			_contraintList.DoList(contraintsRect);

			var c = GUI.color;
			GUI.color = new Color(.6f, .6f, 1f);
			if (GUI.Button(interpStack[2], "Donate"))
			{
				Application.OpenURL("https://www.paypal.com/donate/?hosted_button_id=UA959RWJHC6AG");
			}
			GUI.color = c;
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

		private void CreateConstraintList(SerializedProperty property)
		{

			var constraintsProperty = property.FindPropertyRelative("_constraints");
			Curve curve = property.GetValue<Curve>();
			_contraintList = new ReorderableList(curve.Constraints, typeof(Curve), false, true, true, true);
			_contraintList.drawHeaderCallback = r => EditorGUI.LabelField(r, _constraintsLabel);
			_contraintList.onAddDropdownCallback = OnConstraintAddDropdownCallback;
			_contraintList.elementHeightCallback = ind =>
			{
				SerializedProperty element = GetArrayProperty(ind, constraintsProperty);
				return element.CountInProperty() * (EditorGUIUtility.singleLineHeight + 2) - 2;
			};
			_contraintList.drawElementCallback = (r, i, act, foc) =>
			{
				var element = GetArrayProperty(i, constraintsProperty);
				int count = element.Copy().CountInProperty();
				var line = r.TakeFromTop(EditorGUIUtility.singleLineHeight).Expand(-8,0,0,0);

				EditorGUI.PropertyField(line, element);
				element.NextVisible(true);
				line = line.Expand(-20, 0, 0, 0);
				bool visitChild = false;
				for (int ind = 1; ind < count; ind++)
				{
					visitChild = false;
					if (element.propertyType == SerializedPropertyType.ManagedReference)
						visitChild = true;

					line = line.MoveDown();
					EditorGUI.BeginChangeCheck();
					EditorGUI.PropertyField(line, element);
					if (EditorGUI.EndChangeCheck())
					{
						property.serializedObject.ApplyModifiedProperties();
						(GetArrayProperty(i, constraintsProperty).managedReferenceValue as CurveConstraint)?.OnCurveChanged(curve);
						curve.BumpVersion();
						this.RepaintSceneViews();
					}
					if (!element.isArray || element.type == "string")
						element.NextVisible(visitChild);
				}
			};
		}

		private static SerializedProperty GetArrayProperty(int ind, SerializedProperty arrayProperty)
		{
			SerializedProperty element = arrayProperty.Copy();
			element.Next(true);
			element.Next(true);
			for (int i = 0; i <= ind; i++)
			{
				element.Next(true);
			}

			return element;
		}

		private void OnConstraintAddDropdownCallback(Rect buttonRect, ReorderableList list)
		{
			var menu = new GenericMenu();

			foreach (Type type in Assembly.GetAssembly(typeof(CurveConstraint)).GetTypes()
				.Where(myType => myType.IsClass && !myType.IsAbstract && !myType.IsGenericType && myType.IsSubclassOf(typeof(CurveConstraint))))
			{
				menu.AddItem(new GUIContent(type.Name), true, handler, Activator.CreateInstance(type));
			}

			menu.ShowAsContext();

			void handler(object data)
			{
				//var val = data as CurveConstraint;
				_contraintList.list.Add(data);
				_contraintList.index = _contraintList.list.Count - 1;
			}
		}
	}
}