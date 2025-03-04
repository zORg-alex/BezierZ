using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BezierCurveZ;
using Sirenix.OdinInspector;
using System;
using System.Linq;
using BezierCurveZ.MeshGeneration;
using UnityEditor;

//TODO Editor add one list for all things, add settings for node to update Transform/BendMeshes,
//that will know if there are any MeshColliders or not
/// <summary>
/// Bends set of Meshes inMeshFilters and MeshColliders along Curve.
/// First curve point should be pointing forward, since Bendspace is calculated from forward direction of this transform
/// </summary>
public class MeshBenderScript : MonoBehaviour
{
	[SerializeField, OnValueChanged(nameof(Generate))]
	private float _bendLength = 1f;
	[SerializeField, OnValueChanged(nameof(Generate))]
	private bool _scaleBendToCurve = true;
	[SerializeField, OnValueChanged(nameof(Generate))]
	private bool _autoNormals;
	[SerializeField]
	private Curve _curve;
	[SerializeField, OnValueChanged(nameof(OnEnable))]
	private bool _autoUpdate;
	private bool _prevAutoUpdate;

	[SerializeField]
	private MeshFilter[] _meshFilters;
	[SerializeField]
	private MeshCollider[] _meshColliders;
	[SerializeField]
	private Mesh[] _originalMeshes;
	[SerializeField]
	private Mesh[] _originalColliderMeshes;
	[SerializeField]
	private Transform[] _transforms;
	[SerializeField]
	private Vector3[] _positions;
	[SerializeField]
	private Quaternion[] _rotations;
	[SerializeField]
	private Matrix4x4[] _originalLocalToWorldMatrices;
	[SerializeField]
	private Matrix4x4 _thisWorldToLocalMatrix;
	[SerializeField]
	private Matrix4x4 _thisLocalToWorldMatrix;

	private void OnEnable()
	{
		CheckSubscriptipn(true);
		if (_autoUpdate)
			Generate();
	}

	private void CheckSubscriptipn(bool force = false)
	{
		if (force || _autoUpdate != _prevAutoUpdate)
		{
			_prevAutoUpdate = _autoUpdate;
			if (_autoUpdate)
			{
#if UNITY_EDITOR
				_curve.OnCurveChanged -= _curve_OnCurveChanged;
#endif
				_curve.OnCurveChanged += _curve_OnCurveChanged;
			}
			else
				_curve.OnCurveChanged -= _curve_OnCurveChanged;
		}

		void _curve_OnCurveChanged(Curve curve) => Generate();
	}

	[Button]
	public void ResetAndRecache()
	{
		Cache(true);
	}
	public void ResetTransformsAndMeshes()
	{
		int len = Mathf.Min(_meshFilters.Length, _originalMeshes?.Length ?? 0, _originalColliderMeshes?.Length ?? 0);
		for (int i = 0; i < len; i++)
		{
			if (_meshFilters.Length > i && _meshFilters[i])
				_meshFilters[i].sharedMesh = _originalMeshes[i];
			if (_meshColliders.Length > i && _meshColliders[i] && _originalColliderMeshes.Length > i && _originalColliderMeshes[i])
				_meshColliders[i].sharedMesh = _originalColliderMeshes[i];
			_transforms[i].localPosition = _positions[i];
			_transforms[i].localRotation = _rotations[i];
		}
	}

