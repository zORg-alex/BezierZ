using UnityEngine;
using UnityEditor;
using System;
using Utility;
using UnityEngine.SceneManagement;
using System.Diagnostics;

public abstract class SubscribableEditor<T> where T : EditableClass
{
	protected T field;
	protected SerializedProperty property;

	public virtual void Start(T curve, SerializedProperty property)
	{
		Unsubscribe();
		if (this.field != null) this.field._isInEditMode = false;
		this.field = curve;
		this.property = property;
		SceneView.duringSceneGui += EditorOnSceneGUI;
		Selection.selectionChanged += Stop;
		SceneManager.sceneUnloaded += Stop;
		AssemblyReloadEvents.beforeAssemblyReload += Stop;
	}

	public void Stop(Scene sc) => Stop();
	public virtual void Stop()
	{
		field._isInEditMode = false;
		field = null;
		Unsubscribe();
	}

	private void Unsubscribe()
	{
		SceneView.duringSceneGui -= EditorOnSceneGUI;
		Selection.selectionChanged -= Stop;
		SceneManager.sceneUnloaded -= Stop;
		AssemblyReloadEvents.beforeAssemblyReload -= Stop;
	}

	private void EditorOnSceneGUI(SceneView sv)
	{
		if (!CheckPropertyIsOK())
			Stop();
		else
			OnSceneGUI();
	}

	private bool CheckPropertyIsOK()
	{
		OtherCurve c = null;
		bool ok = false;
		try
		{
			c = property.GetValue<OtherCurve>();
			ok = c != null && field._id == c._id;
			if (!ok)
			{
				//TODO Is this necessary?
				if (c != null) c._isInEditMode = false;
				field._isInEditMode = false;
			}
		}
		catch (Exception)
		{

		}
		return ok;
	}

	public abstract void OnSceneGUI();
}

public class OtherCurveEditor : SubscribableEditor<OtherCurve>
{
	private static OtherCurveEditor instance;
	public static OtherCurveEditor Instance
	{
		[DebuggerStepperBoundary]
		get
		{
			if (instance == null) instance = new OtherCurveEditor();
			return instance;
		}
	}

	public override void Start(OtherCurve curve, SerializedProperty property)
	{
		base.Start(curve, property);
		instance = this;
	}
	public override void Stop()
	{
		base.Stop();
		instance = null;
	}

	public override void OnSceneGUI()
	{
		ProcessInput();
		DrawStuff();
	}

	private void ProcessInput()
	{

	}

	private void DrawStuff()
	{
		var c = Handles.color;
		var m = Handles.matrix;
		Handles.matrix = ((Component)property.serializedObject.targetObject).transform.localToWorldMatrix;

		Handles.color = new Color(1f, .25f, .25f);
		//Handles.DrawAAPolyLine(curve.VertexDataPoints);
		foreach (var p in field.VertexData)
		{
			Handles.DrawAAPolyLine(p.Position, p.Position + p.right * .2f);
		}

		Handles.matrix = m;
		Handles.color = c;
	}
}