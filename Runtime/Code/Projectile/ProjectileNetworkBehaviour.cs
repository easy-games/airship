using System;
using Assets.Code.Alignment;
using Assets.Code.Misc;
using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Assets.Code.Projectiles
{
	[LuauAPI]
	public class ProjectileNetworkBehaviour : NetworkBehaviour
	{
		[SyncVar][HideInInspector]
		private float gravity;

		[SyncVar(OnChange = nameof(startingVelocity_OnChange))][HideInInspector]
		private Vector3 startingVelocity;

		[SerializeField]
		private Transform graphicalTR;

		public bool useTrailLineRenderer = true;
		public CustomAlignmentOptions CustomAlignmentOptions;
		public float spawnTimeSec { get; private set; }
		public event Action<Collision> OnCollide;
		public event TriggerEnterDelegate triggerEnterEvent;
		public delegate void TriggerEnterDelegate(object hitPoint, object velocity, Collider collider);

		private CollisionWatcher collisionWatcher;

		private void TimeManager_OnTick()
		{
			// this.rigidbody.AddForce(new Vector3(0, this.gravity, 0), ForceMode.Acceleration);
		}

		private Rigidbody rigidbody;
		private void Awake()
		{
			if (RunCore.IsClient())
			{
				// ProjectileTrailManager.Instance.Register(this);
			}

			this.rigidbody = this.GetComponent<Rigidbody>();

			this.spawnTimeSec = Time.time;

			if (!gameObject.TryGetComponent(typeof(DestroyWatcher), out var destroyWatcher))
			{
				gameObject.AddComponent<DestroyWatcher>();
			}

			this.collisionWatcher = this.GetComponent<CollisionWatcher>();
			if (this.collisionWatcher == null)
			{
				this.collisionWatcher = this.AddComponent<CollisionWatcher>();
			}
			this.collisionWatcher.OnCollide += this.CollisionWatcher_OnCollide;

			InstanceFinder.TimeManager.OnTick += this.TimeManager_OnTick;
		}

		private void CollisionWatcher_OnCollide(Collision collision)
		{
			this.OnCollide?.Invoke(collision);
		}

		public void SetInitialShootingValues(Vector3 velocity, float gravity)
		{
			/* If the object is not yet initialized then
            * this is being set prior to network spawning.
            * Such a scenario occurs because the client which is
            * predicted spawning sets the synctype value before network
            * spawning to ensure the server receives the value.
            * Just as when the server sets synctypes, if they are set
            * before the object is spawned it's gauranteed clients will
            * get the value in the spawn packet; same practice is used here. */
			if (!base.IsSpawned)
			{
				this.SetVelocity(velocity);
			}

			this.startingVelocity = velocity;
			this.gravity = gravity;
		}

		/// <summary>
		/// When starting force changes set that velocity to the rigidbody.
		/// This is an example as how a predicted spawn can modify sync types for server and other clients.
		/// </summary>
		private void startingVelocity_OnChange(Vector3 prev, Vector3 next, bool asServer)
		{
			this.SetVelocity(next);
		}

		/// <summary>
		/// Sets velocity of the rigidbody.
		/// </summary>
		private void SetVelocity(Vector3 value)
		{
			this.rigidbody.velocity = value;
		}

		public Vector3? PreviousVisualPosition { get; private set; }
		public Vector3 CurrentVisualPosition { get => this.graphicalTR.position; }

		private void LateUpdate()
		{
			if (RunCore.IsClient() && !this.IsDestroyed())
			{
				this.PreviousVisualPosition = this.CurrentVisualPosition;
			}
		}

		public void Despawn()
		{
			this.Cleanup(despawn: true);
		}

		private void OnDestroy()
		{
			this.Cleanup(despawn: false);
		}

		private void Cleanup(bool despawn)
		{
			if (RunCore.IsClient())
			{
				// ProjectileTrailManager.Instance.Unregister(this);
			}

			if (this.collisionWatcher != null)
			{
				this.collisionWatcher.OnCollide -= this.CollisionWatcher_OnCollide;
			}

			if (InstanceFinder.TimeManager != null)
			{
				InstanceFinder.TimeManager.OnTick -= this.TimeManager_OnTick;
			}

			if (despawn && !this.IsDestroyed())
			{
				this.Despawn(this.gameObject, DespawnType.Destroy);
			}
		}
	}
}