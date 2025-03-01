using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace BezierZUtility
{
	public static class DefaultUtils
	{

		public static float SmoothStep(this float t) => t * t * (3f - 2f * t);
		public static float SmootherStep(this float t) => t * t * t * (t * (t * 6 - 15) + 10);
		public static float InverseSmoothStep(this float t) => .5f - Mathf.Sin(Mathf.Asin(1f - 2f * t) / 3f);
		public static float SinStep(this float t) => Mathf.Sin(Mathf.PI * (t - .5f)) / 2 + .5f;

		/// <summary>
		/// Smoothly interpolates t with variable curvature of p
		/// </summary>
		/// <param name="t"></param>
		/// <param name="p">[0..1] For Inverse smoothstep, [1..infinity] for smoothstep<para/> When equals to 2 returns similar results as Smoothstep</param>
		/// <returns></returns>
		public static float SmoothStepParametric(this float t, float p = 2f)
		{
			var tp = Mathf.Pow(t, p);
			return tp / (tp + Mathf.Pow(1 - t, p));
		}

		[DebuggerStepperBoundary]
		public static T2[] SelectArray<T1, T2>(this IEnumerable<T1> collection, Func<T1, T2> selector)
		{
			var r = new T2[collection.Count()];
			var ind = 0;
			foreach (var item in collection)
			{
				r[ind++] = selector(item);
			}
			return r;
		}

		public static T[] SetValues<T>(this T[] array, int startIndex, params T[] values)
		{
			for (int i = 0; i < values.Length; i++)
			{
				array[startIndex + i] = values[i];
			}
			return array;
		}

		public static int BinarySearchIndex<T>(this T[] array, Func<T, int> comparer) => array.BinarySearchIndex(comparer, 0);
		public static int BinarySearchIndex<T>(this T[] array, Func<T, int> comparer, int low = 0)
		{
			var high = array.Length - 1;
			while (high - low != 1)
			{
				var mid = (high - low) / 2 + low;
				int rez = comparer(array[mid]);
				if (rez == 0)
					return mid;
				if (rez > 0)
					low = mid;
				else
					high = mid;
			}
			return low;
		}
		public static int BinarySearchIndex<T>(this T[] array, Func<T, float> comparer) => array.BinarySearchIndex(comparer, 0);
		public static int BinarySearchIndex<T>(this T[] array, Func<T, float> comparer, int low = 0)
		{
			var high = array.Length - 1;
			while (high - low != 1)
			{
				var mid = (high - low) / 2 + low;
				float rez = comparer(array[mid]);
				if (rez == 0)
					return mid;
				if (rez > 0)
					low = mid;
				else
					high = mid;
			}
			return low;
		}
		public static T BinarySearch<T>(this T[] array, Func<T, int> comparer, int low = 0) => array[array.BinarySearchIndex(comparer, low)];
		public static T BinarySearch<T>(this T[] array, Func<T, int> comparer) => array[array.BinarySearchIndex(comparer, 0)];

		public static T BinarySearch<T>(this T[] array, Func<T, float> comparer, int low = 0) => array[array.BinarySearchIndex(comparer, low)];
		public static T BinarySearch<T>(this T[] array, Func<T, float> comparer) => array[array.BinarySearchIndex(comparer, 0)];

		public static void AddRange_<T>(this List<T> collection, params T[] values) => collection.AddRange(values);

		public static float LerpTo(this float value, float targetValue, float t = .5f) =>
			Mathf.Lerp(value, targetValue, t);

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
		public static Vector3 Not(this Vector3 v, Direction dir)
		{
			switch (dir)
			{
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

		/// <summary>
		/// Returns new vector with y = 0
		/// </summary>
		public static Vector3 Horizontal(this Vector3 v) => new Vector3(v.x, 0, v.z);
		public static Vector3 SetY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);

		[DebuggerStepThrough]
		public static float DistanceTo(this Vector3 vector1, Vector3 vector2) =>
			Vector3.Distance(vector1, vector2);
		[DebuggerStepThrough]
		public static float DistanceTo(this Vector2 vector1, Vector2 vector2) =>
			Vector2.Distance(vector1, vector2);

		public static float Dot(this Vector3 vector1, Vector3 vector2) =>
			Vector3.Dot(vector1, vector2);
		public static Vector3 Cross(this Vector3 vector1, Vector3 vector2) =>
			Vector3.Cross(vector1, vector2);

		[DebuggerStepThrough]
		public static Color MultiplyAlpha(this Color c, float alpha) => new Color(c.r, c.g, c.b, c.a * alpha);

		[DebuggerStepThrough]
		public static Vector3 Abs(this Vector3 v) => new Vector3(v.x.Abs(), v.y.Abs(), v.z.Abs());
		[DebuggerStepThrough]
		public static float Abs(this float v) => Math.Abs(v);
		[DebuggerStepThrough]
		public static int Abs(this int v) => Math.Abs(v);
		[DebuggerStepThrough]
		public static int CeilToInt(this float v) => Mathf.CeilToInt(v);
		[DebuggerStepThrough]
		public static float Ceil(this float v) => Mathf.Ceil(v);
		[DebuggerStepThrough]
		public static int FloorToInt(this float v) => Mathf.FloorToInt(v);
		[DebuggerStepThrough]
		public static float Remainder(this float v) => v - Mathf.Floor(v);
		[DebuggerStepThrough]
		public static float Floor(this int v) => Mathf.Floor(v);
		[DebuggerStepThrough]
		public static float Min(this float v, float min) => Math.Min(v, min);
		[DebuggerStepThrough]
		public static int Min(this int v, int min) => Math.Min(v, min);
		[DebuggerStepThrough]
		public static float Max(this float v, float min) => Math.Max(v, min);
		[DebuggerStepThrough]
		public static int Max(this int v, int min) => Math.Max(v, min);
		[DebuggerStepThrough]
		public static float Deg2Rad(this float v) => v * Mathf.Deg2Rad;
		[DebuggerStepThrough]
		public static float Rad2Deg(this float v) => v * Mathf.Rad2Deg;

		/// <returns>Inverse of this rotation</returns>
		[DebuggerStepThrough]
		public static Quaternion Inverted(this Quaternion q) => Quaternion.Inverse(q);

		[DebuggerStepThrough]
		public static IEnumerable<T> Foreach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var item in collection)
			{
				action(item);
			}
			return collection;
		}

		[DebuggerStepThrough]
		public static T Min<T>(this IEnumerable<T> collection, Func<T, float> predicate, out int indexOf)
		{
			T min = collection.FirstOrDefault();
			float minVal = predicate(min);
			indexOf = 0;
			int i = 1;
			foreach (var item in collection.Skip(1))
			{
				float v = predicate(item);
				if (v < minVal)
				{
					minVal = v;
					min = item;
					indexOf = i;
				}
				i++;
			}
			return min;
		}

		public static Vector3 MultiplyComponentwise(this Vector3 v1, Vector3 v2) => new Vector3(v1.x * v2.x, v1.y * v2.y, v1.z * v2.z);

		public static Vector3 Sum(this IEnumerable<Vector3> vector3s)
		{
			var result = Vector3.zero;
			foreach (var v in vector3s)
			{
				result += v;
			}
			return result;
		}

		[DebuggerStepThrough]
		public static Vector3 Sum<T>(this IEnumerable<T> collection, Func<T, Vector3> getter)
		{
			var sum = Vector3.zero;
			foreach (var item in collection)
			{
				sum += getter(item);
			}
			return sum;
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

		public static Transform Reset(this Transform transform)
		{
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			transform.localScale = Vector3.one;
			return transform;
		}
		public static Transform Reset(this Transform transform, Transform parent)
		{
			transform.parent = parent;
			transform.Reset();
			return transform;
		}
		public static bool IsNullOrDestroyed(this object obj)
		{

			if (ReferenceEquals(obj, null)) return true;

			if (obj is UnityEngine.Object) return obj as UnityEngine.Object == null;

			return false;
		}

		public static Rect Extend(this Rect rect, float horizontal, float vertical)
		{
			//rect = rect.Abs();
			return new Rect(
				x: rect.x - horizontal,
				y: rect.y - vertical,
				width: rect.width + 2 * horizontal,
				height: rect.height + 2 * vertical
			);
		}
		public static Rect Extend(this Rect rect, float left, float top, float right, float bottom)
		{
			//rect = rect.Abs();
			return new Rect(
				x: rect.x - left,
				y: rect.y - top,
				width: rect.width + left + right,
				height: rect.height + top + bottom
			);
		}
		public static T[] CheckCachedValueVersion<T, SourceT>(this SourceT @this, ref T[] cacheField, Func<SourceT, T[]> selector, ref int cacheVersion, int mainVersion, bool force = true)
		{
			if (cacheVersion != mainVersion || cacheField == null || force)
			{
				cacheField = selector(@this);
				cacheVersion = mainVersion;
			}
			return cacheField;
		}

	}
}