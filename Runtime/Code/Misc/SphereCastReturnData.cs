using UnityEngine;

namespace Assets.Code.Misc
{
	[LuauAPI]
	public class SphereCastReturnData
	{
		public int HitCount { get; }
		public RaycastHit[] RaycastHits { get; }

		public SphereCastReturnData(int hitCount, RaycastHit[] raycastHits)
		{
			this.HitCount = hitCount;
			this.RaycastHits = raycastHits;
		}
	}
}