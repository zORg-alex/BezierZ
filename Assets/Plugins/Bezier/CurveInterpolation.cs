using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BezierCurveZ
{
	public static class CurveInterpolation {
		public static SplitData SplitCurveByAngleError(Curve curve, float maxAngleError, float minSplitDistance, int accuracy = 10)
		{
			if (curve.Points.Count == 1)
				return new SplitData()
				{
					points = new List<Vector3>() { curve.Points[0], curve.Points[0] },
					tangents = new List<Vector3>() { Vector3.forward, Vector3.forward },
					indices = new List<int>() { 0, 0 },
					cumulativeLength = new List<float>() { 0, 0 },
					segmentTime = new List<float>() { 0, 0 }
				};
			else if (curve.Points.Count == 0) return null;

			var data = new SplitData();

			var firstSegment = curve.Segments.FirstOrDefault();
			var firstTangent = CurveUtils.EvaluateDerivative(0, firstSegment).normalized;
			var currentPoint = CurveUtils.Evaluate(0, firstSegment);
			var previousPoint = currentPoint - firstTangent;
			var previousAngle = 0f;

			//Should correct for previous point estimation
			var dist = -1f;
			var length = -1f;
			Vector3 nextPoint = Vector3.zero;

			//data.points.Add(currentPoint);
			//data.tangents.Add(firstTangent);
			//data.cumulativeLength.Add(length);
			//data.segmentTime.Add(0);
			//data.indices.Add(0);

			var i = 0;
			foreach (var segment in curve.Segments)
			{
				var estimatedSegmentLength = CurveUtils.EstimateSegmentLength(segment);
				int divisions = (estimatedSegmentLength * accuracy).CeilToInt();
				float increment = 1f / divisions;
				if (i == 0)
					nextPoint = CurveUtils.Evaluate(increment, segment);

				float t = 0f;
				while (true)
				{
					var edgePoint = (t == 0) || (t >= 1 && i == curve.SegmentCount - 1);

					Vector3 toLastPoint = previousPoint - currentPoint;
					var toLastPointMag = toLastPoint.magnitude;
					length += toLastPointMag;
					float angle = 180 - Vector3.Angle(toLastPoint, nextPoint - currentPoint);
					float angleError = angle.Max(previousAngle);

					if (edgePoint || angleError > maxAngleError && dist >= minSplitDistance)
					{
						data.points.Add(currentPoint);
						data.tangents.Add(CurveUtils.EvaluateDerivative(t, segment).normalized);
						data.segmentTime.Add(t);
						data.cumulativeLength.Add(length);
						data.indices.Add(i);
						dist = 0;
						previousPoint = currentPoint;
					}
					else dist += toLastPointMag;

					if (t >= 1f) break;
					t = (t + increment).Min(1f);
					currentPoint = nextPoint;
					nextPoint = CurveUtils.Evaluate(t + increment, segment);
					previousAngle = angle;
				}

				i++;
			}
			return data;
		}

		public static Quaternion[] GetRotations(SplitData data)
		{
			if (data.points.Count == 0) return new Quaternion[] { Quaternion.identity };
			var up = Vector3.up;
			var r = new Quaternion[data.points.Count];
			//var point = data.points[0];
			//var previousPoint = point;
			var tangent = data.tangents[0];
			r[0] = Quaternion.LookRotation(tangent, up);
			var lastRot = r[0];

			for (int i = 1; i < data.points.Count; i++)
			{
				//point = data.points[i];
				tangent = data.tangents[i];

				r[i] = Quaternion.LookRotation(tangent, lastRot * Vector3.up);
				lastRot = r[i];

				//previousPoint = point;
			}

			return r;
		}

		public class SplitData
		{
			public List<Vector3> points = new List<Vector3>();
			public List<Vector3> tangents = new List<Vector3>();
			public List<int> indices = new List<int>();
			public List<float> cumulativeLength = new List<float>();
			public List<float> segmentTime = new List<float>();
		}
	}
}