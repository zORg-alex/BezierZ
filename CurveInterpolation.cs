using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BezierCurveZ
{
	public static class CurveInterpolation
	{
		public static SplitData SplitCurveByAngleError(Curve curve, float maxAngleError, float minSplitDistance, int accuracy = 10, bool useRotations = false)
		{


			if (curve.Points.Count == 1)
				return new SplitData()
				{
					points = new List<Vector3>() { curve.Points[0], curve.Points[0] },
					tangents = new List<Vector3>() { Vector3.forward, Vector3.forward },
					segmentIndices = new List<int>() { 0 },
					cumulativeLength = new List<float>() { 0, 0 },
					cumulativeTime = new List<float>() { 0, 0 },
					rotations = new List<Quaternion> { Quaternion.identity, Quaternion.identity }
				};
			else if (curve.Points.Count == 0) return null;

			var data = new SplitData();

			var firstSegment = curve.Segments.FirstOrDefault();
			var firstTangent = CurveUtils.EvaluateDerivative(0, firstSegment).normalized;
			var _currentPoint = CurveUtils.Evaluate(0, firstSegment);
			var _lastAddedPoint = _currentPoint - firstTangent;
			var _previousAngle = 0f;
			var _nextEvalPoint = Vector3.zero;

			//Should correct for previous point estimation
			var _dist = -1f;
			var length = -1f;

			var angles = new float[curve.SegmentCount + 3];
			var rotations = new Quaternion[curve.SegmentCount + 3];
			if (!useRotations)
			{
				FillArrayForCatmullRomThing(angles, i => curve.Points[curve.GetPointIndex(i)].angle, (a, b) => a + Mathf.DeltaAngle(a, b));
			}
			else
			{
				FillArrayForCatmullRomThing(rotations, i => curve.Points[curve.GetPointIndex(i)].rotation);
			}

			void FillArrayForCatmullRomThing<T>(T[] array, Func<int, T> getter, Func<T, T, T> fixWithPreviousValue = null)
			{
				int lastSeg = curve.SegmentCount;
				array[0] = curve.IsClosed ? getter(lastSeg) : getter(0);
				array[array.Length - 3] = getter(lastSeg);
				array[array.Length - 2] = curve.IsClosed ? getter(0) : getter(lastSeg);
				array[array.Length - 1] = curve.IsClosed ? getter(1) : getter(lastSeg);
				for (int i = 0; i < lastSeg; i++)
				{
					array[i + 1] = getter(i);
				}
				//loop angles around, so that change would be minimum, since we can't set rotation past 360deg
				if (fixWithPreviousValue != null)
					for (int j = 1; j < array.Length; j++)
					{
						array[j] = fixWithPreviousValue(array[j - 1], array[j]);
					}
			}

			bool nextCPIsAutomatic = curve.Points[curve.GetPointIndex(1)].mode.HasFlag(Curve.BezierPoint.Mode.Automatic);
			var segInd = 0;
			foreach (var segment in curve.Segments)
			{
				var prevUp = curve.GetCPRotation(segInd) * Vector3.up;
				var firstIndex = data.points.Count;
				var estimatedSegmentLength = CurveUtils.EstimateSegmentLength(segment);
				int divisions = (estimatedSegmentLength * accuracy).CeilToInt();
				float increment = divisions == 1 ? 1f : 1f / divisions;
				_nextEvalPoint = CurveUtils.Evaluate(increment, segment);
				bool prevCPIsAutomatic = curve.Points[curve.GetPointIndex(segInd)].mode.HasFlag(Curve.BezierPoint.Mode.Automatic);

				int lastSegmentIndex = (curve.GetPointIndex(segInd) + 3).Min(curve.Points.Count - 1);
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
						Quaternion rotation = Quaternion.identity;
						if (!useRotations)
						{
							var approxAngle = CatmullRomCurveUtility.Evaluate(t, .5f, angles[segInd], angles[segInd + 1], angles[segInd + 2], angles[segInd + 3]);
							rotation = Quaternion.LookRotation(tang) * Quaternion.Euler(0, 0, approxAngle);
						}
						else
						{
							rotation = Quaternion.LookRotation(tang, prevUp);
							prevUp = rotation * Vector3.up;
						}

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

				if (useRotations)
				{
					//Go back and adjust angles
					var i = data.rotations.Count - 1;

					var right = curve.GetCPRotation(segInd + 1);
					var rmLast = data.rotations[data.rotations.Count - 1];
					var correction = Quaternion.Euler(0, 0, right.eulerAngles.z - rmLast.eulerAngles.z);
					while (i > firstIndex)
					{
						t = data.cumulativeTime[i] - segInd;

						var r = data.rotations[i] * Quaternion.Slerp(Quaternion.identity, correction, t);
						data.rotations[i] = r;

						i--;
					}
				}

				segInd++;
			}

			return data;
		}

		public class SplitData
		{
			public List<Vector3> points = new List<Vector3>();
			public List<Vector3> tangents = new List<Vector3>();
			public List<int> segmentIndices = new List<int>();
			public List<float> cumulativeLength = new List<float>();
			public List<float> cumulativeTime = new List<float>();
			public List<Quaternion> rotations = new List<Quaternion>();
			internal List<bool> isSharp = new List<bool>();
		}
	}
}