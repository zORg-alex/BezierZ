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
			public bool isSharp;

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
		private bool[] _isSharp;
		public bool[] IsSharp => _isSharp;


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
			_isSharp = data.isSharp.ToArray();
		}

		public Vector3 GetPointAtLength(float length) => LerpVector3(length, ref _cumulativeLengths, ref _points);
		public Vector3 GetPointAtTime(float time) => LerpVector3(time, ref _times, ref _points);
		public Vector3 GetPoint(int index) => _points[index];

		public Quaternion GetRotationAtLength(float length) => LerpQuaternion(length, ref _cumulativeLengths, ref _rotations);
		public Quaternion GetRotationAtTime(float time) => LerpQuaternion(time, ref _cumulativeLengths, ref _rotations);
		public Quaternion GetRotation(int index) => _rotations[index];


		private Vector3 LerpVector3(float value, ref float[] array, ref Vector3[] valArray)
		{
			var ind = BinarySearchPreviousIndex(value, ref array);
			if (value == 0) return valArray[0];
			else if (ind == array.Length - 1) return valArray[ind];
			var a = array[ind];
			var b = array[ind + 1];
			var dist = b - a;
			return Vector3.Lerp(valArray[ind], valArray[ind + 1], (value - a) / dist);
		}
		private Quaternion LerpQuaternion(float value, ref float[] array, ref Quaternion[] valArray)
		{
			var ind = BinarySearchPreviousIndex(value, ref array);
			if (value == 0) return valArray[0];
			else if (ind == array.Length - 1) return valArray[ind];
			var a = array[ind];
			var b = array[ind + 1];
			var dist = b - a;
			return Quaternion.Lerp(valArray[ind], valArray[ind + 1], (value - a) / dist);
		}

		private int BinarySearchPreviousIndex(float value, ref float[] array)
		{
			int low = 0;
			int high = array.Length - 1;
			while (high - low != 1)
			{
				var mid = (high - low) / 2 + low;
				if (array[mid] < value)
				{
					low = mid;
				} else
				{
					high = mid;
				}
			}
			return low;
		}

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
					time = _times[i],
					isSharp = _isSharp[i]
				};
			}
		}
	}
}