using BezierZUtility;
using System.Runtime.CompilerServices;
using UnityEngine;
using static BezierCurveZ.Curve;

namespace BezierCurveZ
{
	public struct VertexData
	{
		public Vector3 Position;
		public Quaternion Rotation;
		public Vector3 Scale;
		public Vector3 up => Rotation * Vector3.up;
		public Vector3 right => Rotation * Vector3.right;
		public Vector3 normal => Rotation * Vector3.right;
		public Vector3 forward => Rotation * Vector3.forward;
		public Vector3 tangent => Rotation * Vector3.forward;
		public Matrix4x4 TRS => Matrix4x4.TRS(Position, Rotation, Scale);

		public static implicit operator Vector3(VertexData v) => v.Position;

		public float cumulativeTime;
		public float distance;
		public int segmentInd;
		public int segmentStartVertInd;
		public bool isSharp;

		internal static VertexData[] GetVertexData(Curve otherCurve)
		{
			if (otherCurve.PointCount == 0) otherCurve.Reset();
			var splitdata = CurveInterpolation.SplitCurveByAngleError(
				otherCurve.Segments,
				otherCurve.EndPoints.SelectArray(p => p.rotation),
				otherCurve.EndPoints.SelectArray(p => p.scale),
				otherCurve.EndPoints.SelectArray(p => p.IsAutomatic),
				otherCurve.IsClosed,
				otherCurve.InterpolationMaxAngleError,
				otherCurve.InterpolationMinDistance,
				otherCurve.InterpolationAccuracy,
				otherCurve.InterpolationMethod,
				otherCurve.InterpolationCapmullRomTension);
			var vd = new VertexData[splitdata.Count];

			for (int i = 0; i < splitdata.Count; i++)
			{
				vd[i] = new VertexData()
				{
					Position = splitdata.points[i],
					Rotation = splitdata.rotations[i],
					Scale = splitdata.scales[i],
					distance = splitdata.cumulativeLength[i],
					cumulativeTime = splitdata.cumulativeTime[i],
					isSharp = splitdata.isSharp[i],
					segmentInd = splitdata.cumulativeTime[i].FloorToInt(),
					segmentStartVertInd = splitdata.firstSegmentIndex[i]

				};
			}

			return vd;
		}

		public VertexData LerpTo(VertexData b, float t) => Lerp(this, b, t);
		public static VertexData Lerp(VertexData a, VertexData b, float t)
		{
			float lerpT = Mathf.Lerp(a.cumulativeTime, b.cumulativeTime, t);
			int segInd = Mathf.FloorToInt(lerpT);
			var firstSeg = a.segmentInd == segInd;
			return new VertexData()
			{
				cumulativeTime = lerpT,
				Position = Vector3.Lerp(a, b, t),
				Rotation = Quaternion.Lerp(a.Rotation, b.Rotation, t),
				Scale = Vector3.Lerp(a.Scale, b.Scale, t),
				distance = Mathf.Lerp(a.distance, b.distance, t),
				segmentInd = segInd,
				segmentStartVertInd = firstSeg ? a.segmentStartVertInd : b.segmentStartVertInd
			};
		}

		public override string ToString() =>
			$"VertexData {{Pos = {Position}, Eulers = {Rotation.eulerAngles}, {(isSharp ? "Shapr" : "Smooth")}, dist = {distance}, time = {cumulativeTime}, segmentInd = {segmentInd}}}";
	}

	public static class OtherVertexDataExtensions
	{
		internal static int GetStartAtSegmentIndex(this VertexData[] vertexData, int segmentInd) =>
			vertexData.BinarySearch(v => v.segmentInd.CompareTo(segmentInd)).segmentStartVertInd;

		public static float CurveLength(this VertexData[] vertexData) => vertexData[vertexData.Length - 1].distance;

		public static VertexData GetPointFromTime(this VertexData[] vertexData, float t)
		{
			if (t < 0) return vertexData[0];
			if (t > vertexData[vertexData.Length - 1].cumulativeTime) return vertexData[vertexData.Length - 1];
			var ind = vertexData.BinarySearchIndex(v => t - v.cumulativeTime);
			var a = vertexData[ind];
			var b = vertexData[ind + 1];
			var relT = (t - a.cumulativeTime) / (b.cumulativeTime - a.cumulativeTime);
			return a.LerpTo(b, relT);
		}

		public static VertexData GetPointFromDistance(this VertexData[] vertexData, float distance)
		{
			if (distance <= 0) return vertexData[0];
			if (distance >= vertexData.CurveLength()) return vertexData[vertexData.Length - 1];
			var ind = vertexData.BinarySearchIndex(v => distance - v.distance);
			var a  = vertexData[ind];
			var b = vertexData[ind + 1];
			var relDist = (distance - a.distance) / (b.distance - a.distance);
			return a.LerpTo(b, relDist);
		}

		public static VertexData[] GetSegmentVerts(this VertexData[] vertexData, int segmentInd)
		{
			var start = vertexData.GetStartAtSegmentIndex(segmentInd);
			var end = vertexData.GetStartAtSegmentIndex(segmentInd + 1);

			return null;
		}
	} 
}