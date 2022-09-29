using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BezierCurveZ
{
	public static class CurveInterpolation
	{
		public static float MinSplitDistance = 0.000001f;
		public static SplitData SplitCurveByAngleError(Vector3[][] segments, Quaternion[] cpRotations, bool[] cpIsSharp, bool IsClosed, float maxAngleError, float minSplitDistance, int accuracy = 10, bool useLinear = false, bool useSmooth = false, bool useCR = true)
		{
			if (segments.Length == 0) return null;

			var data = new SplitData();

			var firstSegment = segments[0];
			var firstTangent = CurveUtils.EvaluateDerivative(0, firstSegment).normalized;
			var _currentPoint = CurveUtils.Evaluate(0, firstSegment);
			var _lastAddedPoint = _currentPoint - firstTangent;
			var _previousAngle = 0f;

			var rotationsCR = new Quaternion[cpRotations.Length + 2];
			var relrotationCR = new Quaternion[cpRotations.Length + 2];
			var relAnglesCR = new float[cpRotations.Length + 2];
			if (useCR)
			{
				rotationsCR[0] = IsClosed ? cpRotations[cpRotations.Length - 2] : cpRotations[0];
				rotationsCR[rotationsCR.Length - 1] = IsClosed ? cpRotations[1] : cpRotations[cpRotations.Length - 1];
				for (int i = 0; i < cpRotations.Length; i++)
				{
					rotationsCR[i + 1] = cpRotations[i];
				}
			}

			//Should correct for previous point estimation
			var _dist = -1f;
			var length = -1f;
			bool nextCPIsAutomatic = cpIsSharp[0];
			var segInd = 0;
			var prevUp = cpRotations[segInd] * Vector3.up;
			foreach (var segment in segments)
			{
				if (useLinear || useSmooth)
					prevUp = cpRotations[segInd] * Vector3.up;
				var prevTang = cpRotations[segInd] * Vector3.forward;
				var firstIndex = data.points.Count;
				var estimatedSegmentLength = CurveUtils.EstimateSegmentLength(segment);
				int divisions = (estimatedSegmentLength * accuracy).CeilToInt();
				float increment = divisions == 1 ? 1f : 1f / divisions;
				Vector3 _nextEvalPoint = CurveUtils.Evaluate(increment, segment);
				float _rollAngle = 0f;
					float _rollIncrement = segInd < cpRotations.Length - 1 ? ((cpRotations[segInd].eulerAngles.z - cpRotations[segInd + 1].eulerAngles.z) / divisions).Abs() : 0f;
				bool prevCPIsAutomatic = cpIsSharp[segInd];

				float t = 0f;
				while (true)
				{
					var _edgePoint = (t == 0) || (t >= 1 && segInd == segments.Length - 1);
					var _isSharp = (t == 0 && !prevCPIsAutomatic) || (t >= 1 && !nextCPIsAutomatic);

					Vector3 _toLastPoint = _lastAddedPoint - _currentPoint;
					var _toLastPointMag = _toLastPoint.magnitude;
					float _angle = 180 - Vector3.Angle(_toLastPoint, _nextEvalPoint - _currentPoint);
					float _angleError = _angle.Max(_previousAngle);
					if (segInd < cpRotations.Length - 1)
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
					nextCPIsAutomatic = prevCPIsAutomatic;
				}

				if (useCR && segInd < cpRotations.Length - 1)
				{
					Quaternion diff = Quaternion.LookRotation(cpRotations[segInd + 1] * Vector3.forward, prevUp).Inverted() * cpRotations[segInd + 1]; //(rotation.Inverted() * cpRotations[segInd + 1]).Inverted();
					relrotationCR[segInd + 2] = diff;
					relAnglesCR[segInd + 2] = diff.eulerAngles.z;
				}

				if (useLinear || useSmooth)
				{
					var i = data.rotations.Count - 1;

					var right = cpRotations[(segInd + 1) % cpRotations.Length];
					var rmLast = data.rotations[data.rotations.Count - 1];
					var correction = (rmLast.Inverted() * right).normalized;
					while (i > firstIndex)
					{
						t = data.cumulativeTime[i] - segInd;
						if (useSmooth)
							data.rotations[i] = data.rotations[i] * Quaternion.Slerp(Quaternion.identity, correction, t.SmoothStep());
						if (useLinear)
							data.rotations[i] = data.rotations[i] * Quaternion.Slerp(Quaternion.identity, correction, t);

						i--;
					}
				}

				segInd++;
			}

			Quaternion secondToLastRotation() => data.rotations[data.segmentIndices[data.segmentIndices.Count - 2]];
			Quaternion secondToLastCPRotation() => cpRotations[cpRotations.Length - 2];
			Quaternion secondRotation() => data.rotations[data.segmentIndices[1]];
			Quaternion secondCPRotation() => cpRotations[1];
			Quaternion allignedSecondToLastRotation() => Quaternion.LookRotation(secondToLastCPRotation() * Vector3.forward, secondToLastRotation() * Vector3.up);
			Quaternion allignedSecondRoation() => Quaternion.LookRotation(secondCPRotation() * Vector3.forward, secondRotation() * Vector3.up);

			if (useCR)
			{
				relrotationCR[0] = IsClosed ? allignedSecondToLastRotation().Inverted() * secondToLastCPRotation() : Quaternion.identity;
				relrotationCR[1] = Quaternion.identity;
				relrotationCR[relrotationCR.Length - 1] = IsClosed ? allignedSecondRoation().Inverted() * secondCPRotation() : relrotationCR[relrotationCR.Length - 2];
				relAnglesCR[0] = (IsClosed ? allignedSecondToLastRotation().Inverted() * secondToLastCPRotation() : Quaternion.identity).eulerAngles.z;
				relAnglesCR[1] = 0;
				relAnglesCR[relrotationCR.Length - 1] = (IsClosed ? allignedSecondRoation().Inverted() * secondCPRotation() : relrotationCR[relrotationCR.Length - 2]).eulerAngles.z;
				for (int i = 0; i < relAnglesCR.Length - 1; i++)
				{
					relAnglesCR[(i + 1) % relAnglesCR.Length] = relAnglesCR[i] + Mathf.DeltaAngle(relAnglesCR[i], relAnglesCR[(i + 1) % relAnglesCR.Length]);
				}
				var rotations = new Quaternion[data.rotations.Count];
				{
					int i = 0;
					foreach (var r in data.rotations)
					{
						segInd = data.cumulativeTime[i].FloorToInt();
						if (segInd < cpRotations.Length - 1)
							rotations[i] = r * Quaternion.Euler(0, 0,
								CatmullRomCurveUtility.Evaluate(data.cumulativeTime[i] - segInd, 1f,
								relAnglesCR[segInd], relAnglesCR[segInd + 1], relAnglesCR[segInd + 2], relAnglesCR[segInd + 3]));
						else
							rotations[i] = rotations[i - 1];
						i++;
					}
					data.rotations = new List<Quaternion>(rotations);
				}
			}

			return data;
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