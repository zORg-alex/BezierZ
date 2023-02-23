using BezierCurveZ;
using BezierCurveZ.MeshGeneration;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class Sample2Script : MonoBehaviour
{
	public Curve curve;
	public MeshFilter meshfilter;
	public MeshCombine meshSource;
	public float length;

	internal void GenerateMesh()
	{
		var sourceToThis = meshSource.transform.FromToMatrix(transform);
		var thisToMeshFilter = transform.FromToMatrix(meshfilter.transform);
		Mesh mesh = MeshBendUtility.GetBentMesh(meshSource.GetMesh(), meshfilter.sharedMesh, curve, sourceToThis, thisToMeshFilter, length);
		mesh.RecalculateNormals();
		meshfilter.sharedMesh = mesh;
	}

#if UNITY_EDITOR
	private void OnDrawGizmosSelected()
	{
		var m = Handles.matrix;
		Handles.matrix = transform.localToWorldMatrix;
		var c = Handles.color;
		Handles.color = Color.blue;

		Handles.DrawAAPolyLine(Vector3.zero, Vector3.forward * length);

		Handles.color = c;
		Handles.matrix = m;
	}
#endif
}


#if UNITY_EDITOR
[CustomEditor(typeof(Sample2Script))]
#if ODIN_INSPECTOR
public class Test3ScriptInspector : Sirenix.OdinInspector.Editor.OdinEditor
#else
	public class Test3ScriptInspector : Editor
#endif
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		if (GUILayout.Button("Generate Mesh"))
		{
			((Sample2Script)target).GenerateMesh();
		}
	}
}
#endif