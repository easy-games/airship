using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AccessoryFaceAudioReader : AudioSourceReader {
    public AccessoryFaceComponent face;
    public VisualGraphComponent visualizer;

    protected void Awake() {
        base.Awake();
        if (visualizer) {
            visualizer.SetRange(0, .5f);
        }
    }

    protected void Update() {
        base.Update();
        face.SetTalkingAudioLevels(RMS, Flux);
        if (visualizer) {
            visualizer.AddValues(new Vector3(RMS / .1f, Flux,0));
        
            Debug.Log("Energy: " + RMS + " flux: " + Flux);
        }
    }
}