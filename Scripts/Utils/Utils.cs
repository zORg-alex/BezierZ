using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace BezierZUtility
{
	public static class Utils
	{

		public static Vector2 FindNearestPointOnFiniteLine(Vector2 origin, Vector2 end, Vector2 point) {
			//Get heading
			Vector2 heading = (end - origin);
			float magnitudeMax = heading.magnitude;
			heading.Normalize();

			//Do projection from the point but clamp it
			Vector2 lhs = point - origin;
			float dotP = Vector2.Dot(lhs, heading);
			dotP = Mathf.Clamp(dotP, 0f, magnitudeMax);
			return origin + heading * dotP;
		}

		public static void UndoWrap(this MonoBehaviour mb, Action action, [CallerMemberName] string callerName = "")
		{
#if UNITY_EDITOR
			Undo.RecordObject(mb, callerName);
#endif
			action?.Invoke();
		}

		public static T SingletonGetNew<T>(ref T instance) where T : new()
		{
			if (instance == null) instance = new T();
			return instance;
		}
	}
}