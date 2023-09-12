using FishNet;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AirshipConstantForce : MonoBehaviour {
    private Rigidbody rb;
    public Vector3 force;

    private void Awake() {
        this.rb = GetComponent<Rigidbody>();
    }

    private void OnEnable() {
        if (InstanceFinder.TimeManager) {
            InstanceFinder.TimeManager.OnTick += OnTick;
        }
    }

    private void OnDisable() {
        if (InstanceFinder.TimeManager) {
            InstanceFinder.TimeManager.OnTick -= OnTick;
        }
    }

    private void OnTick() {
        this.rb.AddForce(this.force * (float)InstanceFinder.TimeManager.TickDelta, ForceMode.Acceleration);
    }
}