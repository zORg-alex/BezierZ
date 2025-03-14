using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace BezierCurveZ
{
	[Serializable]
	public struct Point
	{

		[SerializeField] internal Vector3 _position;
		public Vector3 position { [DebuggerStepThrough] get => _position; }

		[SerializeField] internal Quaternion _rotation;
		public Quaternion rotation { [DebuggerStepThrough] get => _rotation; }

		[SerializeField] internal Vector3 _scale;
		public Vector3 scale { [DebuggerStepThrough] get => _scale; }

		public Vector3 forward { [DebuggerStepThrough] get => rotation * Vector3.forward; }
		public Vector3 right { [DebuggerStepThrough] get => rotation * Vector3.right; }
		public Vector3 up { [DebuggerStepThrough] get => rotation * Vector3.up; }

		[SerializeField] internal Type _type;
		public Type type { [DebuggerStepThrough] get => _type; }
		public bool IsEndPoint { [DebuggerStepThrough] get => type == Type.EndPoint; }

		public bool isRightHandle { [DebuggerStepThrough] get => type == Type.Right; }

		public bool isLeftHandle { [DebuggerStepThrough] get => type == Type.Left; }
		public static Type[] AllTypes { [DebuggerStepThrough] get => new Type[] { Type.EndPoint, Type.Right, Type.Left }; }

		[SerializeField] internal Mode _mode;

		public Mode mode { [DebuggerStepThrough] get => _mode; }
		public bool IsLinear { [DebuggerStepThrough] get => mode == Mode.Linear; }
		public bool IsAutomatic { [DebuggerStepThrough] get => mode.HasFlag(Mode.Automatic); }
		public bool IsManual { [DebuggerStepThrough] get => mode.HasFlag(Mode.Manual); }
		public bool IsProportional { [DebuggerStepThrough] get => mode.HasFlag(Mode.Proportional); }

		public static Mode[] AllModes { [DebuggerStepThrough] get => new Mode[] { Mode.Automatic, Mode.Proportional, Mode.Manual, Mode.Linear }; }

		public Point(Point point) : this(point.position, point.rotation, Vector3.one, point.type, point.mode) { }
		public Point(Vector3 position) : this(position, Quaternion.identity) { }
		public Point(Vector3 position, Type type = Type.EndPoint) : this(position, Quaternion.identity, Vector3.one, type) { }
		public Point(Vector3 position, Mode mode = Mode.Automatic) : this(position, Quaternion.identity, Vector3.one, mode: mode) { }
		public Point(Vector3 position, Type type = Type.EndPoint, Mode mode = Mode.Automatic) : this(position, Quaternion.identity, Vector3.one, type, mode) { }
		public Point(Vector3 position, Quaternion rotation = default, Vector3 scale = default, Type type = Type.EndPoint, Mode mode = Mode.Automatic)
		{
			if (scale == default) scale = Vector3.one;
			_position = position;
			_rotation = rotation;
			_scale = scale;
			_type = type;
			_mode = mode;
		}

		public Point SetPosition(Vector3 position)
		{
			if (position.x == float.NaN) return this;
			_position = position;
			return this;
		}

		public Point SetRotation(Quaternion rotation)
		{
			_rotation = rotation;
			return this;
		}

		public Point SetScale(Vector3 scale)
		{
			_scale = scale;
			return this;
		}

		public Point SetMode(Mode mode)
		{
			_mode = mode;
			return this;
		}

		public Point RemoveAutomaticMode()
		{
			_mode = _mode == Mode.Automatic ? Mode.Manual : _mode ^ Mode.Automatic;
			return this;
		}

		[DebuggerStepThrough]
		public Point SetTangent(Vector3 tangent)
		{
			_rotation = Quaternion.LookRotation(tangent, rotation * Vector3.up);
			return this;
		}
		[DebuggerStepThrough]
		public static implicit operator Vector3(Point ep) => ep.position;
		[DebuggerStepThrough]
		public static Vector3 operator +(Point a, Point b) => a.position + b.position;
		[DebuggerStepThrough]
		public static Vector3 operator -(Point a, Point b) => a.position - b.position;
		[DebuggerStepThrough]
		public static Vector3 operator +(Point a, Vector3 b) => a.position + b;
		[DebuggerStepThrough]
		public static Vector3 operator -(Point a, Vector3 b) => a.position - b;
		[DebuggerStepThrough]
		public static Vector3 operator +(Vector3 a, Point b) => a + b.position;
		[DebuggerStepThrough]
		public static Vector3 operator -(Vector3 a, Point b) => a - b.position;
		[DebuggerStepThrough]
		public static Vector3 operator -(Point a) => -a.position;
		[DebuggerStepThrough]
		public static Vector3 operator *(Point a, float f) => a.position * f;

		[DebuggerStepThrough]
		public static Point EndPoint(Vector3 position, Quaternion rotation = default, Mode mode = Mode.Automatic) => new Point(position, rotation, Vector3.one, Type.EndPoint, mode);

		[DebuggerStepThrough]
		public static Point RightHandle(Vector3 position, Quaternion rotation = default, Mode mode = Mode.Automatic) => new Point(position, rotation, Vector3.one, Type.Right, mode);

		[DebuggerStepThrough]
		public static Point LeftHandle(Vector3 position, Quaternion rotation = default, Mode mode = Mode.Automatic) => new Point(position, rotation, Vector3.one, Type.Left, mode);

		public enum Mode { None = 0, Automatic = 1, Manual = 2, Linear = 4, Proportional = Automatic | Manual }
		public enum Type { EndPoint, Right, Left }

		[DebuggerStepThrough]
		public override string ToString() => base.ToString() + $"{position}, {rotation.eulerAngles} {mode} {type}";

	}
}