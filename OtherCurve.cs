using BezierCurveZ;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[Serializable]
public class OtherCurve : ISerializationCallbackReceiver, ICurve
{
#if UNITY_EDITOR
	[SerializeField] public bool _previewOn;
	[SerializeField] public bool _isInEditMode;
	public bool _isMouseOverProperty;
#endif

	[SerializeField] internal List<OtherPoint> _points;
	private int _bVersion = 1;

	/// <summary>
	/// Points and segments in open curve: {Control, Right, Left}, {Control}
	/// Points and segment in closed curve: {Control, Right, Left},{Control, Right, Left}
	/// </summary>
	public List<OtherPoint> Points { [DebuggerStepThrough] get => _points; }
	public int LastPointInd { [DebuggerStepThrough] get => _points.Count - 1; }
	public int ControlPointCount { [DebuggerStepThrough] get => (_points.Count / 3f).CeilToInt(); }

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
	public Vector3 GetPointPosition(int index) => Points[index].position;

	[DebuggerStepThrough]
	public bool IsAutomaticHandle(int index) => Points[index].mode.HasFlag(OtherPoint.Mode.Automatic);

	[DebuggerStepThrough]
	public bool IsControlPoint(int index) => Points[index].type == OtherPoint.Type.Control;

	public Vector3[][] Segments
	{
		[DebuggerStepThrough]
		get
		{
			Vector3[][] r = new Vector3[SegmentCount][];
			for (int i = 0; i < SegmentCount; i++)
				r[i] = new Vector3[] { _points[i * 3].position, _points[i * 3 + 1].position, _points[i * 3 + 2].position, _points[(i * 3 + 3) % Points.Count].position };
			return r;
		}
	}

	[DebuggerStepThrough]
	public Vector3[] Segment(int segmentIndex) => Segments[segmentIndex];

	IEnumerable<Vector3[]> ICurve.Segments => Segments;
	private int _pposVersion;
	private Vector3[] _pointPositions;
	public Vector3[] PointPositions { get { if (_pposVersion != _bVersion) _pointPositions = Points.SelectArray(p => p.position); return _pointPositions; } }
	private int _protVersion;
	private Quaternion[] _pointRotations;
	public Quaternion[] PointRotations { get { if (_protVersion != _bVersion) _pointRotations = Points.SelectArray(p => p.rotation); return _pointRotations; } }
	public int PointCount { [DebuggerStepThrough] get => Points.Count; }
	private ushort lastPointInd { [DebuggerStepThrough] get => (ushort)(Points.Count - 1); }

	[SerializeField] internal bool _isClosed;
	public bool IsClosed { [DebuggerStepThrough] get => _isClosed; [DebuggerStepThrough] set { if (value != _isClosed) SetIsClosed(value); } }

	public OtherCurve()
	{
		var rot = Quaternion.LookRotation(Vector3.right);
		_points = new List<OtherPoint> {
				new OtherPoint(Vector3.zero, rot, OtherPoint.Type.Control),
				new OtherPoint(Vector3.right * .33333f, rot, OtherPoint.Type.Right),
				new OtherPoint(Vector3.right * .66666f, rot, OtherPoint.Type.Left),
				new OtherPoint(Vector3.right, rot, OtherPoint.Type.Control),
			};
		_bVersion = 1;
	}


	[SerializeField]
	private OtherPoint.Mode[] _preservedNodeModesWhileClosed = new OtherPoint.Mode[2];

	public void SetIsClosed(bool value)
	{
		_isClosed = value;
		if (!IsClosed)
		{
			Points.RemoveAt(LastPointInd);
			Points.RemoveAt(LastPointInd);
			Points.RemoveAt(LastPointInd);
			Points[0] = Points[0].SetMode(_preservedNodeModesWhileClosed[0]);
			Points[LastPointInd] = Points[LastPointInd].SetMode(_preservedNodeModesWhileClosed[1]);
			_bVersion++;
		}
		else
		{
			Points.Add(new OtherPoint(getHandlePosition(LastPointInd, LastPointInd - 1), OtherPoint.Type.Right, _points[LastPointInd].mode));
			Points.Add(new OtherPoint(getHandlePosition(0, 1), OtherPoint.Type.Left, _points[0].mode));
			Points.Add(new OtherPoint(Points[0]));
			_bVersion++;

			Vector3 getHandlePosition(int ind, int otherind)
			{
				Vector3 r = Points[ind] * 2f - Points[otherind];
				return r;
			}
		}
	}

