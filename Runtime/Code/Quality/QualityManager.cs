using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sentry;
using Tayx.Graphy;

namespace Code.Quality {
    // We should redo this to be a single core number for quality
    // NOTE: must match FrameHealth in Airship.d.ts
    public enum FrameHealth {
        Ok = 0,
        Unhealthy = 1,
    }

    [LuauAPI]
    public struct QualityReport {
        public double gpuAvg;
        public double cpuMainAvg;
        public double cpuRenderAvg;

        public int numFrames;
    }
    
    [LuauAPI(LuauContext.Game)]
    public class QualityManager : Singleton<QualityManager> {
        public static event Action<object, object> OnQualityCheck;
        
        private const int MaxSampleCount = 500;
        private int samplesCaptured = 0;
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
        private short[] _fps = new short[MaxSampleCount];
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
        private bool firstRun = true;

        private bool capturingFrameTimings = false;
        private float captureFrameTimingsUntilTime = 0;

        private ITransactionTracer tracer;

        private void Start() {
            this.StartCapture();
        }

        private void StartCapture() {
            this.capturingFrameTimings = true;
            this.captureFrameTimingsUntilTime = Time.unscaledTime + QualityCheckTimeSec;
            this.samplesCaptured = 0;
            this.tracer = SentrySdk.StartTransaction("quality-manager", "sample-quality");
        }

        private void Update() {
            if (this.capturingFrameTimings) {
                FrameTimingManager.CaptureFrameTimings();

                if (!_fpsSamplesLoaded && _fpsFront == MaxSampleCount - 1) _fpsSamplesLoaded = true;
                if (this.samplesCaptured < MaxSampleCount) {
                    _fps[_fpsFront++ % MaxSampleCount] = (short) (1d / Time.unscaledDeltaTime);
                    this.samplesCaptured++;
                }

                // Are we ready to stop capturing and do the quality check?
                if (Time.unscaledTime > this.captureFrameTimingsUntilTime) {
                    this.capturingFrameTimings = false;
                    DoQualityCheck(this.firstRun);
                    this.firstRun = false;
                    this.StartCoroutine(this.ScheduleNextCapture());
                }
            }
        }

        private IEnumerator ScheduleNextCapture() {
            yield return new WaitForSeconds(120);
            this.StartCapture();
        }

        private int GetRealTargetFPS() {
            var targetFrameRate = Application.targetFrameRate;
            if (targetFrameRate < 0 || targetFrameRate > Screen.currentResolution.refreshRateRatio.value)
                targetFrameRate = (int) (1.0 / Screen.currentResolution.refreshRateRatio.value);
            return targetFrameRate;
        }

        private void DoQualityCheck(bool fireSignal) {
            var targetFrameRate = this.GetRealTargetFPS();
            
            var currentFivePercent = GetPercentFps(0.05f);
            var pointFivePercent = GetPercentFps(0.005f);

            var frameHealth = FrameHealth.Ok;
            var avgFrameTimings = GetRecentAverageFrameTimings();
            
            // If our 5% is lower than 80% of target we should drop quality
            if (currentFivePercent < 0.80 * targetFrameRate) {
                frameHealth = FrameHealth.Unhealthy;
            }

            print($"[Quality Check] 5%: {currentFivePercent}, 0.5%: {pointFivePercent}, health: {frameHealth}");

            if (this.tracer != null) {
                this.tracer.SetMeasurement("fps_5_percent", currentFivePercent);
                this.tracer.SetMeasurement("fps_0.5_percent", pointFivePercent);
                this.tracer.SetMeasurement("target_fps", targetFrameRate);
                this.tracer.SetMeasurement("cpu_main_avg", avgFrameTimings.cpuMainAvg);
                this.tracer.SetMeasurement("cpu_render_avg", avgFrameTimings.cpuRenderAvg);
                this.tracer.SetMeasurement("gpu_avg", avgFrameTimings.gpuAvg);
                this.tracer.SetData("frame_health", frameHealth == FrameHealth.Ok ? "ok" : "unhealthy");
                this.tracer.Finish(SpanStatus.Ok);
                this.tracer = null;
            }

            if (fireSignal) {
                OnQualityCheck?.Invoke(frameHealth, avgFrameTimings);
            }
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
            result.numFrames = (int)numLatestTimings;
            return result;
        }
        
        /// <summary>
        /// Gets the slowest percent of frames and averages them.
        /// </summary>
        private double GetPercentFps(float percent) {
            var percentSampleCount = (int) Mathf.Ceil(this.samplesCaptured * percent);
            
            // Load the first N samples and keep samples sorted
            var samples = new List<short>(_fps.AsSpan(0, percentSampleCount).ToArray());
            samples.Sort();
            
            for (var i = percentSampleCount; i < this.samplesCaptured; i++) {
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