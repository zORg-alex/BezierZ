using UnityEditor;
using System;
using BezierZUtility;
using UnityEngine.SceneManagement;

namespace BezierCurveZ.Editor
{
	public abstract class SubscribableEditor<T> where T : EditableClass
	{
		protected T field;
		protected SerializedProperty property;

		public virtual void Start(T editable, SerializedProperty property)
		{
			Unsubscribe();
			if (this.field != null) this.field.IsInEditMode = false;
			this.field = editable;
			this.property = property;
			SceneView.duringSceneGui += EditorOnSceneGUI;
			Selection.selectionChanged += Stop;
			SceneManager.sceneUnloaded += Stop;
			AssemblyReloadEvents.beforeAssemblyReload += Stop;
		}

		public void Stop(Scene sc) => Stop();
		public virtual void Stop()
		{
			field.IsInEditMode = false;
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
			Curve c = null;
			bool ok = false;
			try
			{
				c = property.GetValue<Curve>();
				ok = c != null && field._id == c._id;
				if (!ok)
				{
					//TODO Is this necessary?
					if (c != null) c.IsInEditMode = false;
					field.IsInEditMode = false;
				}
			}
			catch (Exception)
			{

			}
			return ok;
		}

		public abstract void OnSceneGUI();
	}
}