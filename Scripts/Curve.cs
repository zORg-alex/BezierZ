using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BezierCurveZ
{
	/// <summary>
	/// Curve consists of control points grouped in segments. Each Point has local position, rotation and scale. <para/>
	/// 
	/// <see cref="Points"/> contains all control points of a Bezier curve, they are organized in
	/// ep, rh, lh, ep, rh, lh, ep fashion (ep - EndPoint, rh - RightHandle, lh - LeftHandle).
	/// In case of Closed curve, last segment is a closing segment.
	/// Segments are groups of four control points ep, rh, lh, ep where last end point is a next segments first endpoint.
	/// So that in a <see cref="Segments">Segment</see> ep,rh,lh,ep are four points, even though a step is 3.<para/>
	/// 
	/// Ex:Curve with 3 Segments, have 10 points, 3 * 3 + 1;<para/>
	/// 
	/// PropertyDrawer of this class implements an editor, so that this class could be used as a field
	/// or even in an array or list and still be editable in your script's inspector.<para/>
	/// 
	/// For Mesh Generation or cached position/rotation/scale lookups, use <see cref="VertexData"/>
	/// </summary>
	/// TODO Warning if two points are in the same place, it will break interpolation.
	[Serializable]
	public class Curve : EditableClass, ISerializationCallbackReceiver
	{

		public delegate void CurveChanged(Curve curve);
		public event CurveChanged OnCurveChanged;

		[SerializeField]
		private int _bVersion = new System.Random().Next();
		
		[SerializeField]
		internal List<Point> _points;

		public void BumpVersion(bool updateVertexData = false)
		{
			_bVersion++;
			if (updateVertexData)
				UpdateVertexData(force: true);
			OnCurveChanged?.Invoke(this);
		}

		/// <summary>
		/// Points and segments in open curve: {Control, Right, Left}, {Control}
		/// Points and segment in closed curve: {Control, Right, Left},{Control, Right, Left}
		/// </summary>
		public List<Point> Points { [DebuggerStepThrough] get => _points; }
		public IEnumerable<Point> EndPoints
		{
			[DebuggerStepThrough]
			get
			{
				for (int i = 0; i < _points.Count; i += 3)
					yield return _points[i];
			}
		}
		public int LastPointInd { [DebuggerStepThrough] get => _points.Count - 1; }
		public int EndPointCount { [DebuggerStepThrough] get => (_points.Count / 3f).CeilToInt(); }
		public IEnumerable<int> EndPointIndexes
		{
			[DebuggerStepThrough]
			get
			{
				for (int i = 0; i < _points.Count; i += 3)
					yield return i;
			}
		}

		public int SegmentCount { [DebuggerStepThrough] get => (_points.Count / 3f).FloorToInt(); }

		[DebuggerStepThrough]
		public int GetSegmentIndex(int index) => GetSegmentIndex((ushort)index);
		[DebuggerStepThrough]
		public int GetSegmentIndex(ushort index) => (index / 3) % EndPointCount;
		[DebuggerStepThrough]
		public int GetPointIndex(int segmentIndex) => GetPointIndex((ushort)segmentIndex);
		[DebuggerStepThrough]
		public int GetPointIndex(ushort segmentIndex) => segmentIndex * 3 % _points.Count;

		[DebuggerStepThrough]
		public Vector3 GetPointPosition(int index) => _points[index].position;

		[DebuggerStepThrough]
		public bool IsAutomaticHandle(int index) => _points[index].IsAutomatic;

		[DebuggerStepThrough]
		public bool IsEndPoint(int index) => _points[index].IsEndPoint;

		// Cached arrays

		private int _sVersion;
		private Vector3[][] _segments;
		public Vector3[][] Segments
		{
			[DebuggerStepThrough]
			get
			{
				return this.CheckCachedValueVersion(ref _segments, c => getValue(c), ref _sVersion, _bVersion);

				static Vector3[][] getValue(Curve curve)
				{
					Vector3[][] r = new Vector3[curve.SegmentCount][];
					for (int i = 0; i < curve.SegmentCount; i++)
						r[i] = new Vector3[] {
							curve._points[i * 3].position,
							curve._points[i * 3 + 1].position,
							curve._points[i * 3 + 2].position,
							curve._points[(i * 3 + 3) % curve._points.Count].position };
					return r;
				}
			}
		}
		private int _pposVersion;
		private Vector3[] _pointPositions;
		/// <summary>
		/// Cached Point positions
		/// </summary>
		public Vector3[] PointPositions
		{
			[DebuggerStepThrough]
			get => this.CheckCachedValueVersion(ref _pointPositions, t => t._points.SelectArray(p => p.position), ref _pposVersion, _bVersion);
		}

		// { get { if (_pposVersion != _bVersion) _pointPositions = _points.SelectArray(p => p.position); return _pointPositions; } }

		private int _eprotVersion;
		private Quaternion[] _epRotations;
		/// <summary>
		/// Cached Rotations of endpoints
		/// </summary>
		public Quaternion[] EndPointRotations
		{
			[DebuggerStepThrough]
			get => this.CheckCachedValueVersion(ref _epRotations, t => t._points.SelectArray(p => p.rotation), ref _eprotVersion, _bVersion);
		}

		private int _epscalesVersion;
		private Vector3[] _epScales;
		/// <summary>
		/// Cached scales of endpoints
		/// </summary>
		public Vector3[] EndPointScales
		{
			[DebuggerStepThrough]
			get => this.CheckCachedValueVersion(ref _epScales, t => t._points.SelectArray(p => p.scale), ref _epscalesVersion, _bVersion);
		}

		public int PointCount { [DebuggerStepThrough] get => _points.Count; }
		private ushort lastPointInd { [DebuggerStepThrough] get => (ushort)(Points.Count - 1); }

		[SerializeField] internal bool _isClosed;
		public bool IsClosed { [DebuggerStepThrough] get => _isClosed; [DebuggerStepThrough] set { if (value != _isClosed) SetIsClosed(value); } }

		public Curve()
		{
			_points = defaultPoints;
		}

		/// <summary>
		/// Reset curve to default points and clears cache
		/// </summary>
		public void Reset()
		{
			_points = defaultPoints;
			_bVersion = -1;
			_vertexData = null;
			_pointPositions = null;
			_epRotations = null;
			_epScales = null;
			_segments = null;
			_vertexDataPoints = null;
			_interpolationAccuracy = 10;
			_interpolationMaxAngleError = 5;
			_interpolationMinDistance = 0.000001f;
			_interpolationCapmullRomTension = .5f;
			InterpolationMethod = InterpolationMethod.CatmullRomAdditive;
		}

		private static List<Point> defaultPoints
		{
			get
			{
				var rot = Quaternion.LookRotation(Vector3.right);
				return new List<Point> {
				new Point(Vector3.zero, rot, Vector3.one, Point.Type.EndPoint, Point.Mode.Automatic),
				new Point(Vector3.right * .33333f, rot, Vector3.one, Point.Type.Right, Point.Mode.Automatic),
				new Point(Vector3.right * .66666f, rot, Vector3.one, Point.Type.Left, Point.Mode.Automatic),
				new Point(Vector3.right, rot, Vector3.one, Point.Type.EndPoint, Point.Mode.Automatic),
			};
			}
		}

		[SerializeField]
		private Point.Mode[] _preservedNodeModesWhileClosed = new Point.Mode[2];

		public void SetIsClosed(bool value)
		{
			_isClosed = value;
			if (!IsClosed)
			{
				_points.RemoveAt(LastPointInd);
				_points.RemoveAt(LastPointInd);
				_points.RemoveAt(LastPointInd);
				if (_preservedNodeModesWhileClosed.Length == 2)
				{
					_points[0] = _points[0].SetMode(_preservedNodeModesWhileClosed[0]);
					_points[LastPointInd] = _points[LastPointInd].SetMode(_preservedNodeModesWhileClosed[1]);
				}
				BumpVersion();
			}
			else
			{
				_points.Add(new Point(getHandlePosition(LastPointInd, LastPointInd - 1), Point.Type.Right, _points[LastPointInd].mode));
				_points.Add(new Point(getHandlePosition(0, 1), Point.Type.Left, _points[0].mode));
				_points.Add(new Point(Points[0]));
				BumpVersion();

				Vector3 getHandlePosition(int ind, int otherind)
				{
					Vector3 r = _points[ind] * 2f - _points[otherind];
					return r;
				}
			}
		}

		/// <summary>
		/// Updates point position. Used in case point parameters changed that can influence other points.
		/// </summary>
		public void UpdatePosition(int index) => SetPointPosition(index, _points[index]);
		[DebuggerStepThrough]
		public void SetPointPosition(int index, Vector3 position) => SetPointPosition((ushort)index, position, true);
		[DebuggerStepThrough]
		public void SetPointPosition(int index, Vector3 position, bool recursive = true) => SetPointPosition((ushort)index, position, recursive);
		public void SetPointPosition(ushort index, Vector3 position, bool recursive = true)
		{
			var thisPoint = _points[index];
			if (thisPoint.IsEndPoint)
			{
				var diff = position - thisPoint;

				_points[index] = thisPoint.SetPosition(position);
				if (IsClosed && index == lastPointInd)
					_points[0] = _points[lastPointInd];
				if (IsClosed && index == 0)
					_points[lastPointInd] = _points[0];

				var leftIsLinear = false;
				var rightIsLinear = false;
				var leftIsAuto = false;
				var rightIsAuto = false;
				Point leftPoint = default(Point); Point rightPoint = default(Point);
				if (index > 0 || IsClosed)
				{
					var i = GetLeftIndex(index);
					leftIsLinear = _points[i].IsLinear;
					leftIsAuto = _points[i].IsAutomatic;
					Vector3 dir = _points[i] - _points[index];
					if (dir == Vector3.zero) dir = _points[i - 1] - _points[index];
					leftPoint = leftIsLinear ? GetLinearHandle(i) : _points[i].SetPosition(_points[i] + diff).SetRotation(Quaternion.LookRotation(dir));
					_points[i] = leftPoint;
				}
				if (index < lastPointInd || IsClosed)
				{
					var i = GetRightIndex(index);
					rightIsLinear = _points[i].IsLinear;
					rightIsAuto = _points[i].IsAutomatic;
					Vector3 dir = _points[i] - _points[index];
					if (dir == Vector3.zero) dir = _points[i + 1] - _points[index];
					rightPoint = rightIsLinear ? GetLinearHandle(i) : _points[i].SetPosition(_points[i] + diff).SetRotation(Quaternion.LookRotation(dir));
					_points[i] = rightPoint;
				}
				if (rightIsAuto && leftIsLinear)
				{
					_points[GetRightIndex(index)] = rightPoint.SetPosition(-(leftPoint - thisPoint).normalized * (rightPoint - thisPoint).magnitude);
				}
				if (leftIsAuto && rightIsLinear)
					_points[GetLeftIndex(index)] = leftPoint.SetPosition(-(rightPoint - thisPoint).normalized * (leftPoint - thisPoint).magnitude);
			}
			else
			{
				var endpointIndex = thisPoint.isRightHandle ? index - 1 : index + 1;
				var endPoint = _points[endpointIndex];

				int handleDirection = thisPoint.isRightHandle ? 1 : -1;
				var rotationForward = (position - endPoint) * handleDirection;
				//Fix for handles positioned at endpoint position
				if (rotationForward != Vector3.zero)
					rotationForward = (_points[index + handleDirection] - endPoint) * handleDirection;
				_points[endpointIndex] = endPoint.SetRotation(Quaternion.LookRotation(rotationForward, endPoint.up).normalized);

				var otherHandleIndex = thisPoint.isRightHandle ? index - 2 : index + 2;
				bool outOfBounds = (otherHandleIndex < 0 || otherHandleIndex >= PointCount);
				if (outOfBounds)
					otherHandleIndex = thisPoint.isRightHandle ? LastPointInd - 1 : 1;
				var otherHandle = _points[otherHandleIndex];
				if (!outOfBounds || IsClosed)
				{

					if (thisPoint.IsAutomatic && otherHandle.IsAutomatic)
					{
						if (thisPoint.IsManual || otherHandle.IsManual)
						{
							//Proportional
							var diff = position - endPoint;
							//_points[index] = _points[index].SetPosition(position);
							if (diff.sqrMagnitude != 0 && (IsClosed || (index > 1 && index < lastPointInd - 1)))
								_points[otherHandleIndex] = otherHandle.SetPosition(endPoint - diff * ((otherHandle - endPoint).magnitude / diff.magnitude))
									.SetRotation(endPoint.rotation);
						}
						else
						{
							//Automatic, edit both handles mirrored
							//_points[index] = thisPoint.SetPosition(position);
							if (IsClosed || (index > 1 && index < lastPointInd - 1))
								_points[otherHandleIndex] = otherHandle.SetPosition(endPoint + endPoint - position)
									.SetRotation(endPoint.rotation);
						}
					}
				}

				if (thisPoint.IsAutomatic && (!outOfBounds || IsClosed) && otherHandle.IsLinear)
				{
					otherHandle = GetLinearHandle(otherHandleIndex);
					_points[otherHandleIndex] = otherHandle;
					_points[index] = thisPoint.SetPosition(-(otherHandle - endPoint).normalized * (position - endPoint).magnitude);
				}
				else if (!thisPoint.IsLinear)
					_points[index] = thisPoint.SetPosition(position).SetRotation(Quaternion.LookRotation(thisPoint - endPoint));

				var nextHandleIndex = thisPoint.isRightHandle ? index + 1 : index - 1;
				var nextHandle = _points[nextHandleIndex];

				if (nextHandle.IsLinear)
					_points[nextHandleIndex] = GetLinearHandle(nextHandleIndex);
			}
			BumpVersion();

			Point GetLinearHandle(int index)
			{
				int segmentIndex = GetSegmentIndex(index);
				int aind = GetPointIndex(segmentIndex);
				var a = _points[aind];
				var b = _points[aind + 3 < _points.Count ? aind + 3 : GetPointIndex(segmentIndex + 1)];
				var isRight = index == GetPointIndex(segmentIndex) + 1;
				var otherInd = index + (isRight ? 1 : -1);
				Vector3 otherPoint;
				if (_points[otherInd].IsLinear)
				{
					otherInd += isRight ? 1 : -1;
					if (recursive)
						SetPointPosition((ushort)otherInd, _points[otherInd], false);
					otherPoint = _points[otherInd];
				}
				else
					otherPoint = _points[otherInd];
				var diff = a - b;
				var tang = isRight ? otherPoint - a : otherPoint - b;
				var pos = (isRight ? a : b) + tang.normalized * diff.magnitude * .1f;
				return new Point(pos, Quaternion.LookRotation(tang), Vector3.one, isRight ? Point.Type.Right : Point.Type.Left, Point.Mode.Linear);
			}
		}

		/// <summary>
		/// Set EndPoint rotation and rotate handles
		/// </summary>
		public void SetEPRotation(int segmentIndex, Quaternion rotation) => SetEPRotation((ushort)segmentIndex, rotation);
		/// <summary>
		/// Set EndPoint rotation and rotate handles
		/// </summary>
		public void SetEPRotation(ushort segmentIndex, Quaternion rotation)
		{
			var index = GetPointIndex(segmentIndex);
			var delta = _points[index].rotation.Inverted() * rotation;
			_points[index] = _points[index].SetRotation(rotation);
			if (IsClosed && (index == 0 || index == LastPointInd))
				_points[index == 0 ? LastPointInd : 0] = _points[index];

			RotateHandles(index, _points[index], delta, rotation);
			BumpVersion();
		}
		/// <summary>
		/// Add rotation to EndPoint and rotate handles
		/// </summary>
		public void AddEPRotation(int segmentIndex, Quaternion delta) => AddEPRotation((ushort)segmentIndex, delta);
		/// <summary>
		/// Add rotation to EndPoint and rotate handles
		/// </summary>
		public void AddEPRotation(ushort segmentIndex, Quaternion delta)
		{
			var index = GetPointIndex(segmentIndex);
			var rotation = delta * _points[index].rotation;
			var point = _points[index];
			_points[index] = point.SetRotation(Quaternion.LookRotation(GetEPTangentFromPoints(segmentIndex, index), rotation * Vector3.up));
			if (IsClosed && (index == 0 || index == LastPointInd))
				_points[index == 0 ? LastPointInd : 0] = _points[index];

			RotateHandles(index, _points[index], delta, _points[index].rotation);
			BumpVersion();
		}
		/// <summary>
		/// Set EndPoint scale parameter
		/// </summary>
		public void SetEPScale(int segmentIndex, Vector3 scale) => SetEPScale((ushort)segmentIndex, scale);
		/// <summary>
		/// Set EndPoint scale parameter
		/// </summary>
		public void SetEPScale(ushort segmentIndex, Vector3 scale)
		{
			var index = GetPointIndex(segmentIndex);
			_points[index] = _points[index].SetScale(scale);
			if (IsClosed && (index == 0 || index == LastPointInd))
				_points[index == 0 ? LastPointInd : 0] = _points[index];

			BumpVersion();
		}

		private void RotateHandles(int index, Vector3 origin, Quaternion delta, Quaternion rotation)
		{
			DoActionForHandles(index, i =>
			{
				var pos = origin + delta * (_points[i] - origin);
				_points[i] = _points[i].SetPosition(pos).SetRotation(rotation);
			});
		}

		private void DoActionForHandles(int index, Action<int> action)
		{
			if (index > 0 || IsClosed)
			{
				action(GetLeftIndex(index));
			}
			if (index < lastPointInd || IsClosed)
			{
				action(GetRightIndex(index));
			}
		}


		public void SetPointMode(int index, Point.Mode mode) => SetPointMode((ushort)index, mode);
		public void SetPointMode(ushort index, Point.Mode mode)
		{
			Point thisPoint = _points[index];
			_points[index] = thisPoint.SetMode(mode);
			if (_points[index].IsEndPoint)
			{
				if (index > 0 || IsClosed)
				{
					var i = GetLeftIndex(index);
					_points[i] = _points[i].SetMode(mode);
				}
				if (index < lastPointInd || IsClosed)
				{
					int i = GetRightIndex(index);
					_points[i] = _points[i].SetMode(mode);
				}

				UpdatePosition(index);
			}
			else if (mode == Point.Mode.Linear)
			{
				int epIndex = index + (_points[index].isRightHandle ? -1 : 1);
				if (epIndex < lastPointInd && _points[index].isRightHandle)
				{
					_points[epIndex] = _points[epIndex].RemoveAutomaticMode();
					int leftIndex = index - 2;
					if (_points[leftIndex].IsAutomatic)
						_points[leftIndex] = _points[leftIndex].RemoveAutomaticMode();
					else if (_points[leftIndex].IsLinear)
						_points[epIndex] = _points[epIndex].SetMode(Point.Mode.Linear);
				}
				if (epIndex > 0 && _points[index].isLeftHandle)
				{
					_points[epIndex] = _points[epIndex].RemoveAutomaticMode();
					int rightIndex = index + 2;
					if (_points[rightIndex].IsAutomatic)
						_points[rightIndex] = _points[rightIndex].RemoveAutomaticMode();
					else if (_points[rightIndex].IsLinear)
						_points[epIndex] = _points[epIndex].SetMode(Point.Mode.Linear);
				}
				UpdatePosition((index + 1) % PointCount);
				UpdatePosition((index - 1) % PointCount);
			}
			BumpVersion();
		}

		public void OnBeforeSerialize() { }
		public void OnAfterDeserialize() => UpdateVertexData(true);

		//========================

		/// <summary>
		/// Cuts curve at point splitting segment in two, returning new EndPoint
		/// </summary>
		public Point SplitCurveAt(Vector3 point)
		{
			var t = GetClosestPointTimeSegment(point, out var segmentIndex);

			SplitCurveAt(segmentIndex, t);
			return _points[segmentIndex];
		}

		/// <summary>
		/// Splits segment at time using Castejau method and replaces initial segment.
		/// </summary>
		public void SplitCurveAt(int segmentIndex, float t)
		{
			var newSegments = CasteljauUtility.GetSplitSegmentPoints(t, Segments[segmentIndex]);

			ReplaceCurveSegment(segmentIndex, newSegments);
		}

		public VertexData GetClosestPoint(Vector3 position)
		{
			var t = GetClosestPointTimeSegment(position, out var segmentIndex);
			return VertexData.GetPointFromTime(segmentIndex + t);
		}

		/// <summary>
		/// Goest through all VertexData and looks for closest point and returns t [0..1] and segmentIndex of it.
		/// </summary>
		/// <param name="position"></param>
		/// <param name="segmentIndex"></param>
		/// <returns></returns>
		public float GetClosestPointTimeSegment(Vector3 position, out int segmentIndex)
		{
			var vpair = VertexData.Take(2).ToArray();
			float minDist = float.MaxValue;
			foreach (var v in VertexData.Skip(1))
			{
				var dist = (position - v.Position).magnitude;
				if (dist < minDist)
				{
					vpair[1] = vpair[0];
					vpair[0] = v;
					minDist = dist;
				}
			}
			var a = vpair[0].cumulativeTime < vpair[1].cumulativeTime ? vpair[0] : vpair[1];
			var b = vpair[0].cumulativeTime > vpair[1].cumulativeTime ? vpair[0] : vpair[1];
			Vector3 dir = b.Position - a.Position;
			float mag = dir.magnitude;
			dir.Normalize();
			var locPos = position - a.Position;
			var dot = Mathf.Clamp(Vector3.Dot(dir, locPos), 0, mag) / mag;
			var timeDist = b.cumulativeTime - a.cumulativeTime;
			float t = a.cumulativeTime + dot * timeDist;
			segmentIndex = Mathf.Min(SegmentCount - 1, Mathf.FloorToInt(t));
			return t - segmentIndex;
		}

		/// <summary>
		/// Dissolves Endpoint and its handles. Uses Casteljau method to calculate merged segment.
		/// </summary>
		/// <param name="segmentIndex"></param>
		public void DissolveEP(int segmentIndex)
		{
			if (segmentIndex <= 0 && segmentIndex >= SegmentCount) return;

			if ((segmentIndex == 0 || segmentIndex == SegmentCount) && IsClosed)
			{
				var s = new Vector3[][] { Segments[SegmentCount - 1], Segments[0] };
				ReplaceCurveSegment(0, 1, CasteljauUtility.JoinSegments(s));
				_points.RemoveRange(PointCount - 3, 3);
			}
			else
			{
				var s = Segments.Skip(segmentIndex - 1).Take(2);
				var segment = CasteljauUtility.JoinSegments(s);

				ReplaceCurveSegment(segmentIndex - 1, 2, segment);
			}
		}

		/// <summary>
		/// Removes Endpoints and its respective handles.
		/// </summary>
		/// <param name="indexes"></param>
		public void RemoveMany(IEnumerable<int> indexes)
		{
			foreach (var index in indexes.Where(i => IsEndPoint(i)).OrderByDescending(i => i))
			{
				if (PointCount <= (IsClosed ? 7 : 4))
				{
					BumpVersion();
					return;
				}
				else if (index == 0 || index == LastPointInd)
				{
					if (index == 0 || (IsClosed && index == LastPointInd))
						_points.RemoveRange(0, 3);
					if (index == LastPointInd || (IsClosed && index == 0))
						_points.RemoveRange(PointCount - 3, 3);
				}
				else
				{
					//Cancel if not a control point
					//if (!IsEndPoint(index)) return;
					//First just remove that point
					_points.RemoveRange(index - 1, 3);

				}
			}
			BumpVersion();
		}

		private void ReplaceCurveSegment(int segmentInd, Vector3[] newSegments) => ReplaceCurveSegment(segmentInd, 1, newSegments);
		private void ReplaceCurveSegment(int segmentInd, int replaceCount, Vector3[] newSegments)
		{
			if (newSegments.Length % 3 != 1) return;

			var newPoints = new Point[newSegments.Length];
			var types = Point.AllTypes;
			var typeInd = 0;
			for (int i = 0; i < newSegments.Length; i++)
			{
				var rot = Quaternion.identity;
				if (typeInd == 0)
				{
					//Skip to current segmant and find closest point to get rotation from
					var firstVInd = _vertexData.GetStartIndex(segmentInd);
					var min = _vertexData.Select(v => v.Position).Skip(firstVInd).Min((v) => newSegments[i].DistanceTo(v), out var ind);
					rot = _vertexData[firstVInd + ind].Rotation;
				}
				newPoints[i] = new Point(newSegments[i], rot, Vector3.one, types[typeInd], Point.Mode.Automatic);
				typeInd++;
				typeInd %= 3;
			}

			int index = GetPointIndex(segmentInd);

			Points.RemoveRange(index, replaceCount * 3 + 1);
			Points.InsertRange(index, newPoints);

			BumpVersion();
		}

		private int GetRightIndex(int index) => index + 1 - (IsClosed && index == lastPointInd ? lastPointInd : 0);

		private int GetLeftIndex(int index) => index - 1 + (IsClosed && index == 0 ? lastPointInd : 0);

		public Quaternion GetEPRotation(int segmentIndex) => GetEPRotation((ushort)segmentIndex);
		public Quaternion GetEPRotation(int segmentIndex, int index = -1) => GetEPRotation((ushort)segmentIndex, index);
		public Quaternion GetEPRotation(ushort segmentIndex, int index = -1)
		{
			if (index == -1)
				index = GetPointIndex(segmentIndex);
			return Quaternion.LookRotation(GetEPTangentFromPoints(segmentIndex, index), _points[index].up);
		}

		/// <summary>
		/// Calculates tangent of set point. Pass either segmentIndex or point index.
		/// </summary>
		/// <param name="segmentIndex">if index is set, it's optional</param>
		/// <param name="index"></param>
		/// <returns></returns>
		public Vector3 GetEPTangentFromPoints(int segmentIndex, int index = -1)
		{
			if (IsClosed && segmentIndex == SegmentCount)
			{
				segmentIndex = 0;
				index = 0;
			}
			if (index == -1)
				index = GetPointIndex(segmentIndex);
			var point = _points[index];
			bool isAuto = point.IsAutomatic;

			//Manual 0 open || Auto < last open || Closed
			//Calculate from next point
			if (!IsClosed && ((isAuto && index < lastPointInd) || (!isAuto && index == 0)) || IsClosed)
			{
				var nextPoint = _points[(index + 1) % PointCount];
				//if (nextPoint.IsLinear)
				//	nextPoint = _points[(index + 2) % PointCount];
				return (nextPoint - point).normalized;
			}
			//Auto last open || Manual last open
			//Calculate from previous point
			else if (!IsClosed && index == lastPointInd)
			{
				var prevPoint = _points[index - 1];
				//if (prevPoint.IsLinear)
				//	prevPoint = _points[(PointCount + index - 2) % PointCount];
				return (point - prevPoint).normalized;
			}
			//Manual avg
			else
			{
				var nextIndex = index + 1;
				var prevIndex = index - 1;
				return (_points[nextIndex] - _points[prevIndex]).normalized;
			}
		}

		[NonSerialized]
		private int _vVersion;
		private VertexData[] _vertexData;
		public VertexData[] VertexData
		{
			[DebuggerStepThrough]
			get
			{
				UpdateVertexData();
				return _vertexData;
			}
		}
		public void UpdateVertexData(bool force = false) =>
			_vertexData = this.CheckCachedValueVersion(ref _vertexData, c => BezierCurveZ.VertexData.GetVertexData(c), ref _vVersion, _bVersion, force);


		private int _vDPVersion;
		private Vector3[] _vertexDataPoints;
		public Vector3[] VertexDataPoints
		{
			[DebuggerStepThrough]
			get => this.CheckCachedValueVersion(ref _vertexDataPoints, c => c.VertexData.SelectArray(v => v.Position), ref _vDPVersion, _vVersion);
		}


		[SerializeField]
		private float _interpolationMaxAngleError = 5;
		[SerializeField]
		private float _interpolationMinDistance = 0.000001f;
		[SerializeField]
		private int _interpolationAccuracy = 10;
		public int InterpolationAccuracy { get => _interpolationAccuracy; set { if (_interpolationAccuracy != value) { _interpolationAccuracy = value; BumpVersion(); } } }
		public float InterpolationMaxAngleError { get => _interpolationMaxAngleError; set { if (_interpolationMaxAngleError != value) { _interpolationMaxAngleError = value; BumpVersion(); } } }
		public float InterpolationMinDistance { get => _interpolationMinDistance; set { if (_interpolationMinDistance != value) { _interpolationMinDistance = Mathf.Max(value, 0.000001f); BumpVersion(); } } }

		[SerializeField]
		private InterpolationMethod _interpolationmethod = InterpolationMethod.CatmullRomAdditive;
		public InterpolationMethod InterpolationMethod { get => _interpolationmethod; set { if (_interpolationmethod != value) { _interpolationmethod = value; BumpVersion(); } } }
		[SerializeField]
		float _interpolationCapmullRomTension = .5f;
		public float InterpolationCapmullRomTension { get => _interpolationCapmullRomTension; set { if (_interpolationCapmullRomTension != value) { _interpolationCapmullRomTension = value; _vVersion++; } } }

		public Curve Copy()
		{
			var c = new Curve();
			c._points = _points;
			c._isClosed = _isClosed;
			c._interpolationMaxAngleError = _interpolationMaxAngleError;
			c._interpolationMinDistance = _interpolationMinDistance;
			c._interpolationAccuracy = _interpolationAccuracy;
			return c;
		}

		public void CopyFrom(Curve curve)
		{
			_points = curve._points;
			_isClosed = curve._isClosed;
			_interpolationMinDistance = curve._interpolationMinDistance;
			_interpolationMaxAngleError = curve._interpolationMaxAngleError;
			_interpolationAccuracy = curve._interpolationAccuracy;
			BumpVersion();
		}

		public override bool Equals(object obj)
		{
			return obj is Curve curve &&
#if UNITY_EDITOR
					_id == curve._id &&
#endif
					_bVersion == curve._bVersion &&
					EqualityComparer<List<Point>>.Default.Equals(_points, curve._points) &&
					_isClosed == curve._isClosed &&
					_interpolationMaxAngleError == curve._interpolationMaxAngleError &&
					_interpolationMinDistance == curve._interpolationMinDistance &&
					_interpolationAccuracy == curve._interpolationAccuracy &&
					_interpolationCapmullRomTension == curve._interpolationCapmullRomTension;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(
#if UNITY_EDITOR
				_id, 
# endif
				_bVersion, _points, _isClosed, _interpolationMaxAngleError, _interpolationMinDistance, _interpolationAccuracy, _interpolationCapmullRomTension);
		}

		public static bool operator ==(Curve left, Curve right)
		{
			return EqualityComparer<Curve>.Default.Equals(left, right);
		}

		public static bool operator !=(Curve left, Curve right)
		{
			return !(left == right);
		}
	}

}