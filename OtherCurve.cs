using BezierCurveZ;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[Serializable]
public class OtherCurve : ICurve
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
			var controlPoint = thisPoint.isRightHandle ? Points[index - 1] : Points[index + 1];
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
								.SetRotation(Quaternion.LookRotation(otherHandle - controlPoint));
					}
					else
					{
						//Automatic, edit both handles mirrored
						Points[index] = thisPoint.SetPosition(position);
						if (IsClosed || (index > 1 && index < lastPointInd - 1))
							Points[otherHandleIndex] = otherHandle.SetPosition(controlPoint + controlPoint - position)
								.SetRotation(Quaternion.LookRotation(otherHandle - controlPoint));
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
		_points[index] = _points[index].SetRotation(rotation);
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
	}

	private int GetControlsRightHandle(ushort index) => index + 1 - (IsClosed && index == lastPointInd ? lastPointInd : 0);

	private int GetControlsLeftIndex(ushort index) => index - 1 + (IsClosed && index == 0 ? lastPointInd : 0);

	public Quaternion GetCPRotation(int segmentIndex) => GetCPRotation((ushort)segmentIndex);
	public Quaternion GetCPRotation(ushort segmentIndex)
	{
		return _points[GetSegmentIndex(segmentIndex)].GetRotation(Vector3.forward);
	}


	private int _vVersion;
	private OtherVertexData[] _vertexData;
	public OtherVertexData[] VertexData;
	private int[] _vertexDataGroupIndexes;
	public IEnumerable<IEnumerable<OtherVertexData>> VertexDataGroups;

	public void Update(bool force = false)
	{
		if (_bVersion != _vVersion || force)
		{
			_vertexData = OtherVertexData.GetVertexData(this, 1f, .01f, 10);
		}
		_vVersion = _bVersion;
	}

}
