using UnityEngine;

namespace Code.Projectile {
    public struct ProjectileHitEvent {
        public RaycastHit raycastHit;
        public Vector3 velocity;
    }
}