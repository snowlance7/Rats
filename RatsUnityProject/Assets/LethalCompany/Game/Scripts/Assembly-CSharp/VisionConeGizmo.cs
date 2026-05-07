using System;
using UnityEngine;

public static class VisionConeGizmo
{
	private const float DotThreshold = 0.5f;

	private const float Distance = 10f;

	private const int LineSegments = 4;

	private const int RadialSegments = 24;

	public static void DrawLOS(Transform gizmoTransform, float angle = 70f, float distance = 10f, Color? color = null, int lineSegments = 4, int radialSegments = 24)
	{
		Color rgbColor = (Gizmos.color = color ?? Color.cyan);
		Vector3 position = gizmoTransform.position;
		Vector3 forward = gizmoTransform.forward;
		Vector3 right = gizmoTransform.right;
		angle *= 2f;
		angle = Mathf.Clamp(angle, 1f, 359f);
		Vector3 vector = Quaternion.AngleAxis((0f - angle) / 2f, right) * forward;
		for (int i = 0; i < lineSegments; i++)
		{
			float num = (float)i * MathF.PI * 2f / (float)lineSegments;
			Gizmos.DrawLine(position, position + (Quaternion.AngleAxis(57.29578f * num, forward) * vector).normalized * distance);
		}
		float num2 = Mathf.Cos(angle * (MathF.PI / 180f) / 2f) * distance;
		float num3 = Mathf.Sin(angle * (MathF.PI / 180f) / 2f) * distance;
		DrawCircle(position + forward * num2, num3, forward);
		DrawCircle(position + forward * num2 / 2f, num3 / 2f, forward);
		if (angle > 180f)
		{
			DrawCircle(position, distance, forward);
		}
		for (int j = 1; j < lineSegments / 2 + 1; j++)
		{
			Vector3 normal = Quaternion.AngleAxis((float)j * 360f / (float)lineSegments, forward) * right;
			float startAngleDeg = 0f - angle / 2f;
			float arcAngleDeg = angle;
			DrawArc(position, distance, normal, forward, startAngleDeg, arcAngleDeg, radialSegments);
		}
		Color.RGBToHSV(rgbColor, out var H, out var S, out var V);
		Gizmos.color = Color.HSVToRGB(H, S, V / 2f);
		Gizmos.DrawLine(position, position + forward * distance);
	}

	private static void DrawCircle(Vector3 center, float radius, Vector3 normal)
	{
		Vector3 normalized = Vector3.Cross(normal, Vector3.right).normalized;
		if (normalized == Vector3.zero)
		{
			normalized = Vector3.Cross(normal, Vector3.up).normalized;
		}
		Vector3 normalized2 = Vector3.Cross(normalized, normal).normalized;
		Vector3 vector = center + normalized2 * radius;
		for (int i = 1; i <= 24; i++)
		{
			float f = (float)i * MathF.PI * 2f / 24f;
			Vector3 vector2 = center + (Mathf.Cos(f) * normalized2 + Mathf.Sin(f) * normalized) * radius;
			Gizmos.DrawLine(vector, vector2);
			vector = vector2;
		}
	}

	private static void DrawArc(Vector3 center, float radius, Vector3 normal, Vector3 referenceDir, float startAngleDeg, float arcAngleDeg, int segments)
	{
		normal = normal.normalized;
		Vector3 normalized = Vector3.ProjectOnPlane(referenceDir, normal).normalized;
		if (!(normalized == Vector3.zero))
		{
			Vector3 vector = Vector3.Cross(normal, normalized);
			float num = startAngleDeg * (MathF.PI / 180f);
			float num2 = arcAngleDeg * (MathF.PI / 180f);
			Vector3 vector2 = center + (Mathf.Cos(num) * normalized + Mathf.Sin(num) * vector) * radius;
			for (int i = 1; i <= segments; i++)
			{
				float num3 = (float)i / (float)segments;
				float f = num + num2 * num3;
				Vector3 vector3 = center + (Mathf.Cos(f) * normalized + Mathf.Sin(f) * vector) * radius;
				Gizmos.DrawLine(vector2, vector3);
				vector2 = vector3;
			}
		}
	}
}
