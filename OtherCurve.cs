using BezierCurveZ;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[Serializable]
public class OtherCurve : ISerializationCallbackReceiver
{
#if UNITY_EDITOR
	[NonSerialized]
	public bool _previewOn;
	[NonSerialized]
	public bool _isInEditMode;
	public bool _isMouseOverProperty;
	[NonSerialized]
	public int _id = new System.Random().Next();
	public static int _idCounter;

	//[SerializeField]
#endif
	private int _bVersion = new System.Random().Next();

	[SerializeField]
	internal List<OtherPoint> _points;

	public void BumpVersion()
	{
		_bVersion++;
		UpdateVertexData(force: true);
	}

	/// <summary>
	/// Points and segments in open curve: {Control, Right, Left}, {Control}
	/// Points and segment in closed curve: {Control, Right, Left},{Control, Right, Left}
	/// </summary>
	public List<OtherPoint> Points { [DebuggerStepThrough] get => _points; }
	public IEnumerable<OtherPoint> ControlPoints
	{
		[DebuggerStepThrough]
		get
		{
			for (int i = 0; i < _points.Count; i += 3)
				yield return _points[i];
		}
	}
	public int LastPointInd { [DebuggerStepThrough] get => _points.Count - 1; }
	public int ControlPointCount { [DebuggerStepThrough] get => (_points.Count / 3f).CeilToInt(); }
	public IEnumerable<int> ControlPointIndexes
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
	public int GetSegmentIndex(ushort index) => (index / 3) % ControlPointCount;
	[DebuggerStepThrough]
	public int GetSegmentIndex(int index) => GetSegmentIndex((ushort)index);
	[DebuggerStepThrough]
	public int GetPointIndex(ushort segmentIndex) => segmentIndex * 3 % _points.Count;
	[DebuggerStepThrough]
	public int GetPointIndex(int segmentIndex) => GetPointIndex((ushort)segmentIndex);

	[DebuggerStepThrough]
	public Vector3 GetPointPosition(int index) => _points[index].position;

	[DebuggerStepThrough]
	public bool IsAutomaticHandle(int index) => _points[index].IsAutomatic;

	[DebuggerStepThrough]
	public bool IsControlPoint(int index) => _points[index].IsControlPoint;

	private Vector3[][] _segments;
	private int _sVersion;
	public Vector3[][] Segments
	{
		[DebuggerStepThrough]
		get
		{
			if (_sVersion != _bVersion || _segments == null)
			{
				_segments = getValue();
				_sVersion = _bVersion;
			}
			return _segments;

			Vector3[][] getValue()
			{
				Vector3[][] r = new Vector3[SegmentCount][];
				for (int i = 0; i < SegmentCount; i++)
					r[i] = new Vector3[] { _points[i * 3].position, _points[i * 3 + 1].position, _points[i * 3 + 2].position, _points[(i * 3 + 3) % _points.Count].position };
				return r;
			}
		}
	}

	private int _pposVersion;
	private Vector3[] _pointPositions;
	public Vector3[] PointPositions { get { if (_pposVersion != _bVersion) _pointPositions = _points.SelectArray(p => p.position); return _pointPositions; } }
	private int _protVersion;
	private Quaternion[] _pointRotations;
	public Quaternion[] PointRotations { get { if (_protVersion != _bVersion) _pointRotations = _points.SelectArray(p => p.rotation); return _pointRotations; } }
	public int PointCount { [DebuggerStepThrough] get => _points.Count; }
	private ushort lastPointInd { [DebuggerStepThrough] get => (ushort)(Points.Count - 1); }

	[SerializeField] internal bool _isClosed;
	public bool IsClosed { [DebuggerStepThrough] get => _isClosed; [DebuggerStepThrough] set { if (value != _isClosed) SetIsClosed(value); } }

	public OtherCurve()
	{
		_points = defaultPoints;
		_bVersion = 1;
#if UNITY_EDITOR
		_id = _idCounter++;
#endif
	}

