using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaceCamera : MonoBehaviour
{
    Camera cam;
    public bool Flipped = false;

    // Start is called before the first frame update
    void Start() {
        cam = Camera.main;
    }

    // Update is called once per frame
   void LateUpdate() {
       if (cam) {
           if (Flipped)
           {
               transform.forward = -cam.transform.forward;   
           } else
           {
               transform.forward = cam.transform.forward;
           }
       }
   }
}
