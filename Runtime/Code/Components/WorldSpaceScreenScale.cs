using System;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class WorldSpaceScreenScale : MonoBehaviour
{
    [SerializeField] private int scale = 1;
    [NonSerialized] private float updateIntervalSeconds = 0.05f;
    [NonSerialized] private float minScaleDelta = 0.0025f;
    private Vector3 defaultScale;
    private Camera cam;
    private RectTransform rect;
    private float nextUpdateTime;
    private float lastAppliedScale = -1f;

    // Start is called before the first frame update
    void Start() {
        defaultScale = transform.localScale;
        cam = Camera.main;
        if (!cam) {
            this.enabled = false;
            return;
        }
        rect = GetComponent<RectTransform>();
    }
    
    // Update is called once per frame
    void Update() {
        if (Time.unscaledTime < nextUpdateTime) {
            return;
        }
        nextUpdateTime = Time.unscaledTime + updateIntervalSeconds;

        float dist = Vector3.Distance(cam.transform.position, transform.position);
        float targetScale = 1 + (dist / 100f) * scale;
        if (Mathf.Abs(lastAppliedScale - targetScale) < minScaleDelta) {
            return;
        }

        lastAppliedScale = targetScale;
        rect.transform.localScale = new Vector3(
            targetScale * defaultScale.x,
            targetScale * defaultScale.y,
            defaultScale.z
        );
    }
}
