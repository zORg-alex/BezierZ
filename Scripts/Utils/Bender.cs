using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace BezierCurveZ.MeshGeneration
{
	[ExecuteAlways]
	[RequireComponent(typeof(MeshFilter))]
	public class Bender : MonoBehaviour
	{
		[SerializeField, OnValueChanged("UpdateMeshes")]
		private Transform _meshFilterParent;
		[SerializeField, OnValueChanged("UpdateBend")]
		private Vector3 BendOriginPosition;
		[SerializeField, OnValueChanged("UpdateBend")]
		private Quaternion BendOriginRotation = Quaternion.identity;
		[SerializeField, OnValueChanged("UpdateBend")]
		private Curve Curve;
		[SerializeField, OnValueChanged("UpdateBend")]
		private float BendLength = 1f;
		[SerializeField, OnValueChanged("UpdateBend")]
		private bool ScaleBendToCurve;
		[SerializeField, OnValueChanged("UpdateBend")]
		private bool AutoNormals;

		private MeshFilter _meshFilter;
		public Mesh _originalMesh;

		private void Start() => Initialize();
		private void OnEnable()
		{
#if UNITY_EDITOR
			UnityEditor.AssemblyReloadEvents.afterAssemblyReload += Initialize;
#endif
		}

		private void Initialize()
		{
			TryGetComponent(out _meshFilter);
			Curve.OnCurveChanged += Curve_OnCurveChanged;
			UpdateMeshes();
		}

		private void Curve_OnCurveChanged(Curve curve) => UpdateBend();


#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("UpdateAll")]
#endif
		public void UpdateMeshes()
		{
			_originalMesh = MeshBendUtility.CombineMeshFilters(_meshFilterParent.GetComponentsInChildren<MeshFilter>(), _meshFilterParent.transform.worldToLocalMatrix);
			_meshFilter.sharedMesh = _originalMesh;
			UpdateBend();
		}

#if ODIN_INSPECTOR
		[Button]
#else
		[ContextMenu("UpdateBend")]
#endif
		public void UpdateBend()
		{
			if (_originalMesh == null) return;

			if (BendLength <= 0)
				_meshFilter.sharedMesh = _originalMesh;
			else
				_meshFilter.sharedMesh = MeshBendUtility.BendMesh(_originalMesh, _meshFilter.sharedMesh, Curve, BendLength, ScaleBendToCurve, AutoNormals);
		}
	}
}