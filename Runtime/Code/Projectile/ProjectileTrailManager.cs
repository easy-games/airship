using System;
using System.Collections.Generic;
using DigitalRuby.FastLineRenderer;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Code.Projectiles
{
	public class ProjectileTrailManager : MonoBehaviour
	{
		public static ProjectileTrailManager Instance;

		[SerializeField]
		private bool showDebugInfo = false;

		[SerializeField]
		private FastLineRenderer lineRenderer;

		[SerializeField]
		private float trailStartDelaySec = 0.25f;

		[SerializeField]
		private float trailRadius = 0.5f;

		[SerializeField]
		private float trailLifetimeSec = 0.25f;

		private readonly Dictionary<int, ProjectileNetworkBehaviour> projectilesById = new Dictionary<int, ProjectileNetworkBehaviour>();
		private Dictionary<ProjectileNetworkBehaviour, FastLineRenderer> projectileToFLR = new Dictionary<ProjectileNetworkBehaviour, FastLineRenderer>();

		private TimeSpan sendToCacheTimeSpan;

		private void Awake()
		{
			Instance = this;

			this.sendToCacheTimeSpan = new TimeSpan(0, 0, Mathf.CeilToInt(this.trailLifetimeSec));
		}

		public void Register(ProjectileNetworkBehaviour projectileNetworkBehaviour)
		{
			this.projectilesById.Add(projectileNetworkBehaviour.gameObject.GetInstanceID(), projectileNetworkBehaviour);
		}

		public void Unregister(ProjectileNetworkBehaviour projectileNetworkBehaviour)
		{
			if (projectileNetworkBehaviour != null && !projectileNetworkBehaviour.IsDestroyed())
			{
				this.projectilesById.Remove(projectileNetworkBehaviour.gameObject.GetInstanceID());

				if (this.projectileToFLR.TryGetValue(projectileNetworkBehaviour, out var flr))
				{
					flr.SendToCacheAfter(this.sendToCacheTimeSpan);

					this.projectileToFLR.Remove(projectileNetworkBehaviour);
				}
			}
		}

		private void LateUpdate()
		{
			foreach (var kvp in this.projectilesById)
			{
				var projectile = kvp.Value;

				if (
					projectile.NetworkObject.IsSpawned &&
					projectile.useTrailLineRenderer &&
					projectile.PreviousVisualPosition.HasValue &&
					(Time.time - projectile.spawnTimeSec) > this.trailStartDelaySec)
				{
					var hasFLR = this.projectileToFLR.TryGetValue(projectile, out var flr);

					if (!hasFLR)
					{
						flr = FastLineRenderer.CreateWithParent(this.gameObject, this.lineRenderer);
						flr.Material.DisableKeyword("SOFTPARTICLES_ON");
						flr.Material.EnableKeyword("DISABLE_CAPS");

						this.projectileToFLR.Add(projectile, flr);
					}

					var start = projectile.PreviousVisualPosition.Value;
					var end = projectile.CurrentVisualPosition;

					var props = new FastLineRendererProperties()
					{
						Start = hasFLR ? end : start, // When using AppendLine(), just fill in the Start value.
						End = end,
						Color = Color.white,
						LineType = FastLineRendererLineSegmentType.Full,
						Radius = this.trailRadius,
					};

					props.SetLifeTime(this.trailLifetimeSec);

					if (hasFLR)
					{
						flr.AppendLine(props);
					}
					else
					{
						flr.AddLine(props);
					}

					flr.Apply();

					if (this.showDebugInfo)
					{
#if UNITY_EDITOR
						Debug.Log($"ProjectileManager.LateUpdate(frame: {Time.frameCount}) adding line from: ({start}, to: ({end}))");
						Debug.DrawLine(start, end, Color.yellow, 5f);
						DebugUtil.DrawSphere(start, Quaternion.identity, 0.25f, Color.black, 4, 5);
						DebugUtil.DrawSphere(end, Quaternion.identity, 0.35f, Color.white, 4, 5);
#endif
					}
				}
			}
		}
	}
}