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
		internal int lastPointInd => points.Count - 1;

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
				points[0] = points[0].SetMode(Point.Mode.Manual);
				points[lastPointInd] = points[lastPointInd].SetMode(Point.Mode.Manual);
			}
			else if (!value && _isClosed) {
				points[0] = points[0].SetMode(_preservedNodeModesWhileClosed[0]);
				points[lastPointInd] = points[lastPointInd].SetMode(_preservedNodeModesWhileClosed[1]);
			}
			_isClosed = value;
		}

		public int ControlPointCount => points.Count / 3 + 1;
		public int SegmentCount => (points?.Count ?? 0) / 3 + (_isClosed ? 1 : 0);

		public void SetPoint(int index, Vector3 position)
		{
			index = Mathf.Clamp(index, 0, lastPointInd);

			var thisPoint = points[index];
			if (thisPoint.type == Point.Type.Control)
			{
				var diff = position - points[index];
				var isLinear = thisPoint.mode == Point.Mode.Linear;
				if (index > 0)
					points[index - 1] = points[index - 1].SetPosition(isLinear ? points[index] : points[index - 1] + diff);
				if (index < lastPointInd)
					points[index + 1] = points[index + 1].SetPosition(isLinear ? points[index] : points[index + 1] + diff);
			}
			else
			{
				var controlPoint = points[index].type == Point.Type.LeftHandle ? points[index + 1] : points[index - 1];
				var otherHandleIndex = points[index].type == Point.Type.LeftHandle ? index + 2 : index - 2;

				if (controlPoint.mode.HasFlag(Point.Mode.Automatic) && index > 1 && index < lastPointInd - 1)
				{
					var otherHandle = points[otherHandleIndex];

					if (controlPoint.mode.HasFlag(Point.Mode.Manual))
					{
						//Proportional
						var diff = position - thisPoint;
						var otherRelToControl = (controlPoint - position) * ((controlPoint - otherHandle).magnitude / (controlPoint - thisPoint).magnitude);
						if (index > 1 && index < lastPointInd - 1)
							points[otherHandleIndex] = otherHandle.SetPosition(controlPoint + otherRelToControl);
					}
					else
					{
						//Automatic
						if (index > 1 && index < lastPointInd - 1)
							points[otherHandleIndex] = otherHandle.SetPosition(controlPoint + controlPoint - position);
					}
				}
			}
			points[index] = points[index].SetPosition(position);
			_bVersion++;
		}

		public void SetPointMode(int index, Point.Mode mode)
		{
			index = Mathf.Clamp(index, 0, lastPointInd);
			if (_isClosed && (index == 0 || index == lastPointInd))
				_preservedNodeModesWhileClosed[Mathf.Clamp(index, 0, 1)] = mode;
			else
				points[index] = points[index].SetMode(mode);

			if (points[index].type == Point.Type.Control)
			{
				if (index > 0)
					points[index - 1] = points[index - 1].SetMode(mode);
				if (index < lastPointInd)
					points[index + 1] = points[index + 1].SetMode(mode);
			}

			_bVersion++;
		}

		public IEnumerable<Vector3[]> Segments { get {
				for (int i = 0; i < SegmentCount; i++)
					yield return new Vector3[] { points[i*3], points[i * 3 + 1], points[i * 3 + 2], points[i * 3 + 3] };
			}
		}

		public Vector3[] Segment(int index) =>
			new Vector3[] { points[index * 3], points[index * 3 + 1], points[index * 3 + 2], points[index * 3 + 3] };

		public Point.Mode DefaultAddedPointMode { get; set; }

		public void AddPointAtEnd(Vector3 point) {
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
			points.InsertRange(points.Count, addedPoints);
			_bVersion++;

			Vector3 getSecondHandle(Vector3 lastPoint, Vector3 newPoint, Vector3 offset) =>
				lastPoint + Vector3.Reflect(lastPoint - newPoint, offset);
		}
		public void AddPointAtStart(Vector3 point)
		{
			Point[] addedPoints = new Point[3];
			addedPoints[0] = Point.Control(point);
			if (DefaultAddedPointMode == Point.Mode.Linear)
			{
				var rightHandle = (points[0] - point) / 3f;
				addedPoints[1] = Point.RightHandle(point - rightHandle);
				addedPoints[2] = Point.LeftHandle(points[0] + rightHandle);
			}
			else
			{
				var invertedHandleOffset = points[0] - points[1];
				addedPoints[1] = Point.RightHandle(getSecondHandle(points[0], point, invertedHandleOffset));
				addedPoints[2] = Point.LeftHandle(invertedHandleOffset + points[0]);
			}
			points.InsertRange(0, addedPoints);
			_bVersion++;

			Vector3 getSecondHandle(Vector3 lastPoint, Vector3 newPoint, Vector3 offset) =>
				lastPoint + Vector3.Reflect(lastPoint - newPoint, offset);
		}

		public void AddInitialPoints(Vector3[] points)
		{
			if (points.Length < 4) return;
			int newLength = points.Length - (points.Length % 3) + 1;
			if (this.points == null)
				this.points = new List<Point>(newLength);
			else if (this.points.Count > 0)
				return;

			var newPoints = new Point[newLength];
			var type = IsClosed ? Point.Type.LeftHandle : Point.Type.Control;
			for (int i = 0; i < newLength; i++)
			{
				newPoints[i] = new Point(points[i], type);
				type++;
				type = (Point.Type)((int)type % 3);
			}

			this.points.AddRange(newPoints);
			_bVersion++;
		}

		public void RemoveAt(int index)
		{
			if (index == 0)
				points.RemoveRange(0, 3);
			else if (index == lastPointInd)
				points.RemoveRange(points.Count - 4, 3);
			else
			{
				//Cancel if not a control point
				if (index > 1 && index % 3 != 0) return;
				//Then compensate neighbouring handles
				var prevCP = index - 3;
				var prevHandle = index - 2;
				var nextHandle = index + 2;
				var nextCP = index + 3;
				points[prevHandle] = points[prevHandle].SetPosition(-points[prevCP].point + points[prevHandle].point * 2f);
				points[nextHandle] = points[nextHandle].SetPosition(-points[nextCP].point + points[nextHandle].point * 2f);
				//First just remove that point
				points.RemoveRange(index - 1, 3);

			}
			_bVersion++;
		}

		private void ReplaceSegment(int segmentInd, Vector3[] newSegments)
		{
			if (newSegments.Length % 3 != 1) return;

			var newPoints = new Point[newSegments.Length];
			var type = Point.Type.Control;
			for (int i = 0; i < newSegments.Length; i++)
			{
				newPoints[i] = new Point(newSegments[i], type);
				type++;
				type = (Point.Type)((int)type % 3);
			}

			points.RemoveRange(segmentInd * 3, 4);
			points.InsertRange(segmentInd * 3, newPoints);

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
		public void SplitAt(Vector3 point)
		{
			var t = GetClosestTimeSegment(point, out var segmentInd);

			var newSegments = CasteljauUtility.GetSplitSegmentPoints(t, Segment(segmentInd));

			ReplaceSegment(segmentInd, newSegments);
		}

		public float GetClosestTimeSegment(Vector3 position, out int segmentInd)
		{
			Update();
			var minDist = float.MaxValue;
			var closestTime = float.MaxValue;
			var closestIndex = -1;
			BezierCurveVertexData.VertexData prevPoint = default;
			foreach (var point in VertexData)
			{
				if (!prevPoint.Equals(default))
				{
					var t = Vector3.Dot((point.point - prevPoint.point), position - prevPoint.point);
					var newPos = Vector3.Lerp(prevPoint.point, point.point, t);
					var dist = newPos.DistanceTo(position);
					if (dist < minDist)
					{
						minDist = dist;
						if (prevPoint.segmentIndex == closestIndex)
							closestTime = Mathf.Lerp(prevPoint.time, point.time, t);
						else
							closestTime = Mathf.Lerp(prevPoint.time, point.time + 1f, t);

						closestIndex = prevPoint.segmentIndex + closestTime.FloorToInt();
						closestTime %= 1f;
					}
				}
				prevPoint = point;
			}
			segmentInd = closestIndex;
			return closestTime;
		}

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