﻿using System.Linq;
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using RectEx;
using Utility.Editor;

public partial class OtherCurvePropertyDrawer
{
	private float EditorHeight(OtherCurve curve) => propValue._isInEditMode ? EditorGUIUtility.singleLineHeight + 64 + 2 * 3 : 0;

	private void DrawEditor(Rect position)
	{
		var lines = position.Column(new float[] { 0, 0 }, new float[] { EditorGUIUtility.singleLineHeight, 64 });
		var firstLine = lines[0].Row(2);
		var secLine = lines[1].Row(new float[] { 0, 1 }, new float[] { 64, 0 });

		//var maxAngleError = EditorGUI.PropertyField();

		//var minDistance = EditorGUI.PropertyField();

		if (GUI.Button(secLine[0], isOpenClosedTexture))
		{
			Undo.RecordObject(targetObject, $"IsClosed changed on {curve}");
			curve.SetIsClosed(!curve.IsClosed);
		}
	}

	Vector3 TransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.TransformPoint(v) : v;
	Vector3 InverseTransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformPoint(v) : v;
	Vector3 TransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.TransformDirection(v) : v;
	Vector3 InverseTransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformDirection(v) : v;
	Vector3 TransformVector(Vector3 v) => targetIsGameObject ? targetTransform.TransformVector(v) : v;
	Vector3 InverseTransformVector(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformVector(v) : v;
	Matrix4x4 localToWorldMatrix => targetIsGameObject ? targetTransform.localToWorldMatrix : Matrix4x4.identity;
	Quaternion TransformRotation => targetIsGameObject ? targetTransform.rotation : Quaternion.identity;

	public bool snapKeyDown { get; private set; }

	private Color CurveColor = Color.green * .6666f + Color.white * .3333f;
	private Color NormalColor = Color.red * .5f + Color.white * .5f;
	private Color UpColor = Color.green * .5f + Color.white * .5f;
	private Color ForwardColor = Color.blue * .5f + Color.white * .5f;
	private Color HandleColor = Color.white * .8f;
	private Color SelecrionRectColor = Color.blue * .5f + Color.white * .5f;
	private bool updateClosestPoint = true;
	private int closestIndex;
	private int closestControlIndex;
	private Tool currentInternalTool;
	private bool selectHandlesOnly;
	private OtherPoint closestPoint;
	private OtherPoint closestControlPoint;
	private Vector3 editedPosition;
	private bool drawTools = true;
	private EditorInputProcessor selectMultipleInputProcessor;
	private EditorInputProcessor contextMenuProcessor;
	//private EditorInputProcessor[] inputList;
	private List<int> selectedPointIdexes = new List<int>();
	private Vector2 mouseDownPosition;
	private DateTime mouseDownDateTime;
	private bool selectingMultiple;
	private int controlID;

	private void EditorStarted()
	{

	}

	private void EditorFinished()
	{

	}

	private void ProcessInput()
	{
		if (GetKeyDown(KeyCode.S))
			snapKeyDown = true;
		else if (GetKeyUp(KeyCode.S))
			snapKeyDown = false;

		//Rect selection + Shift/Ctrl click
		if (GetMouseDown(0))
		{
			mouseDownPosition = current.mousePosition;
			selectingMultiple = true;
			selectedPointIdexes.Clear();
		}
		else if (GetMouseUp(0))
		{
			selectingMultiple = false;
			mouseDownPosition = Vector2.zero;
		}
		if (selectingMultiple)
			WhileSelectingMultiple();

		//Context menu
		if (GetMouseDown(1) && closestIndex != -1)
		{
			mouseDownPosition = current.mousePosition;
			mouseDownDateTime = DateTime.Now;
		}
		else if (GetMouseUp(1) && !(mouseDownDateTime.AddSeconds(1f) < DateTime.Now || closestIndex == -1 || (mouseDownPosition - current.mousePosition).magnitude > 5f))
			ContextMenuOpen();



		bool GetKeyDown(KeyCode key) => current.type == EventType.KeyDown && current.keyCode == key;
		bool GetKeyUp(KeyCode key) => current.type == EventType.KeyUp && current.keyCode == key;
		bool GetMouseDown(int button) => current.type == EventType.MouseDown && current.button == button;
		bool GetMouseUp(int button) => current.type == EventType.MouseUp && current.button == button;
	}

	private void ContextMenuOpen()
	{
		var menu = new GenericMenu();
		foreach (var mode in OtherPoint.AllModes)
		{
			var capturedMode = mode;
			menu.AddItem(new GUIContent(mode.ToString()), curve.Points[closestIndex].mode == capturedMode, () =>
			{
				curve.SetPointMode(closestIndex, capturedMode);
			});
		}

		Vector2 mousePosition = current.mousePosition;
		menu.DropDown(new Rect(mousePosition, Vector2.zero));
	}

	private void WhileSelectingMultiple()
	{
		if (GUIUtility.hotControl != 0) {
			selectingMultiple = false;
			return;
		}
		if (!(current.type == EventType.Repaint || current.type == EventType.MouseDrag || current.type == EventType.MouseDown))
			return;
		if (!current.shift && !EditorGUI.actionKey)
			selectedPointIdexes.Clear();

		//Extend selection rect just enough to include points touching, this will also add clicked points
		var rect = new Rect(mouseDownPosition, current.mousePosition - mouseDownPosition).Abs().Extend(18,18);
		for (int i = 0; i < curve.Points.Count; i++)
		{
			var point = curve.Points[i];
			if (!point.IsControlPoint) continue;

			if (rect.Contains(HandleUtility.WorldToGUIPoint(TransformPoint(point))))
			{
				if (EditorGUI.actionKey)
					selectedPointIdexes.Remove(i);
				else if(!selectedPointIdexes.Contains(i))
					selectedPointIdexes.Add(i);
			}
		}

		CallAllSceneViewRepaint();
	}

	/// <summary>
	/// Draws curve in local space
	/// </summary>
	private void DrawCurve(OtherCurve curve)
	{
		var c = Handles.color;
		var m = Handles.matrix;
		Handles.matrix = localToWorldMatrix;
		foreach (var segment in curve.Segments)
		{
			Handles.color = CurveColor;
			Handles.DrawBezier(segment[0], segment[3], segment[1], segment[2], CurveColor, null, curve._isMouseOverProperty ? 2.5f : 1.5f);
			Handles.color = HandleColor;
			if (curve._isInEditMode)
			{
				Handles.DrawAAPolyLine(segment[0], segment[1]);
				Handles.DrawAAPolyLine(segment[2], segment[3]);
			}
		}

		Handles.color = Color.red / 2 + Color.white / 2;
		//foreach (var vert in curve.VertexData)
		//{
		//	Handles.DrawAAPolyLine(vert.point, vert.point + vert.normal * .2f);
		//	//Handles.Label(vert.point, $"{vert.length}, {vert.time}");
		//}
		//DrawCurveFromVertexData(curve.VertexData);
		Handles.color = c;
		Handles.matrix = m;
	}

	/// <summary>
	/// Draws curve handles in local space
	/// </summary>
	void DrawCurveHandles()
	{

		var cam = Camera.current;
		Vector3 camLocalPos = InverseTransformPoint(cam.transform.position);
		for (int i = 0; i < curve.PointCount; i++)
		{
			var point = curve.Points[i];
			float size = HandleUtility.GetHandleSize(point) * .2f;
			bool isSelected = selectedPointIdexes.Contains(i);
			Handles.color = HandleColor * (isSelected ? 1 : .8f);
			if (point.type == OtherPoint.Type.Control)
			{
				GUIUtils.DrawCircle(point, point - camLocalPos, size, false, isSelected ? 2.5f : 1.5f, 24);
			}
			else
			{
				Handles.DrawAAPolyLine(1.5f, GetHandleShapePoints(point, point - camLocalPos, size));
			}
		}

		Vector3[] GetHandleShapePoints(OtherPoint point, Vector3 normal, float size)
		{
			var cross = normal.normalized.Cross(point.forward).normalized;
			Vector3 d1 = (cross + point.forward) * size;
			Vector3 d2 = (cross - point.forward) * size;
			return new Vector3[] { point - d1, point - d2, point + d1, point + d2, point - d1 };
		}
	}
	/// <summary>
	/// Draw two sided curve
	/// </summary>
	/// <param name="vertexData"></param>
	private void DrawCurveFromVertexData(IEnumerable<(Vector3 position, Vector3 up)> vertexData)
	{
		var c = Handles.color;
		var m = Handles.matrix;
		Handles.matrix = localToWorldMatrix;
		var vertices = vertexData.Take(1).Select(v => v.position).ToList();
		Vector3 campos = InverseTransformPoint(Camera.current.transform.position);
		var upDotCamera = Vector3.Dot(vertexData.FirstOrDefault().up, vertexData.FirstOrDefault().position - campos);
		foreach (var v in vertexData.Skip(1))
		{
			vertices.Add(v.position);
			var newDot = Vector3.Dot(v.up, v.position - campos);
			if (upDotCamera != newDot)
			{
				DrawVertices(vertices, !(upDotCamera > 0));

				vertices.Clear();
				vertices.Add(v.position);
				upDotCamera = newDot;
			}
		}
		DrawVertices(vertices, upDotCamera > 0);

		Handles.color = c;
		Handles.matrix = m;

		static void DrawVertices(List<Vector3> vertices, bool towardCamera)
		{
			Handles.color = towardCamera ? Color.green : Color.red * .6666f + Color.green * .3333f;
			Handles.DrawAAPolyLine((towardCamera ? 4 : 2), vertices.ToArray());
		}
	}

	private void DrawSceneEditor()
	{
		if (updateClosestPoint && !selectingMultiple)
			UpdateClosestPoint();
		DrawStuff();
	}

	private void UpdateClosestPoint()
	{
		//Just a reminder: IMGUI coordinates starts from top-left of actual view
		Vector2 mousePos = current.mousePosition;
		var minDist = float.MaxValue;
		var minHDist = float.MaxValue;
		closestIndex = -1;
		closestControlIndex = -1;
		for (int i = 0; i < curve.PointCount; i++)
		{
			bool selectControlsOnly = currentInternalTool == Tool.Rotate;
			bool isContolPoint = curve.IsControlPoint(i);
			if ((selectHandlesOnly && isContolPoint) || selectControlsOnly && !isContolPoint)
				continue;

			var dist = HandleUtility.WorldToGUIPoint(TransformPoint(curve.Points[i])).DistanceTo(mousePos);
			if (dist <= minDist + .01f)
			{
				minDist = dist;
				closestIndex = i;
			}
			else
			if (dist <= minHDist + .1f)
			{
				minHDist = dist;
				//closestHandleIndex = i;
			}
		}
		closestPoint = curve.Points[closestIndex];
		var cind = closestIndex + (closestPoint.isRightHandle ? -1 : closestPoint.isLeftHandle ? 1 : 0);
		closestControlIndex = cind;
		closestControlPoint = curve.Points[cind];
		if (closestPoint.mode == OtherPoint.Mode.Linear)
		{
			editedPosition = TransformPoint(curve.Points[cind]);
		}
		else
			editedPosition = TransformPoint(closestPoint.position);
		if (minDist > 100)
			closestIndex = -1;
	}

	private void DrawStuff()
	{
		controlID = GUIUtility.GetControlID(932795649, FocusType.Passive);
		current = Event.current;
		if (current.type == EventType.Layout)
			//Magic thing to stop mouse from selecting other objects
			HandleUtility.AddDefaultControl(controlID);

		//Update curve if Undo performed
		if (current.type == EventType.ValidateCommand && current.commandName == "UndoRedoPerformed")
			curve.Update(true);

		ProcessInput();
		var m = Handles.matrix;
		var c = Handles.color;
		Handles.matrix = localToWorldMatrix;
		DrawCurve(curve);
		DrawCurveHandles();
		Handles.matrix = m;
		Handles.color = c;

		DrawTools();
	}

	/// <summary>
	/// Draws tool handles in global space
	/// </summary>
	private void DrawTools()
	{
		if (Tools.current != Tool.None)
		{
			currentInternalTool = Tools.current;
			Tools.current = Tool.None;
		}

		if (selectingMultiple && GUIUtility.hotControl == 0 )
		{
			if (current.type == EventType.Repaint)
			{
				var mousePos = current.mousePosition;
				var rect = new Vector3[] {
				HandleUtility.GUIPointToWorldRay(mouseDownPosition).GetPoint(.1f),
				HandleUtility.GUIPointToWorldRay(new Vector2(mouseDownPosition.x, mousePos.y)).GetPoint(.1f),
				HandleUtility.GUIPointToWorldRay(mousePos).GetPoint(.1f),
				HandleUtility.GUIPointToWorldRay(new Vector2(mousePos.x, mouseDownPosition.y)).GetPoint(.1f),
				HandleUtility.GUIPointToWorldRay(mouseDownPosition).GetPoint(.1f),
			};
				Handles.color = SelecrionRectColor * .5f;
				Handles.DrawAAConvexPolygon(rect);
				Handles.color = SelecrionRectColor;
				Handles.DrawAAPolyLine(rect);
			}
		}

		if (closestIndex == -1 || !drawTools)
			return;
		else if (currentInternalTool == Tool.Move)
		{
			Handles.Label(closestPoint, GUIUtility.hotControl.ToString());

			EditorGUI.BeginChangeCheck();
			var pos = Vector3.zero;
			var rotation = Tools.pivotRotation == PivotRotation.Local ? TransformRotation * closestPoint.rotation : Quaternion.identity;
			if (!current.shift)
			{
				pos = Handles.PositionHandle(editedPosition, rotation);
				if (snapKeyDown)
					ProcessSnapping(ref pos);
			}
			else
				pos = Handles.FreeMoveHandle(editedPosition, rotation, HandleUtility.GetHandleSize(editedPosition) * .16f, Vector3.one * .2f, Handles.RectangleHandleCap);


			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(targetObject, "Point position changed");

				//TODO Extrude mechanics. Here we know movement direction, so we can start extruding in right direction and change if needed
				//DoExtrusionWhileMoveTool(pos);

				if (selectedPointIdexes.Count == 0)
				{
					var index = closestPoint.IsLinear ? closestControlIndex : closestIndex;

					curve.SetPointPosition(index, InverseTransformPoint(pos));
				}
				else
				{
					var delta = InverseTransformPoint(pos) - curve.Points[closestIndex];
					foreach (var ind in selectedPointIdexes)
					{
						var point = curve.Points[ind];
						curve.SetPointPosition(ind, point + delta);
					}
				}
				editedPosition = TransformPoint(curve.Points[closestIndex]);
			}
		}
	}

	private void ProcessSnapping(ref Vector3 pos)
	{

	}
}
