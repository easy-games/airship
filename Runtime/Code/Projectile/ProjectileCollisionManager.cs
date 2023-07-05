using UnityEngine;

namespace Assets.Code.Projectiles
{
	public class ProjectileCollisionManager : MonoBehaviour
	{
		public static ProjectileCollisionManager Instance;

		public LayerMask HitLayerMask;

		private void Awake()
		{
			Instance = this;
		}
	}
}