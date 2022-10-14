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
		private float[] _cumulativeTimes;
		public float[] CumulativeTimes => _cumulativeTimes;
		[SerializeField, HideInInspector]
		private bool[] _isSharp;

		public bool[] IsSharp => _isSharp;


		[SerializeField, HideInInspector]
		private int[] _segmentIndexes;
		public int[] SegmentIndexes => _segmentIndexes;

		public BezierCurveVertexData(Curve bezierCurve, float minSamplingDistance = .001f, float maxAngleError = .05f, bool _useRotations = false)
		{
			var points = new Curve.BezierPoint[bezierCurve.SegmentCount + 1];
			for (int i = 0; i < points.Length; i++)
			{
				points[i] = bezierCurve.Points[(bezierCurve.IsClosed ? 1 : 0) + (i * 3) % bezierCurve.PointCount];
			}
			var data = CurveInterpolation.SplitCurveByAngleError(
				bezierCurve.Segments.ToArray(),
				points.SelectArray(p => p.rotation),
				points.SelectArray(p => p.mode.HasFlag(Curve.BezierPoint.Mode.Automatic)),
				bezierCurve.IsClosed, maxAngleError, minSamplingDistance, 10);

			_points = data.points.ToArray();
			_segmentIndexes = data.segmentIndices.ToArray();
			_cumulativeLengths = data.cumulativeLength.ToArray();
			_tangents = data.tangents.ToArray();
			_rotations = data.rotations.ToArray();
			_cumulativeTimes = data.cumulativeTime.ToArray();
			_isSharp = data.isSharp.ToArray();
		}

		public float GetLength(int segmentInd, float t)
		{
			var i = BinarySearchPreviousIndex(segmentInd + t, ref _cumulativeTimes, _segmentIndexes[segmentInd]);
			var alen = _cumulativeLengths[i];
			var blen = _cumulativeLengths[i + 1];
			var tDist = _cumulativeTimes[i + 1] - _cumulativeTimes[i];
			var ta = _cumulativeTimes[i].Remainder();
			return Mathf.Lerp(alen, blen, (t - ta) / tDist);
		}

		public Vector3 GetPointAtLength(float length) => LerpVector3(length, ref _cumulativeLengths, ref _points);
		/// <param name="time">segmentwise time, 1.12345f -> segment 1, time 0.12345f</param>
		public Vector3 GetPointAtTime(float time) => LerpVector3(time, ref _cumulativeTimes, ref _points);
		public Vector3 GetPoint(int index) => _points[index];

		public Quaternion GetRotationAtLength(float length) => LerpQuaternion(length, ref _cumulativeLengths, ref _rotations);
		/// <param name="time">segmentwise time, 1.12345f -> segment 1, time 0.12345f</param>
		public Quaternion GetRotationAtTime(float time) => LerpQuaternion(time, ref _cumulativeTimes, ref _rotations);
		public Quaternion GetRotation(int index) => _rotations[index];


		private float LerpFloat(float value, ref float[] array, ref float[] valArray, int startFrom = 0)
		{
			var ind = BinarySearchPreviousIndex(value, ref array, startFrom);
			if (value == 0) return valArray[0];
			else if (ind == array.Length - 1) return valArray[ind];
			var a = array[ind];
			var b = array[ind + 1];
			var dist = b - a;
			return Mathf.Lerp(valArray[ind], valArray[ind + 1], (value - a) / dist);
		}
		private Vector3 LerpVector3(float value, ref float[] array, ref Vector3[] valArray, int startFrom = 0)
		{
			var ind = BinarySearchPreviousIndex(value, ref array, startFrom);
			if (value == 0) return valArray[0];
			else if (ind == array.Length - 1) return valArray[ind];
			var a = array[ind];
			var b = array[ind + 1];
			var dist = b - a;
			return Vector3.Lerp(valArray[ind], valArray[ind + 1], (value - a) / dist);
		}
		private Quaternion LerpQuaternion(float value, ref float[] array, ref Quaternion[] valArray, int startFrom = 0)
		{
			var ind = BinarySearchPreviousIndex(value, ref array, startFrom);
			if (value == 0) return valArray[0];
			else if (ind == array.Length - 1) return valArray[ind];
			var a = array[ind];
			var b = array[ind + 1];
			var dist = b - a;
			return Quaternion.Lerp(valArray[ind], valArray[ind + 1], (value - a) / dist);
		}

		private int BinarySearchPreviousIndex(float value, ref float[] array, int low = 0)
		{
			int high = array.Length - 1;
			while (high - low != 1)
			{
				var mid = (high - low) / 2 + low;
				if (array[mid] <= value)
				{
					low = mid;
				}
				else
				{
					high = mid;
				}
			}
			return low;
		}

		private int BinarySearchPreviousIndex(float value, ref int[] array, int low = 0)
		{
			int high = array.Length - 1;
			while (high - low != 1)
			{
				var mid = (high - low) / 2 + low;
				if (array[mid] <= value)
				{
					low = mid;
				}
				else
				{
					high = mid;
				}
			}
			return low;
		}

		public IEnumerable<VertexData> GetEnumerable()
		{
			int sind = 0;
			for (int i = 0; i < _points?.Length; i++)
			{
				if (_segmentIndexes.Length > sind + 1 && _segmentIndexes[sind + 1] == i)
					sind++;
				yield return new VertexData()
				{
					point = _points[i],
					tangent = _tangents[i],
					rotation = _rotations[i],
					length = _cumulativeLengths[i],
					segmentIndex = sind,
					time = _cumulativeTimes[i],
					isSharp = _isSharp[i]
				};
			}
		}

		internal int FirstIndexOfSegment(int segmentIndex)
		{
			return _segmentIndexes[segmentIndex];
		}
		internal int SegmentIndex(int index) => _cumulativeTimes[index].FloorToInt();
	}
}