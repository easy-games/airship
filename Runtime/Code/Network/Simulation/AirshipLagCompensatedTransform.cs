using System;
using Mirror;
using UnityEngine;

namespace Code.Network.Simulation
{
    struct TransformSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    /**
     * This component is used to allow for lag compensation to operate on rigidbodies controlled by the server.
     */
    public class AirshipLagCompensatedTransform : NetworkBehaviour
    {
        [Tooltip("Time kept in history in seconds.")] [Range(0, 1)]
        public float historySize = 1;

        [Tooltip(
            "Amount of ticks buffered on the client before rendering. " +
            "It's important to get this number correct as it controls the additional rollback time required to correctly " +
            "produce the client's view of the world during lag compensation. It should match the number of physics updates " +
            "you wait before rendering the object. By default, Airship has 2 updates buffered by Mirror.")]
        public uint bufferSizeMultiplier = 2;

        [Tooltip("The send rate being used to update the position. This is used to calculate the " +
                 "additional rollback time required to produce the client's view of the world during lag compensation. " +
                 "It should match the number of updates sent per second. Airship uses a default send rate of 50 for Mirror.")]
        public uint updateSendRate = 50;
        
        private History<TransformSnapshot> history;

        private void Awake()
        {
            // Lag compensation only occurs on the server, so this component only operates on the server.
            if (isServer)
            {
                var snapshots = historySize / Time.fixedDeltaTime;
                history = new History<TransformSnapshot>((int)Math.Ceiling(snapshots));
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
            this.history.Add(time, new TransformSnapshot()
            {
                position = transform.position,
                rotation = transform.rotation
            });
        }

        private void SetSnapshot(double time)
        {
            var snapshot = this.history.Get(time);
            this.transform.position = snapshot.position;
            this.transform.rotation = snapshot.rotation;
        }

        private void LagCompensationCheck(int clientId, double time, double latency)
        {
            var sendRate = 1f / this.updateSendRate;
            var bufferedTime = time - latency - (this.bufferSizeMultiplier * sendRate);
            this.SetSnapshot(bufferedTime);
        }
    }
}