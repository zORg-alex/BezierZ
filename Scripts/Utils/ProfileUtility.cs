using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using BezierZUtility;

namespace BezierCurveZ.MeshGeneration
{
	public static class ProfileUtility
	{
		[Flags]
		public enum UVMode { None = 0, UUniform = 1, USegment = 2, ULength = 4, VUniform = 8, VSegment = 16, VLength = 32, Uniform = UUniform | VUniform, Length = ULength | VLength, Segment = USegment | VSegment }

		/// <summary>
		/// Generates mesh from curve profile.
		/// This method is not supported, it is present as an example of how mesh can be generated
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		public static Mesh GenerateProfileMesh(Curve curve, MeshGeneration.MeshProfile profile) =>
			GenerateProfileMesh(curve, profile, Vector3.zero, Vector3.one);
		/// <summary>
		/// Generates mesh from curve profile.
		/// This method is not supported, it is present as an example of how mesh can be generated
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="offset">profile offset</param>
		/// <param name="scale">profile scale</param>
		public static Mesh GenerateProfileMesh(Curve curve, MeshGeneration.MeshProfile profile, Vector3 offset, Vector3 scale) =>
			GenerateProfileMesh(curve, profile, new Vector3[] { offset }, new Vector3[] { scale });
		/// <summary>
		/// Generates mesh from curve profile iterating it multiple times with offsets and scales applied.
		/// This method is not supported, it is present as an example of how mesh can be generated
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="offsets">profile offset</param>
		/// <param name="scales">profile scale</param>
		public static Mesh GenerateProfileMesh(Curve curve, MeshGeneration.MeshProfile profile, Vector3[] offsets, Vector3[] scales)
		{
			bool usePathNormals = false;

			int pVerticesCount = curve.VertexDataPoints.Length;
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
					var pos = point.Position;

					var loopFirstVert = (i + o * pVerticesCount) * profileLen;
					var previousLoopFirstVert = (PrevIndex(i, pVerticesCount) + o * pVerticesCount) * profileLen;
					for (int j = 0; j < profileLen; j++)
					{
						var p = ((Vector3)profile[j]).MultiplyComponentwise(scales[o]) + offsets[o];
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

		/// <summary>
		/// Generates mesh from curve profile.
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
		/// <param name="mode">UV coordinate generation, Uniform [0..1] from curve start to end,
		/// Segment [0..1] for each segment, Length coordinate equals to point distance from start</param>
		public static Mesh GenerateProfileMesh(Curve curve, Curve profile, bool autoNormals = true, UVMode mode = UVMode.Uniform, string name = null) =>
			GenerateProfileMesh(curve, profile, new Vector3[] { Vector3.zero }, new Vector3[] { Vector3.one }, autoNormals, mode, name);

		/// <summary>
		/// Generates mesh from curve profile.
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
		/// <param name="mode">UV coordinate generation, Uniform [0..1] from curve start to end,
		/// Segment [0..1] for each segment, Length coordinate equals to point distance from start</param>
		public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector2 UVScale, bool autoNormals = true, UVMode mode = UVMode.Uniform, string name = null) =>
			GenerateProfileMesh(curve, profile, new Vector3[] { Vector3.zero }, new Vector3[] { Vector3.one }, UVScale, autoNormals, mode, name);

		/// <summary>
		/// Generates mesh from curve profile.
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="offset">profile offset</param>
		/// <param name="scale">profile scale</param>
		/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
		/// <param name="mode">UV coordinate generation, Uniform [0..1] from curve start to end,
		/// Segment [0..1] for each segment, Length coordinate equals to point distance from start</param>
		public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3 offset, Vector3 scale, bool autoNormals = true, UVMode mode = UVMode.Uniform, string name = null) =>
			GenerateProfileMesh(curve, profile, new Vector3[] { offset }, new Vector3[] { scale }, autoNormals, mode, name);

		/// <summary>
		/// Generates mesh from curve profile.
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="offset">profile offset</param>
		/// <param name="scale">profile scale</param>
		/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
		/// <param name="mode">UV coordinate generation, Uniform [0..1] from curve start to end,
		/// Segment [0..1] for each segment, Length coordinate equals to point distance from start</param>
		public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3 offset, Vector3 scale, Vector2 UVScale, bool autoNormals = true, UVMode mode = UVMode.Uniform, string name = null) =>
			GenerateProfileMesh(curve, profile, new Vector3[] { offset }, new Vector3[] { scale }, UVScale, autoNormals, mode, name);

		public static readonly Vector2 UVDefaultScale = new Vector2(1,1);

