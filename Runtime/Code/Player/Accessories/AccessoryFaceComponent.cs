using System;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

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
    
    [Header("References")]
    public Renderer faceRenderer;
    
    [Header("Variables")]
    public float volumeDBMin = .1f;
    public float volumeDBYell = .2f;
    public float pitchVariationMax = .5f;
    
    [Header("Debugging")]
    public bool logValues = false;
    
    public Material faceMat { get; set; }
    private int setFaceIndex = -1;
    private FaceRenderMode setFaceMode;
    private float currentVolume = 0;
    private float currentPitchVariation = 0;
    private int currentTalkingIndex = 0;
    private float lastFaceChangeTime = 0;
    private float currentFaceChangeDuration = 0;
    private float minTalkHoldTime = 0;
    private float timeTalking = 0;
    private bool currentlyTalking = false;

    private void Awake() {
        if (!faceRenderer) {
            faceRenderer = gameObject.GetComponent<Renderer>();
            if (!faceRenderer) {
                Debug.LogError("Face Component requires a renderer");
                return;
            }
        }
#if UNITY_EDITOR
        faceMat = Application.isPlaying ? faceRenderer.material : faceRenderer.sharedMaterial;
#else
        faceMat = faceRenderer.material;
#endif
    }

    private void Update() {
        if (logValues) {
            Debug.Log("Volume: " + currentVolume + " Pitch Variation: " + currentPitchVariation);
        }
        // Based on pitch changes, should we change mouth shapes?
        // The longest hold on a face will be 1 and the shortest is .1
        currentFaceChangeDuration = Mathf.Lerp(1, .1f, currentPitchVariation / pitchVariationMax);
        
        if (Time.time > lastFaceChangeTime + currentFaceChangeDuration) {
            lastFaceChangeTime = Time.time;
            // Change talking face
            currentTalkingIndex = Random.Range(0, 2);
            Refresh();
        }
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
    

    public void SetTalkingAudioLevels(float volume, float variation) {
        currentVolume = volume;
        currentPitchVariation = variation;
        Refresh();
    }

    private void Refresh() {
        var canTalk = setFaceMode != FaceRenderMode.OverwriteVoice && setFaceMode != FaceRenderMode.OverwriteAll;
        currentlyTalking = currentVolume > volumeDBMin || Time.time < minTalkHoldTime;
        
        if (currentlyTalking && canTalk) {
            // Talking
            if (currentVolume > volumeDBMin) {
                //If we started to talk, don't swap it off too quick
                minTalkHoldTime = Time.time + .15f;
            }

            faceMat.SetFloat("_MouthStrength", 1);
            faceMat.SetFloat("_MouthIndex", currentVolume >= volumeDBYell ? 2 : currentTalkingIndex);
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
