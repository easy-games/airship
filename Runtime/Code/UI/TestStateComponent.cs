using System;
using TMPro;
using UnityEngine;

public class TestStateComponent : MonoBehaviour {
    private void Awake() {
        print("TestState.Awake " + gameObject.name);
    }

    private void Start() {
        print("TestState.Start " + gameObject.name);
    }

    private void OnEnable() {
        print("TestState.OnEnable " + gameObject.name);
    }

    private void OnDisable() {
        print("TestState.OnDisable " + gameObject.name);
    }
}