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
		public bool LockFirstPoint = true;
		public override void OnCurveChanged(Curve curve)
		{
			Point point0 = curve._points[0];
			if (LockFirstPoint && point0.position != Vector3.zero)
				curve._points[0] = point0.SetPosition(Vector3.zero);
			if (LockFirstPoint && point0.rotation != Quaternion.identity)
				curve._points[0] = point0.SetRotation(Quaternion.identity);
			Point point1 = curve._points[1];
			if (point1.position.normalized != point0.forward)
				curve._points[1] = curve._points[1].SetPosition(point0.position + point0.forward * (point1.position- point0.position).magnitude);
			if (point1.rotation != point0.rotation)
				curve._points[1] = curve._points[1].SetRotation(point0.rotation);
		}
		public override bool OnBeforeSetPositionCancel(Curve curve, int index, Vector3 position)
		{
			if (index == 1)
				curve._points[1] = curve._points[1].SetPosition(curve._points[0].position + curve._points[0].forward * (position - curve._points[0].position).magnitude);

			curve.BumpVersion();

			return index == 1 || (LockFirstPoint && index == 0);
		}

		public override bool OnBeforeSetRotationCancel(Curve curve, int index, Quaternion rotation) => LockFirstPoint && index == 0;
	}

	[Serializable]
	public class DummyConstraint : CurveConstraint
	{
		public int num;
		public string text;
		public List<string> list;
	}
}