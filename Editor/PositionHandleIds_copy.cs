using UnityEngine;

namespace BezierCurveZ
{
	/// <summary>
	/// That's a copy of UnityEditor internal class. So we could know exact Move Tool handle Id's
	/// </summary>
	internal struct PositionHandleIds_copy
	{
		public readonly int x;

		public readonly int y;

		public readonly int z;

		public readonly int xy;

		public readonly int yz;

		public readonly int xz;

		public readonly int xyz;

		public static PositionHandleIds_copy _default;

		public static PositionHandleIds_copy @default
		{
			get
			{
				if (_default.Equals((PositionHandleIds_copy)default)) _default = new PositionHandleIds_copy(GUIUtility.GetControlID(s_xAxisMoveHandleHash, FocusType.Passive), GUIUtility.GetControlID(s_yAxisMoveHandleHash, FocusType.Passive), GUIUtility.GetControlID(s_zAxisMoveHandleHash, FocusType.Passive), GUIUtility.GetControlID(s_xyAxisMoveHandleHash, FocusType.Passive), GUIUtility.GetControlID(s_xzAxisMoveHandleHash, FocusType.Passive), GUIUtility.GetControlID(s_yzAxisMoveHandleHash, FocusType.Passive), GUIUtility.GetControlID(s_FreeMoveHandleHash, FocusType.Passive));
				return _default;
			}
		}

		public int this[int index]
		{
			get
			{
				switch (index)
				{
					case 0:
						return x;
					case 1:
						return y;
					case 2:
						return z;
					case 3:
						return xy;
					case 4:
						return yz;
					case 5:
						return xz;
					case 6:
						return xyz;
					default:
						return -1;
				}
			}
		}
		//	index switch
		//{
		//	0 => x,
		//	1 => y,
		//	2 => z,
		//	3 => xy,
		//	4 => yz,
		//	5 => xz,
		//	6 => xyz,
		//	_ => -1,
		//};

		public bool Has(int id)
		{
			return x == id || y == id || z == id || xy == id || yz == id || xz == id || xyz == id;
		}

		public PositionHandleIds_copy(int x, int y, int z, int xy, int xz, int yz, int xyz)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.xy = xy;
			this.yz = yz;
			this.xz = xz;
			this.xyz = xyz;
		}

		public override int GetHashCode()
		{
			return x ^ y ^ z ^ xy ^ xz ^ yz ^ xyz;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is PositionHandleIds_copy))
			{
				return false;
			}

			PositionHandleIds_copy positionHandleIds = (PositionHandleIds_copy)obj;
			return positionHandleIds.x == x && positionHandleIds.y == y && positionHandleIds.z == z && positionHandleIds.xy == xy && positionHandleIds.xz == xz && positionHandleIds.yz == yz && positionHandleIds.xyz == xyz;
		}

		internal static int s_xAxisMoveHandleHash = "xAxisFreeMoveHandleHash".GetHashCode();

		internal static int s_yAxisMoveHandleHash = "yAxisFreeMoveHandleHash".GetHashCode();

		internal static int s_zAxisMoveHandleHash = "zAxisFreeMoveHandleHash".GetHashCode();

		internal static int s_FreeMoveHandleHash = "FreeMoveHandleHash".GetHashCode();

		internal static int s_xzAxisMoveHandleHash = "xzAxisFreeMoveHandleHash".GetHashCode();

		internal static int s_xyAxisMoveHandleHash = "xyAxisFreeMoveHandleHash".GetHashCode();

		internal static int s_yzAxisMoveHandleHash = "yzAxisFreeMoveHandleHash".GetHashCode();

		internal static int s_xAxisScaleHandleHash = "xAxisScaleHandleHash".GetHashCode();

		internal static int s_yAxisScaleHandleHash = "yAxisScaleHandleHash".GetHashCode();

		internal static int s_zAxisScaleHandleHash = "zAxisScaleHandleHash".GetHashCode();

	}
}