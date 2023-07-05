using System;
using UnityEngine;

[LuauAPI]
public class ProjectileManager : Singleton<ProjectileManager>
{
    /// <summary>
    /// Fires when a projectile collides with another collider.
    /// Params: EasyProjectile, Collision
    /// </summary>
    public event Action<object, object> onProjectileCollide;

    public event Action<object> onProjectileValidate;

    /// <summary>
    /// Fired when a projectile is fired.
    /// Params: EasyProjectile, shooter: GameObject
    /// </summary>
    public event Action<object, object> onProjectileLaunched;

    public void InvokeCollision(EasyProjectile projectile, Collision collision)
    {
        this.onProjectileCollide?.Invoke(projectile, collision);
    }

    public void InvokeProjectileValidate(ProjectileValidateEvent evt)
    {
        this.onProjectileValidate?.Invoke(evt);
    }

    public void InvokeProjectileLaunched(EasyProjectile projectile, ProjectileLauncher projectileLauncher)
    {
        this.onProjectileLaunched?.Invoke(projectile, projectileLauncher.gameObject);
    }
}