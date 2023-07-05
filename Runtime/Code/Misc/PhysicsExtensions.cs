using UnityEngine;

namespace Assets.Code.Misc
{
	[LuauAPI]
	public class PhysicsExt
	{
		private static readonly RaycastHit[] raycast_hits = new RaycastHit[10];

		public static SphereCastReturnData EasySphereCast(Vector3 start, Vector3 direction, float radius, float distance, int layerMask)
		{
			var hitCount = Physics.SphereCastNonAlloc(new Ray(start, direction), radius, raycast_hits, distance, layerMask);
			return new SphereCastReturnData(hitCount, raycast_hits);
		}
	}
}
