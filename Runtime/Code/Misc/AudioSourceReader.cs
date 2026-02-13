using System;
using UnityEngine;

[LuauAPI]
[RequireComponent(typeof(AudioSource))]
public class AudioSourceReader : MonoBehaviour {
    [Header("Microphone")]
    public int fftSize = 1024;

    [Header("Speech Frequency Range (Hz)")]
    public float minHz = 180f;
    public float maxHz = 4000f;

    [Header("Thresholds")]
    public float rmsThreshold = 0.005f;

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float fluxSmoothing = 0.3f;
    
    [Header("Debug")]
    public float audioStartTime = 0;

    public bool isSpeaking { get; private set; }
    public float RMS { get; private set; }
    public float Flux { get; private set; }

    protected AudioSource source;

    protected float[] spectrum;
    protected float[] prevSpectrum;

    protected float smoothedFlux;

    protected void Awake() {
        // Initialize variables
        source = GetComponent<AudioSource>();
        source.time = audioStartTime;
        spectrum = new float[fftSize];
        prevSpectrum = new float[fftSize];
    }

    protected void Update() {
        AnalyzeSpectrum();
        Classify();
    }

    protected void AnalyzeSpectrum() {
        if (source.clip == null) return;
        
        // Load spectrum from audio source (index = frequency range, value = magnitude of that frequency)
        source.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        var sampleRate = source.clip.frequency;
        // Determine the constant used to find the frequency from the spectrum index 
        float binWidth = (sampleRate * 0.5f) / spectrum.Length;

        float flux = 0f;
        float energy = 0f;

        for (int i = 0; i < spectrum.Length; i++) {
            float freq = i * binWidth;

            // Ignore background noise and harmonics
            if (freq < minHz || freq > maxHz)
                continue;

            float value = spectrum[i];
            float prev = prevSpectrum[i];

            // Find the change in spectrums over time
            float diff = value - prev;
            if (diff > 0f)
                flux += diff;

            // Let all valid frequencies add to the amount of energy in the noise
            energy += value;
        }

        // Normalize to make flux amplitude-independent
        flux /= energy + 1e-6f;

        smoothedFlux = Mathf.Lerp(smoothedFlux, flux, fluxSmoothing);

        Flux = smoothedFlux;

        // Copy for next frame
        Array.Copy(spectrum, prevSpectrum, spectrum.Length);
    }

    protected void Classify() {
        isSpeaking = RMS >= rmsThreshold;
    }

    protected void OnAudioFilterRead(float[] data, int channels) {
        float sum = 0f;
        int count = data.Length / channels;

        for (int i = 0; i < data.Length; i += channels) {
            float sample = data[i];
            sum += sample * sample;
        }

        RMS = Mathf.Sqrt(sum / count);
    }
}
