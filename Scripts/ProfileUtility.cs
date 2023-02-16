using UnityEngine;
using BezierCurveZ;
using System.Collections.Generic;
using System.Linq;

namespace BezierCurveZ
{
	public static class ProfileUtility
	{
		public enum UVMode { None = 0, UUniform = 1, USegment = 2, ULength = 4, VUniform = 8, VSegment = 16, VLength = 32, Uniform = UUniform | VUniform, Length = ULength | VLength, Segment = USegment | VSegment }

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
		/// 
		/// </summary>
		/// <param name="curve"></param>
		/// <param name="profile"></param>
		/// <param name="offset">profile offset</param>
		/// <param name="scale">profile scale</param>
		/// <param name="autoNormals">Use GenerateNormals or leave calculated by curve rotations</param>
		/// <param name="mode">UV coordinate generation, Uniform [0..1] from curve start to end,
		/// Segment [0..1] for each segment, Length coordinate equals to point distance from start</param>
		/// <returns></returns>
		public static Mesh GenerateProfileMesh(Curve curve, Curve profile, Vector3 offset, Vector3 scale, bool autoNormals = true, UVMode mode = UVMode.Uniform, string name = null)
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
				foreach (var ep in cStrip)
				{
					var profInd = 0;
					var prevProfileInd = profileLen - 1;
					//Profile flowing segmentws
					foreach (var pStrip in profileStrips)
					{
						var right = ep.Rotation * Vector3.right;
						var up = ep.Rotation * Vector3.up;
						//Each profile point in strips
						foreach (var pp in pStrip)
						{
							//Transform point
							var rot = ep.Rotation * pp.Rotation;
							var scaledprofpoint = (offset + Vector3.Scale(pp.Position, scale));
							var pos = ep.Position + right * scaledprofpoint.x + up * scaledprofpoint.y;
							//var pos = ep.point + rot * scaledprofpoint;

							vertices[curveInd + profInd] = pos;
							normals[curveInd + profInd] = rot * Vector3.right;
							//uvs[curveInd + profInd] = new Vector2(pp.distance / profile.VertexData.CurveLength(), unifiedVCoofdinate ? ep.distance / curve.VertexData.CurveLength() : ep.distance);

							uvs[curveInd + profInd] =
								Vector2.up * (mode.HasFlag(UVMode.VUniform) ? (ep.distance / curve.VertexData.CurveLength()) :
								mode.HasFlag(UVMode.VLength) ? ep.distance :
								mode.HasFlag(UVMode.VSegment) ? ep.cumulativeTime - ep.segmentInd : 0) +
								Vector2.right * (mode.HasFlag(UVMode.UUniform) ? pp.distance / profile.VertexData.CurveLength() :
								mode.HasFlag(UVMode.ULength) ? pp.distance :
								mode.HasFlag(UVMode.USegment) ? pp.cumulativeTime - ep.segmentInd : 0);

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
					curveInd += profileLen;
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