using System;
using FishNet;
using UnityEngine;

[RequireComponent(typeof(DestroyWatcher), typeof(Rigidbody))]
[LuauAPI]
public class EasyProjectile : MonoBehaviour
{
    /// <summary>
    /// Direction to travel.
    /// </summary>
    private Vector3 velocity;

    private Rigidbody rb;

    public float gravity;
    public float drag;
    public int itemTypeId;

    /// <summary>
    /// Distance remaining to catch up. This is calculated from a passed time and move rate.
    /// </summary>
    private float passedTime = 0f;

    /// <summary>
    /// Fires when a projectile collides with another collider.
    /// Params: Collision, Velocity
    /// </summary>
    public event Action<object, object> onCollide;

    private void Awake()
    {
        this.rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Initializes this projectile.
    /// </summary>
    /// <param name="direction">Direction to travel.</param>
    /// <param name="passedTime">How far in time this projectile is behind te prediction.</param>
    public void Initialize(Vector3 startingVelocity, float gravity, float drag, float passedTime, int itemTypeId)
    {
        this.velocity = startingVelocity;
        this.gravity = gravity;
        this.drag = drag;
        this.passedTime = passedTime;
        this.rb.velocity = velocity;
        this.transform.LookAt(this.transform.position + velocity.normalized);
        this.itemTypeId = itemTypeId;
    }

    private void FixedUpdate()
    {
        this.Move();
    }

    /// <summary>
    /// Move the projectile each frame. This would be called from Update.
    /// </summary>
    private void Move()
    {
        //Frame delta, nothing unusual here.
        float delta = Time.deltaTime;

        //See if to add on additional delta to consume passed time.
        float passedTimeDelta = 0f;
        if (passedTime > 0f)
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
            float step = (passedTime * 0.08f);
            passedTime -= step;

            /* If the remaining time is less than half a delta then
             * just append it onto the step. The change won't be noticeable. */
            if (passedTime <= (delta / 2f))
            {
                step += passedTime;
                passedTime = 0f;
            }
            passedTimeDelta = step;
        }

        var timeStep = delta + passedTimeDelta;
        velocity += new Vector3(0, this.gravity, 0) * timeStep;

        // var velWithTimeStep = velocity * timeStep;

        this.rb.velocity = velocity;
        // transform.position += velocity * timeStep;
        // rb.transform.LookAt();
        transform.LookAt(transform.position + velocity.normalized);
    }

    /// <summary>
    /// Handles collision events.
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        /* These projectiles are instantiated locally, as in,
         * they are not networked. Because of this there is a very
         * small chance the occasional projectile may not align with
         * 100% accuracy. But, the differences are generally
         * insignifcant and will not affect gameplay. */

        print("invoking onCollide");
        this.onCollide?.Invoke(collision, this.velocity);
        ProjectileManager.Instance.InvokeCollision(this, collision);

        //Destroy projectile (probably pool it instead).
        Destroy(gameObject);
    }
}