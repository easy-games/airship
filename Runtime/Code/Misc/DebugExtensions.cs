using UnityEngine;
using UnityEditor;

/** Taken from: https://dev-tut.com/2022/unity-debug/ */
[LuauAPI]
public class DebugUtil : UnityEngine.Debug
{
	public static void TogglePauseEngine() {
		#if UNITY_EDITOR
			EditorApplication.isPaused = !EditorApplication.isPaused;
		#endif
	}
	
	public static void DrawSingleLine(Vector3 startPosition, Vector3 endPosition, Color color, float durationSec = 0) {
		DrawLine(startPosition, endPosition, color, durationSec);
	}
	
	public static void DrawBox(Vector3 position, Quaternion orientation, Vector3 halfSize, Color color, float durationSec = 0)
	{
		Vector3 offsetX = orientation * Vector3.right * halfSize.x;
		Vector3 offsetY = orientation * Vector3.up * halfSize.y;
		Vector3 offsetZ = orientation * Vector3.forward * halfSize.z;
 
		Vector3 pointA = -offsetX + offsetY;
		Vector3 pointB = offsetX + offsetY;
		Vector3 pointC = offsetX - offsetY;
		Vector3 pointD = -offsetX - offsetY;

		Vector2 planeSize = new Vector2(halfSize.x, halfSize.y) * 2;
		DrawRect(position - offsetZ, orientation, planeSize, color, durationSec);
		DrawRect(position + offsetZ, orientation, planeSize, color, durationSec);
 
		DrawLine(pointA - offsetZ + position, pointA + offsetZ + position, color, durationSec);
		DrawLine(pointB - offsetZ + position, pointB + offsetZ + position, color, durationSec);
		DrawLine(pointC - offsetZ + position, pointC + offsetZ + position, color, durationSec);
		DrawLine(pointD - offsetZ + position, pointD + offsetZ + position, color, durationSec);
	}
	
	// Draw a rectangle defined by two points, origin and orientation
	public static void DrawRect(Vector3 origin, Quaternion orientation, Vector2 extent, Color color, float durationSec = 0) 
	{
		// Calculate rotated axes
		Vector3 rotatedRight = orientation * Vector3.right;
		Vector3 rotatedUp = orientation * Vector3.up;
         
		// Calculate each rectangle point
		Vector3 pointA = origin - rotatedRight * (extent.x * .5f) + rotatedUp * (extent.y * .5f);
		Vector3 pointB = pointA + rotatedRight * extent.x;
		Vector3 pointC = pointB - rotatedUp * extent.y;
		Vector3 pointD = pointC - rotatedRight * extent.x;
 
		DrawQuad(pointA, pointB, pointC, pointD, color, durationSec);
	}
	
	public static void DrawQuad(Vector3 pointA, Vector3 pointB, Vector3 pointC, Vector3 pointD, Color color, float durationSec = 0)
	{
		// Draw lines between the points
		DrawLine(pointA, pointB, color, durationSec);
		DrawLine(pointB, pointC, color, durationSec);
		DrawLine(pointC, pointD, color, durationSec);
		DrawLine(pointD, pointA, color, durationSec);
	}
	
	public static void DrawSphere(Vector3 position, Quaternion orientation, float radius, Color color, int segments = 4, float durationSec = 0)
	{
		if (segments < 2)
		{
			segments = 2;
		}

		int doubleSegments = segments * 2;

		// Draw meridians

		float meridianStep = 180.0f / segments;

		for (int i = 0; i < segments; i++)
		{
			DrawCircle(position, orientation * Quaternion.Euler(0, meridianStep * i, 0), radius, doubleSegments, color, durationSec);
		}

		// Draw parallels

		Vector3 verticalOffset = Vector3.zero;
		float parallelAngleStep = Mathf.PI / segments;
		float stepRadius = 0.0f;
		float stepAngle = 0.0f;

		for (int i = 1; i < segments; i++)
		{
			stepAngle = parallelAngleStep * i;
			verticalOffset = (orientation * Vector3.up) * Mathf.Cos(stepAngle) * radius;
			stepRadius = Mathf.Sin(stepAngle) * radius;

			DrawCircle(position + verticalOffset, orientation * Quaternion.Euler(90.0f, 0, 0), stepRadius, doubleSegments, color, durationSec);
		}
	}

