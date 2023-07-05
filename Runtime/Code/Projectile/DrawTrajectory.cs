using DigitalRuby.FastLineRenderer;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Projectiles
{
	[LuauAPI]
	public class DrawTrajectory : MonoBehaviour
	{
		public static DrawTrajectory Instance;

		[SerializeField]
		private float flightTimeSec = 5f;

		[SerializeField]
		private FastLineRenderer lineRenderer;

		[SerializeField]
		private int lineSegmentCount = 20;

		[SerializeField]
		private float radius = 0.25f;

		[SerializeField]
		private bool stopLineAtHit = true;

		[SerializeField]
		private LayerMask hitLayerMask;

#if UNITY_EDITOR
		[Header("Testing fields")]

		[SerializeField]
		private Transform startPoint;

		[SerializeField]
		private Transform endPoint;

		[SerializeField]
		private float mass;

		[SerializeField]
		private float velocityScaler = 1f;
#endif

		private List<Vector3> lineRendererBuffer;

		private readonly RaycastHit[] raycastHits = new RaycastHit[1];

		private void Awake()
		{
			Instance = this;

            this.lineRenderer.Material.DisableKeyword("SOFTPARTICLES_ON");
            this.lineRenderer.Material.EnableKeyword("DISABLE_CAPS");

            this.lineRendererBuffer = new List<Vector3>(this.lineSegmentCount * 2);

			for (var i = 0; i < this.lineSegmentCount * 2; i++)
			{
				this.lineRendererBuffer.Add(Vector3.zero);
			}
		}

#if UNITY_EDITOR
		[SerializeField]
		private bool showDebugInfo;

		[SerializeField]
		private bool updateTrajectory;

		[SerializeField]
		private bool shootPrefabNextFrame;

		[SerializeField]
		private GameObject bulletPrefab;

		private void Update()
		{
			if (this.updateTrajectory)
			{
				var velocity = (this.endPoint.position - this.startPoint.position) * this.velocityScaler;

				Debug.DrawRay(this.startPoint.position, velocity);

				this.UpdateTrajectory(this.startPoint.position, velocity, Physics.gravity.y);

				if (this.shootPrefabNextFrame)
				{
					this.shootPrefabNextFrame = false;

					var bulletGO = GameObject.Instantiate(this.bulletPrefab, this.startPoint.position, Quaternion.identity);

					var rb = bulletGO.GetComponent<Rigidbody>();
					rb.mass = this.mass;
					rb.velocity = velocity;
				}
			}
		}
#endif

		private bool isInitialized = false;
		public void UpdateTrajectory(Vector3 startingPoint, Vector3 velocity, float gravity)
		{
#if UNITY_EDITOR
			//Debug.Log($"DrawTrajectory.UpdateTrajectory(startingPoint: {startingPoint}, velocity: {velocity}), gravity: {gravity}, this.flightTimeSec: {this.flightTimeSec}");
#endif

			var vec3 = Vector3.zero;

			var stepTime = this.flightTimeSec / this.lineSegmentCount;

			var i = 0;

			Vector3? previousPoint = null;
			var hasHitSomething = false;
			for (; i < this.lineSegmentCount; i++)
			{
				var bufferIndex = i == 0 ? 0 : i * 2 - 1;

				Vector3 point;
				if (hasHitSomething)
				{
					point = previousPoint.Value;
				}
				else
				{
					var stepTimePassed = stepTime * i;

					var movementVector = new Vector3(
						velocity.x * stepTimePassed,
						velocity.y * stepTimePassed + 0.5f * gravity * stepTimePassed * stepTimePassed,
						velocity.z * stepTimePassed
					);

					point = startingPoint + movementVector;

#if UNITY_EDITOR
					if (previousPoint.HasValue)
					{
						if (this.showDebugInfo)
						{
							Debug.DrawLine(previousPoint.Value, point, Color.red, 10);
						}
					}
#endif

					if (this.stopLineAtHit && previousPoint.HasValue)
					{
						var diff = point - previousPoint.Value;

						var hitCount = Physics.RaycastNonAlloc(
							previousPoint.Value,
							Vector3.Normalize(diff),
							this.raycastHits,
							diff.magnitude,
							this.hitLayerMask.value,
							QueryTriggerInteraction.Ignore);

						hasHitSomething = hitCount > 0;

						if (hasHitSomething)
						{
							point = this.raycastHits[0].point;
						}
					}

					previousPoint = point;
				}

				this.lineRendererBuffer[bufferIndex] = point;

				if (i > 0 && i < this.lineSegmentCount - 1)
				{
					this.lineRendererBuffer[bufferIndex + 1] = point;
				}
			}

			if(this.isInitialized)
			{
				i = 0;
				for (; i < this.lineSegmentCount; i++)
				{
					var bufferIndex = i * 2;

					this.lineRenderer.ChangePosition(i, this.lineRendererBuffer[bufferIndex], this.lineRendererBuffer[bufferIndex + 1]);
				}
			}
			else
			{
                // Note: We need to call this to free up used lists within the library.
				// If we don't call it, the line renderer leaks lots of memory.
                this.lineRenderer.Reset();

                var props = new FastLineRendererProperties()
                {
                    Start = this.lineRendererBuffer[0],
                    End = this.lineRendererBuffer[this.lineRendererBuffer.Count - 1],
                    Color = Color.white,
                    LineType = FastLineRendererLineSegmentType.RoundJoin,
                    LineJoin = FastLineRendererLineJoin.AttachToPrevious,
                    Radius = this.radius,
                };

                this.lineRenderer.AddLines(
                    props,
                    this.lineRendererBuffer,
                    null,
                    startCap: false,
                    endCap: false);

				this.isInitialized = true;
            }

			this.lineRenderer.Apply();
		}

		public void DisableTrajectory()
		{
			this.lineRenderer.Reset();
			this.isInitialized = false;
		}
	}
}