using System;
using System.Collections.Generic;
using UnityEngine;

namespace BezierCurveZ
{
	public partial class Curve
	{
		[Serializable]
		public struct Point
		{
			public Vector3 point;
			public static implicit operator Vector3(Point cp) => cp.point;

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

			public Point(Vector3 position) : this()
			{
				point = position;
			}
			public Point(Vector3 position, Type type)
			{
				point = position;
				mode = Mode.Automatic;
				this.type = type;
			}
			public Point(Vector3 position, Mode mode)
			{
				point = position;
				this.mode = mode;
				type = Type.Control;
			}
			public Point(Vector3 position, Type type, Mode mode)
			{
				this.point = position;
				this.type = type;
				this.mode = mode;
			}
			public static Point Control(Vector3 position, Mode mode = Mode.Automatic) => new Point(position, Type.Control, mode);
			public static Point LeftHandle(Vector3 position, Mode mode = Mode.Automatic) => new Point(position, Type.LeftHandle, mode);
			public static Point RightHandle(Vector3 position, Mode mode = Mode.Automatic) => new Point(position, Type.RightHandle, mode);

			public Point SetPosition(Vector3 position)
			{
				point = position;
				return this;
			}

			public Point SetMode(Mode mode)
			{
				this.mode = mode;
				return this;
			}

			public static Vector3 operator +(Point a, Point b) => a.point + b.point;
			public static Vector3 operator -(Point a, Point b) => a.point - b.point;
			public static Vector3 operator -(Point a) => -a.point;
		}
	}
}