	public void UpdatePosition(int index) => SetPointPosition(index, Points[index]);
	public void SetPointPosition(int index, Vector3 position) => SetPointPosition((ushort)index, position, true);
	public void SetPointPosition(int index, Vector3 position, bool recursive = true) => SetPointPosition((ushort)index, position, recursive);
	public void SetPointPosition(ushort index, Vector3 position, bool recursive = true)
	{
		var thisPoint = Points[index];
		if (thisPoint.IsControlPoint)
		{
			var diff = position - thisPoint;

			Points[index] = thisPoint.SetPosition(position).SetTangent(index < lastPointInd ? thisPoint - Points[index + 1] : Points[index - 1] - thisPoint);
			if (IsClosed && index == lastPointInd)
				Points[0] = Points[lastPointInd];
			if (IsClosed && index == 0)
				Points[lastPointInd] = Points[0];

			if (index > 0 || IsClosed)
			{
				var i = GetControlsLeftIndex(index);
				Points[i] = Points[i].IsLinear ? GetLinearHandle(i) : Points[i].SetPosition(Points[i] + diff).SetRotation(Quaternion.LookRotation(Points[i] - Points[index]));
			}
			if (index < lastPointInd || IsClosed)
			{
				var i = GetControlsRightHandle(index);
				Points[i] = Points[i].IsLinear ? GetLinearHandle(i) : Points[i].SetPosition(Points[i] + diff).SetRotation(Quaternion.LookRotation(Points[i] - Points[index]));
			}
		}
		else
		{
			var controlIndex = thisPoint.isRightHandle ? index - 1 : index + 1;
			var controlPoint = Points[controlIndex];

			Points[controlIndex] = controlPoint.SetRotation(Quaternion.LookRotation((position - controlPoint) * (thisPoint.isRightHandle ? 1 : -1), controlPoint.up).normalized);

			var otherHandleIndex = thisPoint.isRightHandle ? index - 2 : index + 2;
			bool outOfBounds = (otherHandleIndex < 0 || otherHandleIndex >= PointCount);
			if (!outOfBounds || IsClosed)
			{
				if (outOfBounds && IsClosed)
					otherHandleIndex = (PointCount + (thisPoint.isRightHandle ? index - 3 : index + 3)) % PointCount;
				var otherHandle = Points[otherHandleIndex];

				if (thisPoint.IsAutomatic && otherHandle.IsAutomatic)
				{
					if (thisPoint.IsManual || otherHandle.IsManual)
					{
						//Proportional
						var diff = position - controlPoint;
						Points[index] = Points[index].SetPosition(position);
						if (IsClosed || (index > 1 && index < lastPointInd - 1))
							Points[otherHandleIndex] = otherHandle.SetPosition(controlPoint - diff * ((otherHandle - controlPoint).magnitude / diff.magnitude))
								.SetRotation(controlPoint.rotation);
					}
					else
					{
						//Automatic, edit both handles mirrored
						Points[index] = thisPoint.SetPosition(position);
						if (IsClosed || (index > 1 && index < lastPointInd - 1))
							Points[otherHandleIndex] = otherHandle.SetPosition(controlPoint + controlPoint - position)
								.SetRotation(controlPoint.rotation);
					}
				}
			}
			
			if (!thisPoint.IsLinear)
				_points[index] = thisPoint.SetPosition(position).SetRotation(Quaternion.LookRotation(thisPoint - controlPoint));

			var nextHandleIndex = thisPoint.isRightHandle ? index + 1 : index - 1;
			var nextHandle = Points[nextHandleIndex];

			if (nextHandle.IsLinear)
				Points[nextHandleIndex] = GetLinearHandle(nextHandleIndex);
		}
		_bVersion++;

		OtherPoint GetLinearHandle(int index)
		{
			int segmentIndex = GetSegmentIndex(index);
			int aind = GetPointIndex(segmentIndex);
			var a = Points[aind];
			var b = Points[aind + 3 < Points.Count ? aind + 3 : GetPointIndex(segmentIndex + 1)];
			var isRight = index == GetPointIndex(segmentIndex) + 1;
			var otherInd = index + (isRight ? 1 : -1);
			Vector3 otherPoint;
			if (Points[otherInd].IsLinear)
			{
				otherInd += isRight ? 1 : -1;
				if (recursive)
					SetPointPosition((ushort)otherInd, Points[otherInd], false);
				otherPoint = Points[otherInd];
			}
			else
				otherPoint = Points[otherInd];
			var diff = a - b;
			var tang = isRight ? otherPoint - a : otherPoint - b;
			var pos = (isRight ? a : b) + tang.normalized * diff.magnitude * .1f;
			return new OtherPoint(pos, Quaternion.LookRotation(tang), isRight? OtherPoint.Type.Right : OtherPoint.Type.Left, OtherPoint.Mode.Linear);
		}
	}

