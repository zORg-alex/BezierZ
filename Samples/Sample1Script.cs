using BezierCurveZ;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using BezierCurveZ.MeshGeneration;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BezierCurveZ.Samples {
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class Sample1Script : MonoBehaviour
	{
		public Curve profile;
		public Curve path;
		public List<Curve> curves;
		public Vector3 scale = Vector3.one;
		public Vector3 offset;
		public ProfileUtility.UVMode mode;

		public void Generate()
		{
			var mf = GetComponent<MeshFilter>();
			var meshes = new List<Mesh>
			{
				ProfileUtility.GenerateProfileMesh(path, profile, offset, scale, mode: mode)
			};
			foreach (var curve in curves)
			{
				meshes.Add(ProfileUtility.GenerateProfileMesh(curve, profile, offset, scale, mode: mode));
			}
			CombineInstance[] combine = new CombineInstance[meshes.Count];
			for (int i = 0; i < meshes.Count; i++)
			{
				combine[i] = new CombineInstance() { mesh= meshes[i], transform = Matrix4x4.identity };
			}
			var combinedMesh = new Mesh();
			combinedMesh.CombineMeshes(combine, true);

			mf.sharedMesh = combinedMesh;
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(Sample1Script))]
#if ODIN_INSPECTOR
	public class Sample1ScriptInspector : Sirenix.OdinInspector.Editor.OdinEditor
#else
	public class Sample1ScriptInspector : Editor
#endif
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			if (GUILayout.Button("Generate"))
			{
				((Sample1Script)target).Generate();
			}
		}
	}
#endif
}