using Sirenix.OdinInspector;
using UnityEngine;

namespace BezierCurveZ.MeshGeneration
{
	[ExecuteAlways]
	[RequireComponent(typeof(MeshFilter))]
	public class Bender : MonoBehaviour
	{
		public Transform meshFilterParent;
		public Vector3 BendOriginPosition;
		public Quaternion BendOriginRotation = Quaternion.identity;
		public Curve Curve;
		public float BendLength = 1f;
		public bool ScaleBendToCurve;
		public bool AutoNormals;

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
			UpdateMeshes();
		}

		[Button]
		public void UpdateMeshes()
		{
			_originalMesh = MeshBendUtility.CombineMeshFilters(meshFilterParent.GetComponentsInChildren<MeshFilter>(), meshFilterParent.transform.worldToLocalMatrix);
			_meshFilter.sharedMesh = _originalMesh;
			//UpdateBend();
		}

		[ContextMenu("UpdateBend")]
#if ODIN_INSPECTOR
		[Button]
#endif
		public void UpdateBend()
		{
			if (_originalMesh == null) return;

			var mesh = _meshFilter.sharedMesh;
			MeshBendUtility.BendMesh(_originalMesh, ref mesh, Curve, BendOriginPosition, BendOriginRotation, BendLength, ScaleBendToCurve, AutoNormals);
		}
	}
}