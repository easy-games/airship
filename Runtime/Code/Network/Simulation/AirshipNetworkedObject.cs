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
    public class AirshipNetworkedObject : MonoBehaviour {
        
        [Tooltip("Adjusts the lag compensation timing by this amount in seconds. Useful to add or remove additional delay on the lag compensation request. Ex. Removing observer buffer delay for non-buffered entities.")]
        [Range(-1, 1)]
        public float bufferAdjustment = 0;
        
        private History<TransformSnapshot> history;

        private void Start()
        {
            history = new History<TransformSnapshot>(1);
            AirshipSimulationManager.Instance.OnCaptureSnapshot += this.CaptureSnapshot;
            AirshipSimulationManager.Instance.OnSetSnapshot += this.SetSnapshot;
            AirshipSimulationManager.Instance.OnLagCompensationCheck += this.LagCompensationCheck;
        }

        private void OnDestroy() {
            var simManager = AirshipSimulationManager.Instance;
            if (!simManager) return;
                
            simManager.OnCaptureSnapshot -= this.CaptureSnapshot;
            simManager.OnSetSnapshot -= this.SetSnapshot;
            simManager.OnLagCompensationCheck -= this.LagCompensationCheck;
        }

        private void CaptureSnapshot(int tick, double time, bool replay)
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
            if (objTick is int tick) {
                var snapshot = this.history.Get(tick);
                this.transform.position = snapshot.position;
                this.transform.rotation = snapshot.rotation;
            }
        }

        private void LagCompensationCheck(int clientId, int tick, double time, double latency, double bufferTime)
        {
            var commandBufferTime = (NetworkServer.sendInterval * (NetworkClient.bufferTimeMultiplier / 2f));
            
            var totalBuffer = (latency * 2) + bufferTime + commandBufferTime;
            var lagCompensatedTime = time - (totalBuffer + bufferAdjustment);
            var lagCompensatedTick = AirshipSimulationManager.Instance.GetNearestTickForUnscaledTime(lagCompensatedTime);
            this.SetSnapshot(lagCompensatedTick);
        }
    }
}