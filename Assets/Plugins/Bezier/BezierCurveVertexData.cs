using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BezierCurveZ
{
	[Serializable]
	public struct BezierCurveVertexData
	{
		public struct VertexData
		{
			public Vector3 point;
			public Vector3 tangent;
			public Quaternion rotation;
			public int segmentIndex;
			public float length;
		}
		[SerializeField, HideInInspector]
		private Vector3[] _points;
		public Vector3[] Points => _points;
		[SerializeField, HideInInspector]
		private Vector3[] _tangents;
		public Vector3[] Tangents => _tangents;
		[SerializeField, HideInInspector]
		private Quaternion[] _rotations;
		public Quaternion[] Rotations => _rotations;
		[SerializeField, HideInInspector]
		private float[] _cumulativeLengths;
		public float[] CumulativeLengths => _cumulativeLengths;

		[SerializeField, HideInInspector]
		private int[] _segmentIndexes;
		public int[] SegmentIndexes => _segmentIndexes;

		public BezierCurveVertexData(Curve bezierCurve, float minSamplingDistance = .001f, float maxAngleError = .05f)
		{
			var data = CurveInterpolation.SplitCurveByAngleError(bezierCurve, maxAngleError, minSamplingDistance);
			//TODO Implement this
			_points = data.points.ToArray();
			_segmentIndexes = data.indices.ToArray();
			_cumulativeLengths = data.cumulativeLength.ToArray();
			_tangents = data.tangents.ToArray();
			_rotations = CurveInterpolation.GetRotations(data);
		}

		public Vector3 GetPointAtLength(float length) => throw new NotImplementedException();
		public Vector3 GetPointAtTime(float time) => throw new NotImplementedException();

		public Vector3 GetRotationAtLength(float length) => throw new NotImplementedException();
		public Vector3 GetRotationAtTime(float time) => throw new NotImplementedException();

		public IEnumerable<VertexData> GetEnumerable()
		{
			for (int i = 0; i < _points?.Length; i++)
			{
				yield return new VertexData()
				{
					point = _points[i],
					tangent = _tangents[i],
					rotation = _rotations[i],
					length = _cumulativeLengths[i],
					segmentIndex = _segmentIndexes[i]
				};
			}
		}
	}
}