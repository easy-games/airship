using System;
using UnityEngine;

namespace Code.Network.Simulation
{
    /**
     * Disables a rigidbodies physics (sets it to kinematic) when resimulation or lag compensation is taking place.
     */
    public class AirshipOfflineRigidbody : MonoBehaviour
    {
        public Rigidbody rigidbody;
        private bool kinematicSetting = false;

        private Vector3 position;
        private Quaternion rotation;
        private Vector3 linearVelocity;
        private Vector3 angularVelocity;

        public void Start()
        {
            this.rigidbody = this.GetComponent<Rigidbody>();
            AirshipSimulationManager.Instance.OnSetPaused += OnPause;
        }

        private void OnDestroy()
        {
            AirshipSimulationManager.Instance.OnSetPaused -= OnPause;
        }

        private void OnPause(bool paused)
        {
            if (this.rigidbody == null) return;
            if (paused)
            {
                this.kinematicSetting = this.rigidbody.isKinematic;
                this.rigidbody.isKinematic = true;
                this.position = this.rigidbody.position;
                this.rotation = this.rigidbody.rotation;
                this.linearVelocity = this.rigidbody.linearVelocity;
                this.angularVelocity = this.rigidbody.angularVelocity;
            }
            else
            {
                this.rigidbody.isKinematic = this.kinematicSetting;
                this.rigidbody.position = this.position;
                this.rigidbody.rotation = this.rotation;
                if (!this.kinematicSetting)
                {
                    this.rigidbody.linearVelocity = this.linearVelocity;
                    this.rigidbody.angularVelocity = this.angularVelocity;
                }
            }
        }
    }
}