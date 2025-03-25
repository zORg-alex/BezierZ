using System.Collections.Generic;
using UnityEngine;
using BezierCurveZ;
using Sirenix.OdinInspector;
using System;
using BezierCurveZ.MeshGeneration;
using BezierZUtility;

//TODO Editor add one list for all things, add settings for node to update Transform/BendMeshes,
//that will know if there are any MeshColliders or not
/// <summary>
/// Bends set of Meshes inMeshFilters and MeshColliders along Curve.
/// First curve point should be pointing forward, since Bendspace is calculated from forward direction of this transform
/// </summary>
public class MeshBenderScript : MonoBehaviour, IHaveCurve
{
	[PropertySpace()]
	[SerializeField, OnValueChanged(nameof(Generate))]
	private float _bendLength = 1f;
	[SerializeField, OnValueChanged(nameof(Generate))]
	private bool _scaleBendToCurve = true;
	[SerializeField, OnValueChanged(nameof(Generate))]
	private bool _autoNormals;
	[SerializeField]
	private Curve _curve;
	public Curve Curve => _curve;
	[SerializeField, OnValueChanged(nameof(OnEnable))]
	private bool _autoUpdate;
	private bool _prevAutoUpdate;

	[PropertySpace]
	[SerializeField, PropertyOrder(-1), Tooltip("Drag MeshFilters to add to a working list"), OnValueChanged(nameof(AddMeshFiltersInEditor))]
	private MeshFilter[] _addMeshFilters;

	[SerializeField, PropertyOrder(1)]
	private List<BentMeshCompound> _meshes = new();
	private Matrix4x4 _thisWorldToLocalMatrix;
	private Matrix4x4 _thisLocalToWorldMatrix;

	private void AddMeshFiltersInEditor()
	{
		AddMeshFilters(_addMeshFilters);
		_addMeshFilters = new MeshFilter[0];
	}

	public void AddMeshFilters(MeshFilter[] meshFilters)
	{
		foreach (var mf in meshFilters)
		{
			_meshes.Add(new BentMeshCompound(mf, transform));
		}
	}


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

	[ButtonGroup, Button(SdfIconType.ArrowCounterclockwise), PropertyTooltip("Restores meshes and transforms to before generation.")]
	public void ResetTransformsAndMeshes()
	{
		for (int i = 0; i < _meshes.Count; i++)
		{
			var c = _meshes[i];
			c.MeshFilter.sharedMesh = c.OriginalMesh;
			c.MeshCollider.sharedMesh = c.OriginalColliderMesh;
			c.Transform.position = transform.TransformPoint(c.Position);
			c.Transform.rotation = transform.rotation * c.Rotation;
		}
	}
	[ButtonGroup, Button(SdfIconType.DashSquare, Style = ButtonStyle.CompactBox), GUIColor("#FFAAAA"), PropertyTooltip("Clears cached data. !Reset before use!")]
	public void Clear() => this.UndoWrap(()=>_meshes.Clear());

	[ButtonGroup, Button(SdfIconType.PlusSquareFill), GUIColor("#AAFFAA"), PropertyTooltip("Adds all children MeshFilters and caches positions. In case of adding to a list, use Add Mesh Filters field.")]
	public void AddChildren() => this.UndoWrap(()=> AddMeshFilters(GetComponentsInChildren<MeshFilter>()));

