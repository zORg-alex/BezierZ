using BezierCurveZ;
using BezierCurveZ.MeshGeneration;
using System;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class Sample0Script : MonoBehaviour
{
	public Curve curve;
	public MeshProfile profile;
	public Vector3[] offsets = new Vector3[] { Vector3.zero };
	public Vector3[] scales = new Vector3[] { Vector3.one };
	internal void GenerateMesh()
	{
		var mf = GetComponent<MeshFilter>();
		mf.sharedMesh = ProfileUtility.GenerateProfileMesh(curve, profile, offsets, scales);
	}
}


#if UNITY_EDITOR
[CustomEditor(typeof(Sample0Script))]
#if ODIN_INSPECTOR
public class Test2ScriptInspector : Sirenix.OdinInspector.Editor.OdinEditor
#else
	public class Test2ScriptInspector : Editor
#endif
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		if (GUILayout.Button("Generate Mesh"))
		{
			((Sample0Script)target).GenerateMesh();
		}
	}
}
#endif