using System;
using Code.PoolManager;
using UnityEngine;

public class EffectCleanup : MonoBehaviour {
    private void OnDisable() {
        var particles = gameObject.GetComponentsInChildren<ParticleSystem>();
        print("Clearing " + particles.Length + " particle systems.");
        foreach (var particle in particles) {
            particle.Clear();
        }
        PoolManager.ReleaseObject(gameObject);
    }
}