using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[LuauAPI]
[ExecuteInEditMode]
public class EasyMotion : MonoBehaviour {
#if UNITY_EDITOR
    public bool runInEditor = false;
#endif
    public Space transformSpace = Space.World;
    
    [Header("Translation")]
    public bool translate = false;
    public Vector3 translationSpeed;
    [Header("Rotation")]
    public bool rotate = false;
    public Vector3 angularRotationSpeed;
    [Header("Scale")]
    public bool scale = false;
    public Vector3 scaleSpeed;

    // Update is called once per frame
    void Update() {
#if UNITY_EDITOR
        if(!Application.isPlaying && !runInEditor){
            return;
        }
#endif
        if (translate) {
            transform.Translate(translationSpeed, transformSpace);
        }
        if (rotate) {
            transform.Rotate(angularRotationSpeed, transformSpace);
        }
        if (scale) {
            Vector3 newScale = transform.eulerAngles;
            newScale += scaleSpeed * Time.deltaTime;
            transform.eulerAngles = newScale;
        }
    }
}
