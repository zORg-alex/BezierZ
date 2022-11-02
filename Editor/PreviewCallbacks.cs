using UnityEditor;
using Utility;
using System;
using UnityEngine.SceneManagement;

namespace BezierCurveZ
{
	internal class PreviewCallbacks
	{
		public PreviewCallbacks(OtherCurve curve, Action<OtherCurve> unsubscribe, Action<OtherCurve> preview, SerializedProperty property)
		{
			this.curve = curve;
			this.unsubscribe = unsubscribe;
			this.preview = preview;
			this.property = property;
		}
		public OtherCurve curve;
		public Action<OtherCurve> unsubscribe;
		private readonly Action<OtherCurve> preview;
		private SerializedProperty property;

		public bool isExisting =>
			property != default ? curve == property.GetValue<OtherCurve>() : false;

		public void OnPreview(SceneView s) => preview(curve);
		public void OnPreview() => preview(curve);
		public void UnsubscribePreview() => unsubscribe(curve);
		public void UnsubscribePreview(Scene s) => unsubscribe(curve);
		public void UnsubscribePreviewIfNotOn() => unsubscribe(curve);

		public override bool Equals(object obj)
		{
			var c = (PreviewCallbacks)obj;
			return curve.Equals(c.curve) && unsubscribe == c.unsubscribe;
		}
		public override int GetHashCode() => base.GetHashCode();
	}
}