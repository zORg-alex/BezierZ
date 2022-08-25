using BezierCurveZ;
using System;
using UnityEngine;

public struct OtherVertexData
{
	public Vector3 Position;
	public Quaternion Rotation;
	public float cumulativeTime;
	public float distance;
	public int segmentInd;
	public int segmentStartVertInd;
	public bool isSharp;

	internal static OtherVertexData[] GetVertexData(OtherCurve otherCurve, float maxAngleError, float minSplitDistance, int accuracy)
	{
		var splitdata = CurveInterpolation.SplitCurveByAngleError(otherCurve, maxAngleError, minSplitDistance, accuracy);
		var vd = new OtherVertexData[splitdata.Count];
		var segInd = 0;
		for (int i = 0; i < splitdata.Count; i++)
		{
			if (segInd + 1 < splitdata.segmentIndices.Count && splitdata.segmentIndices[segInd + 1] == i)
				segInd++;
			vd[i] = new OtherVertexData()
			{
				Position = splitdata.points[i],
				Rotation = splitdata.rotations[i],
				distance = splitdata.cumulativeLength[i],
				cumulativeTime = splitdata.cumulativeTime[i],
				isSharp = splitdata.isSharp[i],
				segmentInd = segInd,
				segmentStartVertInd = splitdata.segmentIndices[segInd]
			};
		}

		return vd;
	}
}

public static class OtherVertexDataExtensions
{
	internal static int GetStartIndex(this OtherVertexData[] vertexData, int segmentInd) =>
		vertexData.BinarySearch(v => v.segmentInd.CompareTo(segmentInd)).segmentStartVertInd;
}