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

        private void CaptureSnapshot(uint tick, double time, bool replay)
        {
            if (replay)
            {
                var state = this.history.Get(tick);
                this.transform.position = state.position;
                this.transform.rotation = state.rotation;
                return;
            }
            
            this.history.Add(tick, new TransformSnapshot()
            {
                position = this.transform.position,
                rotation = this.transform.rotation
            });
        }

        private void SetSnapshot(object objTick)
        {
            if (objTick is uint tick) {
                var snapshot = this.history.Get(tick);
                this.transform.position = snapshot.position;
                this.transform.rotation = snapshot.rotation;
            }
        }

        private void LagCompensationCheck(int clientId, uint tick, double time, double latency, double buffer)
        {
            var bufferedTicks = Math.Round((latency - NetworkClient.bufferTime - Time.fixedDeltaTime) / Time.fixedDeltaTime);
            this.SetSnapshot((uint) tick - bufferedTicks);
        }
    }
}