using System.Linq;
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

public partial class OtherCurvePropertyDrawer
{
	private float EditorHeight(OtherCurve curve) => curve._isInEditMode ? EditorGUIUtility.singleLineHeight + 32 + 6 : 0;

	private void DrawEditor(Rect position)
	{
		EditorGUI.LabelField(position, $"I'm in Editor");
	}

	Vector3 TransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.TransformPoint(v) : v;
	Vector3 InverseTransformPoint(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformPoint(v) : v;
	Vector3 TransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.TransformDirection(v) : v;
	Vector3 InverseTransformDirection(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformDirection(v) : v;
	Vector3 TransformVector(Vector3 v) => targetIsGameObject ? targetTransform.TransformVector(v) : v;
	Vector3 InverseTransformVector(Vector3 v) => targetIsGameObject ? targetTransform.InverseTransformVector(v) : v;
	Matrix4x4 localToWorldMatrix => targetIsGameObject ? targetTransform.localToWorldMatrix : Matrix4x4.identity;
	Quaternion TransformRotation => targetIsGameObject ? targetTransform.rotation : Quaternion.identity;

	private Color CurveColor = Color.green * .6666f + Color.white * .3333f;
	private Color NormalColor = Color.red * .5f + Color.white * .5f;
	private Color UpColor = Color.green * .5f + Color.white * .5f;
	private Color ForwardColor = Color.blue * .5f + Color.white * .5f;
	private Color HandleColor = Color.white * .6666f;

	/// <summary>
	/// Draws OnMouseOver curve preview and base part of Editor draw call
	/// </summary>
	private void DrawCurve()
	{
		var c = Handles.color;
		var m = Handles.matrix;
		Handles.matrix = localToWorldMatrix;
		foreach (var segment in curve.Segments)
		{
			Handles.color = CurveColor;
			Handles.DrawBezier(segment[0], segment[3], segment[1], segment[2], CurveColor, null, curve._isMouseOverProperty ? 2f : 1f);
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
	/// Draw two sided curve
	/// </summary>
	/// <param name="vertexData"></param>
	/// <exception cref="NotImplementedException"></exception>
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
		var m = Handles.matrix;
		Handles.matrix = localToWorldMatrix;
		var cam = Camera.current;

		//if (!curve._previewOn)
		DrawCurve();

		Vector3 camLocalPos = InverseTransformPoint(cam.transform.position);
		foreach (var point in curve.points)
		{
			float size = HandleUtility.GetHandleSize(point) * .2f;
			if (point.type == OtherPoint.Type.Control)
			{
				Handles.DrawWireDisc(point, (point - camLocalPos), size);
			}
			else
			{
				Handles.DrawAAPolyLine(GetHandleShapePoints(point, camLocalPos, size));
			}
		}

		Handles.matrix = m;
	}

	private Vector3[] GetHandleShapePoints(OtherPoint point, Vector3 camLocalPos, float size)
	{
		var cross = (point - camLocalPos).normalized.Cross(point.forward).normalized;
		Vector3 d1 = (cross + point.forward) * size;
		Vector3 d2 = (cross - point.forward) * size;
		return new Vector3[] { point - d1, point - d2, point + d1, point + d2, point - d1 };
	}
}
