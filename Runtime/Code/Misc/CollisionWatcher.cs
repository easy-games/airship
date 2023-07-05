using System;
using UnityEngine;

namespace Assets.Code.Misc
{
	[LuauAPI]
	public class CollisionWatcher : MonoBehaviour
	{
		private Rigidbody rb;
		private void Awake()
		{
			this.rb = this.GetComponent<Rigidbody>();
		}

		public event Action<Collision> OnCollide;

		private void OnCollisionEnter(Collision collision)
		{
			this.OnCollide?.Invoke(collision);
		}
	}
}
