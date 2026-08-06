/*******************************************************
Product - Smart Mouse Selection Tools
Publisher - TelePresent Games
			http://TelePresentGames.dk
Author    - Martin Hansen
Created   - 2026
(c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEngine;

namespace TelePresent.SmartMouse
{
	internal static class SmartMouseGeometry
	{

		public static bool RayIntersectsTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2,
			out Vector3 intersectionPoint, out float t)
		{
			intersectionPoint = Vector3.zero;
			t = 0f;
			const float epsilon = 1e-8f;

			Vector3 e1 = v1 - v0;
			Vector3 e2 = v2 - v0;
			Vector3 h = Vector3.Cross(ray.direction, e2);
			float a = Vector3.Dot(e1, h);

			if (Mathf.Abs(a) < epsilon) return false;

			float f = 1f / a;
			Vector3 s = ray.origin - v0;
			float u = f * Vector3.Dot(s, h);
			if (u < 0f || u > 1f) return false;

			Vector3 q = Vector3.Cross(s, e1);
			float v = f * Vector3.Dot(ray.direction, q);
			if (v < 0f || u + v > 1f) return false;

			t = f * Vector3.Dot(e2, q);
			if (t > epsilon)
			{
				intersectionPoint = ray.origin + ray.direction * t;
				return true;
			}
			return false;
		}

		// Two triangles of a corner quad, in the winding both hit-testers use.
		public static bool RayIntersectsQuad(Ray ray, Vector3[] corners, out Vector3 hitPoint)
		{
			if (RayIntersectsTriangle(ray, corners[0], corners[1], corners[2], out hitPoint, out _)) return true;
			if (RayIntersectsTriangle(ray, corners[0], corners[2], corners[3], out hitPoint, out _)) return true;
			return false;
		}

		public static int LargestAxis(Vector3 size)
		{
			if (size.x > size.y && size.x > size.z) return 0;
			if (size.y > size.z) return 1;
			return 2;
		}
	}
}
