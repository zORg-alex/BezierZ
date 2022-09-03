using System.Linq;
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using RectEx;
using Utility.Editor;
using BezierCurveZ;

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
	/// <summary>
	/// Transforms position from local space to world space.
	/// </summary>
	Vector3 TransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.TransformPoint(v) : v;
	/// <summary>
	/// Transforms position from world space to local space.
	/// </summary>
	Vector3 InverseTransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformPoint(v) : v;
	Vector3 TransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.TransformDirection(v) : v;
	Vector3 InverseTransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformDirection(v) : v;
	Vector3 TransformVector(Vector3 v) => targetIsGameObject ? targetTransform.TransformVector(v) : v;
	Vector3 InverseTransformVector(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformVector(v) : v;
	Matrix4x4 localToWorldMatrix => targetIsGameObject ? targetTransform.localToWorldMatrix : Matrix4x4.identity;
	Matrix4x4 worldToLocalMatrix => targetIsGameObject ? targetTransform.worldToLocalMatrix : Matrix4x4.identity;
	Quaternion TransformRotation => targetIsGameObject ? targetTransform.rotation : Quaternion.identity;

	public bool snapKeyDown { get; private set; }

	private Color CurveColor = Color.green * .6666f + Color.white * .3333f;
	private Color NormalColor = Color.red * .5f + Color.white * .5f;
	private Color UpColor = Color.green * .5f + Color.white * .5f;
	private Color ForwardColor = Color.blue * .5f + Color.white * .5f;
	private Color HandleColor = Color.white * .8f;
	private Color SelecrionRectColor = Color.blue * .5f + Color.white * .5f;
	private Color CutColor = Color.red * .5f + Color.white * .5f;
	private bool updateClosestPoint = true;
	private int closestIndex;
	private int closestControlIndex;
	private Tool currentInternalTool;
	private bool selectHandlesOnly;
	private OtherPoint closestPoint;
	private OtherPoint closestControlPoint;
	private Vector3 editedPosition;
	private Quaternion editedRotation;
	private bool drawTools = true;
	private EditorInputProcessor selectMultipleInputProcessor;
	private EditorInputProcessor contextMenuProcessor;
	//private EditorInputProcessor[] inputList;
	private List<int> selectedPointIdexes = new List<int>();
	private Vector2 mouseDownPosition;
	private Vector2 rightMouseDownPosition;
	private DateTime mouseDownDateTime;
	private bool selectingMultiple;
	private int controlID;
	private bool openContext;
	private bool cutInitiated;
	private Vector3 cutPoint;
	private bool extrudeInitiated;
	private int extrudedIndex;
	private OtherCurve _backupCurve;
	private int _backupClosestIndex;
	private Vector3 _backupEditedPosition;

	private void EditorStarted()
	{

	}

	private void EditorFinished()
	{

	}

	private void ProcessInput()
	{
		if (GetKeyDown(KeyCode.Delete) && closestIndex != -1)
		{
			current.Use();
			Undo.RecordObject(targetObject, "Delete Points");
			if (selectedPointIdexes.Count == 0)
				curve.DissolveCP(curve.GetSegmentIndex(closestIndex));
			else
				curve.RemoveMany(selectedPointIdexes);
		}

		if (GetKeyDown(KeyCode.C))
		{
			selectHandlesOnly = true;
		}
		else if (GetKeyUp(KeyCode.C))
		{
			selectHandlesOnly = false;
		}

		if (GetKeyDown(KeyCode.S))
			snapKeyDown = true;
		else if (GetKeyUp(KeyCode.S))
			snapKeyDown = false;

		if (GetKeyUp(KeyCode.X))
			UpdateClosestPoint();

		//Rect selection + Shift/Ctrl click
		if (selectingMultiple && GUIUtility.hotControl == 0 && current.type == EventType.MouseDrag )
			WhileSelectingMultiple();
		else if (selectingMultiple && GUIUtility.hotControl == 0 && mouseDownPosition == current.mousePosition)
		{
			selectedPointIdexes.Clear();
			WhileSelectingMultiple();
		}
		if (GetMouseDown(0))
		{
			mouseDownPosition = current.mousePosition;
			selectingMultiple = true;
		}
		else if (GetMouseUp(0) || GUIUtility.hotControl != 0)
		{
			selectingMultiple = false;
			mouseDownPosition = Vector2.zero;
		}

		//Context menu
		if (openContext && current.type == EventType.Repaint)
		{
			openContext = false;
			ContextMenuOpen();
		}
		if (GetMouseDown(1) && closestIndex != -1)
		{
			rightMouseDownPosition = current.mousePosition;
			mouseDownDateTime = DateTime.Now;
		}
		else if (GetMouseUp(1))
			if (!(mouseDownDateTime.AddSeconds(1f) < DateTime.Now || closestIndex == -1 || (rightMouseDownPosition - current.mousePosition).magnitude > 5f))
		{
			openContext = true;
			CallAllSceneViewRepaint();
		}


		//Cut/Extrude
		if (GetKeyDown(KeyCode.V))
		{
			if (PositionHandleIds_copy.@default.Has(GUIUtility.hotControl))
			{
				extrudeInitiated = true;
				extrudedIndex = closestIndex;
				StartExtruding();
			}
			else
			{
				cutInitiated = true;
				GUIUtility.hotControl = controlID;
			}
		}
		else if (GetKeyUp(KeyCode.V))
		{
			extrudeInitiated = false;
		}
		else if (cutInitiated && GetMouseDown(0))
		{
			cutInitiated = false;
			Cut();
		}
		else if (cutInitiated && (GetKeyDown(KeyCode.Escape) || GetMouseDown(1)))
		{
			cutInitiated = false;
		}



		bool GetKeyDown(KeyCode key) => current.type == EventType.KeyDown && current.keyCode == key;
		bool GetKeyUp(KeyCode key) => current.type == EventType.KeyUp && current.keyCode == key;
		bool GetMouseDown(int button) => current.type == EventType.MouseDown && current.button == button;
		bool GetMouseUp(int button) => current.type == EventType.MouseUp && current.button == button;
	}

	private void StartExtruding()
	{
		Undo.RecordObject(targetObject, "Curve Extrude");
		int segmentIndex = curve.GetSegmentIndex(closestIndex);
		BackupCurve();
		if (curve.GetCPTangentFromPoints(segmentIndex).Dot(TransformPoint(closestPoint) - editedPosition) > 0)
		{
			curve.SplitCurveAt(segmentIndex, 0f);
			closestIndex += 3;
		}
		else
		{
			curve.SplitCurveAt(segmentIndex - 1, 1f);
		}
		closestPoint = curve.Points[closestIndex];
	}

	private void BackupCurve()
	{
		_backupCurve = curve.Copy();
		_backupClosestIndex = closestIndex;
		_backupEditedPosition = editedPosition;
	}
	private void RestoreBackup()
	{
		curve.CopyFrom(_backupCurve);
		closestIndex = _backupClosestIndex;
		editedPosition = _backupEditedPosition;
		closestPoint = curve.Points[closestIndex];
	}

	private void Cut()
	{
		Undo.RecordObject(targetObject, "Curve Cut");
		curve.SplitCurveAt(InverseTransformPoint(cutPoint));
	}

	private void ContextMenuOpen()
	{
		var contextMenu = new GenericMenu();
		foreach (var mode in OtherPoint.AllModes)
		{
			var capturedMode = mode;
			contextMenu.AddItem(new GUIContent(mode.ToString()), curve.Points[closestIndex].mode == capturedMode, () =>
			{
				EditorGUI.BeginChangeCheck();
				if (selectedPointIdexes.Count > 0)
					foreach (var ind in selectedPointIdexes)
					{
						curve.SetPointMode(ind, capturedMode);
					}
				else
					curve.SetPointMode(closestIndex, capturedMode);
				Undo.RecordObject(targetObject, $"Curve {closestControlIndex} point mode changed to {mode}");
			});
		}

		Vector2 mousePosition = current.mousePosition;
		contextMenu.DropDown(new Rect(mousePosition, Vector2.zero));
	}

	private void WhileSelectingMultiple()
	{
		if (GUIUtility.hotControl != 0)
		{
			selectingMultiple = false;
			return;
		}
		if (!(current.type == EventType.Repaint || current.type == EventType.MouseDrag || current.type == EventType.MouseDown))
			return;
		if (!current.shift && !EditorGUI.actionKey)
			selectedPointIdexes.Clear();

		//Extend selection rect just enough to include points touching, this will also add clicked points
		var rect = new Rect(mouseDownPosition, current.mousePosition - mouseDownPosition).Abs().Extend(18, 18);
		for (int i = 0; i < curve.Points.Count; i++)
		{
			var point = curve.Points[i];
			if (!point.IsControlPoint || (curve.IsClosed && i == curve.LastPointInd)) continue;

			if (rect.Contains(HandleUtility.WorldToGUIPoint(TransformPoint(point))))
			{
				if (EditorGUI.actionKey)
					selectedPointIdexes.Remove(i);
				else if (!selectedPointIdexes.Contains(i))
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
		var c = Handles.color;
		var m = Handles.matrix;
		Handles.matrix = localToWorldMatrix;
		Handles.color = HandleColor;
		foreach (var segment in curve.Segments)
		{
			Handles.DrawAAPolyLine(segment[0], segment[1]);
			Handles.DrawAAPolyLine(segment[2], segment[3]);
		}
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
		Handles.color = c;
		Handles.matrix = m;

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
		controlID = GUIUtility.GetControlID(932795649, FocusType.Passive);
		current = Event.current;
		if (current.type == EventType.Layout)
			//Magic thing to stop mouse from selecting other objects
			HandleUtility.AddDefaultControl(controlID);

		//Update curve if Undo performed
		if (current.type == EventType.ValidateCommand && current.commandName == "UndoRedoPerformed")
			curve.UpdateVertexData(true);

		if (updateClosestPoint && !selectingMultiple)
			UpdateClosestPoint();
		ProcessInput();
		DrawStuff();
	}

	private void UpdateClosestPoint()
	{
		if (GUIUtility.hotControl != 0) return;
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
		if (curve.IsClosed && closestIndex == curve.LastPointInd) closestIndex = 0;
		closestPoint = curve.Points[closestIndex];
		var cind = closestIndex + (closestPoint.isRightHandle ? -1 : closestPoint.isLeftHandle ? 1 : 0);
		closestControlIndex = cind;
		closestControlPoint = curve.Points[cind];
		if (closestPoint.mode == OtherPoint.Mode.Linear)
		{
			editedPosition = TransformPoint(curve.Points[cind]);
		}
		else
		{
			editedPosition = TransformPoint(closestPoint.position);
		}
		editedRotation = toolRotation;
		if (minDist > 100)
			closestIndex = -1;
	}

	private void DrawStuff()
	{
		DrawCurve(curve);
		DrawCurveHandles();
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
		}
		Tools.current = Tool.None;
		var c = Handles.color;
		Handles.BeginGUI();
		GUI.Label(new Rect(5, 5, 200, 18), currentInternalTool.ToString());
		Handles.EndGUI();

		if (selectingMultiple && GUIUtility.hotControl == 0)
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
				Handles.color = c;
			}
		}

		if (cutInitiated && current.type == EventType.MouseMove)
		{
			cutPoint = HandleUtility.ClosestPointToPolyLine(curve.VertexDataPoints.SelectArray(v => TransformPoint(v)));
		} else if (cutInitiated && current.type == EventType.Repaint)
		{
			Handles.color = CutColor;
			Handles.DrawSolidDisc(cutPoint, cutPoint - Camera.current.transform.position, HandleUtility.GetHandleSize(cutPoint) * .1f);
			Handles.color = c;
		}
		if (closestIndex == -1 || !drawTools)
			return;
		else if (currentInternalTool == Tool.Move)
		{
			EditorGUI.BeginChangeCheck();
			var pos = Vector3.zero;
			if (!current.shift)
			{
				pos = Handles.PositionHandle(editedPosition, toolRotation);
				if (snapKeyDown)
					ProcessSnapping(ref pos);
			}
			else
				pos = Handles.FreeMoveHandle(editedPosition, toolRotation, HandleUtility.GetHandleSize(editedPosition) * .16f, Vector3.one * .2f, Handles.RectangleHandleCap);


			if (EditorGUI.EndChangeCheck())
			{
				if (extrudeInitiated)
				{
					Undo.RecordObject(targetObject, "Point extrusion");
				}
				else
				{
					Undo.RecordObject(targetObject, "Point position changed");

					//TODO Extrude mechanics. Here we know movement direction, so we can start extruding in right direction and change if needed
					//DoExtrusionWhileMoveTool(pos);

					if (selectedPointIdexes.Count == 0)
					{
						var ind = closestPoint.IsLinear ? closestControlIndex : closestIndex;

						curve.SetPointPosition(ind, InverseTransformPoint(pos));
						editedPosition = TransformPoint(curve.Points[closestIndex]);
					}
					else
					{
						var delta = InverseTransformVector(pos - editedPosition);
						foreach (var ind in selectedPointIdexes)
						{
							var point = curve.Points[ind];
							curve.SetPointPosition(ind, point + delta);
						}
						editedPosition += TransformVector(delta);
					}
				}
			}
		}
		else if (currentInternalTool == Tool.Rotate)
		{
			EditorGUI.BeginChangeCheck();
			int segmentIndex = curve.GetSegmentIndex(closestControlIndex);
			float handleSize = HandleUtility.GetHandleSize(editedPosition);

			var r = Handles.DoRotationHandle(editedRotation, editedPosition).normalized;
			var delta = r * editedRotation.Inverted();
			delta.ToAngleAxis(out var dangle, out var daxis);
			var pointDelta = Quaternion.AngleAxis(dangle, InverseTransformVector(daxis));
			editedRotation = r;
			if (selectedPointIdexes.Count == 0)
				GUIUtils.DrawAxes(editedPosition, localToWorldMatrix.rotation * curve.Points[closestControlIndex].rotation, handleSize, 3f);
			else
				foreach (var ind in selectedPointIdexes)
				{
					float size = HandleUtility.GetHandleSize(TransformPoint(curve.Points[ind]));
					GUIUtils.DrawAxes(TransformPoint(curve.Points[ind]), localToWorldMatrix.rotation * curve.Points[ind].rotation, size, 3f);
				}

			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(targetObject, "Point Roation changed");

				if (selectedPointIdexes.Count == 0)
				{
					var ind = closestControlIndex;

					curve.AddCPRotation(segmentIndex, pointDelta, true);
				}
				else
				{
					Vector3 localEditedPosition = InverseTransformPoint(editedPosition);
					foreach (var ind in selectedPointIdexes)
					{
						var pos = pointDelta * (curve.Points[ind] - localEditedPosition) + localEditedPosition;
						curve.SetPointPosition(ind, pos);
						curve.AddCPRotation(curve.GetSegmentIndex(ind), pointDelta, true);
					}
				}
			}
		}
	}

	private Quaternion toolRotation => Tools.pivotRotation == PivotRotation.Local ? localToWorldMatrix.rotation * closestPoint.rotation : Quaternion.identity;

	private void ProcessSnapping(ref Vector3 pos)
	{

	}
}
