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

			public float angle { get => _rotation.eulerAngles.z; }

			[SerializeField]
			private Quaternion _rotation;
			public Quaternion rotation { get => _rotation; set
				{
					_rotation = value;
				}
			}

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
				rotation = Quaternion.identity;
			}
			public BezierPoint(Vector3 position, Type type) : this()
			{
				point = position;
				mode = Mode.Automatic;
				this.type = type;
				rotation = Quaternion.identity;
			}
			public BezierPoint(Vector3 position, Mode mode) : this()
			{
				point = position;
				this.mode = mode;
				type = Type.Control;
				rotation = Quaternion.identity;
			}
			public BezierPoint(Vector3 position, Type type, Mode mode) : this()
			{
				point = position;
				this.type = type;
				this.mode = mode;
				rotation = Quaternion.identity;
			}
			public BezierPoint(Vector3 position, Quaternion rotation, Type type, Mode mode) : this()
			{
				point = position;
				this.type = type;
				this.mode = mode;
				this.rotation = rotation;
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

			public BezierPoint SetTangent(Vector3 tangent)
			{
				rotation = Quaternion.LookRotation(tangent, rotation * Vector3.up);
				return this;
			}
			public BezierPoint SetRotation(Quaternion rotation)
			{
				this.rotation = rotation;
				return this;
			}

			public static Vector3 operator +(BezierPoint a, BezierPoint b) => a.point + b.point;
			public static Vector3 operator -(BezierPoint a, BezierPoint b) => a.point - b.point;
			public static Vector3 operator -(BezierPoint a) => -a.point;

			internal Quaternion GetRotation(Vector3 tangent)
			{
				return Quaternion.LookRotation(tangent, rotation * Vector3.up);
			}
		}
	}
}