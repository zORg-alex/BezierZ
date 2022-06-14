using System;
using System.Collections.Generic;
using UnityEngine;

namespace BezierCurveZ
{
	public partial class Curve
	{
		[Serializable]
		public struct BezierPoint
		{
			public override string ToString()
			{
				return $"{point} {type} {mode}";
			}
			public Vector3 point;
			public static implicit operator Vector3(BezierPoint cp) => cp.point;

			public float angle;

			[Flags]
			public enum Mode { None = 0, Linear = 1, Automatic = 2, Manual = 4, Proportional = Automatic | Manual }
			public Mode mode;
			public static Mode[] AllModes = new Mode[]
			{
				Mode.Automatic,
				Mode.Manual,
				Mode.Linear,
				Mode.Proportional
			};

			public enum Type { Control = 0, RightHandle = 1, LeftHandle = 2 }
			public Type type;

			public BezierPoint(Vector3 position) : this()
			{
				point = position;
			}
			public BezierPoint(Vector3 position, Type type)
			{
				point = position;
				angle = 0f;
				mode = Mode.Automatic;
				this.type = type;
			}
			public BezierPoint(Vector3 position, Mode mode)
			{
				point = position;
				angle = 0f;
				this.mode = mode;
				type = Type.Control;
			}
			public BezierPoint(Vector3 position, Type type, Mode mode)
			{
				this.point = position;
				this.angle = 0f;
				this.type = type;
				this.mode = mode;
			}
			public static BezierPoint Control(Vector3 position, Mode mode = Mode.Automatic) => new BezierPoint(position, Type.Control, mode);
			public static BezierPoint LeftHandle(Vector3 position, Mode mode = Mode.Automatic) => new BezierPoint(position, Type.LeftHandle, mode);
			public static BezierPoint RightHandle(Vector3 position, Mode mode = Mode.Automatic) => new BezierPoint(position, Type.RightHandle, mode);

			public BezierPoint SetPosition(Vector3 position)
			{
				point = position;
				return this;
			}

			public BezierPoint SetMode(Mode mode)
			{
				this.mode = mode;
				return this;
			}

			public BezierPoint SetRotation(float rotation)
			{
				this.angle = rotation;
				return this;
			}

			public static Vector3 operator +(BezierPoint a, BezierPoint b) => a.point + b.point;
			public static Vector3 operator -(BezierPoint a, BezierPoint b) => a.point - b.point;
			public static Vector3 operator -(BezierPoint a) => -a.point;
		}
	}
}