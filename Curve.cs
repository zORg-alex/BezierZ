using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Diagnostics;

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
		public bool IsClosed { [DebuggerStepThrough] get => _isClosed; [DebuggerStepThrough] set { CloseCurve(value); _bVersion++; } }
		private Curve.BezierPoint.Mode[] _preservedNodeModesWhileClosed = new BezierPoint.Mode[2];

		[SerializeField]
		internal bool _useRotations;
		public bool UseRotations { [DebuggerStepThrough] get => _useRotations; [DebuggerStepThrough] set { _useRotations = value; _bVersion++; } }

		public void CloseCurve(bool value)
		{
			if (value && !_isClosed)
			{
				_isClosed = value;
				//_preservedNodeModesWhileClosed[0] = points[0].mode;
				//_preservedNodeModesWhileClosed[1] = points[lastPointInd].mode;
				//points[0] = points[0].SetMode(BezierPoint.Mode.Manual);
				//points[lastPointInd] = points[lastPointInd].SetMode(BezierPoint.Mode.Manual);
				points.Insert(0, new BezierPoint(points[0].point/* * 2f - points[1].point*/, BezierPoint.Type.LeftHandle, points[0].mode));
				points.Add(new BezierPoint(points[lastPointInd].point/* * 2f - points[lastPointInd - 1].point*/, BezierPoint.Type.RightHandle, points[lastPointInd].mode));
				SetPoint(1, points[1].point);
				SetPoint(lastPointInd - 1, points[lastPointInd - 1].point);
				_bVersion++;
			}
			else if (!value && _isClosed)
			{
				_isClosed = value;
				points.RemoveAt(0);
				points.RemoveAt(lastPointInd);
				points[0] = points[0].SetMode(_preservedNodeModesWhileClosed[0]);
				points[lastPointInd] = points[lastPointInd].SetMode(_preservedNodeModesWhileClosed[1]);
				_bVersion++;
			}
		}

		public int ControlPointCount { [DebuggerStepThrough] get => points.Count / 3; }

		public int SegmentCount { [DebuggerStepThrough] get => (points?.Count ?? 0) / 3; }

		[DebuggerStepThrough]
		public int GetPointIndex(int segmentIndex) =>
			segmentIndex % SegmentCount * 3 + (IsClosed ? 1 : 0);

		[DebuggerStepThrough]
		public int GetSegmentIndex(int index) => IsClosed ?
			((points.Count + (index - 1)) % points.Count / 3f).FloorToInt() :
			((points.Count + index) % points.Count / 3f).FloorToInt();

		[DebuggerStepThrough]
		public bool IsControlPoint(int index) =>
			index < 0 || index >= points.Count ? false :
			points[index].type == BezierPoint.Type.Control;

		private Vector3 GetLinearHandle(int index)
		{
			int segmentIndex = GetSegmentIndex(index);
			int aind = GetPointIndex(segmentIndex);
			var a = points[aind];
			var b = points[aind + 3 < points.Count ? aind + 3 : GetPointIndex(segmentIndex + 1)];
			var isRight = index == GetPointIndex(segmentIndex) + 1;
			var h = points[(points.Count + index + (isRight ? 1 : -1)) % points.Count];
			var diff = a - b;
			var tang = isRight ? h - a : h - b;
			return (isRight ? a : b) + tang.normalized * diff.magnitude * .1f;
		}
		public void SetPoint(int index, Vector3 position)
		{
			index = Mathf.Clamp(index, 0, lastPointInd);

			var thisPoint = points[index];
			if (thisPoint.type == BezierPoint.Type.Control)
			{
				var diff = position - points[index];
				var isLinear = thisPoint.mode == BezierPoint.Mode.Linear;
				points[index] = thisPoint.SetPosition(position).SetTangent(index < lastPointInd ? points[index] - points[index + 1] : points[index - 1] - points[index]);
				if (IsClosed || index > 0)
				{
					var i = index == 0 ? points.Count : index - 1;
					points[i] = points[i].SetPosition(isLinear ? GetLinearHandle(i) : points[i] + diff);
				}
				if (IsClosed || index < lastPointInd)
				{
					var i = index == points.Count ? 0 : index + 1;
					points[i] = points[i].SetPosition(isLinear ? GetLinearHandle(i) : points[i] + diff);
				}
			}
			else
			{
				var controlPoint = points[index].type == BezierPoint.Type.LeftHandle ? points[index + 1] : points[index - 1];
				var otherHandleIndex = ((points[index].type == BezierPoint.Type.LeftHandle ? index + 2 : index - 2) + points.Count) % points.Count;

				if (controlPoint.mode.HasFlag(BezierPoint.Mode.Automatic))
				{
					var otherHandle = points[otherHandleIndex];

					if (controlPoint.mode.HasFlag(BezierPoint.Mode.Manual))
					{
						//Proportional
						var diff = position - controlPoint;
						points[index] = points[index].SetPosition(position);
						if (IsClosed || (index > 1 && index < lastPointInd - 1))
							points[otherHandleIndex] = otherHandle.SetPosition(controlPoint - diff * ((otherHandle - controlPoint).magnitude / diff.magnitude));
					}
					else
					{
						//Automatic, edit both handles mirrored
						points[index] = points[index].SetPosition(position);
						if (IsClosed || (index > 1 && index < lastPointInd - 1))
							points[otherHandleIndex] = otherHandle.SetPosition(controlPoint + controlPoint - position);
					}
				}
				else if (controlPoint.mode.HasFlag(BezierPoint.Mode.Linear))
				{
					points[index] = points[index].SetPosition(GetLinearHandle(index));
				}
				else
					points[index] = points[index].SetPosition(position);
			}
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
		/// Rotate point angle and handles
		/// </summary>
		/// <param name="segmentIndex"></param>
		/// <param name="rotation"></param>
		public void SetCPRotationWithHandles(int segmentIndex, Quaternion rotation, bool additive = false, int index = -1)
		{
			if (index == -1)
				index = GetPointIndex(segmentIndex);
			BezierPoint point = points[index];
			var deltaRotation = additive? rotation : rotation * point.rotation.Inverted();
			if (additive)
				rotation *= point.rotation.normalized;

			//Fix any additive error
			points[index] = point.SetRotation(Quaternion.LookRotation(GetCPTangent(segmentIndex), rotation * Vector3.up));
			
			_bVersion++;

			//Rotate handle positions and set rotations
			if (index > 0)
			{
				var leftHandle = points[index - 1];
				BezierPoint v = leftHandle.SetPosition(point + deltaRotation * (leftHandle - point));
				if (point.mode.HasFlag(BezierPoint.Mode.Automatic))
					v.SetRotation(point.rotation);
				else
					v.SetRotation(Quaternion.LookRotation(point - v, point.rotation * Vector3.up));
				points[index - 1] = v;
			}
			if (index < lastPointInd)
			{
				var rightHandle = points[index + 1];
				BezierPoint v = rightHandle.SetPosition(point + deltaRotation * (rightHandle - point));
				if (point.mode.HasFlag(BezierPoint.Mode.Automatic))
					v.SetRotation(point.rotation);
				else
					v.SetRotation(Quaternion.LookRotation(v - point, point.rotation * Vector3.up));
				points[index + 1] = v;
			}
		}

		public Quaternion GetCPRotation(int segmentIndex)
		{
			var index = GetPointIndex(segmentIndex);
			return points[index].GetRotation(GetCPTangent(segmentIndex));
		}

		public Vector3 GetCPTangent(int segmentIndex)
		{
			var index = GetPointIndex(segmentIndex);
			var point = points[index];
			var isZero = point.mode == BezierPoint.Mode.Linear;
			var nextPoint = IsClosed ? points[(index + (isZero ? 2 : 1)) % points.Count] : index < points.Count ? points[(index + (isZero ? 2 : 1))] : index > 0 ? point + point - points[index - 1] : point;
			return (point.mode.HasFlag(BezierPoint.Mode.Automatic)) ? getAutoTangent() : getAvgTangent();

			Vector3 getAutoTangent()
			{
				return (nextPoint - point).normalized;
			}

			Vector3 getAvgTangent()
			{
				var prevPoint = IsClosed ? points[(points.Count + index - (isZero ? 2 : 1)) % points.Count] : point;
				return (nextPoint - prevPoint).normalized;
			}
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
			if (DefaultAddedPointMode == BezierPoint.Mode.Linear)
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
			if (points.Count == 1 || DefaultAddedPointMode == BezierPoint.Mode.Linear)
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
			if (!IsClosed && index == 0)
				points.RemoveRange(0, 3);
			else if (!IsClosed && index == lastPointInd)
				points.RemoveRange(points.Count + (IsClosed ? - 3 : - 3), 3);
			else
			{
				//Cancel if not a control point
				if (!IsControlPoint(index)) return;
			
				DissolveCP(GetSegmentIndex(index));
			}
			_bVersion++;
		}

		public void DissolveCP(int segmentIndex)
		{
			if (segmentIndex <= 0 && segmentIndex >= SegmentCount) return;

			var s = Segments.Skip(segmentIndex - 1).Take(2);
			var segment = CasteljauUtility.JoinSegments(s);

			ReplaceSegments(segmentIndex - 1, 2, segment);
		}

		public void RemoveMany(IEnumerable<int> indexes)
		{
			foreach (var index in indexes.Where(i=>IsControlPoint(i)).OrderByDescending(i=>i))
			{
				if (points.Count < 4)
				{
					_bVersion++;
					return;
				}
				else if (index == 0 + (IsClosed ? 1 : 0))
					points.RemoveRange(0, 3);
				else if (index == lastPointInd - (IsClosed ? 1 : 0))
					points.RemoveRange(points.Count + (IsClosed ? -3 : -3), 3);
				else
				{
					//Cancel if not a control point
					if (!IsControlPoint(index)) return;
					//First just remove that point
					points.RemoveRange(index - 1, 3);

				}
			}
			_bVersion++;
		}


		private void ReplaceSegment(int segmentInd, Vector3[] newSegments) => ReplaceSegments(segmentInd, 1, newSegments);
		private void ReplaceSegments(int segmentInd, int replaceCount, Vector3[] newSegments)
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

			if (IsClosed && segmentInd >= SegmentCount - replaceCount) {
				points.RemoveRange(GetPointIndex(segmentInd), replaceCount * 3 - 1);
				points.AddRange(newPoints.Take(newPoints.Length - 2));
				points[0] = newPoints[newPoints.Length - 2];
				points[1] = newPoints[newPoints.Length - 1];
			}
			else
			{
				points.RemoveRange(GetPointIndex(segmentInd), replaceCount * 3 + 1);
				points.InsertRange(GetPointIndex(segmentInd), newPoints);
			}

			_bVersion++;
		}

		//===== Vertex Curve =====
		[NonSerialized]
		private int _vVersion = 0;
		private bool vertexCurveIsUpToDate => _vVersion == _bVersion;
		[SerializeField, HideInInspector, Min(.01f)]
		private float _minSamplingDistance;
		public float MinSamplingDistance { get { return _minSamplingDistance; } set { var v = value.Min(.01f); if (v != _minSamplingDistance) _vVersion++; _minSamplingDistance = v; } }
		[SerializeField, HideInInspector, Min(.05f)]
		private float _maxAngleError;
		public float MaxAngleError { get { return _maxAngleError; } set { var v = value.Min(.05f); if (v != _maxAngleError) _vVersion++; _maxAngleError = v; } }

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
			_vertexData = new BezierCurveVertexData(this, _minSamplingDistance, _maxAngleError, _useRotations);
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
			if (SegmentCount == 0) return Vector3.forward;
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
			if (SegmentCount == 0) return Vector3.right;
			return segmentIndex < SegmentCount ?
			CurveUtils.EvaluateHackNormal(time, points[segmentIndex * 3], points[segmentIndex * 3 + 1], points[segmentIndex * 3 + 2], points[segmentIndex * 3 + 3], out _) :
			CurveUtils.EvaluateHackNormal(1, points[segmentIndex * 3 - 3], points[segmentIndex * 3 - 2], points[segmentIndex * 3 - 1], points[segmentIndex * 3], out _);
		}

	}
}