using UnityEngine;

namespace BezierCurveZ
{
	//[CreateAssetMenu]
	public class CurveEditorPersistentData : ScriptableObject
	{
		static CurveEditorPersistentData _instance;
		public static CurveEditorPersistentData Instance => _instance;
		public CurveEditorPersistentData() => _instance = this;

		//public CurveEditorTransformOrientation.TransformOrientation CurrentOrientation;
	}
}