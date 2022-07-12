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

	public Quaternion a;
	public Quaternion b;

	private void Refresh()
	{
		curve = new Curve();
		curve.SetInitialPoints(points);
	}

#if UNITY_EDITOR
	public int  gizmoQuality = 50;
	public float normalLength = 1f;
	private void OnDrawGizmos()
	{
	}

	private void DrawAxes(float handleSize, Vector3 position, Quaternion rotation)
	{
		var c = Handles.color;
		Handles.color = Color.red;
		Handles.DrawAAPolyLine(position, position + rotation * Vector3.right * handleSize);
		Handles.color = Color.green;
		Handles.DrawAAPolyLine(position, position + rotation * Vector3.up * handleSize);
		Handles.color = Color.blue;
		Handles.DrawAAPolyLine(position, position + rotation * Vector3.forward * handleSize);
		Handles.color = c;
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
#endif
}
