using System;
using TMPro;
using UnityEngine;

public class TestStateComponent : MonoBehaviour {
    private void Start() {
        print("TestState.Start");

        var type = typeof(TMP_Text);
        print(typeof(TMP_Text));
    }

    private void OnEnable() {
        print("TestState.OnEnable");
    }

    private void OnDisable() {
        print("TestState.OnDisable");
    }
}