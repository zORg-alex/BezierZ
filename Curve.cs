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
			points = new List<BezierPoint> {
				new BezierPoint(Vector3.zero, BezierPoint.Type.Control),
				new BezierPoint(Vector3.right / 3, BezierPoint.Type.RightHandle),
				new BezierPoint(Vector3.right/3 *2, BezierPoint.Type.LeftHandle),
				new BezierPoint(Vector3.right, BezierPoint.Type.Control),
			};
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
				points.Insert(0, new BezierPoint(getHandlePosition(0, 1), BezierPoint.Type.LeftHandle, points[0].mode));
				points.Add(new BezierPoint(getHandlePosition(lastPointInd, lastPointInd - 1), BezierPoint.Type.RightHandle, points[lastPointInd].mode));
				SetPoint(1, points[1].point);
				SetPoint(lastPointInd - 1, points[lastPointInd - 1].point);
				_bVersion++;

				Vector3 getHandlePosition(int ind, int otherind) {
					Vector3 r = points[ind].point * 2f - points[otherind].point;
					return r;
				}
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
					var i = (points.Count + index - 1) % points.Count;
					points[i] = points[i].SetPosition(isLinear ? GetLinearHandle(i) : points[i] + diff);
				}
				if (IsClosed || index < lastPointInd)
				{
					var i = (index + 1) % points.Count;
					points[i] = points[i].SetPosition(isLinear ? GetLinearHandle(i) : points[i] + diff);
				}
				//Move adjacent linar handles
				if (thisPoint.mode == BezierPoint.Mode.Linear)
				{
					var i = (points.Count + index - 2) % points.Count;
					if (points[i].mode == BezierPoint.Mode.Linear && (IsClosed || index > 0))
					{
						points[i] = points[i].SetPosition(isLinear ? GetLinearHandle(i) : points[i] + diff);
					}
					i = (index + 2) % points.Count;
					if (points[i].mode == BezierPoint.Mode.Linear && (IsClosed || index < lastPointInd))
					{
						points[i] = points[i].SetPosition(isLinear ? GetLinearHandle(i) : points[i] + diff);
					}
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
		public void SetCPRotationWithHandles(int segmentIndex, Quaternion rotation, bool additive = false, int index = -1, bool isRotationAlligned = true)
		{
			if (index == -1)
				index = GetPointIndex(segmentIndex);
			BezierPoint point = points[index];

			if (!isRotationAlligned)
				rotation = Quaternion.LookRotation(point.rotation * Vector3.forward, rotation * Vector3.up);

			var deltaRotation = additive? rotation : rotation * point.rotation.Inverted();
			if (additive)
				rotation *= point.rotation.normalized;

			//Fix any additive error
			points[index] = point.SetRotation(Quaternion.LookRotation(GetCPTangent(segmentIndex), rotation * Vector3.up));
			
			_bVersion++;

			if (!isRotationAlligned || point.mode == BezierPoint.Mode.Linear) return;
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

		public Vector3[] Segment(int segmentIndex) => IsClosed ?
			new Vector3[] { points[segmentIndex * 3 + 1], points[segmentIndex * 3 + 2], points[(segmentIndex * 3 + 3) % points.Count], points[(segmentIndex * 3 + 4) % points.Count] } :
			new Vector3[] { points[segmentIndex * 3], points[segmentIndex * 3 + 1], points[segmentIndex * 3 + 2], points[segmentIndex * 3 + 3] };

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
				var rot = Quaternion.identity;
				if (type == BezierPoint.Type.Control)
				{
					//Skip to current segmant and find closest point to get rotation from
					var firstVInd = _vertexData.SegmentIndexes.IndexOf(segmentInd);
					var min = _vertexData.Points.Skip(firstVInd).Min((v) => newSegments[i].DistanceTo(v), out var ind);
					rot = _vertexData.Rotations[firstVInd + ind];
				}
				newPoints[i] = new BezierPoint(newSegments[i], rot, type, BezierPoint.Mode.Proportional);
				type++;
				type = (BezierPoint.Type)((int)type % 3);
			}

			int index = GetPointIndex(segmentInd);
			if (IsClosed && segmentInd >= SegmentCount - replaceCount) {
				points.RemoveRange(index, replaceCount * 3 - 1);
				points.AddRange(newPoints.Take(newPoints.Length - 2));
				points[0] = newPoints[newPoints.Length - 2];
				points[1] = newPoints[newPoints.Length - 1];
			}
			else
			{
				points.RemoveRange(index, replaceCount * 3 + 1);
				points.InsertRange(index, newPoints);
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

		public BezierCurveVertexData _vertexData;
		public IEnumerable<BezierCurveVertexData.VertexData> VertexData { get {
				if (!vertexCurveIsUpToDate) Update();
				return _vertexData.GetEnumerable();
			} }

		public Vector3[] Vertices { get {
				if (!vertexCurveIsUpToDate) Update();
				return _vertexData.Points;
			} }

		public float VertexDataLength => _vertexData.CumulativeLengths[_vertexData.CumulativeLengths.Length - 1];

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
			var t = GetClosestTimeSegment(point, out var segmentIndex);

			SplitAt(segmentIndex, t);
		}

		public void SplitAt(int segmentIndex, float t)
		{
			var newSegments = CasteljauUtility.GetSplitSegmentPoints(t, Segment(segmentIndex));

			ReplaceSegment(segmentIndex, newSegments);
		}

		//TODO Debug how to work with low vertex count
		public float GetClosestTimeSegment(Vector3 position, out int segmentInd)
		{
			Update();
			var minDist = float.MaxValue;
			var closestTime = float.MaxValue;
			segmentInd = -1;

			var prevVert = VertexData.FirstOrDefault();

			var i = 0;
			foreach (var v in VertexData.Skip(1))
			{
				Vector3 direction = (v.point - prevVert.point);
				float magMax = direction.magnitude;
				var normDirection = direction.normalized;
				Vector3 localPosition = position - prevVert.point;
				var dot = Mathf.Clamp(Vector3.Dot(normDirection, localPosition), 0, magMax);
				var point = prevVert.point + normDirection * dot;
				var dist = Vector3.Distance(point, position);
				if (dist < minDist)
				{
					minDist = dist;
					segmentInd = v.segmentIndex;
					closestTime = prevVert.time + (v.time - prevVert.time) * (localPosition.magnitude / direction.magnitude);
				}

				i++;
				prevVert = v;
			}

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

		public Quaternion GetRotationAtTime(float time)
		{
			Update();
			return _vertexData.GetRotationAtTime(time);
		}

		public Quaternion GetRotationAtLength(float length)
		{
			Update();
			return _vertexData.GetRotationAtLength(length);
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

		public Curve Copy()
		{
			Curve curve = new Curve();
			curve.points = points;
			curve._isClosed = _isClosed;
			curve._maxAngleError = _maxAngleError;
			curve._minSamplingDistance = _minSamplingDistance;
			curve._useRotations = _useRotations;
			curve._vertexData = _vertexData;
			return curve;
		}

		public void CopyFrom(Curve curve)
		{
			points = curve.points;
			_isClosed = curve._isClosed;
			_maxAngleError = curve._maxAngleError;
			_minSamplingDistance = curve._minSamplingDistance;
			_useRotations = curve._useRotations;
			_vertexData = curve._vertexData;
		}
	}
}