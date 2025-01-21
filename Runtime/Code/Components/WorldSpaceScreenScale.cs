using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class WorldSpaceScreenScale : MonoBehaviour
{
    [SerializeField] private int scale = 1;
    private Vector3 defaultScale;
    private Camera cam;
    private RectTransform rect;

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
        float dist = (cam.transform.position - transform.position).magnitude;
        rect.transform.localScale = new Vector3((1 + (dist / 100) * scale) * defaultScale.x, (1 + (dist / 100) * scale) * defaultScale.y, 1 * defaultScale.z);
    }
}
