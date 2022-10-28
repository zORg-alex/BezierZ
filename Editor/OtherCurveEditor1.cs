using UnityEngine;
using UnityEditor;
using System;
using Utility;
using UnityEngine.SceneManagement;
using System.Diagnostics;

public class OtherCurveEditor
{
	private static OtherCurveEditor instance;
	public static OtherCurveEditor Instance
	{
		[DebuggerStepperBoundary]
		get {
			if (instance == null) instance = new OtherCurveEditor();
			return instance;
		}
	}

	private OtherCurve curve;
	private SerializedProperty property;

	public void Start(OtherCurve curve, SerializedProperty property)
	{
		Unsubscribe();
		if (this.curve != null) this.curve._isInEditMode = false;
		instance = this;
		this.curve = curve;
		this.property = property;
		SceneView.duringSceneGui += OnSceneGUI;
		Selection.selectionChanged += Stop;
		SceneManager.sceneUnloaded += Stop;
		AssemblyReloadEvents.beforeAssemblyReload += Stop;
	}

	public void Stop(Scene sc) => Stop();
	public void Stop()
	{
		instance = null;
		curve._isInEditMode = false;
		curve = null;
		Unsubscribe();
	}

	private void Unsubscribe()
	{
		SceneView.duringSceneGui -= OnSceneGUI;
		Selection.selectionChanged -= Stop;
		SceneManager.sceneUnloaded -= Stop;
		AssemblyReloadEvents.beforeAssemblyReload -= Stop;
	}

	private void OnSceneGUI(SceneView sv)
	{
		if (!CheckProperty())
			Stop();

		ProcessInput();
		DrawStuff();
	}

	private bool CheckProperty()
	{
		OtherCurve c = null;
		bool ok = false;
		try
		{
			c = property.GetValue<OtherCurve>();
			ok = curve._id == c._id;
			if (!ok)
			{
				//TODO Is this necessary?
				c._isInEditMode = false;
				curve._isInEditMode = false;
			}
		}
		catch (Exception)
		{

		}
		return ok;
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
		foreach (var p in curve.VertexData)
		{
			Handles.DrawAAPolyLine(p.Position, p.Position + p.right * .2f);
		}

		Handles.matrix = m;
		Handles.color = c;
	}
}