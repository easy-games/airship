using UnityEngine;

public class EasySpinner : MonoBehaviour {
    public float xSpeed;
    public float ySpeed;
    public float zSpeed;

    private void Update() {
        var delta = Time.deltaTime;
        transform.Rotate(new Vector3(xSpeed, ySpeed, zSpeed) * delta);
    }
}