using System;
using UnityEngine;

public class CanvasDistanceCondition : MonoBehaviour {
    public Canvas canvas;
    public float maxDistance = 50f;

    private void Update() {
        if (RunCore.IsServer()) return;
        Vector3 pos = Vector3.zero;
        if (Camera.main) {
            pos = Camera.main.transform.position;
        }
        var distance = (pos - this.transform.position).magnitude;
        if (distance <= maxDistance) {
            canvas.enabled = true;
        } else {
            canvas.enabled = false;
        }
    }
}