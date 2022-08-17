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
	private int _bVersion = -1;

	/// <summary>
	/// Points and segments in open curve: {Control, Right, Left}, {Control}
	/// Points and segment in closed curve: {Control, Right, Left},{Control, Right, Left}
	/// </summary>
	public List<OtherPoint> Points { get => _points; }
	public int LastPointInd => _points.Count - 1;
	public int ControlPointCount => (_points.Count / 3f).CeilToInt();
	public int SegmentCount => (_points.Count / 3f).FloorToInt();
	public int GetSegmentIndex(ushort index) => (index / 3) % ControlPointCount;
	public int GetSegmentIndex(int index) => GetSegmentIndex((ushort)index);
	public int GetPointIndex(ushort segmentIndex) => segmentIndex * 3 % _points.Count;
	public int GetPointIndex(int segmentIndex) => GetPointIndex((ushort)segmentIndex);

	public Vector3 GetPointPosition(int index) => Points[index].position;

	public bool IsAutomaticHandle(int index) => Points[index].mode.HasFlag(OtherPoint.Mode.Automatic);

	public bool IsControlPoint(int index) => Points[index].type == OtherPoint.Type.Control;

	public Vector3[] Segment(int segmentIndex) => Segments[segmentIndex];


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

	[SerializeField] internal bool _isClosed;
	public bool IsClosed { [DebuggerStepThrough]get => _isClosed; [DebuggerStepThrough] set { if (value != _isClosed) SetIsClosed(value); } }

	public Vector3[][] Segments { get {
			Vector3[][] r = new Vector3[SegmentCount][];
			for (int i = 0; i < SegmentCount; i++)
				r[i] = new Vector3[] { _points[i * 3].position, _points[i * 3 + 1].position, _points[i * 3 + 2].position, _points[(i * 3 + 3) % Points.Count].position };
			return r;
		}
	}

	IEnumerable<Vector3[]> ICurve.Segments { get; }
	public Vector3[] PointPositions { get; }
	public Quaternion[] PointRotations { get; }
	public int PointCount => Points.Count;

	[SerializeField]
	private OtherPoint.Mode[] _preservedNodeModesWhileClosed = new OtherPoint.Mode[2];

	public void SetIsClosed(bool value)
	{
		_isClosed = value;
		if (!IsClosed)
		{
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
			//SetPointPosition(0, points[1].position);
			//SetPointPosition(lastPointInd - 2, points[lastPointInd - 2].position);
			_bVersion++;

			Vector3 getHandlePosition(int ind, int otherind)
			{
				Vector3 r = Points[ind] * 2f - Points[otherind];
				return r;
			}
		}
	}

	public void SetPointPosition(int index, Vector3 position)
	{
		_points[index] = _points[index].SetPosition(position);
	}

	public void SetPointRotation(int index, Quaternion rotation)
	{
		_points[index] = _points[index].SetRotation(rotation);
	}

	public void SetPointMode(int index, OtherPoint.Mode mode)
	{
		_points[index] = _points[index].SetMode(mode);
	}

	public Quaternion GetCPRotation(int cpIndex)
	{
		return _points[GetSegmentIndex((ushort)cpIndex)].GetRotation(Vector3.forward);
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
			var splitdata = CurveInterpolation.SplitCurveByAngleError(this, 1f, .01f, 10, true);
			_vertexData = new OtherVertexData[splitdata.Count];
			var segInd = 0;
			for (int i = 0; i < splitdata.Count; i++)
			{
				_vertexData[i] = new OtherVertexData() {
					Position = splitdata.points[i],
					Rotation = splitdata.rotations[i],
					distance = splitdata.cumulativeLength[i],
					cumulativeTime = splitdata.cumulativeTime[i],
					isSharp = splitdata.isSharp[i],
					segmentInd = segInd,
					segmentStartVertInd = splitdata.segmentIndices[segInd]
				};
				if (splitdata.segmentIndices[segInd] == i) segInd++;
			}
		}
		_vVersion = _bVersion;
	}

}
