using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Quality {
    // We should redo this to be a single core number for quality
    // NOTE: must match FrameHealth in Airship.d.ts
    public enum FrameHealth {
        Ok = 0,
        Unhealthy = 1,
    }

    public struct QualityReport {
        public double gpuAvg;
        public double cpuMainAvg;
        public double cpuRenderAvg;

        public int numFrames;
    }
    
    [LuauAPI(LuauContext.Game)]
    public class QualityManager : Singleton<QualityManager> {
        public static event Action<object, object> OnQualityCheck;
        
        private const int SampleCount = 500;
        /// <summary>
        /// Time after starting that we'll run a quality check on the client. This
        /// will be sent to game clients to allow them to configure quality based on
        /// performance.
        /// </summary>
        private const float QualityCheckTimeSec = 15;
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

        /// <summary>
        /// For now we only run the quality check once after 15 seconds.
        /// </summary>
        private bool _hasRunQualityCheck;

        private void Awake() {
            _nextQualityCheck = Time.unscaledTime + QualityCheckTimeSec;
        }

        private void Update() {
            if (_hasRunQualityCheck) return;
            
            FrameTimingManager.CaptureFrameTimings();
            
            if (!_fpsSamplesLoaded && _fpsFront == SampleCount - 1) _fpsSamplesLoaded = true;
            _fps[_fpsFront++ % SampleCount] = (short) (1d / Time.unscaledDeltaTime);
            
            // Should we do a quality check?
            if (Time.time > _nextQualityCheck) {
                _hasRunQualityCheck = true;
                DoQualityCheck();
                _nextQualityCheck = Time.time + QualityCheckTimeSec;
            }
        }

        private void DoQualityCheck() {
            var targetFrameRate = Application.targetFrameRate;
            if (targetFrameRate < 0 || targetFrameRate > Screen.currentResolution.refreshRateRatio.value)
                targetFrameRate = (int) (1.0 / Screen.currentResolution.refreshRateRatio.value);
            
            var currentFivePercent = GetPercentFps(0.05f);

            var frameHealth = FrameHealth.Ok;
            var avgFrameTimings = GetRecentAverageFrameTimings();
            
            // If our 5% is lower than 80% of target we should drop quality
            if (currentFivePercent < 0.80 * targetFrameRate) {
                frameHealth = FrameHealth.Unhealthy;
            }
            
            OnQualityCheck?.Invoke(frameHealth, avgFrameTimings);
        }

        private QualityReport GetRecentAverageFrameTimings() {
            if (!FrameTimingManager.IsFeatureEnabled()) return default;
            
            var numLatestTimings = FrameTimingManager.GetLatestTimings(FrameTimingCount, _frameTimings);
            if (numLatestTimings <= 0) return default;

            var result = new QualityReport();
            for (var i = 0; i < numLatestTimings; i++) {
                result.cpuMainAvg += _frameTimings[i].cpuMainThreadFrameTime;
                result.cpuRenderAvg += _frameTimings[i].cpuRenderThreadFrameTime;
                result.gpuAvg += _frameTimings[i].gpuFrameTime;
            }
            result.cpuMainAvg /= numLatestTimings;
            result.cpuRenderAvg /= numLatestTimings;
            result.gpuAvg /= numLatestTimings;
            result.numFrames = (int) numLatestTimings;
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
                    // Will return bitwise compliment if larger than list
                    if (index < 0) index = ~index;
                    
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