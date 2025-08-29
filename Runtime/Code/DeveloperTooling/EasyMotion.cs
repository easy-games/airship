using UnityEngine;

[LuauAPI]
[ExecuteInEditMode]
public class EasyMotion : MonoBehaviour {
    public EngineRunMode refreshMode = EngineRunMode.PLAY;
    public Space transformSpace = Space.Self;
    
    [Header("Translation")]
    public bool translate = false;
    public Vector3 translationSpeed;

    [Header("Rotation")]
    public bool rotate = false;
    public Vector3 angularRotationSpeed;

    [Header("Scale")]
    public bool scale = false;
    public Vector3 scaleSpeed;

    [Header("Sine Motion")]
    public bool sineMotion = false;
    public float sineMod = 1;
    public float sineOffset = 0;
    public bool randomizeOffset = false;

    private Vector3 initialPos = Vector3.zero;
    private Vector3 initialScale = Vector3.zero;
    private Vector3 initialRot = Vector3.zero;
    
    private void Start(){
        if(randomizeOffset){
            sineOffset += Random.Range(0f, 1f);
        }

        this.initialPos = transform.localPosition;
        this.initialScale = transform.localScale;
        this.initialRot = transform.localEulerAngles;
    }

    // Update is called once per frame
    void Update() {
        if (EasyTooling.IsValidRunMode(refreshMode)) {
            if (translate) {
                if(sineMotion){
                    transform.localPosition = translationSpeed * Mathf.Sin(Time.time * sineMod + sineOffset) + this.initialPos;
                }else{
                    transform.Translate(translationSpeed * Time.deltaTime, transformSpace);
                }
            }
            if (rotate) {
                if(sineMotion){
                    transform.localEulerAngles = angularRotationSpeed * Mathf.Sin(Time.time * sineMod + sineOffset)  + this.initialRot;
                }else{
                    transform.Rotate(angularRotationSpeed * Time.deltaTime, transformSpace);
                }
            }
            if (scale) {
                if(sineMotion){
                    transform.localScale = scaleSpeed * ((Mathf.Sin(Time.time * sineMod + sineOffset) + 1) /2) + this.initialScale;
                }else{
                    Vector3 newScale = transform.localScale;
                    newScale += scaleSpeed * Time.deltaTime;
                    transform.localScale = newScale;
                }
            }
        }
    }
}
