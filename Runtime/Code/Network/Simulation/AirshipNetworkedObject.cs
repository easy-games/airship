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
     * This component is used to allow lag compensation, prediction, and other networked state systems to
     * work with a networked object controlled by the server.
     *
     * When this component is placed on a object being networked with Mirror, the server can include it
     * in lag compensation and clients can resimulate their predictions more accurately.
     */
    public class AirshipNetworkedObject : NetworkBehaviour
    {
        private History<TransformSnapshot> history;

        private void Start()
        {
            if (isServer && authority)
            {
                history = new History<TransformSnapshot>(NetworkServer.sendRate);
                AirshipSimulationManager.Instance.OnCaptureSnapshot += this.CaptureSnapshot;
                AirshipSimulationManager.Instance.OnSetSnapshot += this.SetSnapshot;
                AirshipSimulationManager.Instance.OnLagCompensationCheck += this.LagCompensationCheck;
            }

            if (isClient && !authority)
            {
                history = new History<TransformSnapshot>(NetworkClient.sendRate);
                AirshipSimulationManager.Instance.OnCaptureSnapshot += this.CaptureSnapshot;
                AirshipSimulationManager.Instance.OnSetSnapshot += this.SetSnapshot;
            }
        }

        private void OnDestroy()
        {
            AirshipSimulationManager.Instance.OnCaptureSnapshot -= this.CaptureSnapshot;
            AirshipSimulationManager.Instance.OnSetSnapshot -= this.SetSnapshot;
            AirshipSimulationManager.Instance.OnLagCompensationCheck -= this.LagCompensationCheck;
        }

        private void CaptureSnapshot(double time, bool replay)
        {
            if (replay)
            {
                var state = this.history.Get(time);
                this.transform.position = state.position;
                this.transform.rotation = state.rotation;
                return;
            }
            
            this.history.Add(time, new TransformSnapshot()
            {
                position = this.transform.position,
                rotation = this.transform.rotation
            });
        }

        private void SetSnapshot(object objTime)
        {
            if (objTime is double time) {
                var snapshot = this.history.Get(time);
                this.transform.position = snapshot.position;
                this.transform.rotation = snapshot.rotation;
            }
        }

        private void LagCompensationCheck(int clientId, double time, double latency)
        {
            var bufferedTime = time - latency - NetworkClient.bufferTime;
            this.SetSnapshot(bufferedTime);
        }
    }
}