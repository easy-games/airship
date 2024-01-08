using System;
using System.Collections.Generic;
using System.Linq;
using Code.Projectile;
using FishNet;
using UnityEngine;

[RequireComponent(typeof(DestroyWatcher))]
[LuauAPI]
public class AirshipProjectile : MonoBehaviour
{
    /// <summary>
    /// Direction to travel.
    /// </summary>
    private Vector3 velocity;

    public float gravity;
    public float drag;
    /// <summary>
    /// The item that launched this projectile
    /// </summary>
    public int launcherItemTypeId;
    public int itemTypeId;
    private int updateCounter = 0;

    /// <summary>
    /// Distance remaining to catch up. This is calculated from a passed time and move rate.
    /// </summary>
    private float passedTime = 0f;

    /// <summary>
    /// Fires when a projectile collides with another collider.
    /// Params: ProjectileHitEvent
    /// </summary>
    public event Action<object> OnHit;

    private bool destroyed = false;

    private uint spawnTick;
    private uint prevTick;
    
    private Vector3 prevPos;
    private float lastPhysicsStepTime;

    private RaycastHit[] raycastResults = new RaycastHit[10];

    
#if UNITY_EDITOR
    private List<Vector3> checkedPoints = new List<Vector3>();
#endif

    /// <summary>
    /// Initializes this projectile.
    /// </summary>
    /// <param name="direction">Direction to travel.</param>
    /// <param name="passedTime">How far in time this projectile is behind te prediction.</param>
    public void Initialize(Vector3 startingVelocity, float gravity, float drag, float passedTime, int itemTypeId, int launcherItemTypeId) {
        this.velocity = startingVelocity;
        this.gravity = gravity;
        this.drag = drag;
        this.passedTime = passedTime;
        this.itemTypeId = itemTypeId;
        this.launcherItemTypeId = launcherItemTypeId;
        this.UpdateRotation();
        this.spawnTick = InstanceFinder.TimeManager.LocalTick;
        prevPos = transform.position;
        lastPhysicsStepTime = Time.time;
    }

    private void UpdateRotation() {
        transform.LookAt(transform.position + this.velocity.normalized);
    }
    
    private void Update() {
        var frameDiffDelta = Time.time - this.lastPhysicsStepTime; // will grab secs between physics frames
        
        // Update position visually
        transform.position = this.prevPos + this.velocity * frameDiffDelta;
        this.UpdateRotation();
    }

    private void FixedUpdate() {
        if (InstanceFinder.PredictionManager.IsReplaying()) {
            return;
        }
        
        this.lastPhysicsStepTime = Time.time;
        
        //Frame delta, nothing unusual here.
        float delta = Time.fixedDeltaTime;

        //See if to add on additional delta to consume passed time.
        float passedTimeDelta = 0f;
        if (this.passedTime > 0f)
        {
            /* Rather than use a flat catch up rate the
             * extra delta will be based on how much passed time
             * remains. This means the projectile will accelerate
             * faster at the beginning and slower at the end.
             * If a flat rate was used then the projectile
             * would accelerate at a constant rate, then abruptly
             * change to normal move rate. This is similar to using
             * a smooth damp. */

            /* Apply 8% of the step per frame. You can adjust
             * this number to whatever feels good. */
            float step = (this.passedTime * 0.08f);
            this.passedTime -= step;

            /* If the remaining time is less than half a delta then
             * just append it onto the step. The change won't be noticeable. */
            if (this.passedTime <= (delta / 2f))
            {
                step += this.passedTime;
                this.passedTime = 0f;
            }
            passedTimeDelta = step;
        }

        this.velocity.y += this.gravity * delta;
        var posCurrent = prevPos;
        var posNew = prevPos + this.velocity * delta;
        prevPos = posNew;//For next frame
        
        

        float maxDistance = Vector3.Distance(posCurrent, posNew) + 0.1f;
#if UNITY_EDITOR
        checkedPoints.Add(posCurrent);
        checkedPoints.Add(posCurrent + this.velocity.normalized * maxDistance);
#endif
        var hits = Physics.RaycastNonAlloc(posCurrent, this.velocity.normalized, this.raycastResults, maxDistance,
            LayerMask.GetMask("ProjectileReceiver", "Block", "Character"));
        if (hits > 0) {
            var closestHits = this.raycastResults.OrderByDescending((hit => -1 * (hit.point - posCurrent).magnitude));
            foreach (var hit in closestHits) {
                var result = this.HandleHit(hit);
                if (result) {
                    break;
                }
            }
        }

        // print($"update={this.updateCounter}, tick={InstanceFinder.TimeManager.LocalTick} pos={pos}, vel={this.velocity}");
        this.updateCounter++;
        this.prevTick = InstanceFinder.TimeManager.LocalTick;


        
        //Kill Floor
        if (posNew.y < 0) {
            Die();
        }
    }



    private bool HandleHit(RaycastHit raycastHit) {
        /* These projectiles are instantiated locally, as in,
         * they are not networked. Because of this there is a very
         * small chance the occasional projectile may not align with
         * 100% accuracy. But, the differences are generally
         * insignifcant and will not affect gameplay. */

        if (this.destroyed) {
            return false;
        }
        this.destroyed = true;

        var hitEvent = new ProjectileHitEvent() {
            raycastHit = raycastHit,
            velocity = this.velocity + Vector3.zero,
        };
        this.OnHit?.Invoke(hitEvent);
        ProjectileManager.Instance.InvokeCollision(this, hitEvent);

        Die();
        
        return true;
    }

    private void Die() {
        //Destroy projectile (probably pool it instead).
        Destroy(gameObject);
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos() {
        for (int i = 0; i < checkedPoints.Count; i+= 2) {
            Gizmos.color = (i / 2) % 2 == 0 ? Color.black : Color.red; 
            Gizmos.DrawLine(checkedPoints[i], checkedPoints[i+1]);
        }
    }
#endif
}