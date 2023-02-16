using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility
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
	}
}