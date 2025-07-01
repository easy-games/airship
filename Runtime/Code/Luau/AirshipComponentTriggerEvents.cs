using System;
using UnityEngine;

namespace Code.Luau {
    public interface ITriggerReceiver {
        public void OnTriggerStayReceiver(Collider other);
    }
    
    public class AirshipComponentTriggerEvents : MonoBehaviour {
        private ITriggerReceiver receiver;
        
        public void AttachReceiver(ITriggerReceiver receiver) {
            this.receiver = receiver;
        }
        
        private void OnTriggerStay(Collider other) {
            this.receiver.OnTriggerStayReceiver(other);
        }
    }
}