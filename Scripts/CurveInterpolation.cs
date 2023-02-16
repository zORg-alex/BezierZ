using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BezierCurveZ
{
	public enum InterpolationMethod { RotationMinimization = 0, Linear = 1, Smooth = 2, CatmullRomAdditive = 3 }
	public static class CurveInterpolation
	{
		public static float MinSplitDistance = 0.000001f;
		public static SplitData SplitCurveByAngleError(
			Vector3[][] segments, Quaternion[] endPointRotations, Vector3[] endPointScales, bool[] endPointIsSharp,
			bool IsClosed, float maxAngleError, float minSplitDistance, int accuracy = 10,
			InterpolationMethod interpolation = InterpolationMethod.CatmullRomAdditive, float catmullRomTension = 1f)
		{
			if (segments.Length == 0) return new SplitData();

			var data = new SplitData();

			var firstSegment = segments[0];
			var firstTangent = CurveUtils.EvaluateDerivative(0, firstSegment).normalized;
			var _currentPoint = CurveUtils.Evaluate(0, firstSegment);
			var _lastAddedPoint = _currentPoint - firstTangent;
			var _previousAngle = 0f;

			var rotationsCR = new Quaternion[endPointRotations.Length + 2];
			var relrotationCR = new Quaternion[endPointRotations.Length + 2];
			var relAnglesCR = new float[endPointRotations.Length + 2];

			var scalesCR = new Vector3[endPointScales.Length + 2];
			if (interpolation == InterpolationMethod.CatmullRomAdditive)
			{
				rotationsCR[0] = IsClosed ? endPointRotations[endPointRotations.Length - 2] : endPointRotations[0];
				rotationsCR[rotationsCR.Length - 1] = IsClosed ? endPointRotations[1] : endPointRotations[endPointRotations.Length - 1];
				scalesCR[0] = IsClosed ? endPointScales[endPointScales.Length - 2] : endPointScales[0];
				scalesCR[scalesCR.Length - 1] = IsClosed ? endPointScales[1] : endPointScales[endPointScales.Length - 1];
				for (int i = 0; i < endPointRotations.Length; i++)
				{
					rotationsCR[i + 1] = endPointRotations[i];
					scalesCR[i + 1] = endPointScales[i];
				}
			}

			//Should correct for previous point estimation
			var _dist = -1f;
			var length = -1f;
			bool nextEPIsAutomatic = endPointIsSharp[0];
			var segInd = 0;
			var prevUp = endPointRotations[segInd] * Vector3.up;
			foreach (var segment in segments)
			{
				if (InterpolationIsLinearOrSmooth())
					prevUp = endPointRotations[segInd] * Vector3.up;
				var prevTang = endPointRotations[segInd] * Vector3.forward;
				var firstIndex = data.points.Count;
				var estimatedSegmentLength = CurveUtils.EstimateSegmentLength(segment);
				int divisions = (estimatedSegmentLength * accuracy).CeilToInt();
				float increment = Mathf.Max(divisions > 0.00000001f ? 1f / divisions : float.MaxValue, 0.00001f);
				Vector3 _nextEvalPoint = CurveUtils.Evaluate(increment, segment);
				float _rollAngle = 0f;
				float _rollIncrement = segInd < endPointRotations.Length - 1 ? ((endPointRotations[segInd].eulerAngles.z - endPointRotations[segInd + 1].eulerAngles.z) / divisions).Abs() : 0f;
				bool prevEPIsAutomatic = endPointIsSharp[segInd];

				float t = 0f;
				while (true)
				{
					var _edgePoint = (t == 0) || (t >= 1 && segInd == segments.Length - 1);
					var _isSharp = (t == 0 && !prevEPIsAutomatic) || (t >= 1 && !nextEPIsAutomatic);

					Vector3 _toLastPoint = _lastAddedPoint - _currentPoint;
					var _toLastPointMag = _toLastPoint.magnitude;
					float _angle = 180 - Vector3.Angle(_toLastPoint, _nextEvalPoint - _currentPoint);
					float _angleError = _angle.Max(_previousAngle);
					if (segInd < endPointRotations.Length - 1)
						_rollAngle += _rollIncrement;

					if (_isSharp || (_edgePoint && _lastAddedPoint != _currentPoint)
						|| (_angleError > maxAngleError || _rollAngle > maxAngleError)
						&& _dist >= minSplitDistance)
					{
						_rollAngle = 0;
						length += _toLastPointMag;
						Vector3 tang = CurveUtils.EvaluateDerivative(t, segment).normalized;
						if (tang == Vector3.zero) tang = prevTang;
						Quaternion rotation = Quaternion.LookRotation(tang, prevUp);
						prevUp = rotation * Vector3.up;

						data.points.Add(_currentPoint);
						data.tangents.Add(tang);
						data.cumulativeTime.Add(segInd + t);
						data.cumulativeLength.Add(length);
						if (data.segmentIndices.Count == segInd)
							data.segmentIndices.Add(data.points.Count - 1);
						data.rotations.Add(rotation);
						data.scales.Add(GetScale(segInd, t));
						data.isSharp.Add(_isSharp || (segInd == 0 && t == 0) || (segInd == segments.Length - 1 && t == 1));
						_dist = 0;
						_lastAddedPoint = _currentPoint;
						prevTang = tang;
					}
					else _dist += (_currentPoint - _nextEvalPoint).magnitude;

					if (t >= 1f) break;
					t = (t + increment).Min(1f);
					_currentPoint = _nextEvalPoint;
					_nextEvalPoint = CurveUtils.Evaluate(t + increment, segment);
					_previousAngle = _angle;
					nextEPIsAutomatic = prevEPIsAutomatic;
				}

				if (interpolation == InterpolationMethod.CatmullRomAdditive)
				{
					Quaternion rotDiff = Quaternion.LookRotation(endPointRotations[segInd + 1] * Vector3.forward, prevUp).Inverted() * endPointRotations[segInd + 1]; //(rotation.Inverted() * epRotations[segInd + 1]).Inverted();
					relrotationCR[(segInd + 2) % relrotationCR.Length] = rotDiff;
					relAnglesCR[(segInd + 2) % relAnglesCR.Length] = rotDiff.eulerAngles.z;
				}

				if (InterpolationIsLinearOrSmooth())
				{
					var i = data.rotations.Count - 1;

					var right = endPointRotations[(segInd + 1) % endPointRotations.Length];
					var rmLast = data.rotations[data.rotations.Count - 1];
					var correction = (rmLast.Inverted() * right).normalized;
					while (i > firstIndex)
					{
						t = data.cumulativeTime[i] - segInd;
						if (interpolation is InterpolationMethod.Smooth)
							data.rotations[i] = data.rotations[i] * Quaternion.Slerp(Quaternion.identity, correction, t.SmoothStep());
						if (interpolation is InterpolationMethod.Linear)
							data.rotations[i] = data.rotations[i] * Quaternion.Slerp(Quaternion.identity, correction, t);

						i--;
					}
				}

				segInd++;
			}

			if (interpolation == InterpolationMethod.CatmullRomAdditive)
			{
				relrotationCR[0] = IsClosed ? allignedSecondToLastRotation().Inverted() * secondToLastEPRotation() : Quaternion.identity;
				relrotationCR[1] = Quaternion.identity;
				relrotationCR[relrotationCR.Length - 1] = IsClosed ? allignedSecondRoation().Inverted() * secondEPRotation() : relrotationCR[relrotationCR.Length - 2];
				relAnglesCR[0] = (IsClosed ? allignedSecondToLastRotation().Inverted() * secondToLastEPRotation() : Quaternion.identity).eulerAngles.z;
				relAnglesCR[1] = 0;
				relAnglesCR[relrotationCR.Length - 1] = (IsClosed ? secondRotation().normalized.Inverted() * secondEPRotation().normalized : relrotationCR[relrotationCR.Length - 2]).eulerAngles.z;
				for (int i = 1; i < relAnglesCR.Length - 1; i++)
				{
					relAnglesCR[(i + 1) % relAnglesCR.Length] = relAnglesCR[i] + Mathf.DeltaAngle(relAnglesCR[i], relAnglesCR[(i + 1) % relAnglesCR.Length]);
				}
				var rotations = new Quaternion[data.rotations.Count];
				{
					int i = 0;
					foreach (var r in data.rotations)
					{
						segInd = Mathf.Min(data.cumulativeTime[i].FloorToInt(), segments.Length - 1);
						rotations[i] = r * Quaternion.Euler(0, 0,
							CatmullRomCurveUtility.Evaluate(data.cumulativeTime[i] - segInd, catmullRomTension,
							relAnglesCR[segInd], relAnglesCR[segInd + 1], relAnglesCR[segInd + 2], relAnglesCR[segInd + 3]));
						i++;
					}
					data.rotations = new List<Quaternion>(rotations);
				}
			}

			return data;

			//Local methods

			Quaternion secondToLastRotation() => data.rotations[data.segmentIndices[data.segmentIndices.Count - 2]];
			Quaternion secondToLastEPRotation() => endPointRotations[endPointRotations.Length - 2];
			Quaternion secondRotation() => data.rotations[data.segmentIndices[1]];
			Quaternion secondEPRotation() => endPointRotations[1];
			Quaternion allignedSecondToLastRotation() => Quaternion.LookRotation(secondToLastEPRotation() * Vector3.forward, secondToLastRotation() * Vector3.up);
			Quaternion allignedSecondRoation() => Quaternion.LookRotation(secondEPRotation() * Vector3.forward, secondRotation() * Vector3.up);

			bool InterpolationIsLinearOrSmooth() =>
				interpolation is InterpolationMethod.Linear or InterpolationMethod.Smooth;

			Vector3 GetScale(int segInd, float t)
			{
				if (interpolation is InterpolationMethod.CatmullRomAdditive)
				{
					return CatmullRomCurveUtility.Evaluate(t, catmullRomTension,
							scalesCR[segInd], scalesCR[segInd + 1], scalesCR[segInd + 2], scalesCR[segInd + 3]);
				}
				else if (interpolation is InterpolationMethod.Linear)
				{
					return Vector3.Lerp(endPointScales[segInd], endPointScales[segInd + 1], t);
				}
				else
				{
					return Vector3.Lerp(endPointScales[segInd], endPointScales[segInd + 1], t.SmoothStep());
				}
			}
		}


		private static Quaternion GetRotation(Vector3 tangent, Vector3 firstTangent, Vector3 lastTangent, Quaternion[] rotations)
		{
			var t = Vector3.Angle(tangent, firstTangent)/Vector3.Angle(firstTangent,lastTangent);
			return CatmullRomCurveUtility.EvaluateAngleAxis(t, 1, rotations[0], rotations[1], rotations[2], rotations[3]);
		}

		public class SplitData
		{
			public List<Vector3> points = new List<Vector3>();
			public List<Vector3> tangents = new List<Vector3>();
			/// <summary>
			/// contains splitdata first indexes for each segment. It is shorter than rest. It's length is same as EndPoint Count
			/// </summary>
			public List<int> segmentIndices = new List<int>();
			public List<float> cumulativeLength = new List<float>();
			public List<float> cumulativeTime = new List<float>();
			public List<Quaternion> rotations = new List<Quaternion>();
			public List<Vector3> scales = new List<Vector3>();
			internal List<bool> isSharp = new List<bool>();

			public int Count => points.Count;
		}
	}
}