	public static void DrawCircle(Vector3 position, Quaternion rotation, float radius, int segments, Color color, float durationSec)
	{
		// If either radius or number of segments are less or equal to 0, skip drawing
		if (radius <= 0.0f || segments <= 0)
		{
			return;
		}

		// Single segment of the circle covers (360 / number of segments) degrees
		float angleStep = (360.0f / segments);

		// Result is multiplied by Mathf.Deg2Rad constant which transforms degrees to radians
		// which are required by Unity's Mathf class trigonometry methods

		angleStep *= Mathf.Deg2Rad;

		// lineStart and lineEnd variables are declared outside of the following for loop
		Vector3 lineStart = Vector3.zero;
		Vector3 lineEnd = Vector3.zero;

		for (int i = 0; i < segments; i++)
		{
			// Line start is defined as starting angle of the current segment (i)
			lineStart.x = Mathf.Cos(angleStep * i);
			lineStart.y = Mathf.Sin(angleStep * i);
			lineStart.z = 0.0f;

			// Line end is defined by the angle of the next segment (i+1)
			lineEnd.x = Mathf.Cos(angleStep * (i + 1));
			lineEnd.y = Mathf.Sin(angleStep * (i + 1));
			lineEnd.z = 0.0f;

			// Results are multiplied so they match the desired radius
			lineStart *= radius;
			lineEnd *= radius;

			// Results are multiplied by the rotation quaternion to rotate them 
			// since this operation is not commutative, result needs to be
			// reassigned, instead of using multiplication assignment operator (*=)
			lineStart = rotation * lineStart;
			lineEnd = rotation * lineEnd;

			// Results are offset by the desired position/origin 
			lineStart += position;
			lineEnd += position;

			// Points are connected using DrawLine method and using the passed color
			DrawLine(lineStart, lineEnd, color, durationSec);
		}
	}

	public static void DrawArc(float startAngle, float endAngle,
		Vector3 position, Quaternion orientation, float radius,
		Color color, bool drawChord = false, bool drawSector = false,
		int arcSegments = 32, float durationSec = 0)
	{
		float arcSpan = Mathf.DeltaAngle(startAngle, endAngle);

		// Since Mathf.DeltaAngle returns a signed angle of the shortest path between two angles, it 
		// is necessary to offset it by 360.0 degrees to get a positive value
		if (arcSpan <= 0)
		{
			arcSpan += 360.0f;
		}

		// angle step is calculated by dividing the arc span by number of approximation segments
		float angleStep = (arcSpan / arcSegments) * Mathf.Deg2Rad;
		float stepOffset = startAngle * Mathf.Deg2Rad;

		// stepStart, stepEnd, lineStart and lineEnd variables are declared outside of the following for loop
		float stepStart = 0.0f;
		float stepEnd = 0.0f;
		Vector3 lineStart = Vector3.zero;
		Vector3 lineEnd = Vector3.zero;

		// arcStart and arcEnd need to be stored to be able to draw segment chord
		Vector3 arcStart = Vector3.zero;
		Vector3 arcEnd = Vector3.zero;

		// arcOrigin represents an origin of a circle which defines the arc
		Vector3 arcOrigin = position;

		for (int i = 0; i < arcSegments; i++)
		{
			// Calculate approximation segment start and end, and offset them by start angle
			stepStart = angleStep * i + stepOffset;
			stepEnd = angleStep * (i + 1) + stepOffset;

			lineStart.x = Mathf.Cos(stepStart);
			lineStart.y = Mathf.Sin(stepStart);
			lineStart.z = 0.0f;

			lineEnd.x = Mathf.Cos(stepEnd);
			lineEnd.y = Mathf.Sin(stepEnd);
			lineEnd.z = 0.0f;

			// Results are multiplied so they match the desired radius
			lineStart *= radius;
			lineEnd *= radius;

			// Results are multiplied by the orientation quaternion to rotate them 
			// since this operation is not commutative, result needs to be
			// reassigned, instead of using multiplication assignment operator (*=)
			lineStart = orientation * lineStart;
			lineEnd = orientation * lineEnd;

			// Results are offset by the desired position/origin 
			lineStart += position;
			lineEnd += position;

			// If this is the first iteration, set the chordStart
			if (i == 0)
			{
				arcStart = lineStart;
			}

			// If this is the last iteration, set the chordEnd
			if (i == arcSegments - 1)
			{
				arcEnd = lineEnd;
			}

			DrawLine(lineStart, lineEnd, color, durationSec);
		}

		if (drawChord)
		{
			DrawLine(arcStart, arcEnd, color, durationSec);
		}
		if (drawSector)
		{
			DrawLine(arcStart, arcOrigin, color, durationSec);
			DrawLine(arcEnd, arcOrigin, color, durationSec);
		}
	}
}