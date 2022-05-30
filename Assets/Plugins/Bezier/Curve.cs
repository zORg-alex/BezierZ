using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace BezierCurveZ
{
	[Serializable]
	public partial class Curve : ISerializationCallbackReceiver
	{
		//===== Bezier Curve =====
		//Don't need to serialize versions. Generated data won't be serialized
		private int _bVersion = 0;
		[SerializeField, HideInInspector]
		private List<Point> points;
		private int lastPointInd => points.Count - 1;

		private Vector3[] _pointPositions;
		private int _pointPosVersion;
		public Vector3[] PointPositions {get {
				if (_pointPositions == null || _pointPosVersion != _bVersion)
				{
					_pointPositions = points.SelectArray(p => p.point);
					_pointPosVersion = _bVersion;
				}
				return _pointPositions;
			} }
		public List<Point> Points => points;

		[SerializeField]
		private bool _isClosed;
		public bool IsClosed { get => _isClosed; set { CloseCurve(value); _bVersion++; } }
		private Curve.Point.Mode[] _preservedNodeModesWhileClosed = new Point.Mode[2];

		private void CloseCurve(bool value)
		{
			if (value && !_isClosed) {
				_preservedNodeModesWhileClosed[0] = points[0].mode;
				_preservedNodeModesWhileClosed[1] = points[lastPointInd].mode;
			}
			else if (!value && _isClosed) {
				points[0] = points[0].SetMode(_preservedNodeModesWhileClosed[0]);
				points[lastPointInd] = points[0].SetMode(_preservedNodeModesWhileClosed[1]);
			}
			_isClosed = value;
		}

		public int ControlPointCount => points.Count / 3 + 1;
		public int SegmentCount => points.Count / 3 + (_isClosed ? 1 : 0);

		public void SetPoint(int index, Vector3 position)
		{
			index = Mathf.Clamp(index, 0, lastPointInd);
			points[index] = points[index].SetPosition(position);
			_bVersion++;
		}

		public void SetPointMode(int index, Point.Mode mode)
		{
			index = Mathf.Clamp(index, 0, lastPointInd);
			points[index] = points[index].SetMode(mode);
			if (_isClosed && (index == 0 || index == lastPointInd))
				_preservedNodeModesWhileClosed[Mathf.Clamp(index,0,1)] = mode;
			_bVersion++;
		}

		public IEnumerable<Vector3[]> Segments { get {
				for (int i = 0; i < SegmentCount; i++)
					yield return new Vector3[] { points[i*3], points[i * 3 + 1], points[i * 3 + 2], points[i * 3 + 3] };
			}
		}
		
		public Point.Mode DefaultAddedPointMode { get; set; }

		public void AddPointAtEnd(Vector3 point) => AddPointAt(point, true);
		public void AddPointAtStart(Vector3 point) => AddPointAt(point, false);
		public void AddPointAt(Vector3 point, bool end)
		{
			Point[] addedPoints;

			if (!end)
				addedPoints = AddPointAtBeginning(point);
			else
				addedPoints = AddPointAtEnd(point);

			points.InsertRange(end? lastPointInd : 0 , addedPoints);
			_bVersion++;

			Point[] AddPointAtBeginning(Vector3 point)
			{
				Point[] addedPoints = new Point[3];
				addedPoints[0] = Point.Control(point);
				if (DefaultAddedPointMode == Point.Mode.Linear)
				{
					var rightHandle = (points[0] - point) / 3f;
					addedPoints[1] = Point.RightHandle(point - rightHandle);
					addedPoints[2] = Point.LeftHandle(points[0] + rightHandle);
				} else {
					var invertedHandleOffset = points[0] - points[1];
					addedPoints[1] = Point.RightHandle(getSecondHandle(points[0], point, invertedHandleOffset));
					addedPoints[2] = Point.LeftHandle(invertedHandleOffset + points[0]);
				}
				return addedPoints;
			}

			Point[] AddPointAtEnd(Vector3 point)
			{
				Point[] addedPoints = new Point[3];
				if (DefaultAddedPointMode == Point.Mode.Linear)
				{
					var leftHandle = (points[lastPointInd] - point) / 3f;
					addedPoints[0] = Point.RightHandle(points[lastPointInd] + leftHandle);
					addedPoints[1] = Point.LeftHandle(point - leftHandle);
				}
				else
				{
					var invertedHandleOffset = points[lastPointInd] - points[lastPointInd - 1];
					addedPoints[0] = Point.RightHandle(invertedHandleOffset + points[lastPointInd]);
					addedPoints[1] = Point.LeftHandle(getSecondHandle(points[lastPointInd], point, invertedHandleOffset));
				}
				addedPoints[2] = Point.Control(point);
				return addedPoints;
			}

			Vector3 getSecondHandle(Vector3 lastPoint, Vector3 newPoint, Vector3 offset) =>
				lastPoint + Vector3.Reflect(lastPoint - newPoint, offset);
			_bVersion++;

		}

		//TODO create LookUpTable for binary search of closest point for this function
		public void AddPointAt(Vector3 point, int segmentIndex, float t)
		{
			
		}
		public void AddPoints(Vector3[] points)
		{
			if (points.Length < 4) return;
			int newLength = points.Length - (points.Length % 3) + 1;
			if (this.points == null || this.points.Count == 0)
				this.points = new List<Point>(newLength);
			var newPoints = new Point[newLength];
			var t = Point.Type.LeftHandle;
			for (int i = 0; i < newLength; i++)
			{
				newPoints[i] = new Point(points[i], t);
				t++;
				t = (Point.Type)((int)t % 3);
			}

			this.points.AddRange(newPoints);
			_bVersion++;
		}


		//===== Vertex Curve =====
		[NonSerialized]
		private int _vVersion = 0;
		private bool vertexCurveIsUpToDate => _vVersion == _bVersion;
		[SerializeField, HideInInspector]
		private float _minSamplingDistance;
		public float MinSamplingDistance { get { return _minSamplingDistance; } set { if (value != _minSamplingDistance) _vVersion++; _minSamplingDistance = value; } }
		[SerializeField, HideInInspector]
		private float _maxAngleError;
		public float MaxAngleError { get { return _maxAngleError; } set { if (value != _maxAngleError) _vVersion++; _maxAngleError = value; } }

		private BezierCurveVertexData _vertexData;
		public IEnumerable<BezierCurveVertexData.VertexData> VertexData { get {
				if (!vertexCurveIsUpToDate) Update();
				return _vertexData.GetEnumerable();
			} }

		public Vector3[] Vertices { get {
				if (!vertexCurveIsUpToDate) Update();
				return _vertexData.Points;
			} }

		public void Update(bool force = false)
		{
			if (vertexCurveIsUpToDate && !force) return;
			_vertexData = new BezierCurveVertexData(this, _minSamplingDistance, _maxAngleError);
			_vVersion = _bVersion;
		}
		//Curve will be restored on deserialization, we won't serialize generated data
		public void OnBeforeSerialize() { }
		public void OnAfterDeserialize() => Update(true);

		//========================

		public Vector3 GetPoint(int segmentIndex, float time) => CurveUtils.Evaluate(time, points[segmentIndex * 3], points[segmentIndex * 3 + 1], points[segmentIndex * 3 + 2], points[segmentIndex * 3 + 3]);
		public Vector3 GetPointAtTIme(float time)
		{
			Update();
			return _vertexData.GetPointAtTime(time);
		}
		public Vector3 GetPointAtLength(float length)
		{
			Update();
			return _vertexData.GetPointAtLength(length);
		}

		public Vector3 GetRotation(float time)
		{
			Update();
			return _vertexData.GetRotationAtTime(time);
		}

		public Vector3 GetTangent(int segmentIndex, float time) =>
			CurveUtils.EvaluateDerivative(time, points[segmentIndex * 3], points[segmentIndex * 3 + 1], points[segmentIndex * 3 + 2], points[segmentIndex * 3 + 3]);
		public Vector3 GetNormal(int segmentIndex, float time) =>
			CurveUtils.EvaluateHackNormal(time, points[segmentIndex * 3], points[segmentIndex * 3 + 1], points[segmentIndex * 3 + 2], points[segmentIndex * 3 + 3], out _);
	}
}