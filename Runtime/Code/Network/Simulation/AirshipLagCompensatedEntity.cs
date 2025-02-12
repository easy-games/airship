using System;
using Mirror;
using UnityEngine;

namespace Code.Network.Simulation
{
    struct RigidbodySnapshot
    {
        public Vector3 position;
        public Vector3 linearVelocity;
        public Vector3 angularVelocity;
        public Quaternion rotation;
    }
    
    /**
     * This component is used to allow for lag compensation to operate on rigidbodies controlled by the server.
     */
    public class AirshipLagCompensatedEntity: NetworkBehaviour
    {
        [Tooltip("Time in seconds kept in history.")]
        [Range(0, 1)]
        public float historySize = 1;

        private Rigidbody rb;
        private History<RigidbodySnapshot> history;
        
        private void Awake()
        {
            // Lag compensation only occurs on the server, so this component only operates on the server.
            if (isServer)
            {
                rb = this.GetComponent<Rigidbody>();
                var snapshots = historySize / Time.fixedDeltaTime;
                history = new History<RigidbodySnapshot>((int) Math.Ceiling(snapshots));
                AirshipSimulationManager.OnCaptureSnapshot += this.CaptureSnapshot;
                AirshipSimulationManager.OnSetSnapshot += this.SetSnapshot;
                AirshipSimulationManager.OnLagCompensationCheck += this.LagCompensationCheck;
            }
        }

        private void OnDestroy()
        {
            // Lag compensation only occurs on the server, so this component only operates on the server.
            if (isServer)
            {
                AirshipSimulationManager.OnCaptureSnapshot -= this.CaptureSnapshot;
                AirshipSimulationManager.OnSetSnapshot -= this.SetSnapshot;
                AirshipSimulationManager.OnLagCompensationCheck -= this.LagCompensationCheck;
            }
        }

        private void CaptureSnapshot(double time, bool replay)
        {
            this.history.Add(time, new RigidbodySnapshot()
            {
                position = rb.position,
                linearVelocity = rb.velocity,
                angularVelocity = rb.angularVelocity,
                rotation = rb.rotation
            });
        }

        private void SetSnapshot(double time)
        {
            var snapshot = this.history.Get(time);
            this.rb.position = snapshot.position;
            this.rb.velocity = snapshot.linearVelocity;
            this.rb.angularVelocity = snapshot.angularVelocity;
            this.rb.rotation = snapshot.rotation;
        }

        private void LagCompensationCheck(int clientId, double time, double latency)
        {
            // TODO: configure buffer time
            var bufferedTime = time - latency - (3f / Time.fixedDeltaTime);
            this.SetSnapshot(bufferedTime);
        }
    }
}