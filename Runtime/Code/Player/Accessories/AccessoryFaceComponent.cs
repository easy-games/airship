using System;
using UnityEngine;

public class AccessoryFaceComponent : MonoBehaviour {
    public enum FaceRenderMode {
        None = -1,
        Temporary,
        OverwriteDefault,
        OverwriteVoice,
        OverwriteAll
    }

    public enum AirshipFaceDecal {
        None = -1,
        Open = 0,
        OpenO,
        OpenM,
        OpenE,
        Yell,
        Surprise,
        Grimace,
        Smile,
        Pucker,
        Grit,
        Meow,
        Grin,
        Woozy,
        TongueOut,
        Frown,
        Smirk
    }
    
    public Renderer faceRenderer;
    public float volumeDBThreshold = 1;
    public Material faceMat;

    private int setFaceIndex = -1;
    private FaceRenderMode setFaceMode;
    private float currentVolume = 0;
    private float currentPitch = 0;
    private int currentTalkingIndex = 0;

    private void Awake() {
        if (!faceRenderer) {
            faceRenderer = gameObject.GetComponent<Renderer>();
            if (!faceRenderer) {
                Debug.LogError("Face Component requires a renderer");
                return;
            }
        }
#if UNITY_EDITOR
        faceMat = faceRenderer.sharedMaterial;
#else
        faceMat = faceRenderer.material;
#endif
    }

    public void SetFace(AirshipFaceDecal faceType, FaceRenderMode faceMode) {
        setFaceIndex = (int)faceType;
        if (setFaceIndex < 0) {
            setFaceIndex = -1;
            setFaceMode = FaceRenderMode.None;
        } else {
            setFaceMode = faceMode;
        }

        Refresh();
    }
    

    public void SetTalking(float volume, float pitch) {
        Debug.Log("Volume: " + volume + " pitch: " + pitch);
        Refresh();
    }

    private void Refresh() {
        if (setFaceMode != FaceRenderMode.OverwriteVoice && setFaceMode != FaceRenderMode.OverwriteAll && currentVolume > volumeDBThreshold) {
            // Talking
            faceMat.SetFloat("_MouthStrength", 1);
            faceMat.SetFloat("_MouthIndex", currentTalkingIndex);
        } else {
            // Default face
            if (setFaceIndex >= 0) {
                faceMat.SetFloat("_MouthIndex", setFaceIndex);
                faceMat.SetFloat("_MouthStrength", 1);
            } else {
                faceMat.SetFloat("_MouthStrength", 0);
            }
        }
    }
}
