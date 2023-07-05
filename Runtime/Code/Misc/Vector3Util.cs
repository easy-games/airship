using UnityEngine;

namespace Assets.Code.Misc
{
	public class Vector3Util
	{
		public static Vector3 RotateTowards(Vector3 startDir, Vector3 towardsDir, float angle)
		{
			// If you know start will always be normalized, can skip this step
			startDir.Normalize();

			var axis = Vector3.Cross(startDir, towardsDir);

			// Handle case where start is colinear with up
			if (axis == Vector3.zero)
			{
				axis = Vector3.right;
			}

			return Quaternion.AngleAxis(angle, axis) * startDir;
		}
	}
}
