using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BezierCurveZ
{
	public static class CasteljauUtility
	{
		public static Vector3 GetPointFromSegment(float t, params Vector3[] points)
		{
			if (points.Length == 1) return points[0];
			else
			{
				var newPoints = new Vector3[points.Length - 1];
				for (int i = 0; i < newPoints.Length; i++)
				{
					newPoints[i] = (1 - t) * points[i] + t * points[i + 1];
				}
				return GetPointFromSegment(t, newPoints);
			}
		}

		public static Vector3[] GetSplitSegmentPoints(float t, params Vector3[] segment)
		{
			Vector3[] resultPoints = new Vector3[segment.Length * 2 - 1];


			CasteljauSplit(t, segment, resultPoints);

			return resultPoints;
		}

		static void CasteljauSplit(float t, Vector3[] points, in Vector3[] resultPoints, int pos = 0)
		{
			resultPoints[pos] = points[0];
			resultPoints[resultPoints.Length - 1 - pos] = points[points.Length - 1];
			if (points.Length == 1) return;

			var newPoints = new Vector3[points.Length - 1];
			for (int i = 0; i < newPoints.Length; i++)
			{
				newPoints[i] = (1 - t) * points[i] + t * points[i + 1];
			}
			CasteljauSplit(t, newPoints, resultPoints, pos + 1);
		}

		/// <summary>
		/// Solution taken from:
		/// <para/>
		/// https://stackoverflow.com/a/9068155/5847197
		/// </summary>
		public static Vector3[] JoinSegments(IEnumerable<Vector3[]> twoSegments)
		{
			var s = twoSegments.Take(2);
			var P = s.First();
			var Q = s.Last();

			//Hope it works just that easy... Yep it is!
			float t = (P[3] - P[2]).magnitude / (Q[1] - P[2]).magnitude;

			var newSegment = new Vector3[] {
				P[0],
				(P[1]-P[0]*(1-t))/t,
				(Q[2]-Q[3]*t)/(1-t),
				Q[3],
			};

			return newSegment;
		}
	}
}