using System.Threading;
using UnityEngine;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using System.Collections;
using Code.Util;
using Mono.Unix;
using Mono.Unix.Native;
using UnityEngine;
#endif

public class SignalHandler : MonoBehaviour {
    public ServerBootstrap serverBootstrap;

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
                UnityMainThreadDispatcher.Instance().Enqueue(HandleSigterm());
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