	public void SetCPRotation(int segmentIndex, Quaternion rotation) => SetCPRotation((ushort)segmentIndex, rotation);
	public void SetCPRotation(ushort segmentIndex, Quaternion rotation)
	{
		var index = GetPointIndex(segmentIndex);
		var delta = _points[index].rotation.Inverted() * rotation;
		_points[index] = _points[index].SetRotation(rotation);

		RotateHandles(index, _points[index], delta, rotation);
		_bVersion++;
	}
	public void AddCPRotation(int segmentIndex, Quaternion delta, bool local = false) => AddCPRotation((ushort)segmentIndex, delta, local);
	public void AddCPRotation(ushort segmentIndex, Quaternion delta, bool local)
	{
		var index = GetPointIndex(segmentIndex);
		var rotation = delta * _points[index].rotation;
		var point = _points[index];
		_points[index] = point.SetRotation(rotation);

		RotateHandles(index, _points[index], delta, _points[index].rotation);
		_bVersion++;
	}

	private void RotateHandles(int index, Vector3 origin, Quaternion delta, Quaternion rotation)
	{
		DoActionForHandles(index, i =>
		{
			var pos = origin + delta * (Points[i] - origin);
			Points[i] = Points[i].SetPosition(pos).SetRotation(rotation);
		});
	}

	private void DoActionForHandles(int index, Action<int> action)
	{
		if (index > 0 || IsClosed)
		{
			action(GetControlsLeftIndex(index));
		}
		if (index < lastPointInd || IsClosed)
		{
			action(GetControlsRightHandle(index));
		}
	}

	public void SetPointMode(int index, OtherPoint.Mode mode) => SetPointMode((ushort)index, mode);
	public void SetPointMode(ushort index, OtherPoint.Mode mode)
	{
		_points[index] = _points[index].SetMode(mode);
		if (_points[index].IsControlPoint)
		{
			if (index > 0 || IsClosed)
			{
				var i = GetControlsLeftIndex(index);
				Points[i] = Points[i].SetMode(mode);
			}
			if (index < lastPointInd || IsClosed)
			{
				int i = GetControlsRightHandle(index);
				Points[i] = Points[i].SetMode(mode);
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
		return Points[segmentIndex];
	}

	public void SplitCurveAt(int segmentIndex, float t)
	{
		var newSegments = CasteljauUtility.GetSplitSegmentPoints(t, Segment(segmentIndex));

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
		segmentIndex = Mathf.FloorToInt(t);
		return t - segmentIndex;
	}

	public void DissolveCP(int segmentIndex)
	{
		if (segmentIndex <= 0 && segmentIndex >= SegmentCount) return;

		var s = Segments.Skip(segmentIndex - 1).Take(2);
		var segment = CasteljauUtility.JoinSegments(s);

		ReplaceCurveSegment(segmentIndex - 1, 2, segment);
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
				var min = _vertexData.Select(v=>v.Position).Skip(firstVInd).Min((v) => newSegments[i].DistanceTo(v), out var ind);
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

	private int GetControlsRightHandle(int index) => index + 1 - (IsClosed && index == lastPointInd ? lastPointInd : 0);

	private int GetControlsLeftIndex(int index) => index - 1 + (IsClosed && index == 0 ? lastPointInd : 0);

	public Quaternion GetCPRotation(int segmentIndex) => GetCPRotation((ushort)segmentIndex);
	public Quaternion GetCPRotation(ushort segmentIndex)
	{
		return _points[GetSegmentIndex(segmentIndex)].GetRotation(Vector3.forward);
	}

	public Vector3 GetCPTangentFromPoints(int segmentIndex)
	{
		var index = GetPointIndex(segmentIndex);
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
	public OtherVertexData[] VertexData { [ DebuggerStepThrough] get
		{
			UpdateVertexData();
			return _vertexData;
		}
	}
	private int _vDPVersion;
	private Vector3[] _vertexDataPoints;
	public Vector3[] VertexDataPoints { [DebuggerStepThrough] get
		{
			UpdateVertexData();
			if (_vDPVersion != _vVersion)
			{
				_vertexDataPoints = _vertexData.SelectArray(v => v.Position);
				_vDPVersion = _vVersion;
			}
			return _vertexDataPoints;
		}
	}
	//private int[] _vertexDataGroupIndexes;
	//public IEnumerable<IEnumerable<OtherVertexData>> VertexDataGroups;

	public void UpdateVertexData(bool force = false)
	{
		if (_bVersion != _vVersion || force)
		{
			_vertexData = OtherVertexData.GetVertexData(this, 1f, .01f, 10);
			_vVersion = _bVersion;
		}
	}
}
