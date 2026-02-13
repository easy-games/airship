using System;
using System.Collections;
using Code.Player;
using Mirror;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

[LuauAPI]
[ExecuteAlways]
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
        OpenSmall = 0,
        OpenBig,
        Yell,
        Scream,
        Frown,
        Grimace,
        Surprise,
        Smile,
        Pucker,
        Grit,
        Meow,
        Grin,
        Woozy,
        Smirk,
        Scowl,
        TongueOut
    }

    [Header("References")]
    public Renderer faceRenderer;

    [Header("Variables")]
    public bool reactToVoice = true;
    public AirshipFaceDecal initialFace = AirshipFaceDecal.None;
    public FaceRenderMode initialRenderMode = FaceRenderMode.OverwriteDefault;
    
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
    
    // MouthMovementAnim Stuff
    private int _mouth;                 // current mouth index
    private float _attackTimer = 0f;    // accumulates while wanting to go up
    private float _releaseTimer = 0f;   // accumulates while wanting to go down
    private float attackTime = 0.03f;
    private float releaseTime = 0.05f;

#if UNITY_EDITOR
    private AirshipFaceDecal lastInitialFace;
#endif
    private void Awake() {
        if (!faceRenderer) {
            Debug.LogError("Face Component requires a renderer");
            return;
        }
        
#if UNITY_EDITOR
        faceMat = Application.isPlaying ? faceRenderer.material : faceRenderer.sharedMaterial;
#else
        faceMat = faceRenderer.material;
#endif
        SetFace(initialFace, initialRenderMode);
    }

    private void Update() {
#if UNITY_EDITOR
        if(!Application.isPlaying && lastInitialFace != initialFace) {
            lastInitialFace = initialFace;
            SetFace(initialFace, initialRenderMode);
        }
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
    

    public void SetTalkingAudioLevels(float volume, float variation) {
        currentVolume = volume;
        currentPitchVariation = variation;
        Refresh();
    }

    private void Refresh() {
        var canTalk = reactToVoice && setFaceMode != FaceRenderMode.OverwriteVoice && setFaceMode != FaceRenderMode.OverwriteAll;
        currentlyTalking = currentVolume > 0;
        // currentlyTalking = currentVolume > 0 || Time.time < minTalkHoldTime;

        // if (currentlyTalking && canTalk) {
        //     // Talking
        //     if (currentVolume > .01f) {
        //         //Don't swap off talking too quick
        //         minTalkHoldTime = Time.time + .15f;
        //     }
        //
        //     faceMat.SetFloat("_MouthStrength", 1);
        //     var mouthIndex = currentTalkingIndex;
        //     if (currentVolume >= volumeDBScream) {
        //         mouthIndex = 6;
        //     } else if (currentVolume >= volumeDBYell) {
        //         mouthIndex = 2;
        //     }
        //     faceMat.SetFloat("_MouthIndex", mouthIndex);
        // } else {
        //     // Default face
        //     if (setFaceIndex >= 0) {
        //         faceMat.SetFloat("_MouthIndex", setFaceIndex);
        //         faceMat.SetFloat("_MouthStrength", 1);
        //     } else {
        //         faceMat.SetFloat("_MouthStrength", 0);
        //     }
        // }

        if (currentlyTalking && canTalk) {
            int desired = QuantizeMouth(currentVolume);
            if (desired > _mouth)
            {
                // Going UP: attack behavior (fast transition)
                _releaseTimer = 0f;                 // cancel any release hold
                _attackTimer += Time.deltaTime;

                if (_attackTimer >= attackTime)
                {
                    _mouth = desired;               // or step by step if you want
                    _attackTimer = 0f;
                }
            }
            else if (desired < _mouth)
            {
                // Going DOWN: release behavior (hold current mouth)
                _attackTimer = 0f;                  // cancel any attack
                _releaseTimer += Time.deltaTime;

                if (_releaseTimer >= releaseTime)
                {
                    _mouth = desired;               // or step by step
                    _releaseTimer = 0f;
                }
            } else {
                // Same mouth target: reset timers
                _attackTimer = 0f;
                _releaseTimer = 0f;
            }

            faceMat.SetFloat("_MouthIndex", _mouth);
            faceMat.SetFloat("_MouthStrength", 1);
        } else {
            _mouth = 0;
            _attackTimer = 0f;
            _releaseTimer = 0f;
            
            // Default face
            if (setFaceIndex >= 0) {
                faceMat.SetFloat("_MouthIndex", setFaceIndex);
                faceMat.SetFloat("_MouthStrength", 1);
            } else {
                faceMat.SetFloat("_MouthStrength", 0);
            }
        }
    }
    
    int QuantizeMouth(float v)
    {
        if (v < 0.1f) return 0;
        if (v < 0.2f) return 1;
        if (v < 0.3f) return 2;
        return 3;
    }


}
