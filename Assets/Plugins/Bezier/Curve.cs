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
		public Curve()
		{
			points = new List<BezierPoint> { new BezierPoint(Vector3.zero, BezierPoint.Type.Control) };
			_bVersion = 1;
		}

		//===== Bezier Curve =====
		//Don't need to serialize versions. Generated data won't be serialized
		private int _bVersion = 0;
		[SerializeField, HideInInspector]
		private List<BezierPoint> points;
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
		public List<BezierPoint> Points => points;

		[SerializeField]
		private bool _isClosed;
		public bool IsClosed { get => _isClosed; set { CloseCurve(value); _bVersion++; } }
		private Curve.BezierPoint.Mode[] _preservedNodeModesWhileClosed = new BezierPoint.Mode[2];

		internal bool _useRotations;

		public void CloseCurve(bool value)
		{
			if (value && !_isClosed) {
				_preservedNodeModesWhileClosed[0] = points[0].mode;
				_preservedNodeModesWhileClosed[1] = points[lastPointInd].mode;
				points[0] = points[0].SetMode(BezierPoint.Mode.Manual);
				points[lastPointInd] = points[lastPointInd].SetMode(BezierPoint.Mode.Manual);
				points.Insert(0, new BezierPoint(points[0].point * 2f - points[1].point, BezierPoint.Type.LeftHandle, BezierPoint.Mode.Manual));
				points.Add(new BezierPoint(points[lastPointInd].point * 2f - points[lastPointInd - 1].point, BezierPoint.Type.RightHandle, BezierPoint.Mode.Manual));
				_bVersion++;
			}
			else if (!value && _isClosed) {
				points.RemoveAt(0);
				points.RemoveAt(lastPointInd);
				points[0] = points[0].SetMode(_preservedNodeModesWhileClosed[0]);
				points[lastPointInd] = points[lastPointInd].SetMode(_preservedNodeModesWhileClosed[1]);
				_bVersion++;
			}
			_isClosed = value;
		}

		public int ControlPointCount => points.Count / 3 + 1;
		public int SegmentCount => (points?.Count ?? 0) / 3;

		public int GetPointIndex(int segmentIndex) =>
			segmentIndex.Min(SegmentCount) * 3 + (IsClosed ? 1 : 0);
		public int GetSegmentIndex(int index) => IsClosed ?
			((points.Count + (index - 1)) % points.Count / 3f).FloorToInt() :
			((points.Count + index) % points.Count / 3f).FloorToInt();
		public bool IsControlPoint(int index) =>
			index < 0 || index >= points.Count ? false :
			points[index].type == BezierPoint.Type.Control;


		public void SetPoint(int index, Vector3 position)
		{
			index = Mathf.Clamp(index, 0, lastPointInd);

			var thisPoint = points[index];
			if (thisPoint.type == BezierPoint.Type.Control)
			{
				var diff = position - points[index];
				var isLinear = thisPoint.mode == BezierPoint.Mode.Zero;
				if (index > 0)
					points[index - 1] = points[index - 1].SetPosition(isLinear ? points[index] : points[index - 1] + diff);
				if (index < lastPointInd)
					points[index + 1] = points[index + 1].SetPosition(isLinear ? points[index] : points[index + 1] + diff);
			}
			else
			{
				var controlPoint = points[index].type == BezierPoint.Type.LeftHandle ? points[index + 1] : points[index - 1];
				var otherHandleIndex = points[index].type == BezierPoint.Type.LeftHandle ? index + 2 : index - 2;

				if (controlPoint.mode.HasFlag(BezierPoint.Mode.Automatic))
				{
					var otherHandle = points[otherHandleIndex];

					if (controlPoint.mode.HasFlag(BezierPoint.Mode.Manual))
					{
						//Proportional
						var diff = position - controlPoint;
						points[index] = points[index].SetPosition(controlPoint - (otherHandle - controlPoint).normalized * diff.magnitude);
						_bVersion++;
						return;
					}
					else
					{
						//Automatic
						if (index > 1 && index < lastPointInd - 1)
							points[otherHandleIndex] = otherHandle.SetPosition(controlPoint + controlPoint - position);
					}
				}
				else if (controlPoint.mode.HasFlag(BezierPoint.Mode.Zero))
				{
					points[index] = points[index].SetPosition(controlPoint);
					_bVersion++;
					return;
				}
			}
			points[index] = points[index].SetPosition(position);
			_bVersion++;
		}

		public void SetPointMode(int index, BezierPoint.Mode mode)
		{
			index = Mathf.Clamp(index, 0, lastPointInd);
			if (_isClosed && (index == 0 || index == lastPointInd))
				_preservedNodeModesWhileClosed[Mathf.Clamp(index, 0, 1)] = mode;
			else
				points[index] = points[index].SetMode(mode);

			if (points[index].type == BezierPoint.Type.Control)
			{
				if (index > 0)
					points[index - 1] = points[index - 1].SetMode(mode);
				if (index < lastPointInd)
					points[index + 1] = points[index + 1].SetMode(mode);
			}

			_bVersion++;
		}

		/// <summary>
		/// Set point angle in object-space relative to Vector3.up
		/// </summary>
		/// <param name="index"></param>
		/// <param name="rotation"></param>
		public void SetCPRotation(int segmentIndex, Quaternion rotation)
		{
			var index = GetPointIndex(segmentIndex);
			var tang = GetTangent(segmentIndex, 0);
			//Get euler z value from rotation relative default tangent look rotation
			var pointDefaultRotation = Quaternion.LookRotation(tang);
			var adjustedRotation = Quaternion.LookRotation(tang, rotation * Vector3.up);
			var a = (pointDefaultRotation.Inverted() * adjustedRotation).eulerAngles.z;

			_useRotations = true;
			var newPoint = points[index].SetRotation(a);
			if (!points[index].Equals(newPoint))
			{
				points[index] = newPoint;
				_bVersion++;
			}
		}

		/// <summary>
		/// Rotate point angle and handles
		/// </summary>
		/// <param name="segmentIndex"></param>
		/// <param name="deltaRotation"></param>
		public void RotateCPWithHandles(int segmentIndex, Quaternion deltaRotation)
		{
			var deltaEuler = deltaRotation.eulerAngles;
			var index = GetPointIndex(segmentIndex);
			var tang = GetTangent(segmentIndex, 0);
			//Get euler z value from rotation relative default tangent look rotation
			var pointDefaultRotation = Quaternion.LookRotation(tang);
			var adjustedRotation = Quaternion.LookRotation(tang, deltaRotation * Vector3.up);
			var a = (pointDefaultRotation.Inverted() * adjustedRotation).eulerAngles.z;
			_useRotations = true;
			var newPoint = points[index].SetRotation(points[index].angle + a);
			if (!points[index].Equals(newPoint))
			{
				points[index] = newPoint;
				_bVersion++;
			}

			if (deltaEuler.x == 0 && deltaEuler.y == 0) return;

			//Rotate handles if rotation is other than z azis
			var point = points[index];
			if (index > 0)
			{
				var leftHandle = points[index - 1];
				points[index - 1] = leftHandle.SetPosition(point + deltaRotation * (leftHandle - point));
			}
			if (index < lastPointInd)
			{
				var rightHandle = points[index + 1];
				points[index + 1] = rightHandle.SetPosition(point + deltaRotation * (rightHandle - point));
			}
			_bVersion++;
		}

		public Quaternion GetCPRotation(int segmentIndex)
		{
			var index = GetPointIndex(segmentIndex);
			return Quaternion.LookRotation(GetTangent(segmentIndex, 0)) * Quaternion.Euler(0, 0, points[index].angle);
		}

		public Quaternion GetRotation(int segmentIndex, float t)
		{
			if (segmentIndex == SegmentCount)
				return _vertexData.Rotations.Last();
			else
				return _vertexData.GetRotation(_vertexData.SegmentIndexes.IndexOf(segmentIndex));
		}

		public IEnumerable<Vector3[]> Segments { get {
				for (int i = 0; i < SegmentCount; i++)
					yield return Segment(i);
			}
		}

		public Vector3[] Segment(int index) => IsClosed ?
			new Vector3[] { points[index * 3 + 1], points[index * 3 + 2], points[(index * 3 + 3) % points.Count], points[(index * 3 + 4) % points.Count] } :
			new Vector3[] { points[index * 3], points[index * 3 + 1], points[index * 3 + 2], points[index * 3 + 3] };

		public BezierPoint.Mode DefaultAddedPointMode { get; set; }

		public void AddPointAtEnd(Vector3 point) {
			BezierPoint[] addedPoints = new BezierPoint[3];
			if (DefaultAddedPointMode == BezierPoint.Mode.Zero)
			{
				var leftHandle = (points[lastPointInd] - point) / 3f;
				addedPoints[0] = BezierPoint.RightHandle(points[lastPointInd] + leftHandle);
				addedPoints[1] = BezierPoint.LeftHandle(point - leftHandle);
			}
			else
			{
				var invertedHandleOffset = points[lastPointInd] - points[lastPointInd - 1];
				addedPoints[0] = BezierPoint.RightHandle(invertedHandleOffset + points[lastPointInd]);
				addedPoints[1] = BezierPoint.LeftHandle(getSecondHandle(points[lastPointInd], point, invertedHandleOffset));
			}
			addedPoints[2] = BezierPoint.Control(point);
			points.InsertRange(points.Count, addedPoints);
			_bVersion++;

			Vector3 getSecondHandle(Vector3 lastPoint, Vector3 newPoint, Vector3 offset) =>
				lastPoint + Vector3.Reflect(lastPoint - newPoint, offset);
		}
		public void AddPointAtStart(Vector3 point)
		{
			BezierPoint[] addedPoints = new BezierPoint[3];
			addedPoints[0] = BezierPoint.Control(point);
			if (points.Count == 1 || DefaultAddedPointMode == BezierPoint.Mode.Zero)
			{
				var rightHandle = (points[0] - point) / 3f;
				addedPoints[1] = BezierPoint.RightHandle(point - rightHandle);
				addedPoints[2] = BezierPoint.LeftHandle(points[0] + rightHandle);
			}
			else
			{
				var invertedHandleOffset = points[0] - points[1];
				addedPoints[1] = BezierPoint.RightHandle(getSecondHandle(points[0], point, invertedHandleOffset));
				addedPoints[2] = BezierPoint.LeftHandle(invertedHandleOffset + points[0]);
			}
			points.InsertRange(0, addedPoints);
			_bVersion++;

			Vector3 getSecondHandle(Vector3 lastPoint, Vector3 newPoint, Vector3 offset) =>
				lastPoint + Vector3.Reflect(lastPoint - newPoint, offset);
		}

		public void SetInitialPoints(Vector3[] points)
		{
			int newLength = points.Length - (points.Length % 3) + 1;
			this.points = new List<BezierPoint>(newLength);

			var newPoints = new BezierPoint[newLength];
			var type = IsClosed ? BezierPoint.Type.LeftHandle : BezierPoint.Type.Control;
			for (int i = 0; i < newLength; i++)
			{
				newPoints[i] = new BezierPoint(points[i], type);
				type++;
				type = (BezierPoint.Type)((int)type % 3);
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

			var newPoints = new BezierPoint[newSegments.Length];
			var type = BezierPoint.Type.Control;
			for (int i = 0; i < newSegments.Length; i++)
			{
				newPoints[i] = new BezierPoint(newSegments[i], type);
				type++;
				type = (BezierPoint.Type)((int)type % 3);
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

		public Quaternion GetRotation(float time)
		{
			Update();
			return _vertexData.GetRotationAtTime(time);
		}

		/// <summary>
		/// if segmentIndex is out of bounds, returns last point
		/// </summary>
		/// <param name="segmentIndex"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector3 GetTangent(int segmentIndex, float time)
		{
			Update();
			int index = GetPointIndex(segmentIndex);
			return segmentIndex < SegmentCount ?
			CurveUtils.EvaluateDerivative(time, points[index], points[index + 1], points[(index + 2)%points.Count], points[(index + 3)%points.Count]) :
			CurveUtils.EvaluateDerivative(1, points[index - 3], points[index - 2], points[index - 1], points[index]);
		}

		/// <summary>
		/// if segmentIndex is out of bounds, returns last point
		/// </summary>
		/// <param name="segmentIndex"></param>
		/// <param name="time"></param>
		/// <returns></returns>
		public Vector3 GetNormal(int segmentIndex, float time)
		{
			Update();
			return segmentIndex < SegmentCount ?
			CurveUtils.EvaluateHackNormal(time, points[segmentIndex * 3], points[segmentIndex * 3 + 1], points[segmentIndex * 3 + 2], points[segmentIndex * 3 + 3], out _) :
			CurveUtils.EvaluateHackNormal(1, points[segmentIndex * 3 - 3], points[segmentIndex * 3 - 2], points[segmentIndex * 3 - 1], points[segmentIndex * 3], out _);
		}

	}
}