using UnityEngine;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
using System;
using Mono.Unix;
using Mono.Unix.Native;
using UnityEngine;
#endif

public class SignalHandler : MonoBehaviour {
    public ServerBootstrap serverBootstrap;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
    private UnixSignal[] signals;

    void Start() {
        signals = new UnixSignal[] {
            new UnixSignal(Signum.SIGTERM)
        };

        StartCoroutine(CheckForSignals());
    }

    private System.Collections.IEnumerator CheckForSignals() {
        while (true) {
            int index = UnixSignal.WaitAny(signals, -1);

            if (index >= 0 && signals[index].IsSet) {
                HandleSigterm();
                signals[index].Reset();
            }

            yield return null;
        }
    }

    private void HandleSigterm() {
        Debug.Log("SIGTERM received. Performing cleanup.");
        // Perform your cleanup here
        serverBootstrap.InvokeOnProcessExit();
        Application.Quit();
    }
#endif
}