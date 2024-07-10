using FishNet;
using UnityEngine;

public class NetworkRigidbodySmoother : MonoBehaviour {
    public Rigidbody targetRigid;
    public Transform graphicsHolder;

    private Vector3 lastPos = Vector3.zero;
    private Vector3 tickVelocity = Vector3.zero;
    private float lastTickTime = 0; 


    // Start is called before the first frame update
    void Start() {
        InstanceFinder.TimeManager.OnTick += OnTick;
    }

    private void OnTick() {
        lastTickTime = Time.time;
        lastPos = targetRigid.transform.position;
        tickVelocity = targetRigid.velocity;
        graphicsHolder.transform.localPosition = Vector3.zero;
    }

    private void Update(){
        var delta = Time.time - lastTickTime;
        graphicsHolder.position = lastPos + tickVelocity * delta;

        var diff = (graphicsHolder.position - lastPos).magnitude;
        print("RIGIDBODY UPDATE. Speed: " + diff / delta);
        lastPos = graphicsHolder.position;
        lastTickTime = Time.time;
    }
}
