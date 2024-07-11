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
    public bool sineMotion = false;
    public float sineMod = 1;
    public float sineOffset = 0;

    // Update is called once per frame
    void Update() {
#if UNITY_EDITOR
        if(!Application.isPlaying && !runInEditor){
            return;
        }
#endif
        if (translate) {
            if(sineMotion){
                transform.localPosition = translationSpeed * Mathf.Sin(Time.time * sineMod + sineOffset);
            }else{
                transform.Translate(translationSpeed * Time.deltaTime, transformSpace);
            }
        }
        if (rotate) {
            if(sineMotion){
                transform.localEulerAngles = angularRotationSpeed * Mathf.Sin(Time.time * sineMod + sineOffset);
            }else{
                transform.Rotate(angularRotationSpeed * Time.deltaTime, transformSpace);
            }
        }
        if (scale) {
            if(sineMotion){
                transform.localScale = Vector3.one + scaleSpeed * ((Mathf.Sin(Time.time * sineMod + sineOffset) + 1) /2);
            }else{
                Vector3 newScale = transform.localScale;
                newScale += scaleSpeed * Time.deltaTime;
                transform.localScale = newScale;
            }
        }
    }
}
