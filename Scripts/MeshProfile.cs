using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BezierCurveZ.MeshGeneration
{
	/// <summary>
	/// This class won't be supported. It is left for prototyping cases and never for actual use in prod.
	/// </summary>
	[Serializable]
	public class MeshProfile {
		[SerializeField,HideInInspector]
		Vector2[] _points;
		//[SerializeField, HideInInspector]
		//Vector2[] _normals;
		//[SerializeField, HideInInspector]
		//Vector2[] _uvs;
		//[SerializeField, HideInInspector]
		//bool[] _autoNormals;

		public MeshProfile()
		{
			_points = new Vector2[] { Vector2.zero};
			//_normals = new Vector2[] { Vector2.up};
			//_uvs = new Vector2[] { Vector2.zero};
			//_autoNormals = new bool[] { true };
		}

		public Vector2 this[int index] {
			get {
				if (index < 0 || index >= _points.Length) return default;
				return _points[index % Length];
			}

			set {
				if (index < 0 || index >= _points.Length) return;
				_points[index] = Vector2.Min(Vector2.Max(value, -Vector2.one), Vector2.one);
			}
		}

		public Vector2 GetLoopPoint(int index) {
			return _points[index % Length];
		}

		public int Length => _points.Length;

		public Vector2[] GetPoints() => _points;
		public Vector2[] GetLoopedPoints() {
			if (Length == 0) return _points;
			var r = new Vector2[Length + 1];
			for (int i = 0; i < Length + 1; i++) {
				r[i] = GetLoopPoint(i);
			}
			return r;
		}
		public Vector3[] GetPointsV3() => _points.SelectArray(p=>(Vector3)p);

		public void RemoveAt(int ind) {
			if (ind >= _points.Length || ind < 0) return;
			//ArrayUtility.RemoveAt(ref _points, ind);
			var list = _points.ToList();
			list.RemoveAt(ind);
			_points = list.ToArray();

		}

		public void AddAt(int ind, Vector2 point) {
			//ArrayUtility.Insert(ref _points, ind > 0 ? ind : 0, Vector2.Min(Vector2.Max(point, -Vector2.one), Vector2.one));
			var list = _points.ToList();
			list.Insert(ind > 0 ? ind : 0, Vector2.Min(Vector2.Max(point, -Vector2.one), Vector2.one));
			_points = list.ToArray();
		}
	}
}