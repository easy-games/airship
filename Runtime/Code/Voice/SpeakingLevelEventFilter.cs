using System;
using Adrenak.UniVoice;
using Mirror;
using Utils = Adrenak.UniVoice.Utils;

namespace Code.Voice {
    /// <summary>
    /// This won't modify audio. It is used to capture and emit audio speaking level
    /// prior to encoding.
    ///
    /// 
    ///  O O    SUP
    /// .,,,.  /
    /// </summary>
    public class SpeakingLevelEventFilter : IAudioFilter {
        private Action<int, float> speakingLevelEvent;
        
        public SpeakingLevelEventFilter(Action<int, float> speakingLevelEvent) {
            this.speakingLevelEvent = speakingLevelEvent;
        }

        public AudioFrame Run(AudioFrame input) {
            speakingLevelEvent?.Invoke(NetworkClient.connection.connectionId, AirshipVoiceUtils.ComputeSpeakingLevel(Utils.Bytes.BytesToFloats(input.samples)));
            return input;
        }
    }
}