using System.Threading;
using UnityEngine;
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || true
using System;
using System.Collections;
using Mono.Unix;
using Mono.Unix.Native;
using UnityEngine;
#endif

public class SignalHandler : MonoBehaviour {
    public ServerBootstrap serverBootstrap;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX || true
    void Start() {
        var thread = new Thread(new ThreadStart(CheckForSignals));
        thread.Start();
    }

    private void CheckForSignals() {
        var signals = new UnixSignal[] {
            new UnixSignal(Signum.SIGTERM)
        };
        int index = UnixSignal.WaitAny(signals, -1);

        if (index >= 0 && signals[index].IsSet) {
            StartCoroutine(HandleSigterm());
            signals[index].Reset();
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