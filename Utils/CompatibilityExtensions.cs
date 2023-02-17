using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking.Types;

public static class CompatibilityExtensions
{
	public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> collection, int count)
	{
		if (!collection.Any())
			yield break;

		T[] itemqueue = new T[count];
		int i = 0;
		foreach (var item in collection)
		{
			if (i >= count)
			{
				yield return itemqueue[(i - count) % count];
			}
			itemqueue[i++ % count] = item;
		}
	}

	public static void DrawVertices(List<Vector3> vertices, bool towardCamera)
	{
		Handles.color = towardCamera ? Color.green : new Color(.6f, .3f, 0);
		Handles.DrawAAPolyLine((towardCamera ? 2f : 3f), vertices.ToArray());
	}

	public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
	{
		if (dictionary.ContainsKey(key))
			return dictionary[key];
		return default;
	}
}
public static class HashCode
{
	public static int Combine(params object[] objects)
	{
		int hash = 0;
		for (int i = 0; i < objects.Length; i++)
		{
			hash ^= objects[i].GetHashCode();
		}
		return hash;
	}
}