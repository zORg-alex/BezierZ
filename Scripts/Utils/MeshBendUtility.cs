using BezierZUtility;
using System;
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
		[Obsolete]
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


		/// <summary>
		/// Bends <paramref name="source"/> mesh with a curve. Curves first point and direction decides model median,
		/// <paramref name="bendLength"/> length of the bent part,
		/// <paramref name="scaleBendToCurve"/> will set to scale model bendable part to curve length,
		/// or ignore curve after <paramref name="bendLength"/>
		/// </summary>
		/// <param name="source">Original mesh to bend</param>
		/// <param name="mesh">Previously bent mesh, in case there is one</param>
		/// <param name="curve">Curve</param>
		/// <param name="bendLength">Length of <paramref name="source"/> mesh to apply bend</param>
		/// <param name="scaleBendToCurve">If true, bendable part of the <paramref name="source"/> mesh will be resized to curve length,
		/// else curve part further <paramref name="bendLength"/> will be ignored</param>
		/// <param name="autoNormals">Recalculate normals after bending</param>
		/// <returns></returns>
		public static Mesh BendMesh(Mesh source, Mesh mesh, Curve curve, float bendLength, bool scaleBendToCurve = false, bool autoNormals = true) =>
			BendMesh(source, mesh, curve, bendLength, scaleBendToCurve, autoNormals, Matrix4x4.identity, Matrix4x4.identity);

		/// <inheritdoc cref="BendMesh"/>
		/// <param name="meshToBendSpace">CurveTransform.worldToLocalMatrix * MeshTransform.localToWorldMatrix  Mesh=>Curve</param>
		/// <param name="bendSpaceToMesh">MeshTransform.worldToLocalMatrix * CurveTransform.localToWorldMatrix Curve=>Mesh</param>
		public static Mesh BendMesh(Mesh source, Mesh mesh, Curve curve, float bendLength, bool scaleBendToCurve = false, bool autoNormals = true, Matrix4x4 meshToBendSpace = default, Matrix4x4 bendSpaceToMesh = default)
		{
			if (mesh == null || mesh == source)
				mesh = source.Copy();
			if (meshToBendSpace == default)
				meshToBendSpace = Matrix4x4.identity;
			if (bendSpaceToMesh == default)
				bendSpaceToMesh = Matrix4x4.identity;

			float curveLength = curve.VertexData.CurveLength();
			var distCoef = scaleBendToCurve ? bendLength / curveLength : 1f;
			//Setting these to default values helps to move meshes to any curve, but restricts bendspace to a 0,0,0 position and 0,0,1 forward direction
			var bendDir = Vector3.forward;//curve.VertexData[0].forward;
			var origin = Vector3.zero;//curve.VertexData[0].Position;
			var originInv = Quaternion.identity;//curve.VertexData[0].Rotation.Inverted();

			var vertices = source.vertices;
			var normals = source.normals;
			var tangents = source.tangents;
			for (int i = 0; i < vertices.Length; i++)
			{
				var v = meshToBendSpace.MultiplyPoint3x4(vertices[i]);
				var n = normals.Length > 0 ? normals[i] : Vector3.zero;
				var t = tangents.Length > 0 ? tangents[i] : Vector4.zero;

				//float bendSpaceDist = Mathf.Clamp(Vector3.Dot(v - origin, bendDir), 0, bendLength);
				float bendSpaceDist = Mathf.Clamp(v.z, 0, bendLength);
				var curveSpaceDist = Mathf.Clamp(bendSpaceDist / distCoef, 0, curveLength);
				var curvePoint = curve.VertexData.GetPointFromDistance(curveSpaceDist);

				var vRelToCurvePoint = v - bendDir * bendSpaceDist;
				vertices[i] = bendSpaceToMesh.MultiplyPoint3x4(curvePoint.Position - origin + curvePoint.Rotation * vRelToCurvePoint);
				if (n != Vector3.zero)
					normals[i] = /*originInv * */curvePoint.Rotation * normals[i];
				if (t != Vector4.zero)
					tangents[i] = /*originInv * */curvePoint.Rotation * tangents[i];
			}

			mesh.vertices = vertices;
			if (normals.Length > 0)
				mesh.normals = normals;
			if (tangents.Length > 0)
				mesh.tangents = tangents;
			if (autoNormals)
				mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		public static Matrix4x4 FromToMatrix(this Transform from, Transform to) => to.worldToLocalMatrix * from.localToWorldMatrix;

		/// <summary>
		/// Combines all meshes into worldspace coodinates unless is supplied with WorldToLocal matrix of a parent or a relative transform
		/// </summary>
		/// <param name="meshFilters">MeshFilter supplies both sharedMesh and <see cref="Transform.localToWorldMatrix"/> to get a worldspace vertices of that mesh.</param>
		/// <param name="worldToLocal"><see cref="Transform.worldToLocalMatrix"/> of a parent or any other transform to be a coordinate center of new mesh</param>
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