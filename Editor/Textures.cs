using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal class Textures : /*ScriptableSingleton<Textures>*/ ScriptableObject
{
	private static Textures _instance;
	public static Textures instance
	{
		get
		{
			if (_instance) return _instance;
			foreach (var guid in AssetDatabase.FindAssets($"{typeof(Textures).Name} t:scriptableobject"))
			{
				var asset = AssetDatabase.LoadAssetAtPath<Textures>(AssetDatabase.GUIDToAssetPath(guid));
				if (asset != null && asset)
					_instance = asset;
			}
			return _instance;
		}
	}

public TextAsset textures;
}
