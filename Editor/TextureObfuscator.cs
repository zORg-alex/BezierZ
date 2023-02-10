using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;

public static class TextureObfuscator
{
	[MenuItem("Assets/Unack Textures")]
	private static void UnpackTextures()
	{
		var zzzz = UnpackTextures(AssetDatabase.GetAssetPath(Selection.activeObject));
	}

	[MenuItem("Assets/Unack Textures", true)]
	private static bool UnpackTexturesValidator() => Selection.activeObject.name.EndsWith("textures");

	public static Texture2D[] UnpackTextures(string path)
	{
		List<object> raw;
		BinaryFormatter bf = new BinaryFormatter();
		using (FileStream fs = File.OpenRead(path))
		{
			raw = (List<object>)bf.Deserialize(fs);
		}

		var arr = new Texture2D[raw.Count];
		int i = 0;
		foreach ((byte[] raw, string name, int w, int h) r in raw)
		{
			arr[i] = new Texture2D(r.w, r.h);
			arr[i].LoadRawTextureData(r.raw);
			arr[i].Apply();
			arr[i].name = r.name;
			i++;
		}
		return arr;
	}

	[MenuItem("Assets/Pack Textures")]
	private static void PackTextures()
	{
		if (Selection.objects.Length == 0) return;
		var list = new Texture2D[0];
		string path = string.Empty;
		if ( Selection.objects.All(o => o.GetType() == typeof(Texture2D)))
		{
			list = Selection.objects.Select(o => (Texture2D)o).ToArray();
			path = AssetDatabase.GetAssetPath(list[0]);
			path = path.Substring(0, path.LastIndexOf('/'));
		}
		else
		{
			path = AssetDatabase.GetAssetPath(Selection.activeObject);
			var paths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(path)).ToArray();
			list = paths.Select(p => AssetDatabase.LoadAssetAtPath<Texture2D>(p)).Where(t => t != null).ToArray();
		}

		List<object> raw = new List<object>();
		foreach (var t in list)
		{
			raw.Add((raw: t.GetRawTextureData(), name: t.name, w: t.width, h: t.height));
		}

		BinaryFormatter bf = new BinaryFormatter();
		using (MemoryStream ms = new MemoryStream())
		{
			bf.Serialize(ms, raw);
			File.WriteAllBytes(path.Replace('/', '\\') + "\\textures.txt", ms.ToArray());
		}
	}
	[MenuItem("Assets/Pack Textures", true)]
	private static bool PackTexturesValidator()
	{
		var path = AssetDatabase.GetAssetPath(Selection.activeObject);
		return Selection.objects.All(o=>o.GetType() == typeof(Texture2D)) || AssetDatabase.LoadAllAssetsAtPath(path).Length == 0;
	}
}