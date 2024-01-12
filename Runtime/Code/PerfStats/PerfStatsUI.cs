using System;
using Airship;
using FishNet;
using FishNet.Managing.Timing;
using Tayx.Graphy;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PerfStatsUI : MonoBehaviour {
    public Canvas canvas;
    public TMP_Text eventConnectionsText;
    public TMP_Text worldTriText;
    public TMP_Text drawCallsText;
    public TMP_Text hudPingText;
    public TMP_Text hudFpsText;
    public GameObject toggleMenu;

    public float deltaTime;
    public float timeSinceFpsUpdate = 0f;

    [HideInInspector]
    public bool shown = false;

    private TimeManager tm;
    private GraphyManager graphy;

    private void Awake() {
        this.toggleMenu.SetActive(false);
    }

    private void Start() {
        this.tm = InstanceFinder.TimeManager;
        this.graphy = GraphyManager.Instance;
    }

    private void Update() {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;

        // if (Input.GetKeyDown(KeyCode.F4)) {
        //     if (this.shown) {
        //         this.Hide();
        //     } else {
        //         this.Show();
        //     }
        // }

        if (shown) {
            eventConnectionsText.text = $"Event Connections: {LuauCore.eventConnections.Count.ToString("#,0")}";

            AirshipRenderPipelineStatistics.CaptureRenderingStats();
            AirshipRenderPipelineStatistics.ExtractStatsFromScene();
            worldTriText.text = $"World Triangles: {AirshipRenderPipelineStatistics.numTriangles.ToString("#,0")}";
            drawCallsText.text = $"Draw Calls: {AirshipRenderPipelineStatistics.numPasses.ToString("#,0")}";
        }

        if (this.tm) {
            this.hudPingText.text = $"Ping: {this.tm.RoundTripTime.ToString()}ms";
        } else {
            this.hudPingText.text = $"Ping: 0ms";
        }

        this.timeSinceFpsUpdate += Time.deltaTime;
        if (this.timeSinceFpsUpdate > 0.5) {
            this.timeSinceFpsUpdate = 0f;
            this.hudFpsText.text = $"FPS: {Mathf.Ceil(fps).ToString()}";
        }
    }

    public void Show() {
        this.shown = true;
        this.toggleMenu.SetActive(true);
    }

    public void Hide() {
        this.shown = false;
        this.toggleMenu.SetActive(false);
    }
}