		/// <summary>
		/// Generates mesh from curve profile multiple times with given offsets and scales.
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="offsets">profile offsets</param>
		/// <param name="scales">profile scales</param>
		/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
		/// <param name="mode">UV coordinate generation, Uniform [0..1] from curve start to end,
		/// Segment [0..1] for each segment, Length coordinate equals to point distance from start</param>
		public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3[] offsets, Vector3[] scales, bool autoNormals = true, UVMode mode = UVMode.Uniform, string name = null)
			=> GenerateProfileMesh(curve, profile, offsets, scales, Vector2.one, autoNormals, mode, name);

		/// <summary>
		/// Generates mesh from curve profile multiple times with given offsets and scales.
		/// </summary>
		/// <param name="curve">Along this curve profile will be extruded</param>
		/// <param name="profile">Profile for a generated mesh</param>
		/// <param name="offsets">profile offsets</param>
		/// <param name="scales">profile scales</param>
		/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
		/// <param name="mode">UV coordinate generation, Uniform [0..1] from curve start to end,
		/// Segment [0..1] for each segment, Length coordinate equals to point distance from start</param>
		public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3[] offsets, Vector3[] scales, Vector2 UVScale, bool autoNormals = true, UVMode mode = UVMode.Uniform, string name = null)
		{
			if (offsets.Length != scales.Length) throw new ArgumentException("Offets and Scales has different lengths.");
			var curveStrips = GetVertexDataStrips(curve.VertexData, curve.IsClosed);
			var profileStrips = GetVertexDataStrips(profile.VertexData, profile.IsClosed);

			int curveLen = curveStrips.Sum(l => l.Length);
			int profileLen = profileStrips.Sum(l => l.Length);
			var len = curveLen * profileLen * offsets.Length;
			var vertices = new Vector3[len];
			var normals = new Vector3[len];
			var uvs = new Vector2[len];
			var triangles = new List<int>(len / 3 * 2);

			var curveInd = 0;
			var prevCurveInd = (curveLen - 1) * profileLen;
			//Curve flowing segments separated by sharp control points
			for (int offsetind = 0; offsetind < offsets.Length; offsetind++)
			{
				var offsetProfileInd = 0;
				var offset = offsets[offsetind];
				var scale = scales[offsetind];
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
							var right = cp.Rotation * Vector3.right;
							var up = cp.Rotation * Vector3.up;
							int pStripInd = 0;
							//Each profile point in strips
							foreach (var pp in pStrip)
							{
								//Transform point
								var rot = cp.Rotation * pp.Rotation;
								var scaledprofpoint = offset + pp.Position.MultiplyComponentwise(cp.Scale).MultiplyComponentwise(scale);
								var pos = cp.Position + right * scaledprofpoint.x + up * scaledprofpoint.y;

								vertices[curveInd + profInd] = pos;
								normals[curveInd + profInd] = rot * Vector3.right;

								uvs[curveInd + profInd] =
									Vector2.up * (mode.HasFlag(UVMode.VUniform) ? (cp.distance / curve.VertexData.CurveLength()) :
									mode.HasFlag(UVMode.VLength) ? cp.distance :
									mode.HasFlag(UVMode.VSegment) ? cp.cumulativeTime : 0) * UVScale.y +
									Vector2.right * (mode.HasFlag(UVMode.UUniform) ? pp.distance / profile.VertexData.CurveLength() :
									mode.HasFlag(UVMode.ULength) ? pp.distance :
									mode.HasFlag(UVMode.USegment) ? pp.cumulativeTime : 0) * UVScale.x;

								//skip stitching to last curve vertex if open, skip stitching to last profile vertex if open, skip on profile mid strip, skip on next offset sweep
								//In case of profile formed from linear segments, each endpoint has 2 vertices with different normals, that can be skipped to save 0 size triangles
								if ((profInd > 0 || profile.IsClosed) && (curveInd > 0 || curve.IsClosed) && pStripInd > 0 && offsetProfileInd > 0)
								{
									triangles.AddRange_(prevCurveInd + prevProfileInd, prevCurveInd + profInd, curveInd + profInd);
									triangles.AddRange_(prevCurveInd + prevProfileInd, curveInd + profInd, curveInd + prevProfileInd);
								}

								prevProfileInd = profInd;
								profInd++;
								profInd %= profileLen;
								pStripInd++;
							}
						}
						offsetProfileInd++;

						prevCurveInd = curveInd;
						curveInd += profileLen;
						curveInd %= len;
					}
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

		/// <summary>
		/// Separates VertexData collection in continuous segments split on Manual or Linear endpoints.
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="IsClosed"></param>
		/// <returns></returns>
		public static VertexData[][] GetVertexDataStrips(IEnumerable<VertexData> collection, bool IsClosed)
		{
			var list = new List<VertexData[]>();
			var strip = collection.Take(1).ToList();
			var prevV = collection.FirstOrDefault();
			IEnumerable<VertexData> modifiedCollection = collection.Skip(1);
			if (IsClosed)
				modifiedCollection = collection.Skip(1).SkipLast(1);
			var i = 0;
			foreach (var v in modifiedCollection)
			{
				if (v.isSharp && prevV.isSharp && v.Position.Equals(prevV.Position))
				{
					list.Add(strip.ToArray());
					strip.Clear();
				}
				strip.Add(v);

				prevV = v;
				i++;
			}
			if (strip.Count > 0)
			{
				list.Add(strip.ToArray());
				strip.Clear();
			}

			return list.ToArray();
		}
	}
}