	private void Cache(bool force = false, bool dontResetTransforms = false)
	{
		if (!force && _originalMeshes.Length == _meshFilters.Length &&
			_meshFilters.Select((mf, i) => mf.sharedMesh.name == _originalMeshes[i].name).All(b => b) &&
			_meshColliders.Select((mc, i) => mc.sharedMesh.name == _originalColliderMeshes[i].name).All(b=>b)) return;

		if (_meshColliders.Length != _meshFilters.Length)
		{
			_meshColliders = new MeshCollider[_meshFilters.Length];
			for (int i = 0; i < _meshColliders.Length; i++)
			{
				_meshColliders[i] = _meshFilters[i].GetComponent<MeshCollider>();
			}
		}

		if (!dontResetTransforms)
			ResetTransformsAndMeshes();
		var newOriginalMeshes = new Mesh[_meshFilters.Length];
		var newOriginalColliderMeshes = new Mesh[_meshFilters.Length];
		int len = Mathf.Min(_originalMeshes.Length, newOriginalMeshes.Length);
		Array.Copy(_originalMeshes, newOriginalMeshes, len);
		Array.Copy(_originalColliderMeshes, newOriginalColliderMeshes, len);

		for (int i = 0; i < _originalMeshes.Length; i++)
		{
			//if `null` or different name
			if ((!_originalMeshes[i] || !_originalColliderMeshes[i]) ||
				_meshFilters[i].name != _originalMeshes[i].name ||
				_meshColliders[i].name != _originalColliderMeshes[i].name)
			{
				newOriginalMeshes[i] = _meshFilters[i].sharedMesh;
				newOriginalColliderMeshes[i] = _meshColliders[i].sharedMesh;
			}
		}
		for (int i = _originalMeshes.Length; i < newOriginalMeshes.Length; i++)
		{
			newOriginalMeshes[i] = _meshFilters[i].sharedMesh;
			newOriginalColliderMeshes[i] = _meshColliders[i].sharedMesh;
		}
		_originalMeshes = newOriginalMeshes;
		_originalColliderMeshes = newOriginalColliderMeshes;
		_transforms = _meshFilters.Select(mf => mf.transform).ToArray();
		_positions = _transforms.Select(SelectPosition).ToArray();
		_rotations = _transforms.Select(SelectRotation).ToArray();
		_originalLocalToWorldMatrices = _transforms.Select(SelectLocalToWorld).ToArray();

		Quaternion SelectRotation(Transform t) => t.localRotation;
		Vector3 SelectPosition(Transform t) => t.localPosition;
		Matrix4x4 SelectLocalToWorld(Transform t) => t.localToWorldMatrix;
	}


	[Button]
	public void Generate()
	{
		Cache();
		_thisWorldToLocalMatrix = transform.worldToLocalMatrix;
		_thisLocalToWorldMatrix = transform.localToWorldMatrix;
		for (int i = 0; i < _meshFilters.Length; i++)
			GenerateMesh(i);
		CheckSubscriptipn();
	}

	private void GenerateMesh(int i)
	{
		if (_bendLength <= 0)
			_meshFilters[i].sharedMesh = _originalMeshes[i];

		var meshTransform = _transforms[i];
		var curveDirection = _curve.GetEPRotation(0) * Vector3.forward;
		var distance = Vector3.Dot(_thisWorldToLocalMatrix.MultiplyPoint3x4(_positions[i]), curveDirection);
		var point = _curve.GetPointFromDistance(distance);
		meshTransform.position = _thisLocalToWorldMatrix.MultiplyPoint3x4(point.Position);
		meshTransform.rotation = _rotations[i] * point.Rotation;

		_meshFilters[i].sharedMesh = MeshBendUtility.BendMesh(_originalMeshes[i], _meshFilters[i].sharedMesh, _curve,
			_bendLength, _scaleBendToCurve, _autoNormals,
			_thisWorldToLocalMatrix * _originalLocalToWorldMatrices[i],
			meshTransform.worldToLocalMatrix * _thisLocalToWorldMatrix);

		_meshColliders[i].sharedMesh = MeshBendUtility.BendMesh(_originalColliderMeshes[i], _meshColliders[i].sharedMesh, _curve,
			_bendLength, _scaleBendToCurve, _autoNormals,
			_thisWorldToLocalMatrix * _originalLocalToWorldMatrices[i],
			meshTransform.worldToLocalMatrix * _thisLocalToWorldMatrix);
	}

}

//Adds MeshBenderScript to the end of previous Meshbender and chains curve controlling overlapping points. Might need to add controlled child curves running in parallel. Also add objects anchored to curve.
public class CurveChainBuilder : MonoBehaviour { }

