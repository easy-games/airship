using System.Collections;
using UnityEngine;

[ExecuteInEditMode]
public class EntityFaceRig : MonoBehaviour {
    [Header("References")]
    public EntityFaceData faceData;

    public Transform eyeHolderL;
    public Transform eyeHolderR;
    public SpriteRenderer eyeL;
    public SpriteRenderer pupilL;
    public SpriteRenderer eyeR;
    public SpriteRenderer pupilR;
    public SpriteRenderer nose;
    public SpriteRenderer mouth;

    [Header("Variables")]
    public int targetFaceSetIndex = -1;

    public float blinkDelayInSeconds = 4;
    public FaceTextureSet[] faces;

    private int currentFaceSetIndex = -1;
    private float lastBlinkTime = 0;

    private void Update() {
        if (targetFaceSetIndex != currentFaceSetIndex && targetFaceSetIndex >= 0 && targetFaceSetIndex < faces.Length) {
            currentFaceSetIndex = targetFaceSetIndex;
            Redraw();
        }
        
        //Blinking
        if (Time.time - lastBlinkTime > blinkDelayInSeconds) {
            StartCoroutine(Blink());
        }
    }

    private IEnumerator Blink() {
        lastBlinkTime = Time.time;
        eyeL.sprite = faceData.blinkEye;
        eyeR.sprite = faceData.blinkEye;
        pupilL.enabled = false;
        pupilR.enabled = false;
        yield return null;
        yield return null;
        Redraw();
    }

    private void Redraw() {
        SetFaceTextures(faces[currentFaceSetIndex]);
    }
    
    public void SetFaceTextures(FaceTextureSet set) {
        var pos = eyeHolderL.transform.localPosition;
        pos.x = set.eyeDistance;
        eyeHolderR.transform.localPosition = pos;
        pos.x *= -1;
        eyeHolderL.transform.localPosition = pos;
        
        
        if (set.eyeId >= 0) {
            eyeL.sprite = faceData.eyes[set.eyeId];
            eyeR.sprite = faceData.eyes[set.eyeId];
            eyeL.enabled = true;
            eyeR.enabled = true;
        } else {
            eyeL.enabled = false;
            eyeR.enabled = false;
        }

        if (set.pupilId >= 0) {
            pupilL.sprite = faceData.pupils[set.pupilId];
            pupilR.sprite = faceData.pupils[set.pupilId];
            pupilL.enabled = true;
            pupilR.enabled = true;
        } else {
            pupilL.enabled = false;
            pupilR.enabled = false;
        }

        if (set.noseId >= 0) {
            nose.sprite = faceData.noses[set.noseId];
            nose.enabled = true;
        } else {
            nose.enabled = false;
        }

        if (set.mouthId >= 0) {
            mouth.sprite = faceData.mouths[set.mouthId];
            mouth.enabled = true;
        } else {
            mouth.enabled = false;
        }
    }
    
}

[System.Serializable]
public class FaceTextureSet {
    public string friendlyName = "Face";
    [Header("Textures")]
    public int eyeId = 0;
    public int pupilId = 0;
    public int noseId = 0;
    public int mouthId = 0;

    [Header("Spacing")]
    public float eyeDistance = 2.5f;
}