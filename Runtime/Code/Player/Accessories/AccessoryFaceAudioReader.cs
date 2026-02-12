using System;
using UnityEngine;

[LuauAPI]
[RequireComponent(typeof(AudioSource))]
public class AccessoryFaceAudioReader : AudioSourceReader {
    public static bool DetectVoices = true;
    
    public AccessoryFaceComponent face;
    public VisualGraphComponent visualizer;

    protected void Awake() {
        base.Awake();
        if (visualizer) {
            visualizer.SetRange(0, .5f);
        }
    }

    private void Start() {
        ConnectToFace();
    }

    private void OnTransformParentChanged() {
        ConnectToFace();
    }

    private void ConnectToFace() {
        face = transform.parent.gameObject.GetComponent<AccessoryFaceComponent>();
    }

    protected void Update() {
        if (!DetectVoices || !face) {
            return;
        }
        base.Update();
        
        face.SetTalkingAudioLevels(isSpeaking ? RMS : 0 , isSpeaking ? Flux : 0);
        
        if (visualizer) {
            visualizer.AddValues(new Vector3(RMS / .2f, Flux,0));
        
            Debug.Log("Energy: " + RMS + " flux: " + Flux);
        }
    }
}