	[PropertySpace]
	[Button, PropertyOrder(1)]
	public void Generate()
	{
#if UNITY_EDITOR
		UnityEditor.Undo.RecordObject(this, nameof(Generate));
#endif
		float curveLength = _curve.VertexData.CurveLength();
		var lengthCoef = _bendLength / curveLength;
		_thisWorldToLocalMatrix = transform.worldToLocalMatrix;
		_thisLocalToWorldMatrix = transform.localToWorldMatrix;
		for (int i = 0; i < _meshes.Count; i++)
			GenerateMesh(i);
		CheckSubscriptipn();


		void GenerateMesh(int i)
		{
			var c = _meshes[i];
			if (_bendLength <= 0)
				c.MeshFilter.sharedMesh = c.OriginalMesh;

			bool updateTransform = c.Update.HasFlag(BentMeshCompound.SettingsEnum.UpdateTransform);
			if (updateTransform)
			{
				var curveDirection = _curve.GetEPRotation(0) * Vector3.forward;
				var bendspaceDistance = Vector3.Dot(c.Position, curveDirection);
				var curvespaceDistance = Mathf.Clamp(bendspaceDistance / lengthCoef, 0, curveLength);
				var point = _curve.GetPointFromDistance(curvespaceDistance);
				var originOffset = c.Position - (curveDirection * bendspaceDistance);
				c.Transform.SetPositionAndRotation(
					_thisLocalToWorldMatrix.MultiplyPoint3x4(point.TRS.MultiplyPoint3x4(originOffset)),
					c.Transform.rotation = _thisLocalToWorldMatrix.rotation * point.Rotation * c.Rotation);
			}
			else
			{
				c.Transform.SetPositionAndRotation(
					_thisLocalToWorldMatrix.MultiplyPoint3x4(c.Position),
					_thisLocalToWorldMatrix.rotation * c.Rotation);
			}

			bool updateMesh = c.Update.HasFlag(BentMeshCompound.SettingsEnum.UpdateMesh);
			if (updateMesh)
			{
				Matrix4x4 meshToBendSpace = _thisWorldToLocalMatrix * c.LocalToWorld;
				Matrix4x4 bendSpaceToMesh = c.Transform.worldToLocalMatrix * _thisLocalToWorldMatrix;

				c.MeshFilter.sharedMesh = MeshBendUtility.BendMesh(c.OriginalMesh, c.MeshFilter.sharedMesh, _curve,
					_bendLength, _scaleBendToCurve, _autoNormals, meshToBendSpace, bendSpaceToMesh);

				c.MeshCollider.sharedMesh = MeshBendUtility.BendMesh(c.OriginalColliderMesh, c.MeshCollider.sharedMesh, _curve,
					_bendLength, _scaleBendToCurve, _autoNormals, meshToBendSpace, bendSpaceToMesh);
			}
			else if (c.MeshFilter.sharedMesh != c.OriginalMesh)
			{
				c.MeshFilter.sharedMesh = c.OriginalMesh;
				c.MeshCollider.sharedMesh = c.OriginalColliderMesh;
			}
		}
	}

	[Serializable]
	public class BentMeshCompound
	{
		[HorizontalGroup("A"), VerticalGroup("A/Left"), BoxGroup("A/Left/VisualMesh")]
		[SerializeField] private MeshFilter _meshFilter;
		public MeshFilter MeshFilter { get => _meshFilter; set => _meshFilter = value; }

		[VerticalGroup("A/Right"), BoxGroup("A/Right/Collider")]
		[SerializeField] private MeshCollider _meshCollider;
		public MeshCollider MeshCollider { get => _meshCollider; }

		[BoxGroup("A/Left/VisualMesh")]
		[SerializeField] private Mesh _originalMesh;
		public Mesh OriginalMesh { get => _originalMesh; }

		[BoxGroup("A/Right/Collider")]
		[SerializeField] private Mesh _originalColliderMesh;
		public Mesh OriginalColliderMesh { get => _originalColliderMesh; }

		[System.Flags]
		public enum SettingsEnum { UpdateTransform = 1, UpdateMesh = 2 }

		[EnumToggleButtons, HideLabel]
		[SerializeField] private SettingsEnum _update = SettingsEnum.UpdateTransform | SettingsEnum.UpdateMesh;
		public SettingsEnum Update { get => _update; set => _update = value; }

		[FoldoutGroup("Transform Initial")]
		[SerializeField] private Transform _transform;
		public Transform Transform { get => _transform; }

		[FoldoutGroup("Transform Initial")]
		[SerializeField] private Vector3 _position;
		public Vector3 Position { get => _position; }

		[FoldoutGroup("Transform Initial")]
		[SerializeField] private Quaternion _rotation;
		public Quaternion Rotation { get => _rotation; }

		[SerializeField, HideInInspector] private Transform _parent;
		public Matrix4x4 LocalToWorld
		{
			get
			{
				return _parent.localToWorldMatrix * Matrix4x4.TRS(_position, _rotation, _transform.localScale);
			}
		}

		public BentMeshCompound(MeshFilter mf, Transform curveTransform)
		{
			_parent = curveTransform;
			_meshFilter = mf;
			mf.TryGetComponent(out _meshCollider);
			_originalMesh = mf.sharedMesh;
			_originalColliderMesh = _meshCollider.sharedMesh;
			_transform = mf.transform;
			var cwtl = curveTransform.worldToLocalMatrix;
			_position = cwtl.MultiplyPoint3x4(_transform.position);
			_rotation = cwtl.rotation * _transform.rotation;
		}

	}
}

//Adds MeshBenderScript to the end of previous Meshbender and chains curve controlling overlapping points. Might need to add controlled child curves running in parallel. Also add objects anchored to curve.
public class CurveChainBuilder : MonoBehaviour { }

public interface IHaveCurve
{
	Curve Curve { get; }
}
