using UnityEngine;

namespace Code.Voice {
    public class AirshipVoiceUtils {
        /// <summary>
        /// Computes speaking level for display based on a set of samples
        /// </summary>
        public static float ComputeSpeakingLevel(float[] samples) {
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++) {
                sum += samples[i] * samples[i];
            }
            float rms = Mathf.Sqrt(sum / samples.Length);
            return Mathf.Clamp01(rms * 10f); // scale up and clamp
        }
    }
}