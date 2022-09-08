using BezierCurveZ;
using System;
using UnityEngine;

public struct OtherVertexData
{
	public Vector3 Position;
	public Quaternion Rotation;
	public Vector3 up => Rotation * Vector3.up;
	public Vector3 right => Rotation * Vector3.right;
	public Vector3 normal => Rotation * Vector3.right;
	public Vector3 forward => Rotation * Vector3.forward;
	public Vector3 tangent => Rotation * Vector3.forward;

	public static implicit operator Vector3(OtherVertexData v) => v.Position;

	public float cumulativeTime;
	public float distance;
	public int segmentInd;
	public int segmentStartVertInd;
	public bool isSharp;

	internal static OtherVertexData[] GetVertexData(OtherCurve otherCurve, float maxAngleError, float minSplitDistance, int accuracy)
	{
		if (otherCurve.PointCount == 0) otherCurve.Reset();
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