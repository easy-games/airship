using System.Threading.Tasks;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using Code.Player;
using UnityEngine;

namespace Code.Voice {
    [RequireComponent(typeof(StreamedAudioSource))]
    public class PlayerAudioSourceOutput : MonoBehaviour, IAudioOutput {
        const string TAG = "[StreamedAudioSourceOutput]";

        public StreamedAudioSource Stream { get; private set; }
        public GameObject AudioSourceGameObject;

        [System.Obsolete("Cannot use new keyword to create an instance. Use the .New() method instead")]
        public PlayerAudioSourceOutput() { }
        
        /// <summary>
        /// Creates a new instance using the dependencies.
        /// </summary>
        public static PlayerAudioSourceOutput New(int peerId) {
            var audioOutputGO = new GameObject("StreamedAudioSourceOutput");
            DontDestroyOnLoad(audioOutputGO);
            var cted = audioOutputGO.AddComponent<PlayerAudioSourceOutput>();
            
            // Hook up audio source output to player's voiceChatAudioSource
            PlayerManagerBridge.Instance.GetPlayerInfoFromConnectionIdAsync(peerId).ContinueWith(
                (playerInfo) => {
                    if (!playerInfo.IsCompletedSuccessfully) {
                        Debug.LogWarning($"Failed to setup voice AudioSource for {peerId}");
                        return;
                    }
                    
                    var go = playerInfo.Result.voiceChatAudioSource.gameObject;
                    cted.Stream = go.GetComponent<StreamedAudioSource>() ?? go.AddComponent<StreamedAudioSource>();
                }, TaskScheduler.FromCurrentSynchronizationContext());
            return cted;
        }

        /// <summary>
        /// Feeds an incoming <see cref="ChatroomAudioSegment"/> into the audio buffer.
        /// </summary>
        /// <param name="frame"></param>
        public void Feed(AudioFrame frame) {
            if (Stream == null) return;
            Stream.Feed(frame.frequency, frame.channelCount, Utils.Bytes.BytesToFloats(frame.samples));
        }

        /// <summary>
        /// Disposes the instance by deleting the GameObject of the component.
        /// </summary>
        public void Dispose() {
            Debug.unityLogger.Log(LogType.Log, TAG, "Disposing StreamedAudioSource");
            Destroy(gameObject);
        }

        /// <summary>
        /// Creates <see cref="UniVoiceAudioSourceOutput"/> instances
        /// </summary>
        public class Factory : IAudioOutputFactory<int> {
            public IAudioOutput Create(int peerId) {
                return New(peerId);
            }
        }
    }
}