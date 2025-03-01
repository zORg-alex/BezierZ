using BezierZUtility;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BezierCurveZ
{
	[Serializable]
	public class CurveConstraint
	{
		public virtual void OnCurveChanged(Curve curve) { }
		public virtual bool OnBeforeSetPositionCancel(Curve curve, int index, Vector3 position) => false;
		public virtual bool OnBeforeSetRotationCancel(Curve curve, int index, Quaternion rotation) => false;
	}

	[Serializable]
	public class FirstPointIdentityConstraint : CurveConstraint
	{
		public override void OnCurveChanged(Curve curve)
		{
			if (curve._points[0].position != Vector3.zero)
				curve._points[0] = curve._points[0].SetPosition(Vector3.zero);
			if (curve._points[0].rotation != Quaternion.identity)
				curve._points[0] = curve._points[0].SetRotation(Quaternion.identity);
			if (curve._points[1].position.x != 0 && curve._points[1].position.y != 0)
				curve._points[1] = curve._points[1].SetPosition(Vector3.forward * curve._points[1].position.z);
		}
		public override bool OnBeforeSetPositionCancel(Curve curve, int index, Vector3 position)
		{
			if (index == 1)
				curve._points[1] = curve._points[1].SetPosition(Vector3.forward * position.z);

			return index <= 1;
		}

		public override bool OnBeforeSetRotationCancel(Curve curve, int index, Quaternion rotation) => index == 0;
	}

	[Serializable]
	public class DummyConstraint : CurveConstraint
	{
		public int num;
		public string text;
		public List<string> list;
	}
}