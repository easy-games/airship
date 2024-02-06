using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class TestStateComponent : TestStateAbstractBehaviour {
    // public Vector3 OverrideArg = new Vector3(4, 5, 6);
    public GameObject[] testList;
    public Color col;
    
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

    private void Update() {
        col = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
    }
}