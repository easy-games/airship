using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Quality {
    internal struct AverageFrameTimings {
        public double gpu;
        public double cpuMain;
        public double cpuRender;
    }
    
    public class QualityManager : Singleton<QualityManager> {
        private const int SampleCount = 500;
        private const float QualityCheckCooldownSec = 15;
        /// <summary>
        /// We use this to determine GPU/CPU bound.
        /// </summary>
        private const int FrameTimingCount = 50;
        private short[] _fps = new short[SampleCount];
        private FrameTiming[] _frameTimings = new FrameTiming[FrameTimingCount];
        private int currentQualityLevel = 5;

        /// <summary>
        /// FPS array is circular, this is the front index
        /// </summary>
        private int _fpsFront;
        /// <summary>
        /// True once we've first loaded all SampleCount samples into _fps
        /// </summary>
        private bool _fpsSamplesLoaded;
        private float _nextQualityCheck;

        private void Awake() {
            _nextQualityCheck = Time.unscaledTime + QualityCheckCooldownSec;
        }

        private void Update() {
            FrameTimingManager.CaptureFrameTimings();
            
            if (!_fpsSamplesLoaded && _fpsFront == SampleCount - 1) _fpsSamplesLoaded = true;
            _fps[_fpsFront++ % SampleCount] = (short) (1d / Time.unscaledDeltaTime);
            
            // Should we do a quality check?
            if (Time.time > _nextQualityCheck) {
                DoQualityCheck();
                _nextQualityCheck = Time.time + QualityCheckCooldownSec;
            }
        }

        private void DoQualityCheck() {
            var targetFrameRate = Application.targetFrameRate;
            if (targetFrameRate < 0 || targetFrameRate > Screen.currentResolution.refreshRateRatio.value)
                targetFrameRate = (int) (1.0 / Screen.currentResolution.refreshRateRatio.value);
            
            var currentFivePercent = GetPercentFps(0.05f);
            // If our 5% is lower than 90% of target we should drop quality
            if (currentFivePercent < 0.90 * targetFrameRate) {
                var avgFrameTimings = GetRecentAverageFrameTimings();
                Debug.Log("Warning: too laggy!: " + avgFrameTimings.gpu);
                // if (!avgFrameTimings.Equals(default)) {
                //     if (avgFrameTimings.gpu)
                // }
            }
        }

        private AverageFrameTimings GetRecentAverageFrameTimings() {
            var latestTimings = FrameTimingManager.GetLatestTimings(FrameTimingCount, _frameTimings);
            if (latestTimings <= 0) return default;

            var result = new AverageFrameTimings();
            for (var i = 0; i < latestTimings; i++) {
                result.cpuMain += _frameTimings[i].cpuMainThreadFrameTime;
                result.cpuRender += _frameTimings[i].cpuRenderThreadFrameTime;
                result.gpu += _frameTimings[i].gpuFrameTime;
            }
            result.cpuMain /= latestTimings;
            result.cpuRender /= latestTimings;
            result.gpu /= latestTimings;
            return result;
        }
        
        /// <summary>
        /// Gets the slowest percent of frames and averages them.
        /// </summary>
        private double GetPercentFps(float percent) {
            var percentSampleCount = (int) Mathf.Ceil(SampleCount * percent);
            
            // Load the first N samples and keep samples sorted
            var samples = new List<short>(_fps.AsSpan(0, percentSampleCount).ToArray());
            samples.Sort();
            
            for (var i = percentSampleCount; i < _fps.Length; i++) {
                // If we're slower than the best FPS in samples, insert
                var largestFpsSample = samples[^1];
                var sample = _fps[i];
                if (sample < largestFpsSample) {
                    var index = samples.BinarySearch(sample);
                    samples.Insert(index, sample);
                    samples.RemoveAt(samples.Count - 1);
                }
            }

            var fpsTotal = 0d;
            foreach (var sample in samples) {
                fpsTotal += sample;
            }
            return fpsTotal / samples.Count;
        }
    }
}