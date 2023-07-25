using System;
using Code.Projectile;
using UnityEngine;

[LuauAPI]
public class ProjectileManager : Singleton<ProjectileManager>
{
    /// <summary>
    /// Fires when a projectile collides with another collider.
    /// Params: EasyProjectile, Collision
    /// </summary>
    public event Action<object, object> OnProjectileCollide;

    public event Action<object> OnProjectileValidate;

    /// <summary>
    /// Fired when a projectile is fired.
    /// Params: EasyProjectile, shooter: GameObject
    /// </summary>
    public event Action<object, object> OnProjectileLaunched;

    public void InvokeCollision(EasyProjectile projectile, ProjectileHitEvent hitEvent)
    {
        this.OnProjectileCollide?.Invoke(projectile, hitEvent);
    }

    public void InvokeProjectileValidate(ProjectileValidateEvent evt)
    {
        this.OnProjectileValidate?.Invoke(evt);
    }

    public void InvokeProjectileLaunched(EasyProjectile projectile, ProjectileLauncher projectileLauncher)
    {
        this.OnProjectileLaunched?.Invoke(projectile, projectileLauncher.gameObject);
    }
}