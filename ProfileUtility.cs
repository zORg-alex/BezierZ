using UnityEngine;
using BezierCurveZ;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
#endif
public static class ProfileUtility
{
	public static Mesh GenerateProfileMesh(Curve curve, MeshGeneration.MeshProfile profile) => GenerateProfileMesh(curve, profile, Vector3.zero, Vector3.one);
	public static Mesh GenerateProfileMesh(Curve curve, MeshGeneration.MeshProfile profile, Vector3 offset, Vector3 scale) =>
		GenerateProfileMesh(curve, profile, new Vector3[] { offset }, new Vector3[] { scale });
	/// <summary>
	/// For multiple profiles
	/// </summary>
	/// <param name="curve"></param>
	/// <param name="profile"></param>
	/// <param name="offsets"></param>
	/// <param name="scales"></param>
	/// <returns></returns>
	public static Mesh GenerateProfileMesh(Curve curve, MeshGeneration.MeshProfile profile, Vector3[] offsets, Vector3[] scales)
	{
		bool usePathNormals = false;

		int pVerticesCount = curve.Vertices.Length;
		int profileLen = profile.Length;
		var vertices = new Vector3[profileLen * pVerticesCount * offsets.Length];
		var normals = new Vector3[profileLen * pVerticesCount * offsets.Length];
		var triangles = new List<int>(profileLen * (pVerticesCount + 1) * 3 * offsets.Length);

		for (int o = 0; o < offsets.Length; o++)
		{
			var i = 0;
			foreach (var point in curve.VertexData)
			{
				var tangent = point.tangent;
				var normal = point.normal;
				var localUp = (usePathNormals) ? Vector3.Cross(tangent, normal) : point.up;
				var localRight = (usePathNormals) ? normal : Vector3.Cross(localUp, tangent);
				var pos = point.point;

				var loopFirstVert = (i + o * pVerticesCount) * profileLen;
				var previousLoopFirstVert = (PrevIndex(i, pVerticesCount) + o * pVerticesCount) * profileLen;
				for (int j = 0; j < profileLen; j++)
				{
					var p = ((Vector3)profile[j]).Scale_(scales[o]) + offsets[o];
					vertices[loopFirstVert + j] = pos + p.x * localRight + p.y * localUp;

					var next = NextIndex(j, profileLen);
					var prevVert = PrevIndex(j, profileLen);

					if (curve.IsClosed && i == 0 || i > 0)
					{
						triangles.AddRange_(previousLoopFirstVert + j, loopFirstVert + next, loopFirstVert + j);
						triangles.AddRange_(previousLoopFirstVert + j, previousLoopFirstVert + next, loopFirstVert + next);
					}
				}
				i++;
			}
		}

		var mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.triangles = triangles.ToArray();
		mesh.RecalculateNormals();

		return mesh;


		int PrevIndex(int curind, int length) => curind > 0 ? curind - 1 : length - 1;
		int NextIndex(int curind, int length) => (curind + 1 == length) ? 0 : curind + 1;
	}
	public static Mesh GenerateProfileMesh(Curve curve, Curve profile) => GenerateProfileMesh(curve, profile, Vector3.zero, Vector3.one);
	public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3 offset, Vector3 scale) =>
		GenerateProfileMesh(curve, profile, new Vector3[] { offset }, new Vector3[] { scale });
	public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3[] offsets, Vector3[] scales)
	{
		bool usePathNormals = false;

		int curveLen = curve.Vertices.Length;
		int profileLen = profile.Vertices.Length;
		var vertices = new Vector3[profileLen * curveLen * offsets.Length];
		var normals = new Vector3[profileLen * curveLen * offsets.Length];
		var triangles = new List<int>(profileLen * (curveLen + 1) * 3 * offsets.Length);

		for (int o = 0; o < offsets.Length; o++)
		{
			var i = 0;
			foreach (var point in curve.VertexData)
			{
				var tangent = point.tangent;
				var normal = point.normal;
				var localUp = (usePathNormals) ? Vector3.Cross(tangent, normal) : point.up;
				var localRight = (usePathNormals) ? normal : Vector3.Cross(localUp, tangent);

				var loopFirstVert = (i + o * curveLen) * profileLen;
				var previousLoopFirstVert = (PrevIndex(i, curveLen) + o * curveLen) * profileLen;

				var prevSharp = !curve.IsClosed;
				var j = 0;
				foreach (var vert in profile.VertexData)
				{
					var profilePointTransformed = vert.point.Scale_(scales[o] + offsets[o]);
					vertices[loopFirstVert + j] = point.point + point.rotation * profilePointTransformed;
					normals[loopFirstVert + j] = point.rotation * vert.rotation * Vector3.right;

					var next = NextIndex(j, profileLen);
					var prevVert = PrevIndex(j, profileLen);

					if (!(prevSharp && vert.isSharp) || ((curve.IsClosed && i == 0) || i > 0) && ((profile.IsClosed && j == 0) || j > 0))
					{
						triangles.AddRange_(previousLoopFirstVert + j, loopFirstVert + next, loopFirstVert + j);
						triangles.AddRange_(previousLoopFirstVert + j, previousLoopFirstVert + next, loopFirstVert + next);
					}

					prevSharp = vert.isSharp;
					j++;
				}
				i++;
			}
		}

		var mesh = new Mesh();
		mesh.vertices = vertices;
		mesh.triangles = triangles.ToArray();
		mesh.normals = normals;
		//mesh.RecalculateNormals();

		return mesh;


		int PrevIndex(int curind, int length) => curind > 0 ? curind - 1 : length - 1;
		int NextIndex(int curind, int length) => (curind + 1 == length) ? 0 : curind + 1;
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="curve"></param>
	/// <param name="profile"></param>
	/// <param name="offset">profile offset</param>
	/// <param name="scale">profile scale</param>
	/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
	/// <param name="unifiedVCoofdinate">Should UV's be 0..1 along curve or repeat by every unit of length if false</param>
	/// <returns></returns>
	public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3 offset, Vector3 scale, bool autoNormals = true, bool unifiedVCoofdinate = true, string name = null)
	{
		var curveStrips = GetVertexDataStrips(curve.VertexData, curve.IsClosed);
		var profileStrips = GetVertexDataStrips(profile.VertexData, profile.IsClosed);

		int curveLen = curveStrips.Sum(l => l.Length);
		int profileLen = profileStrips.Sum(l => l.Length);
		var len = curveLen * profileLen;
		var vertices = new Vector3[len];
		var normals = new Vector3[len];
		var uvs = new Vector2[len];
		var triangles = new List<int>(len / 3 * 2);

		var curveInd = 0;
		var prevCurveInd = (curveLen - 1) * profileLen;
		//Curve flowing segments separated by sharp control points
		foreach (var cStrip in curveStrips)
		{
			//Each curve point in strips
			foreach (var cp in cStrip)
			{
				var profInd = 0;
				var prevProfileInd = profileLen - 1;
				//Profile flowing segmentws
				foreach (var pStrip in profileStrips)
				{
					var right = cp.rotation * Vector3.right;
					var up = cp.rotation * Vector3.up;
					//Each profile point in strips
					foreach (var pp in pStrip)
					{
						//Transform point
						var rot = cp.rotation * pp.rotation;
						var scaledprofpoint = (offset + Vector3.Scale(pp.point, scale));
						var pos = cp.point + right * scaledprofpoint.x + up * scaledprofpoint.y;
						//var pos = cp.point + rot * scaledprofpoint;

						vertices[curveInd + profInd] = pos;
						normals[curveInd + profInd] = rot * Vector3.right;
						uvs[curveInd + profInd] = new Vector2(pp.length / profile.VertexDataLength, unifiedVCoofdinate ? cp.length / curve.VertexDataLength : cp.length);

						if ((profInd < profileLen - 1 || profile.IsClosed) && (curveInd > 0 || curve.IsClosed))
						{
							var nextProfInd = (profInd + 1) % profileLen;
							triangles.AddRange_(prevCurveInd + profInd, prevCurveInd + nextProfInd, curveInd + nextProfInd);
							triangles.AddRange_(prevCurveInd + profInd, curveInd + nextProfInd, curveInd + profInd);
						}

						prevProfileInd = profInd;
						profInd++;
						profInd %= profileLen;
					}
				}

				prevCurveInd = curveInd;
				curveInd+=profileLen;
				curveInd %= len;
			}
		}

		var m = new Mesh() { name = name ?? "generated profile mesh" };
		m.vertices = vertices;
		m.normals = normals;
		m.uv = uvs;
		m.triangles = triangles.ToArray();
		if (autoNormals)
			m.RecalculateNormals();

		return m;
	}

	public static BezierCurveVertexData.VertexData[][] GetVertexDataStrips(IEnumerable<BezierCurveVertexData.VertexData> collection, bool IsClosed)
	{
		var list = new List<BezierCurveVertexData.VertexData[]>();
		var strip = collection.Take(1).ToList();
		var prevV = collection.FirstOrDefault();
		IEnumerable<BezierCurveVertexData.VertexData> shiftedCollection = collection.Skip(1);
		if (IsClosed)
			shiftedCollection = shiftedCollection.Union(collection.Take(1));
		var i = 0;
		foreach (var v in shiftedCollection)
		{
			if (v.isSharp && prevV.isSharp && v.point.Equals(prevV.point))
			{
				list.Add(strip.ToArray());
				strip.Clear();
			}
			strip.Add(v);

			prevV = v;
			i++;
		}
		if (!IsClosed)
		{
			list.Add(strip.ToArray());
			strip.Clear();
		}

		return list.ToArray();
	}
}
