using UnityEditor;
using System;
using Utility;
using UnityEngine.SceneManagement;

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
