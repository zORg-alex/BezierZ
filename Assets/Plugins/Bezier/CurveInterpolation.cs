using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BezierCurveZ
{
	public static class CurveInterpolation
	{
		public static SplitData SplitCurveByAngleError(Curve curve, float maxAngleError, float minSplitDistance, int accuracy = 10, bool useRotationMinimisation = false)
		{
			if (curve.Points.Count == 1)
				return new SplitData()
				{
					points = new List<Vector3>() { curve.Points[0], curve.Points[0] },
					tangents = new List<Vector3>() { Vector3.forward, Vector3.forward },
					segmentIndices = new List<int>() { 0, 0 },
					cumulativeLength = new List<float>() { 0, 0 },
					segmentTime = new List<float>() { 0, 0 },
					rotations = new List<Quaternion> { Quaternion.identity, Quaternion.identity }
				};
			else if (curve.Points.Count == 0) return null;

			var data = new SplitData();

			var firstSegment = curve.Segments.FirstOrDefault();
			var firstTangent = CurveUtils.EvaluateDerivative(0, firstSegment).normalized;
			var _currentPoint = CurveUtils.Evaluate(0, firstSegment);
			var _previousPoint = _currentPoint - firstTangent;
			var _previousAngle = 0f;
			var _nextPoint = Vector3.zero;

			//Should correct for previous point estimation
			var _dist = -1f;
			var length = -1f;

			var angles = new float[curve.ControlPointCount + 2];
			angles[0] = curve.IsClosed ? curve.Points[curve.lastPointInd].angle : curve.Points[0].angle;
			angles[angles.Length - 2] = curve.Points[curve.lastPointInd].angle;
			angles[angles.Length - 2] = curve.IsClosed ? curve.Points[0].angle : curve.Points[curve.lastPointInd].angle;
			var i = 0;
			foreach (var segment in curve.Segments)
			{
				angles[i + 1] = curve.Points[curve.GetPointIndex(i)].angle;
				var d = Mathf.DeltaAngle(angles[i], angles[i + 1]);
				angles[i + 1] = angles[i] + d;
				i++;
			}
			//loop angles around, so that change would be minimum, since we can't set rotation past 360deg
			for (int j = 1; j < angles.Length; j++)
			{
				var d = Mathf.DeltaAngle(angles[j - 1], angles[j]);
				angles[j] = angles[j - 1] + d;
			}

			var prevUp = Vector3.up;

			var k = 0;
			foreach (var segment in curve.Segments)
			{
				var estimatedSegmentLength = CurveUtils.EstimateSegmentLength(segment);
				int divisions = (estimatedSegmentLength * accuracy).CeilToInt();
				float increment = 1f / divisions;
				if (k == 0)
					_nextPoint = CurveUtils.Evaluate(increment, segment);

				int lastSegmentIndex = (curve.GetPointIndex(k) + 3).Min(curve.Points.Count - 1);
				Vector3 finalTangent = CurveUtils.EvaluateDerivative(1, segment).normalized;

				float t = 0f;
				while (true)
				{
					var _edgePoint = (t == 0) || (t >= 1 && k == curve.SegmentCount - 1);

					Vector3 _toLastPoint = _previousPoint - _currentPoint;
					var _toLastPointMag = _toLastPoint.magnitude;
					length += _toLastPointMag;
					float _angle = 180 - Vector3.Angle(_toLastPoint, _nextPoint - _currentPoint);
					float _angleError = _angle.Max(_previousAngle);

					if (_edgePoint || _angleError > maxAngleError && _dist >= minSplitDistance)
					{
						Vector3 tang = CurveUtils.EvaluateDerivative(t, segment).normalized;
						var approxAngle = CatmullRomCurveUtility.Evaluate(t, .5f, angles[k], angles[k + 1], angles[k + 2], angles[k + 3]);
						Quaternion rotation;
						if (!useRotationMinimisation)
						{
							rotation = Quaternion.LookRotation(tang) * Quaternion.Euler(0, 0, approxAngle);
						}
						else
						{
							rotation = Quaternion.LookRotation(tang, prevUp) * Quaternion.Euler(0, 0, approxAngle);
							prevUp = rotation * Vector3.up;
						}

						data.points.Add(_currentPoint);
						data.tangents.Add(tang);
						data.segmentTime.Add(t);
						data.cumulativeLength.Add(length);
						data.segmentIndices.Add(k);
						data.rotations.Add(rotation);
						_dist = 0;
						_previousPoint = _currentPoint;
					}
					else _dist += _toLastPointMag;

					if (t >= 1f) break;
					t = (t + increment).Min(1f);
					_currentPoint = _nextPoint;
					_nextPoint = CurveUtils.Evaluate(t + increment, segment);
					_previousAngle = _angle;
				}

				k++;
			}

			return data;
		}

		public class SplitData
		{
			public List<Vector3> points = new List<Vector3>();
			public List<Vector3> tangents = new List<Vector3>();
			public List<int> segmentIndices = new List<int>();
			public List<float> cumulativeLength = new List<float>();
			public List<float> segmentTime = new List<float>();
			public List<Quaternion> rotations = new List<Quaternion>();
		}
	}
}