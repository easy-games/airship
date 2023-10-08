using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PerfStatsUI : MonoBehaviour {
    public Canvas canvas;
    public TMP_Text eventConnectionsText;

    [HideInInspector]
    public bool shown = false;

    private void Awake() {
        canvas.enabled = false;
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.F3)) {
            if (this.shown) {
                this.Hide();
            } else {
                this.Show();
            }
        }

        if (shown) {
            eventConnectionsText.text = $"Event Connections: {LuauCore.eventConnections.Count.ToString("#,0")}";
        }
    }

    public void Show() {
        this.shown = true;
        canvas.enabled = true;
    }

    public void Hide() {
        this.shown = false;
        canvas.enabled = false;
    }
}