using System.Collections;
using System.Threading;
using Code.Util;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using Mono.Unix;
using Mono.Unix.Native;
#endif
using UnityEngine;

namespace Code.Bootstrap {
    public class SignalHandler : MonoBehaviour {
        public ServerBootstrap serverBootstrap;
        public UnityMainThreadDispatcher unityMainThread;


#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
        void Start() {
            var thread = new Thread(CheckForSignals);
            thread.Start();
        }

        private void CheckForSignals() {
            while (true) {
                var signals = new UnixSignal[] {
                    new UnixSignal(Signum.SIGTERM)
                };
                int index = UnixSignal.WaitAny(signals, -1);

                if (index >= 0 && signals[index].IsSet) {
                    Debug.Log("Sigterm.1");
                    this.unityMainThread.Enqueue(HandleSigterm());
                    signals[index].Reset();
                }
            }
        }

        private IEnumerator HandleSigterm() {
            Debug.Log("SIGTERM received. Performing cleanup.");
            // Perform your cleanup here
            serverBootstrap.InvokeOnProcessExit();
            yield return null;
            // Application.Quit();
        }
#endif
    }
}
