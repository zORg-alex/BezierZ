using UnityEngine;

namespace BezierCurveZ.MeshGeneration
{
	public static class MeshBendUtility
	{
		/// <summary>
		/// To bend mesh this method need to convert space so that bend start would be at 0 coordinates, and bend axis would be z.
		/// Bend ends at zLength on z axis. Use <see cref="FromToMatrix"/> to get those Matrix4x4 values.
		/// You can create script that uses this method on an empty object positioned at the start of the bend with z axis pointin to a bend direction.
		/// Then use <see cref="FromToMatrix"/> to get right matrix values.
		/// </summary>
		/// <param name="originalMesh">This mesh shouldn't be visible in scene it's velues will be copied to generated mesh</param>
		/// <param name="mesh">This can be reused to save memory, positions will be recalculated</param>
		/// <param name="curve"></param>
		/// <param name="originalToBendSpace"></param>
		/// <param name="bendSpaceToMesh"></param>
		/// <param name="zLength"></param>
		/// <returns></returns>
		public static Mesh GetBentMesh(Mesh originalMesh, Mesh mesh, Curve curve, Matrix4x4 originalToBendSpace, Matrix4x4 bendSpaceToMesh, float zLength, bool autoNormals = true)
		{
			if (mesh == null || mesh == originalMesh)
				mesh = originalMesh.Copy();

			var distCoef = curve.VertexData.CurveLength() / zLength;

			var verts = originalMesh.vertices;
			for (int i = 0; i < verts.Length; i++)
			{
				var v = originalToBendSpace.MultiplyPoint3x4(verts[i]);
				var dist = Mathf.Clamp(v.z, 0, zLength);
				var point = curve.VertexData.GetPointFromDistance(dist);
				var vRelToPoint = v - new Vector3(0, 0, dist);
				verts[i] = bendSpaceToMesh.MultiplyPoint3x4(point.Position + point.Rotation * vRelToPoint);
			}
			mesh.vertices = verts;
			if (autoNormals)
				mesh.RecalculateNormals();
			return mesh;
		}

		public static Matrix4x4 FromToMatrix(this Transform from, Transform to) => to.worldToLocalMatrix * from.localToWorldMatrix;

		public static Mesh CombineMeshFilters(this MeshFilter[] meshFilters, Matrix4x4 worldToLocal, string name = null)
		{
			if (name == null)
			{
				name = $"Combined: {string.Join<MeshFilter>(',', meshFilters)}";
			}
			var mm = new CombineInstance[meshFilters.Length];
			int i = 0;
			foreach (var part in meshFilters)
			{
				mm[i] = new CombineInstance() { mesh = part.sharedMesh, transform = worldToLocal * part.transform.localToWorldMatrix };
				i++;
			}
			var mesh = new Mesh();
			mesh.CombineMeshes(mm);
			mesh.name = name;
			return mesh;
		}

		public static Mesh Copy(this Mesh original)
		{
			var m = new Mesh()
			{
				name = original.name,
				vertices = original.vertices,
				normals = original.normals,
				tangents = original.tangents,
				triangles = original.triangles,
				bounds = original.bounds,
				uv = original.uv,
				uv2 = original.uv2,
				uv3 = original.uv3,
				uv4 = original.uv4,
				uv5 = original.uv5,
				uv6 = original.uv6,
				uv7 = original.uv7,
				uv8 = original.uv8,
				colors = original.colors,
				bindposes = original.bindposes,
				boneWeights = original.boneWeights,
				indexFormat = original.indexFormat,
				indexBufferTarget = original.indexBufferTarget,
				vertexBufferTarget = original.vertexBufferTarget,
				subMeshCount = original.subMeshCount
			};
			for (int i = 0; i < original.subMeshCount; i++)
			{
				m.SetSubMesh(i, original.GetSubMesh(i));
			}
			return m;
		}
	}
}