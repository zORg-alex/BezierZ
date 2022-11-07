using Codice.CM.Common;
using System;
using UnityEngine;
using static BezierCurveZ.Curve;

namespace BezierCurveZ
{

	public struct VertexData
	{
		public Vector3 Position;
		public Quaternion Rotation;
		public Vector3 up => Rotation * Vector3.up;
		public Vector3 right => Rotation * Vector3.right;
		public Vector3 normal => Rotation * Vector3.right;
		public Vector3 forward => Rotation * Vector3.forward;
		public Vector3 tangent => Rotation * Vector3.forward;

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
				otherCurve.ControlPoints.SelectArray(p=>p.rotation),
				otherCurve.ControlPoints.SelectArray(p => !p.IsAutomatic),
				otherCurve.IsClosed,
				otherCurve.InterpolationMaxAngleError,
				otherCurve.InterpolationMinDistance,
				otherCurve.InterpolationAccuracy,
				otherCurve.IterpolationOptionsInd == InterpolationMethod.Linear,
				otherCurve.IterpolationOptionsInd == InterpolationMethod.Smooth,
				otherCurve.IterpolationOptionsInd == InterpolationMethod.CatmullRomAdditive,
				otherCurve.InterpolationCapmullRomTension);
			var vd = new VertexData[splitdata.Count];
			var segInd = 0;
			for (int i = 0; i < splitdata.Count; i++)
			{
				if (segInd + 1 < splitdata.segmentIndices.Count && splitdata.segmentIndices[segInd + 1] == i)
					segInd++;
				vd[i] = new VertexData()
				{
					Position = splitdata.points[i],
					Rotation = splitdata.rotations[i],
					distance = splitdata.cumulativeLength[i],
					cumulativeTime = splitdata.cumulativeTime[i],
					isSharp = splitdata.isSharp[i],
					segmentInd = segInd,
					segmentStartVertInd = splitdata.segmentIndices[segInd]
				};
			}

			return vd;
		}

		public override string ToString() =>
			$"VertexData {{Pos = {Position}, Eulers = {Rotation.eulerAngles}, {(isSharp ? "Shapr" : "Smooth")}, dist = {distance}, time = {cumulativeTime}, segmentInd = {segmentInd}}}";
	}

	public static class OtherVertexDataExtensions
	{
		internal static int GetStartIndex(this VertexData[] vertexData, int segmentInd) =>
			vertexData.BinarySearch(v => v.segmentInd.CompareTo(segmentInd)).segmentStartVertInd;

		public static float CurveLength(this VertexData[] vertexData) => vertexData[vertexData.Length - 1].distance;
	} 
}