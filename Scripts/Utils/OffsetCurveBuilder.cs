using UnityEngine;
using BezierCurveZ;
using Sirenix.OdinInspector;

public class OffsetCurveBuilder : MonoBehaviour
{
	[SerializeField, ValidateInput("CurveProviderValid", "Curve Provider is not " + nameof(IHaveCurve))]
	private MonoBehaviour _curveProvider;
	public IHaveCurve CurveProvider { get => _curveProvider as IHaveCurve; }

	[SerializeField]
	private Curve _offsetCurve;
	public Curve OffsetCurve { get => _offsetCurve; private set => _offsetCurve = value; }
	[SerializeField]
	private Vector3 offset = Vector3.right;

	[Button]
	public void Generate()
	{
		_offsetCurve = CurveProvider.Curve.OffsetCurve(offset);
	}

	private bool CurveProviderValid() => _curveProvider is IHaveCurve;
}
