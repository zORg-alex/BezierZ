using BezierCurveZ.MeshGeneration;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
public class MeshCombine : MonoBehaviour
{
	public MeshFilter[] meshFilters;
	MeshFilter mf;

	private void OnEnable()
	{
		mf = GetComponent<MeshFilter>();
	}

	internal void CombineMesh()
	{
		foreach (var mf in meshFilters)
			mf.gameObject.SetActive(false);

		mf.sharedMesh = meshFilters.CombineMeshFilters(transform.worldToLocalMatrix, name);
	}

	public Mesh GetMesh() => mf.sharedMesh;
}


#if UNITY_EDITOR
[CustomEditor(typeof(MeshCombine))]
#if ODIN_INSPECTOR
public class MeshCombineInspector : Sirenix.OdinInspector.Editor.OdinEditor
#else
	public class MeshCombineInspector : Editor
#endif
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		if (GUILayout.Button("Combine Mesh"))
		{
			((MeshCombine)target).CombineMesh();
		}
	}
}
#endif