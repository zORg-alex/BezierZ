using UnityEngine;

namespace BezierCurveZ
{
	public static class CatmullRomCurveUtility
	{
		/// <summary>
		/// Evaluate Catmull-Rom curve. Curve goes though points smoothly. Needs 4 points to evaluate. Previous and last point do not belong to a curve, but are used as handles.
		/// </summary>
		/// <param name="t">time of a point betwee p1 and p2</param>
		/// <param name="tension">.5f a smooth curve, 1 - straight lines</param>
		/// <param name="p0">Previous point</param>
		/// <param name="p1">Current point</param>
		/// <param name="p2">Next point</param>
		/// <param name="p3">Next next point</param>
		/// <returns></returns>
		public static float Evaluate(float t, float tension, float p0, float p1, float p2, float p3)
		{
			var s = tension * 2;
			var dv1 = (p2 - p0) / s;
			var dv2 = (p3 - p1) / s;

			var t2 = t * t;
			var t3 = t * t2;
			var c0 = 2 * t3 - 3 * t2 + 1;
			var c1 = t3 - 2 * t2 + t;
			var c2 = -2 * t3 + 3 * t2;
			var c3 = t3 - t2;

			return c0 * p1 + c1 * dv1 + c2 * p2 + c3 * dv2;
		}
		/// <summary>
		/// Evaluate Catmull-Rom curve. Curve goes though points smoothly. Needs 4 points to evaluate. Previous and last point do not belong to a curve, but are used as handles.
		/// </summary>
		/// <param name="t">time of a point betwee p1 and p2</param>
		/// <param name="tension">.5f a smooth curve, 1 - straight lines</param>
		/// <param name="p0">Previous point</param>
		/// <param name="p1">Current point</param>
		/// <param name="p2">Next point</param>
		/// <param name="p3">Next next point</param>
		/// <returns></returns>
		public static Vector3 Evaluate(float t, float tension, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
		{
			var s = tension * 2;
			var dv1 = (p2 - p0) / s;
			var dv2 = (p3 - p1) / s;

			var t2 = t * t;
			var t3 = t * t2;
			var c0 = 2 * t3 - 3 * t2 + 1;
			var c1 = t3 - 2 * t2 + t;
			var c2 = -2 * t3 + 3 * t2;
			var c3 = t3 - t2;

			return c0 * p1 + c1 * dv1 + c2 * p2 + c3 * dv2;
		}
	}
}