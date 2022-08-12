using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[Serializable]
public class OtherCurve
{
#if UNITY_EDITOR
	[SerializeField] public bool _previewOn;
	[SerializeField] public bool _isInEditMode;
	public bool _isMouseOverProperty;
#endif

	[SerializeField] internal List<OtherPoint> _points;
	private int _bVersion;

	/// <summary>
	/// Points and segments in open curve: {Control, Right, Left}, {Control}
	/// Points and segment in closed curve: {Control, Right, Left},{Control, Right, Left}
	/// </summary>
	public List<OtherPoint> points { get => _points; }
	public int lastPointInd => _points.Count - 1;
	public int CPCount => (_points.Count / 3f).CeilToInt();
	public int SegmentCount => (_points.Count / 3f).FloorToInt();
	public int CPIndex(ushort index)
	{
		return ((int)index / 3) % CPCount;
	}
	public int GetIndex(ushort cpIndex) => (int)cpIndex * 3 % _points.Count;

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
				r[i] = new Vector3[] { _points[i * 3].position, _points[i * 3 + 1].position, _points[i * 3 + 2].position, _points[i * 3 + 3].position };
			return r;
		}
	}

	[SerializeField]
	private OtherPoint.Mode[] _preservedNodeModesWhileClosed = new OtherPoint.Mode[2];

	public void SetIsClosed(bool value)
	{
		_isClosed = value;
		if (IsClosed)
		{
			_isClosed = value;
			points.RemoveAt(lastPointInd);
			points.RemoveAt(lastPointInd);
			points[0] = points[0].SetMode(_preservedNodeModesWhileClosed[0]);
			points[lastPointInd] = points[lastPointInd].SetMode(_preservedNodeModesWhileClosed[1]);
			_bVersion++;
		}
		else
		{
			points.Add(new OtherPoint(getHandlePosition(lastPointInd, lastPointInd - 1), OtherPoint.Type.Right, _points[lastPointInd].mode));
			points.Add(new OtherPoint(getHandlePosition(0, 1), OtherPoint.Type.Left, _points[0].mode));
			SetPointPosition(1, points[1].position);
			SetPointPosition(lastPointInd - 1, points[lastPointInd - 1].position);
			_bVersion++;

			Vector3 getHandlePosition(int ind, int otherind)
			{
				Vector3 r = points[ind] * 2f - points[otherind];
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
		return _points[CPIndex((ushort)cpIndex)].GetRotation(Vector3.forward);
	}

}