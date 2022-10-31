using System;

public class EditableClass
{
#if UNITY_EDITOR
	[NonSerialized]
	public bool _previewOn;
	[NonSerialized]
	public bool _isInEditMode;
	public bool _isMouseOverProperty;
	[NonSerialized]
	public int _id = new Random().Next();
	public static int _idCounter;
#endif
}