	public void Reset()
	{
		_points = defaultPoints;
		_bVersion = -1;
		_vertexData = null;
		_pointPositions = null;
		_pointRotations = null;
		_vertexDataPoints = null;
		_interpolationAccuracy = 10;
		_interpolationMaxAngleError = 5;
		_interpolationMinDistance = 0;
		_interpolationCapmullRomTension = 1f;
		IterpolationOptionsInd = InterpolationMethod.CatmullRomAdditive;
	}
	private static List<OtherPoint> defaultPoints
	{
		get
		{
			var rot = Quaternion.LookRotation(Vector3.right);
			return new List<OtherPoint> {
				new OtherPoint(Vector3.zero, rot, OtherPoint.Type.Control, OtherPoint.Mode.Automatic),
				new OtherPoint(Vector3.right * .33333f, rot, OtherPoint.Type.Right, OtherPoint.Mode.Automatic),
				new OtherPoint(Vector3.right * .66666f, rot, OtherPoint.Type.Left, OtherPoint.Mode.Automatic),
				new OtherPoint(Vector3.right, rot, OtherPoint.Type.Control, OtherPoint.Mode.Automatic),
			};
		}
	}

	[SerializeField]
	private OtherPoint.Mode[] _preservedNodeModesWhileClosed = new OtherPoint.Mode[2];

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
			_bVersion++;
		}
		else
		{
			_points.Add(new OtherPoint(getHandlePosition(LastPointInd, LastPointInd - 1), OtherPoint.Type.Right, _points[LastPointInd].mode));
			_points.Add(new OtherPoint(getHandlePosition(0, 1), OtherPoint.Type.Left, _points[0].mode));
			_points.Add(new OtherPoint(Points[0]));
			_bVersion++;

			Vector3 getHandlePosition(int ind, int otherind)
			{
				Vector3 r = _points[ind] * 2f - _points[otherind];
				return r;
			}
		}
	}

	public void UpdatePosition(int index) => SetPointPosition(index, _points[index]);
	public void SetPointPosition(int index, Vector3 position) => SetPointPosition((ushort)index, position, true);
	public void SetPointPosition(int index, Vector3 position, bool recursive = true) => SetPointPosition((ushort)index, position, recursive);
	public void SetPointPosition(ushort index, Vector3 position, bool recursive = true)
	{
		var thisPoint = _points[index];
		if (thisPoint.IsControlPoint)
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
			OtherPoint leftPoint = default(OtherPoint); OtherPoint rightPoint = default(OtherPoint);
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
			var controlIndex = thisPoint.isRightHandle ? index - 1 : index + 1;
			var controlPoint = _points[controlIndex];

			_points[controlIndex] = controlPoint.SetRotation(Quaternion.LookRotation((position - controlPoint) * (thisPoint.isRightHandle ? 1 : -1), controlPoint.up).normalized);

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
						var diff = position - controlPoint;
						//_points[index] = _points[index].SetPosition(position);
						if (IsClosed || (index > 1 && index < lastPointInd - 1))
							_points[otherHandleIndex] = otherHandle.SetPosition(controlPoint - diff * ((otherHandle - controlPoint).magnitude / diff.magnitude))
								.SetRotation(controlPoint.rotation);
					}
					else
					{
						//Automatic, edit both handles mirrored
						//_points[index] = thisPoint.SetPosition(position);
						if (IsClosed || (index > 1 && index < lastPointInd - 1))
							_points[otherHandleIndex] = otherHandle.SetPosition(controlPoint + controlPoint - position)
								.SetRotation(controlPoint.rotation);
					}
				}
			}

			if (thisPoint.IsAutomatic && (!outOfBounds || IsClosed) && otherHandle.IsLinear)
			{
				otherHandle = GetLinearHandle(otherHandleIndex);
				_points[otherHandleIndex] = otherHandle;
				_points[index] = thisPoint.SetPosition(-(otherHandle - controlPoint).normalized * (position - controlPoint).magnitude);
			}
			else if (!thisPoint.IsLinear)
				_points[index] = thisPoint.SetPosition(position).SetRotation(Quaternion.LookRotation(thisPoint - controlPoint));

			var nextHandleIndex = thisPoint.isRightHandle ? index + 1 : index - 1;
			var nextHandle = _points[nextHandleIndex];

			if (nextHandle.IsLinear)
				_points[nextHandleIndex] = GetLinearHandle(nextHandleIndex);
		}
		_bVersion++;

		OtherPoint GetLinearHandle(int index)
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
			return new OtherPoint(pos, Quaternion.LookRotation(tang), isRight ? OtherPoint.Type.Right : OtherPoint.Type.Left, OtherPoint.Mode.Linear);
		}
	}

	public void SetCPRotation(int segmentIndex, Quaternion rotation) => SetCPRotation((ushort)segmentIndex, rotation);
	public void SetCPRotation(ushort segmentIndex, Quaternion rotation)
	{
		var index = GetPointIndex(segmentIndex);
		var delta = _points[index].rotation.Inverted() * rotation;
		_points[index] = _points[index].SetRotation(rotation);
		if (IsClosed && (index == 0 || index == LastPointInd))
			_points[index == 0 ? LastPointInd : 0] = _points[index];

		RotateHandles(index, _points[index], delta, rotation);
		_bVersion++;
	}
	public void AddCPRotation(int segmentIndex, Quaternion delta) => AddCPRotation((ushort)segmentIndex, delta);
	public void AddCPRotation(ushort segmentIndex, Quaternion delta)
	{
		var index = GetPointIndex(segmentIndex);
		var rotation = delta * _points[index].rotation;
		var point = _points[index];
		_points[index] = point.SetRotation(Quaternion.LookRotation(GetCPTangentFromPoints(segmentIndex, index), rotation * Vector3.up));
		if (IsClosed && (index == 0 || index == LastPointInd))
			_points[index == 0 ? LastPointInd : 0] = _points[index];

		RotateHandles(index, _points[index], delta, _points[index].rotation);
		_bVersion++;
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

	public void SetPointMode(int index, OtherPoint.Mode mode) => SetPointMode((ushort)index, mode);
	public void SetPointMode(ushort index, OtherPoint.Mode mode)
	{
		OtherPoint thisPoint = _points[index];
		_points[index] = thisPoint.SetMode(mode);
		if (_points[index].IsControlPoint)
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
		_bVersion++;
	}

	public void OnBeforeSerialize() { }
	public void OnAfterDeserialize() => UpdateVertexData(true);

	//========================
	public OtherPoint SplitCurveAt(Vector3 point)
	{
		var t = GetClosestPointTimeSegment(point, out var segmentIndex);

		SplitCurveAt(segmentIndex, t);
		return _points[segmentIndex];
	}

	public void SplitCurveAt(int segmentIndex, float t)
	{
		var newSegments = CasteljauUtility.GetSplitSegmentPoints(t, Segments[segmentIndex]);

		ReplaceCurveSegment(segmentIndex, newSegments);
	}

	//TODO Debug how to work with low vertex count
	public float GetClosestTimeSegment(Vector3 position, out int segmentInd)
	{
		UpdateVertexData();
		var minDist = float.MaxValue;
		var closestTime = float.MaxValue;
		segmentInd = -1;

		var prevVert = VertexData.FirstOrDefault();

		var i = 1;
		foreach (var v in VertexData.Skip(1))
		{
			Vector3 direction = (v.Position - prevVert.Position);
			float magMax = direction.magnitude;
			var normDirection = direction.normalized;
			Vector3 localPosition = position - prevVert.Position;
			var dot = Mathf.Clamp(Vector3.Dot(normDirection, localPosition), 0, magMax);
			var point = prevVert.Position + normDirection * dot;
			var dist = Vector3.Distance(point, position);
			if (dist < minDist)
			{
				minDist = dist;
				segmentInd = _vertexData[i].segmentInd;
				closestTime = prevVert.cumulativeTime + (v.cumulativeTime - prevVert.cumulativeTime) * (localPosition.magnitude / direction.magnitude);
			}

			i++;
			prevVert = v;
		}

		return closestTime;
	}

	public float GetClosestPointTimeSegment(Vector3 point, out int segmentIndex)
	{
		var z = VertexData.Take(2).ToArray();
		float minDist = float.MaxValue;
		foreach (var v in VertexData.Skip(1))
		{
			var dist = (point - v.Position).magnitude;
			if (dist < minDist)
			{
				z[1] = z[0];
				z[0] = v;
				minDist = dist;
			}
		}
		var a = z[0].cumulativeTime < z[1].cumulativeTime ? z[0] : z[1];
		var b = z[0].cumulativeTime > z[1].cumulativeTime ? z[0] : z[1];
		Vector3 dir = b.Position - a.Position;
		float mag = dir.magnitude;
		dir.Normalize();
		var locPos = point - a.Position;
		var dot = Mathf.Clamp(Vector3.Dot(dir, locPos), 0, mag) / mag;
		var timeDist = b.cumulativeTime - a.cumulativeTime;
		segmentIndex = Mathf.FloorToInt(a.cumulativeTime);
		float t = a.cumulativeTime + dot * timeDist;
		segmentIndex = Mathf.Min(SegmentCount - 1, Mathf.FloorToInt(t));
		return t - segmentIndex;
	}

	public void DissolveCP(int segmentIndex)
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

	public void RemoveMany(IEnumerable<int> indexes)
	{
		foreach (var index in indexes.Where(i => IsControlPoint(i)).OrderByDescending(i => i))
		{
			if (PointCount <= (IsClosed ? 7 : 4))
			{
				_bVersion++;
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
				if (!IsControlPoint(index)) return;
				//First just remove that point
				_points.RemoveRange(index - 1, 3);

			}
		}
		_bVersion++;
	}

	private void ReplaceCurveSegment(int segmentInd, Vector3[] newSegments) => ReplaceCurveSegment(segmentInd, 1, newSegments);
	private void ReplaceCurveSegment(int segmentInd, int replaceCount, Vector3[] newSegments)
	{
		if (newSegments.Length % 3 != 1) return;

		var newPoints = new OtherPoint[newSegments.Length];
		var types = OtherPoint.AllTypes;
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
			newPoints[i] = new OtherPoint(newSegments[i], rot, types[typeInd], OtherPoint.Mode.Proportional);
			typeInd++;
			typeInd %= 3;
		}

		int index = GetPointIndex(segmentInd);

		Points.RemoveRange(index, replaceCount * 3 + 1);
		Points.InsertRange(index, newPoints);

		_bVersion++;
	}

	private int GetRightIndex(int index) => index + 1 - (IsClosed && index == lastPointInd ? lastPointInd : 0);

	private int GetLeftIndex(int index) => index - 1 + (IsClosed && index == 0 ? lastPointInd : 0);

	public Quaternion GetCPRotation(int segmentIndex) => GetCPRotation((ushort)segmentIndex);
	public Quaternion GetCPRotation(int segmentIndex, int index = -1) => GetCPRotation((ushort)segmentIndex, index);
	public Quaternion GetCPRotation(ushort segmentIndex, int index = -1)
	{
		if (index == -1)
			index = GetPointIndex(segmentIndex);
		return Quaternion.LookRotation(GetCPTangentFromPoints(segmentIndex, index), _points[index].up);
	}

	/// <summary>
	/// Calculates tangent of set point. Pass either segmentIndex or point index
	/// </summary>
	/// <param name="segmentIndex">if index is set, it's optional</param>
	/// <param name="index"></param>
	/// <returns></returns>
	public Vector3 GetCPTangentFromPoints(int segmentIndex, int index = -1)
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


	private int _vVersion;
	private OtherVertexData[] _vertexData;
	public OtherVertexData[] VertexData
	{
		[DebuggerStepThrough]
		get
		{
			UpdateVertexData();
			return _vertexData;
		}
	}

	private int _vDPVersion;
	private Vector3[] _vertexDataPoints;
	[SerializeField]
	private float _interpolationMaxAngleError;
	[SerializeField]
	private float _interpolationMinDistance;	
	[SerializeField]
	private int _interpolationAccuracy;
	public int InterpolationAccuracy { get => _interpolationAccuracy; set { if (_interpolationAccuracy != value) { _interpolationAccuracy = value; _bVersion++; } } }
	public float InterpolationMaxAngleError { get => _interpolationMaxAngleError; set { if (_interpolationMaxAngleError != value) { _interpolationMaxAngleError = value; _bVersion++; } } }
	public float InterpolationMinDistance { get => _interpolationMinDistance; set { if (_interpolationMinDistance != value) { _interpolationMinDistance = value; _bVersion++; } } }

	public Vector3[] VertexDataPoints
	{
		[DebuggerStepThrough]
		get
		{
			UpdateVertexData();
			if (_vDPVersion != _vVersion || _vertexDataPoints == null)
			{
				_vertexDataPoints = VertexData.SelectArray(v => v.Position);
				_vDPVersion = _vVersion;
			}
			return _vertexDataPoints;
		}
	}

	public enum InterpolationMethod { RotationMinimization = 0, Linear = 1, Smooth = 2, CatmullRomAdditive = 3}
	public InterpolationMethod IterpolationOptionsInd;
	[SerializeField]
	float _interpolationCapmullRomTension;
	public float InterpolationCapmullRomTension { get => _interpolationCapmullRomTension; set { if (_interpolationCapmullRomTension != value) { _interpolationCapmullRomTension = value; _vVersion++; } } }

	public void UpdateVertexData(bool force = false)
	{
		if (_bVersion != _vVersion || _vertexData == null || force)
		{
 			_vertexData = OtherVertexData.GetVertexData(this);
			_vVersion = _bVersion;
		}
	}

	public OtherCurve Copy()
	{
		var c = new OtherCurve();
		c._points = _points;
		c._isClosed = _isClosed;
		c._interpolationMaxAngleError = _interpolationMaxAngleError;
		c._interpolationMinDistance = _interpolationMinDistance;
		c._interpolationAccuracy = _interpolationAccuracy;
		return c;
	}

	public void CopyFrom(OtherCurve curve)
	{
		_points = curve._points;
		_isClosed = curve._isClosed;
		_interpolationMinDistance = curve._interpolationMinDistance;
		_interpolationMaxAngleError = curve._interpolationMaxAngleError;
		_interpolationAccuracy = curve._interpolationAccuracy;
		_bVersion++;
	}

	public override bool Equals(object obj)
	{
		return obj is OtherCurve curve &&
			   _id == curve._id &&
			   _bVersion == curve._bVersion &&
			   EqualityComparer<List<OtherPoint>>.Default.Equals(_points, curve._points) &&
			   _isClosed == curve._isClosed &&
			   _interpolationMaxAngleError == curve._interpolationMaxAngleError &&
			   _interpolationMinDistance == curve._interpolationMinDistance &&
			   _interpolationAccuracy == curve._interpolationAccuracy &&
			   _interpolationCapmullRomTension == curve._interpolationCapmullRomTension;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(_id, _bVersion, _points, _isClosed, _interpolationMaxAngleError, _interpolationMinDistance, _interpolationAccuracy, _interpolationCapmullRomTension);
	}

	public static bool operator ==(OtherCurve left, OtherCurve right)
	{
		return EqualityComparer<OtherCurve>.Default.Equals(left, right);
	}

	public static bool operator !=(OtherCurve left, OtherCurve right)
	{
		return !(left == right);
	}
}
