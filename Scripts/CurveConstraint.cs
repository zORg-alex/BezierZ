using BezierZUtility;
using System;
using UnityEngine;

namespace BezierCurveZ
{
	[Serializable]
	public abstract class CurveConstraint
	{
		public abstract void OnCurveChanged(Curve curve);
	}

	[Serializable]
	public class FirstPointIdentityConstraint : CurveConstraint
	{
		public override void OnCurveChanged(Curve curve)
		{
			curve._points[0] = curve._points[0].SetRotation(Quaternion.identity);
			curve._points[1] = curve._points[1].SetPosition(Vector3.forward.MultiplyComponentwise(curve._points[1].position));
		}
	}
}