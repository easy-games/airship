using FishNet.Object;
using Unity.VisualScripting;
using UnityEngine;

public struct ProjectileValidateEvent
{
    public GameObject shooter;
    public bool validated;

    public string projectilePath;
    public Vector3 position;
    public Vector3 velocity;
    public float gravity;
    public float drag;
    public int itemTypeId;
}

[LuauAPI]
public class ProjectileLauncher : NetworkBehaviour
{
    /// <summary>
    /// Maximum amount of passed time a projectile may have.
    /// This ensures really laggy players won't be able to disrupt
    /// other players by having the projectile speed up beyond
    /// reason on their screens.
    /// </summary>
    private const float MAX_PASSED_TIME = 0.12f;

    /// <summary>
    /// Spawns a projectile locally.
    /// </summary>
    private EasyProjectile SpawnProjectile(string projectilePath, int itemTypeId, Vector3 position, Vector3 velocity, float gravity, float drag, float passedTime)
    {
        GameObject projectilePrefab = AssetBridge.Instance.LoadAssetInternal<GameObject>(projectilePath);
        EasyProjectile projectile = Object.Instantiate(projectilePrefab, position, Quaternion.identity).GetComponent<EasyProjectile>();
        Physics.IgnoreCollision(gameObject.GetComponent<Collider>(), projectile.GetComponent<Collider>());
        projectile.Initialize(velocity, gravity, drag, passedTime, itemTypeId);
        return projectile;
    }

    /// <summary>
    /// Local client fires weapon.
    /// </summary>
    public EasyProjectile ClientFire(string projectilePath, int itemTypeId, Vector3 position, Vector3 velocity, float gravity, float drag)
    {
        /* Spawn locally with 0f passed time.
         * Since this is the firing client
         * they do not need to accelerate/catch up
         * the projectile. */
        var projectile = SpawnProjectile(projectilePath, itemTypeId, position, velocity, gravity, drag, 0f);
        //Ask server to also fire passing in current Tick.
        ServerFire(projectilePath, itemTypeId, position, velocity, gravity, drag, base.TimeManager.Tick);

        return projectile;
    }

    /// <summary>
    /// Fires on the server.
    /// </summary>
    /// <param name="position">Position to spawn projectile.</param>
    /// <param name="direction">Direction to move projectile.</param>
    /// <param name="tick">Tick when projectile was spawned on client.</param>
    [ServerRpc]
    private void ServerFire(string projectilePath, int itemTypeId, Vector3 position, Vector3 velocity, float gravity, float drag, uint tick)
    {
        /* You may want to validate position and direction here.
         * How this is done depends largely upon your game so it
         * won't be covered in this guide. */
        ProjectileValidateEvent evt = new ProjectileValidateEvent()
        {
            shooter = this.gameObject,
            validated = true,
            velocity = velocity,
            gravity = gravity,
            drag = drag,
            itemTypeId = itemTypeId,
        };
        ProjectileManager.Instance.InvokeProjectileValidate(evt);

        if (!evt.validated)
        {
            Debug.LogWarning("Projectile validation failed. Not firing. shooter=" + this.gameObject.transform.name);
            return;
        }

        //Get passed time. Note the false for allow negative values.
        float passedTime = (float)base.TimeManager.TimePassed(tick, false);
        /* Cap passed time at half of constant value for the server.
         * In our example max passed time is 300ms, so server value
         * would be max 150ms. This means if it took a client longer
         * than 150ms to send the rpc to the server, the time would
         * be capped to 150ms. This might sound restrictive, but that would
         * mean the client would have roughly a 300ms ping; we do not want
         * to punish other players because a laggy client is firing. */
        passedTime = Mathf.Min(MAX_PASSED_TIME / 2f, passedTime);

        //Spawn on the server.
        var projectile = SpawnProjectile(projectilePath, itemTypeId, position, velocity, gravity, drag, passedTime);
        ProjectileManager.Instance.InvokeProjectileLaunched(projectile, this);

        //Tell other clients to spawn the projectile.
        ObserversFire(projectilePath, itemTypeId, position, velocity, gravity, drag, tick);
    }

    /// <summary>
    /// Fires on all clients but owner.
    /// </summary>
    [ObserversRpc(ExcludeOwner = true)]
    private void ObserversFire(string projectilePath, int itemTypeId, Vector3 position, Vector3 velocity, float gravity, float drag, uint tick)
    {
        //Like on server get the time passed and cap it. Note the false for allow negative values.
        float passedTime = (float)base.TimeManager.TimePassed(tick, false);
        passedTime = Mathf.Min(MAX_PASSED_TIME, passedTime);

        //Spawn the projectile locally.
        var projectile = SpawnProjectile(projectilePath, itemTypeId, position, velocity, gravity, drag, passedTime);

        ProjectileManager.Instance.InvokeProjectileLaunched(projectile, this);
    }
}