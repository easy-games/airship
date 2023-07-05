using System;
using UnityEngine;

namespace Assets.Code.Misc
{
	[LuauAPI]
	public class TriggerWatcher : MonoBehaviour
	{
		private Rigidbody rb;
		private void Awake()
		{
			this.rb = this.GetComponent<Rigidbody>();
		}

		public event Action<Collider> OnEnter;

		private void OnTriggerEnter(Collider collider)
		{
			this.OnEnter?.Invoke(collider);
		}
	}
}
