using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BezierCurveZ
{
	public static class CurveInterpolation
	{
		public static float MinSplitDistance = 0.000001f;
		public static SplitData SplitCurveByAngleError(ICurve curve, float maxAngleError, float minSplitDistance, int accuracy = 10)
		{
			if (curve.ControlPointCount == 1)
				return new SplitData()
				{
					points = new List<Vector3>() { curve.PointPositions[0], curve.PointPositions[0] },
					tangents = new List<Vector3>() { curve.PointRotations[0] * Vector3.forward, curve.PointRotations[0] * Vector3.forward },
					segmentIndices = new List<int>() { 0 },
					cumulativeLength = new List<float>() { 0, 0 },
					cumulativeTime = new List<float>() { 0, 0 },
					rotations = new List<Quaternion> { curve.PointRotations[0], curve.PointRotations[0] }
				};
			else if (curve.PointCount == 0) return null;

			var data = new SplitData();

			var firstSegment = curve.Segments.FirstOrDefault();
			var firstTangent = CurveUtils.EvaluateDerivative(0, firstSegment).normalized;
			var _currentPoint = CurveUtils.Evaluate(0, firstSegment);
			var _lastAddedPoint = _currentPoint - firstTangent;
			var _previousAngle = 0f;

			//Should correct for previous point estimation
			var _dist = -1f;
			var length = -1f;
			bool nextCPIsAutomatic = curve.IsAutomaticHandle(curve.GetPointIndex(1));
			var segInd = 0;
			foreach (var segment in curve.Segments)
			{
				var prevUp = curve.GetCPRotation(segInd) * Vector3.up;//curve.PointRotations[curve.GetPointIndex(segInd)] * Vector3.up;
				var firstIndex = data.points.Count;
				var estimatedSegmentLength = CurveUtils.EstimateSegmentLength(segment);
				int divisions = (estimatedSegmentLength * accuracy).CeilToInt();
				float increment = divisions == 1 ? 1f : 1f / divisions;
				Vector3 _nextEvalPoint = CurveUtils.Evaluate(increment, segment);
				bool prevCPIsAutomatic = curve.IsAutomaticHandle(curve.GetPointIndex(curve.GetPointIndex(segInd)));

				int lastSegmentIndex = (curve.GetPointIndex(segInd) + 3).Min(curve.PointCount - 1);
				Vector3 finalTangent = CurveUtils.EvaluateDerivative(1, segment).normalized;

				float t = 0f;
				while (true)
				{
					var _edgePoint = (t == 0) || (t >= 1 && segInd == curve.SegmentCount - 1);
					var _isSharp = (t == 0 && !prevCPIsAutomatic) || (t >= 1 && !nextCPIsAutomatic);

					Vector3 _toLastPoint = _lastAddedPoint - _currentPoint;
					var _toLastPointMag = _toLastPoint.magnitude;
					float _angle = 180 - Vector3.Angle(_toLastPoint, _nextEvalPoint - _currentPoint);
					float _angleError = _angle.Max(_previousAngle);

					if (_isSharp || (_edgePoint && _lastAddedPoint != _currentPoint) || _angleError > maxAngleError && _dist >= minSplitDistance)
					{
						length += _toLastPointMag;
						Vector3 tang = CurveUtils.EvaluateDerivative(t, segment).normalized;
						Quaternion rotation = Quaternion.LookRotation(tang, prevUp);
						prevUp = rotation * Vector3.up;

						data.points.Add(_currentPoint);
						data.tangents.Add(tang);
						data.cumulativeTime.Add(segInd + t);
						data.cumulativeLength.Add(length);
						if (data.segmentIndices.Count == segInd)
							data.segmentIndices.Add(data.points.Count - 1);
						data.rotations.Add(rotation);
						data.isSharp.Add(_isSharp || (segInd == 0 && t == 0) || (segInd == curve.SegmentCount - 1 && t == 1));
						_dist = 0;
						_lastAddedPoint = _currentPoint;
					}
					else _dist += (_currentPoint - _nextEvalPoint).magnitude;

					if (t >= 1f) break;
					t = (t + increment).Min(1f);
					_currentPoint = _nextEvalPoint;
					_nextEvalPoint = CurveUtils.Evaluate(t + increment, segment);
					_previousAngle = _angle;
					nextCPIsAutomatic = prevCPIsAutomatic;
				}

				var i = data.rotations.Count - 1;

				var right = curve.GetCPRotation(segInd + 1);
				var rmLast = data.rotations[data.rotations.Count - 1];
				var correction = rmLast.Inverted() * right;
				while (i > firstIndex)
				{
					t = data.cumulativeTime[i] - segInd;

					var r = data.rotations[i] * Quaternion.Slerp(Quaternion.identity, correction, t);
					data.rotations[i] = r;

					i--;
				}

				segInd++;
			}
			return data;
		}

		public class SplitData
		{
			public List<Vector3> points = new List<Vector3>();
			public List<Vector3> tangents = new List<Vector3>();
			/// <summary>
			/// contains splitdata first indexes for each segment. It is shorter than rest.
			/// </summary>
			public List<int> segmentIndices = new List<int>();
			public List<float> cumulativeLength = new List<float>();
			public List<float> cumulativeTime = new List<float>();
			public List<Quaternion> rotations = new List<Quaternion>();
			internal List<bool> isSharp = new List<bool>();
			public int Count => points.Count;
		}
	}
}