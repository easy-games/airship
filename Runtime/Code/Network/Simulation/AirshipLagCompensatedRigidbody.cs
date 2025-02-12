using System;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

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
    public class AirshipLagCompensatedRigidbody : NetworkBehaviour
    {
        [Tooltip("Time kept in history in seconds in seconds.")] [Range(0, 1)]
        public float historySize = 1;

        [Tooltip(
            "Amount of ticks buffered on the client before rendering. " +
            "It's important to get this number correct as it controls the additional rollback time required to correctly " +
            "produce the client's view of the world during lag compensation. It should match the number of physics updates " +
            "you wait before rendering the object. By default, Airship has 2 updates buffered by Mirror.")]
        public uint bufferSizeMultiplier = 2;

        [Tooltip("The send rate being used to update this rigidbodies positions. This is used to calculate the " +
                 "additional rollback time required to produce the client's view of the world during lag compensation. " +
                 "It should match the number of updates sent per second. Airship uses a default send rate of 50 for Mirror.")]
        public uint updateSendRate = 50;

        private Rigidbody rb;
        private History<RigidbodySnapshot> history;

        private void Awake()
        {
            // Lag compensation only occurs on the server, so this component only operates on the server.
            if (isServer)
            {
                rb = this.GetComponent<Rigidbody>();
                var snapshots = historySize / Time.fixedDeltaTime;
                history = new History<RigidbodySnapshot>((int)Math.Ceiling(snapshots));
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
            var sendRate = 1f / this.updateSendRate;
            var bufferedTime = time - latency - (this.bufferSizeMultiplier * sendRate);
            this.SetSnapshot(bufferedTime);
        }
    }
}