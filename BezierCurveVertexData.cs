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
			public float time;

			public Vector3 normal => rotation * Vector3.right;

			public Vector3 up => rotation * Vector3.up;
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
		private float[] _times;
		public float[] Times => _times;


		[SerializeField, HideInInspector]
		private int[] _segmentIndexes;
		public int[] SegmentIndexes => _segmentIndexes;

		public BezierCurveVertexData(Curve bezierCurve, float minSamplingDistance = .001f, float maxAngleError = .05f, bool _useRotations = false)
		{
			var data = CurveInterpolation.SplitCurveByAngleError(bezierCurve, maxAngleError, minSamplingDistance, useRotations: _useRotations);

			_points = data.points.ToArray();
			_segmentIndexes = data.segmentIndices.ToArray();
			_cumulativeLengths = data.cumulativeLength.ToArray();
			_tangents = data.tangents.ToArray();
			_rotations = data.rotations.ToArray();
			_times = data.segmentTime.ToArray();
		}

		public Vector3 GetPointAtLength(float length) => _points[CumulativeLengths.IndexOf(l => l >= length)];
		public Vector3 GetPointAtTime(float time) => _points[Times.IndexOf(t => t >= time)];
		public Vector3 GetPoint(int index) => _points[index];

		public Quaternion GetRotationAtLength(float length) => _rotations[CumulativeLengths.IndexOf(l => l >= length)];
		public Quaternion GetRotationAtTime(float time) => _rotations[Times.IndexOf(t=>t >= time)];
		public Quaternion GetRotation(int index) => _rotations[index];

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
					segmentIndex = _segmentIndexes[i],
					time = _times[i]
				};
			}
		}
	}
}