using System.Collections;
using System.Collections.Generic;
using GameKit.Utilities;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class WorldSpaceScreenScale : MonoBehaviour
{
    [SerializeField] private int scale = 1;
    private Camera cam;
    private RectTransform rect;

    // Start is called before the first frame update
    void Start() {
        cam = Camera.main;
        rect = GetComponent<RectTransform>();
    }
    
    // Update is called once per frame
    void Update()
    {
        float dist = (cam.transform.position - transform.position).magnitude;
        rect.transform.SetScale(new Vector3(1 + (dist/100) * scale, 1 + (dist/100) * scale, 1));
    }
}
