using System.Collections.Generic;
using UnityEngine;

namespace BezierCurveZ
{
	public static class CurveUtils
	{
		public static Vector3 Evaluate(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
		{
			t = Mathf.Clamp01(t);
			return a0 * ((1 - t) * (1 - t) * (1 - t)) + a1 * (3 * (1 - t) * (1 - t) * t) + a2 * (3 * (1 - t) * t * t) + a3 * (t * t * t);
		}
		public static Vector3 Evaluate(float t, Vector3[] a) => Evaluate(t, a[0], a[1], a[2], a[3]);

		public static Vector3 EvaluateDerivative(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
		{
			t = Mathf.Clamp01(t);
			return (1 - t) * (1 - t) * 3 * (a1 - a0) + (1 - t) * t * 6 * (a2 - a1) + t * t * 3 * (a3 - a2);
		}
		public static Vector3 EvaluateDerivative(float t, Vector3[] a) => EvaluateDerivative(t, a[0], a[1], a[2], a[3]);

		/// <summary>
		/// Does this return the same as derivative - point?
		/// </summary>
		public static Vector3 EvaluateLocalDerivative(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
		{
			t = Mathf.Clamp01(t);
			var T = 1 - t;
			var t2 = t * t;
			var t3 = t * t * t;
			var z = (t - 4) * (t2 - 2 * t + 1) * a0 - 3 * (t3 - 5 * t2 + 5 * t - 1) * a1 + t * (3 * (t2 - 4 * t + 2) * a2 - (t - 3) * t * a3);
			var unopt = EvaluateDerivative(t, a0, a1, a2, a3) - Evaluate(t, a0, a1, a2, a3);
			return z;
		}

		public static Vector3 EvaluateSecondDerivative(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
		{
			t = Mathf.Clamp01(t);
			return (1 - t) * 2 * (a1 - a0) + t * 2 * (a2 - a1);
		}

		public static Vector3 EvaluateHackSecondDerivative(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
		{
			t = Mathf.Clamp01(t);
			//snatched from Sebastian Lague
			return (1 - t) * 6 * (a2 - 2 * a1 + a0) + t * 6 * (a3 - 2 * a2 + a1);
		}

		public static Vector3 EvaluateFrenetNormal(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3) =>
			EvaluateFrenetNormal(t, a0, a1, a2, a3, out _);

		public static Vector3 EvaluateFrenetNormal(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3, out Vector3 tangent)
		{
			tangent = EvaluateDerivative(t, a0, a1, a2, a3);
			var b = (tangent + EvaluateSecondDerivative(t, a0, a1, a2, a3));
			var r = Vector3.Cross(tangent, b);
			return Vector3.Cross(r, tangent).normalized;
		}

		public static Vector3 EvaluateHackNormal(float t, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3, out Vector3 tangent)
		{
			tangent = EvaluateDerivative(t, a0, a1, a2, a3);
			var b = EvaluateHackSecondDerivative(t, a0, a1, a2, a3);
			var r = Vector3.Cross(tangent, b);
			return Vector3.Cross(r, tangent).normalized;
		}

		public static IEnumerable<Quaternion> GetRMFrames(Quaternion firstRotation, int steps, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
		{
			var stepTime = 1f / steps;
			var currentRotation = firstRotation;

			for (int i = 1; i <= steps; i++)
			{
				var midTime = (i - .5f) * stepTime;
				var localMidTangent = (EvaluateDerivative(midTime, a0, a1, a2, a3) - Evaluate(midTime, a0, a1, a2, a3)).normalized;
				var nextLocalTangent = -Vector3.Reflect(currentRotation * Vector3.forward, localMidTangent);
				var nextUp = Vector3.Reflect(currentRotation * Vector3.up, localMidTangent);
				var nextRotation = Quaternion.LookRotation(nextLocalTangent, nextUp);

				yield return nextRotation;
				currentRotation = nextRotation;
			}
		}
		public static float EstimateSegmentLength(Vector3[] s) =>
			(s[0] - s[3]).magnitude + ((s[0] - s[1]).magnitude + (s[1] - s[2]).magnitude + (s[2] - s[3]).magnitude) / 2f;

		public static Curve OffsetCurve(this Curve curve, Vector3 Offset)
		{
			var ocurve = new Curve();
			var pind = 0;
			for (int i = 0, oi = 0; i < curve.SegmentCount; i++, oi++)
			{
				//var closestT = curve.GetClosestPointTimeSegment(, out var closestSegInd);
				var cSeg = curve.Segments[0];
				var pointA = curve.Points[pind++];
				var handleA = curve.Points[pind++];
				var handleB = curve.Points[pind++];
				var pointB = curve.Points[pind++];
				var oPosA = pointA.position + pointA.rotation * Offset;
				var oPosB = pointB.position + pointB.rotation * Offset;
				var aForward = pointA.forward;
				var bForward = pointB.forward;
				var posAh = oPosA + aForward * Vector3.Dot(pointA.forward, handleA - pointA);
				var posBh = oPosB + bForward * Vector3.Dot(pointB.forward, handleB - pointB);

				var segVerts = curve.VertexData.GetSegmentVerts(i);

				ocurve.SetSegment(oi, oPosA, posAh, posBh, oPosB, pointA.rotation, pointB.rotation, pointA.scale, pointB.scale, pointA.mode, pointB.mode);
			}

			return ocurve;
		}
	}
}