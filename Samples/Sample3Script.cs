using BezierCurveZ;
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Random = UnityEngine.Random;

public class Sample3Script : MonoBehaviour
{
	public Curve curve;
	public List<PrefabLength> prefabs;
	public int seed;

	internal void Instantiate()
	{
		int childCount = transform.childCount;
		for (int i = childCount - 1; i >= 0; i--)
			if (Application.isPlaying)
				Destroy(transform.GetChild(i).gameObject);
			else
				DestroyImmediate(transform.GetChild(i).gameObject);

		Random.InitState(seed);
		VertexData[] vertexData = curve.VertexData;
		var curveLength = vertexData.CurveLength();

		var len = 0f;
		var tries = 0;
		while(len <= curveLength)
		{
			var prefab = GetPrefab(curveLength - len);
			if (prefab != null)
			{
				var instance = Instantiate(prefab.prefab, transform);
				VertexData v = vertexData.GetPointFromDistance(len + prefab.offset);
				instance.transform.localPosition = v.Position;
				instance.transform.localRotation = v.Rotation;
				instance.transform.localScale = v.Scale;
				len += prefab.length;
			}
			else tries++;
			if (len == 0 || tries > 5)
				break;
		}
	}

	private PrefabLength GetPrefab(float remainingLength)
	{
		for (int i = 0; i < 5; i++)
		{
			var prefab = prefabs[Random.Range(0, prefabs.Count)];
			if (prefab.length <= remainingLength)
				return prefab;
		}
		return null;
	}
}

[Serializable]
public class PrefabLength
{
	public GameObject prefab;
	public float length;
	public float offset;
}


#if UNITY_EDITOR
[CustomEditor(typeof(Sample3Script))]
#if ODIN_INSPECTOR
public class Test3ScriptInspector : Sirenix.OdinInspector.Editor.OdinEditor
#else
public class Test3ScriptInspector : Editor
#endif
{
	public override void OnInspectorGUI()
	{
		base.OnInspectorGUI();
		if (GUILayout.Button("Instantiate"))
		{
			((Sample3Script)target).Instantiate();
		}
	}
}
#endif