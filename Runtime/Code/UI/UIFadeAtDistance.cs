using System.Collections;
using System.Collections.Generic;
using GameKit.Utilities;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UIFadeAtDistance : MonoBehaviour
{
    [SerializeField] private int fadeMinDistance = 50;
    [SerializeField] private int fadeMaxDistance = 80;
    // Minimum transparency at fadeMinDistance or closer
    [SerializeField] private int fadeMinValue = 1;
    // Maximum transparency reached at fadeMaxDistance
    [SerializeField] private int fadeMaxValue = 0;
    private Camera cam;
    private CanvasGroup canvas;
    
    // Start is called before the first frame update
    void Start() {
        cam = Camera.main;
        canvas = GetComponent<CanvasGroup>();
    }
    
    // Update is called once per frame
    void Update()
    {
            float dist = (cam.transform.position - transform.position).magnitude;
            float fadePercent = (dist - fadeMinDistance) / (fadeMaxDistance - fadeMinDistance);

            canvas.alpha = fadePercent * fadeMaxValue + (1 - fadePercent) * fadeMinValue;
    }
}
