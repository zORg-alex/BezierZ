using System;

namespace BezierCurveZ
{
	//[Serializable]
	public class EditableClass
	{
#if UNITY_EDITOR
		public bool PreviewOn { get; set; }
		public bool IsInEditMode { get; set; }
		public bool IsInAlternateEditMode {  get; set; }
		public bool IsMouseOverProperty { get; set; }
		public int _id { get; } = random.Next();

		private static Random random = new Random();
#endif
	}

}