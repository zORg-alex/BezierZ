using BezierZUtility;
using RectEx;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BezierCurveZ.Editor
{
	public class SelectRectContext
	{
		private Vector2 _mouseDownPos;

		public IEnumerable<int> Indexes => _oldIndexes.Where(ind=>!_removeIndexes.Contains(ind)).Concat(_newIndexes);
		public int Count => _oldIndexes.Count + _newIndexes.Count;
		private List<int> _oldIndexes = new();
		private List<int> _newIndexes = new();
		private List<int> _removeIndexes = new();

		public void DoSelectionRect(Event current, IEnumerable<Vector3> points, int controlId)
		{
			if (current.IsMouseDown(0))
			{
				_mouseDownPos = current.mousePosition;
				if (!current.shift && !current.control) _oldIndexes.Clear();
			}
			if (current.IsMouseUp(0)) {
				_mouseDownPos = default;
				_oldIndexes.AddRange(_newIndexes);
				_oldIndexes.RemoveAll(_removeIndexes.Contains);
				_newIndexes.Clear();
				_removeIndexes.Clear();
				return;
			}

			if (!current.IsRepaint() || _mouseDownPos == default) return;
			var rect = new Rect(_mouseDownPos, current.mousePosition - _mouseDownPos).Abs();

			Handles.BeginGUI();
			EditorStyles.selectionRect.Draw(rect, GUIContent.none, controlId);
			Handles.EndGUI();

			_newIndexes.Clear();
			_removeIndexes.Clear();
			rect = rect.Extend(15, 15);
			int i = 0;
			foreach(var point in points)
			{
				if (rect.Contains(HandleUtility.WorldToGUIPoint(point)))
				{
					if (current.control)
						_removeIndexes.Add(i);
					else
						_newIndexes.Add(i);
				}
				i++;
			}
		}

		internal void Cancel() => _mouseDownPos = default;

		internal bool IsSelecting() => _mouseDownPos != default;
	}
}