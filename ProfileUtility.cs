using UnityEngine;
using BezierCurveZ;
using System.Collections.Generic;

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

					if (!(prevSharp && vert.isSharp) || curve.IsClosed && i == 0 || i > 0)
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
}
