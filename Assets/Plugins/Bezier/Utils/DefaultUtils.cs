using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DefaultUtils {

	public static T2[] SelectArray<T1,T2>( this IEnumerable<T1> collection, Func<T1,T2> selector) {
		var r = new T2[collection.Count()];
		var ind = 0;
		foreach (var item in collection) {
			r[ind++] = selector(item);
		}
		return r;
	}

	public static T[] SetValues<T>(this T[] array, int startIndex, params T[] values) {
		for (int i = 0; i < values.Length; i++) {
			array[startIndex + i] = values[i];
		}
		return array;
	}

	public static void AddRange_<T>(this List<T> collection, params T[] values) => collection.AddRange(values);

	public static float LerpTo(this float value, float targetValue, float t = .5f) =>
		Mathf.Lerp(value,targetValue,t);

	public static Vector3 LerpTo(this Vector3 value, Vector3 targetValue, float t = .5f) =>
		Vector3.Lerp(value, targetValue, t);
	public static Vector2 LerpTo(this Vector2 value, Vector2 targetValue, float t = .5f) =>
		Vector2.Lerp(value, targetValue, t);

	public static float Clamp(this float value, float minValue, float maxValue) =>
		Math.Min(Math.Max(value, minValue), maxValue);

	public static int Clamp(this int value, int minValue, int maxValue) =>
		Math.Min(Math.Max(value, minValue), maxValue);

	/// <summary>
	/// One minus value. Like 1 - (0, 1, 0) => (1, 0, 1).
	/// </summary>
	/// <param name="v"></param>
	/// <returns></returns>
	public static Vector3 OneMinus(this Vector3 v) => Vector3.one - v;

	public enum Direction { up, down, right, left, forward, back }
	/// <summary>
	/// returns vector with dir = 0 and multiplies by direction e.g. down is (-x, 0, -z)
	/// </summary>
	/// <param name="v"></param>
	/// <param name="dir"></param>
	/// <returns></returns>
	public static Vector3 Not(this Vector3 v, Direction dir) {
		switch (dir) {
			case Direction.up:
				return new Vector3(v.x, 0, v.z);
			case Direction.down:
				return new Vector3(-v.x, 0, -v.z);
			case Direction.right:
				return new Vector3(0, v.y, v.z);
			case Direction.left:
				return new Vector3(0, -v.y, -v.z);
			case Direction.forward:
				return new Vector3(v.x, v.y, 0);
			case Direction.back:
				return new Vector3(-v.x, -v.y, 0);
			default:
				return Vector3.one;
		}
	}
	public enum Axis { X, Y, Z }
	public static Vector3 Invert(this Vector3 v, Axis axis) => new Vector3((axis == Axis.X ? -1 : 1) * v.x, (axis == Axis.Y ? -1 : 1) * v.y, (axis == Axis.Z ? -1 : 1) * v.z);
	/// <summary>
	/// Returns new vector with y = 0
	/// </summary>
	public static Vector3 Horizontal(this Vector3 v) => new Vector3(v.x, 0, v.z);
	public static Vector3 SetY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);

	public static float DistanceTo(this Vector3 vector1, Vector3 vector2) =>
		Vector3.Distance(vector1, vector2);
	public static float DistanceTo(this Vector2 vector1, Vector2 vector2) =>
		Vector2.Distance(vector1, vector2);

	public static Color MultiplyAlpha(this Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

	public static float Abs(this float v) => Math.Abs(v);
	public static int Abs(this int v) => Math.Abs(v);
	public static int CeilToInt(this float v) => Mathf.CeilToInt(v);
	public static float Ceil(this float v) => Mathf.Ceil(v);
	public static int FloorToInt(this float v) => Mathf.FloorToInt(v);
	public static float Floor(this int v) => Mathf.Floor(v);
	public static float Min(this float v, float min) => Math.Min(v, min);
	public static int Min(this int v, int min) => Math.Min(v, min);
	public static float Max(this float v, float min) => Math.Max(v, min);
	public static int Max(this int v, int min) => Math.Max(v, min);
	public static float Deg2Rad(this float v) => v * Mathf.Deg2Rad;
	public static float Rad2Deg(this float v) => v * Mathf.Rad2Deg;

	/// <returns>Inverse of this rotation</returns>
	public static Quaternion Inverted(this Quaternion q) => Quaternion.Inverse(q);

	public static IEnumerable<T> Foreach<T>(this IEnumerable<T> collection, Action<T> action) {
		foreach (var item in collection) {
			action(item);
		}
		return collection;
	}

	public static Vector3 Scale_(this Vector3 v1, Vector3 v2) => new Vector3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);

	public static Vector3 Sum(this IEnumerable<Vector3> vector3s) {
		var result = Vector3.zero;
		foreach (var v in vector3s) {
			result += v;
		}
		return result;
	}

	public static int IndexOf<T>(this T[] array, Func<T, bool> predicate)
	{
		for (int i = 0; i < array.Length; i++)
		{
			if (predicate(array[i])) return i;
		}
		return -1;
	}

	public static int IndexOf<T>(this T[] array, T value)
	{
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].Equals(value)) return i;
		}
		return -1;
	}

	public static Transform Reset(this Transform transform) {
		transform.localPosition = Vector3.zero;
		transform.localRotation = Quaternion.identity;
		transform.localScale = Vector3.one;
		return transform;
	}
	public static Transform Reset(this Transform transform, Transform parent) {
		transform.parent = parent;
		Reset(transform);
		return transform;
	}

}
