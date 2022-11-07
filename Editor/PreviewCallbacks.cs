using UnityEditor;
using Utility;
using System;
using UnityEngine.SceneManagement;

namespace BezierCurveZ.Editor
{
	internal class PreviewCallbacks
	{
		public PreviewCallbacks(Curve curve, Action<Curve> unsubscribe, Action<Curve> preview, SerializedProperty property)
		{
			this.curve = curve;
			this.unsubscribe = unsubscribe;
			this.preview = preview;
			this.property = property;
		}
		public Curve curve;
		public Action<Curve> unsubscribe;
		private readonly Action<Curve> preview;
		private SerializedProperty property;

		public bool isExisting =>
			property != default ? curve == property.GetValue<Curve>() : false;

		public void OnPreview(SceneView s) => OnPreview();
		public void OnPreview()
		{
				preview(curve);
		}
		public void UnsubscribePreview() => unsubscribe(curve);
		public void UnsubscribePreview(Scene s) => unsubscribe(curve);

		public override bool Equals(object obj)
		{
			var c = (PreviewCallbacks)obj;
			return curve.Equals(c.curve) && unsubscribe == c.unsubscribe;
		}
		public override int GetHashCode() => base.GetHashCode();
	}
}