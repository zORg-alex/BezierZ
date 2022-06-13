using UnityEngine;
using BezierCurveZ;
#if UNITY_EDITOR
using UnityEditor; 
#endif

[ExecuteAlways]
public class BezierZTest : MonoBehaviour
{
	public BezierCurveZ.Curve curve;
	public BezierCurveZ.Curve anotherCurve;
    public bool useRMFrames;
    public int RMFramesPerSegment = 5;
    public Vector3[] points = new Vector3[4];

	private void Refresh()
	{
		curve = new Curve();
  //      curve.SetRMFramesQuality(RMFramesPerSegment);
		//curve.useRotationMinimistionFrames = useRMFrames;
		curve.AddInitialPoints(points);
	}

#if UNITY_EDITOR
	public int  gizmoQuality = 50;
	public float normalLength = 1f;
	private void OnDrawGizmos_()
	{
		var si = 0;
		foreach (var segment in curve.Segments)
		{
			var t1 = transform.TransformPoint(segment[1]);
			var t2 = transform.TransformPoint(segment[2]);
			Vector3 a = transform.TransformPoint(segment[0]);
			Vector3 b = transform.TransformPoint(segment[3]);
			var aScale = HandleUtility.GetHandleSize(a) * .1f;
			var bScale = HandleUtility.GetHandleSize(b) * .1f;

			Handles.DrawBezier(a, b, t1, t2, Color.green, null, 2f);
			Handles.DrawWireDisc(a, -Camera.current.transform.forward, aScale);
			Handles.DrawWireDisc(b, -Camera.current.transform.forward, bScale);
			Handles.DrawAAPolyLine(a, t1);
			Handles.DrawAAPolyLine(b, t2);


			Handles.matrix = transform.localToWorldMatrix;
			Vector3 pp = curve.GetPoint(si, 0);
			Vector3[] bpoints = new Vector3[gizmoQuality];
			Handles.color = Color.blue * new Color(1, 1, 1, 200f / gizmoQuality);
			for (float i = 0; i < gizmoQuality; i++)
			{
				Vector3 p = curve.GetPoint(si, i / gizmoQuality);
				Handles.DrawAAPolyLine(p, p + curve.GetNormal(si, i / gizmoQuality) * normalLength);
				bpoints[(int)i] = p;
				pp = p;
			}
			Handles.color = Color.white;
			Handles.DrawAAPolyLine(bpoints);
			si++;
			//GetRMFrames(Quaternion.LookRotation(curve.GetRotation(0)), RMFramesPerSegment, segment[0], segment[1], segment[2], segment[3]);
		}


	}

	[CustomEditor(typeof(BezierZTest))]
	public class BezierZTestInspector : Editor
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			if (GUILayout.Button("Refresh"))
			{
				BezierZTest t = (BezierZTest)target;
				t.Refresh();
			}
		}
	}

	public void GetRMFrames(Quaternion firstRotation, int steps, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3)
	{
		var stepTime = 1f / steps;
		var currentRotation = firstRotation;

		for (int i = 1; i <= steps; i++)
		{
			var midTime = (i - .5f) * stepTime;
			Vector3 midPos = BezierCurveZ.CurveUtils.Evaluate(midTime, a0, a1, a2, a3);
			var localMidTangent = (CurveUtils.EvaluateDerivative(midTime, a0, a1, a2, a3) - midPos).normalized;
			var nextLocalTangent = CurveUtils.EvaluateLocalDerivative(midTime, a0, a1, a2, a3);//-Vector3.Reflect(currentRotation * Vector3.forward, localMidTangent);
			var nextUp = Vector3.Reflect(currentRotation * Vector3.up, localMidTangent);
			var nextRotation = Quaternion.LookRotation(nextLocalTangent, nextUp);

			Handles.color = Color.yellow * new Color(1,1,1,200f / gizmoQuality);
			Handles.DrawAAPolyLine(midPos, midPos + localMidTangent * .2f);
			Handles.color = Color.red * new Color(1, 1, 1, 200f / gizmoQuality);
			Vector3 nextPos = CurveUtils.Evaluate(i * stepTime, a0, a1, a2, a3);
			Handles.DrawAAPolyLine(nextPos, nextPos + nextUp * .2f);
			Handles.color = Color.green * new Color(1, 1, 1, 200f / gizmoQuality);
			Handles.DrawAAPolyLine(nextPos, nextPos + nextLocalTangent.normalized * .2f);
			Handles.color = Color.blue * new Color(1, 1, 1, 200f / gizmoQuality);
			Handles.DrawAAPolyLine(nextPos, nextPos + (nextRotation * Vector3.right) * .2f);

			//yield return nextRotation;
			currentRotation = nextRotation;
		}
	}
#endif
}
