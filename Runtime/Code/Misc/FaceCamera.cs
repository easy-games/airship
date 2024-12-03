using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class FaceCamera : MonoBehaviour
{
    [SerializeField] Camera camera;
    [FormerlySerializedAs("Flipped")] public bool flipped = false;

    // Start is called before the first frame update
    void Start() {
        if (!this.camera) {
            this.camera = Camera.main;
        }
    }

    // Update is called once per frame
   void LateUpdate() {
       if (this.camera) {
           if (flipped) {
               transform.forward = -camera.transform.forward;
           } else {
               transform.forward = camera.transform.forward;
           }
       }
   }
}
