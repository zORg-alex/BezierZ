using BezierCurveZ;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BezierCurveZ.Samples {
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public class Sample1Script : MonoBehaviour
	{
		public Curve profile;
		public Curve path;
		public Vector3 scale = Vector3.one;
		public Vector3 offset;
		public ProfileUtility.UVMode mode;

		public void Generate()
		{
			var mf = GetComponent<MeshFilter>();
			var mesh = ProfileUtility.GenerateProfileMesh(path, profile, offset, scale, mode: mode);
			mf.sharedMesh = mesh;
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(Sample1Script))]
	public class Sample1ScriptInspector : Editor
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