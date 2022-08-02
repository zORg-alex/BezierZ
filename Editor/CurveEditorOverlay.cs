//using UnityEngine;
//using UnityEditor;
//using UnityEditor.Overlays;
//using UnityEditor.Toolbars;

//namespace BezierCurveZ
//{
//	/// <summary>
//	/// https://docs.unity3d.com/2021.2/Documentation/Manual/overlays-custom.html#panel-overlays
//	/// </summary>
//	[Overlay(typeof(SceneView), "CurveEditor")]
//	[Icon("../Resources/Beziericon.png")]
//	public class CurveEditorOverlay : ToolbarOverlay
//	{
//		public const string toolbarId = "CurveEditorOverlay";
//		private static CurveEditorOverlay instance;

//		//public CurveEditorOverlay() : base(CurveEditorTransformOrientation.id) => instance = this;

//		public static void Hide()
//		{
//			if (instance != null)
//				instance.displayed = false;
//		}

//		public static void Show()
//		{
//			if (instance != null)
//				instance.displayed = true;
//		}
//	}

//	[EditorToolbarElement(id, typeof(SceneView))]
//	public class zzz : EditorToolbarButton
//	{
//		public const string id = CurveEditorOverlay.toolbarId + "/zzz";

//		public zzz()
//		{
//			text = "zzz";
//			tooltip = "zzz tooltip";
//			icon = EditorGUIUtility.isProSkin ? Resources.Load<Texture2D>("Beziericon_d") : Resources.Load<Texture2D>("Beziericon");
//			clicked += () => {
//				Debug.Log("Clicked zzz" + Time.time);

//				//Undo.RegisterCreatedObjectUndo(newObj.gameObject, "Create Cube");
//			};
//		}
//	}

//	//[EditorToolbarElement(id, typeof(SceneView))]
//	//public class CurveEditorTransformOrientation : EditorToolbarDropdown
//	//{
//	//	public const string id = CurveEditorOverlay.toolbarId + "/CurveEditorTransformOrientation";

//	//	public enum TransformOrientation { World = 0, Local = 1, View = 2 }
//	//	public static TransformOrientation orientation = TransformOrientation.World;

//	//	public static Quaternion rotation => orientation switch
//	//	{
//	//		TransformOrientation.World => Quaternion.identity,
//	//		TransformOrientation.Local => ((GameObject)Selection.activeObject)?.transform.rotation??Quaternion.identity,
//	//		TransformOrientation.View => Camera.current.transform.rotation,
//	//		_ => Quaternion.identity
//	//	};

//	//	Texture2D[] icons;

//	//	public CurveEditorTransformOrientation()
//	//	{
//	//		tooltip = "Transform Orientation";
//	//		clicked += ShowDropdown;
//	//		icons = new Texture2D[] {
//	//			Resources.Load<Texture2D>("Bezier.WorldOrientation_d"),
//	//			Resources.Load<Texture2D>("Bezier.LocalOrientation_d"),
//	//			Resources.Load<Texture2D>("Bezier.ViewOrientation_d")
//	//		};
//	//		Update(CurveEditorPersistentData.Instance?.CurrentOrientation ?? TransformOrientation.World);
//	//	}
//	//	void Update(TransformOrientation value)
//	//	{
//	//		if (CurveEditorPersistentData.Instance)
//	//			CurveEditorPersistentData.Instance.CurrentOrientation = value;

//	//		orientation = value;
//	//		text = orientation.ToString();
//	//		icon = icons[(int)orientation];
//	//	}

//	//	void ShowDropdown()
//	//	{
//	//		var menu = new GenericMenu();
//	//		menu.AddItem(new GUIContent(TransformOrientation.World.ToString()),
//	//			orientation == TransformOrientation.World, () => Update(TransformOrientation.World));

//	//		menu.AddItem(new GUIContent(TransformOrientation.Local.ToString()),
//	//			orientation == TransformOrientation.Local, () => Update(TransformOrientation.Local));

//	//		menu.AddItem(new GUIContent(TransformOrientation.View.ToString()),
//	//			orientation == TransformOrientation.View, () => Update(TransformOrientation.View));

//	//		//menu.ShowAsContext();
//	//		menu.DropDown(new Rect(this.worldBound.position + (Vector2.up * this.layout.height), Vector2.zero));
//	//	}
//	//}
//}