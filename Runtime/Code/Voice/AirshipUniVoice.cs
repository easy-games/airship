using System;
using UnityEngine;

using Adrenak.UniMic;
using Adrenak.UniVoice;
using Adrenak.UniVoice.Networks;
using Adrenak.UniVoice.Outputs;
using Adrenak.UniVoice.Inputs;
using Adrenak.UniVoice.Filters;
using Code.Player;
using Mirror;
using Utils = Adrenak.UniVoice.Utils;

namespace Code.Voice {
    [LuauAPI(LuauContext.Protected)]
    public class AirshipUniVoice : MonoBehaviour {
        const string TAG = "[AirshipUniVoice]";

        /// <summary>
        /// Fired with connectionId, speakingLevel [0-1] whenever a client speaks (including local player).
        ///
        /// Only fires on client.
        /// </summary>
        public static event Action<int, float> OnSpeakingLevelChanged;

        /// <summary>
        /// Whether UniVoice server has been setup successfully.
        /// </summary>
        public static bool HasSetUpServer { get; private set; }
        /// <summary>
        /// Whether UniVoice client has been setup successfully.
        /// </summary>
        public static bool HasSetUpClient { get; private set; }

        /// <summary>
        /// The server object.
        /// </summary>
        public static MirrorServer AudioServer { get; private set; }

        /// <summary>
        /// The client session.
        /// </summary>
        public static ClientSession<int> ClientSession { get; private set; }

        private AudioForwarder audioForwarder;

#pragma warning disable CS0414
        [SerializeField] bool useRNNoise4UnityIfAvailable = true;

        [SerializeField] bool useConcentusEncodeAndDecode = true;

        [SerializeField] bool useVad = true;
#pragma warning restore
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticFields() {
            HasSetUpClient = false;
            HasSetUpServer = false;
            AudioServer = null;
            ClientSession = null;
        }

        private void Start() {
            if (RunCore.IsServer()) TrySetupServer();
            if (RunCore.IsClient()) TrySetupClient();
        }

        void OnDestroy() {
            this.audioForwarder?.Dispose();
        }
        
        private void TrySetupServer() {
            if (HasSetUpServer) return;
            
            var createdAudioServer = SetupAudioServer();
            if (!createdAudioServer) {
                Debug.LogError("Could not setup UniVoice server.");
                return;
            }
            HasSetUpServer = true;
        }

        private void TrySetupClient() {
            if (HasSetUpClient) return;
            
            var setupAudioClient = SetupClientSession();
            if (!setupAudioClient) {
                Log("Could not setup UniVoice client.");
                return;
            }
            HasSetUpClient = true;
        }

        bool SetupAudioServer() {
            // ---- CREATE AUDIO SERVER AND SUBSCRIBE TO EVENTS TO PRINT LOGS ----
            // We create a server. If this code runs in server mode, MirrorServer will take care
            // or automatically handling all incoming messages. On a device connecting as a client,
            // this code doesn't do anything.
            AudioServer = new MirrorServer();
            Log("Created MirrorServer object");

            AudioServer.OnServerStart += () => {
                Log("Server started");
            };
            
            AudioServer.OnServerStop += () => {
                Log("Server stopped");
            };

            var serverBootstrap = FindFirstObjectByType<ServerBootstrap>();
            this.audioForwarder = AudioForwarder.Create(
                connId => PlayerManagerBridge.Instance.GetPlayerInfoByConnectionId(connId)?.userId,
                serverBootstrap != null ? serverBootstrap.organizationId : "",
                serverBootstrap != null ? serverBootstrap.gameId : "",
                serverBootstrap != null ? serverBootstrap.serverId : ""
            );
            if (this.audioForwarder != null)
                AudioServer.OnAudioFrameReceived += this.audioForwarder.Send;

            return true;
        }

        bool SetupClientSession() {
            // ---- CREATE AUDIO CLIENT AND SUBSCRIBE TO EVENTS ----
            IAudioClient<int> client = new MirrorClient();
            
            // Forward speaking level to TS for Mic display (this is only for peers)
            client.OnPostProcessedPeerAudioFrame += (connectionId, frame) => {
                var speakingLevel = AirshipVoiceUtils.ComputeSpeakingLevel(Utils.Bytes.BytesToFloats(frame.samples));
                OnSpeakingLevelChanged?.Invoke(connectionId, speakingLevel);
            };
            
            client.OnJoined += (id, peerIds) => {
                Log($"You are Peer ID {id}");
            };

            client.OnLeft += () => {
                Log("You left the chatroom");
            };

            // When a peer joins, we instantiate a new peer view 
            client.OnPeerJoined += id => {
                Log($"Peer {id} joined");
            };

            // When a peer leaves, destroy the UI representing them
            client.OnPeerLeft += id => {
                Log($"Peer {id} left");
            };

            Log("Created MirrorClient object");

            // ---- CREATE AUDIO OUTPUT FACTORY ----
            IAudioOutputFactory<int> outputFactory;
            // We want the incoming audio from peers to be played via the StreamedAudioSourceOutput
            // implementation of IAudioSource interface. So we get the factory for it.
            outputFactory = new PlayerAudioSourceOutput.Factory();
            Log("Using StreamedAudioSourceOutput.Factory as output factory");

            // ---- CREATE CLIENT SESSION AND ADD FILTERS TO IT ----
            // With the client, input and output factory ready, we create create the client session
            ClientSession = new ClientSession<int>(client, null, outputFactory);
            Log("Created session");

#if !UNITY_ANDROID
            if(useRNNoise4UnityIfAvailable) {
                // RNNoiseFilter to remove noise from captured audio
                ClientSession.InputFilters.Add(new RNNoiseFilter());
                Log("Registered RNNoiseFilter as an input filter");
            }
#endif

            if (useVad) {
                // We add the VAD filter after RNNoise. 
                // This way lot of the background noise has been removed, VAD is truly trying to detect voice
                ClientSession.InputFilters.Add(new SimpleVadFilter(new SimpleVad()));
            }
            
            // Inject a "filter" to grab speaking level prior to encode on Input
            ClientSession.InputFilters.Add(new SpeakingLevelEventFilter((id, level) => {
                OnSpeakingLevelChanged?.Invoke(id, level);
            }));

            if (useConcentusEncodeAndDecode) {
                // ConcentureEncoder filter to encode captured audio that reduces the audio frame size
                ClientSession.InputFilters.Add(new ConcentusEncodeFilter());
                Log("Registered ConcentusEncodeFilter as an input filter");

                // For incoming audio register the ConcentusDecodeFilter to decode the encoded audio received from other clients 
                ClientSession.AddOutputFilter<ConcentusDecodeFilter>(() => new ConcentusDecodeFilter());
                Log("Registered ConcentusDecodeFilter as an output filter");
            }

            return true;
        }

        public static void Log(string message) {
#if AIRSHIP_PLAYER
            Debug.Log("[AirshipUniVoice] " + message);
#endif
        }

        public static void StartRecording(Mic.Device mic) {
            // Since in this sample we use microphone input via UniMic, we first check if there
            // are any mic devices available.
            Mic.Init(); // Must do this to use the Mic class
            
            mic.StartRecording();
            Log("Started recording with Mic device named." +
                                                    mic.Name + $" at frequency {mic.SamplingFrequency} with frame duration {mic.FrameDurationMS} ms.");
            ClientSession.Input = new UniMicInput(mic);
            Log("Created UniMicInput");
        }

        /// <summary>
        /// Sets the server muted status for a client. If muted the client will be unable to transmit audio.
        /// </summary>
        [LuauAPI(LuauContext.Game)]
        public static void ServerMute(int connectionId, bool muted) {
            if (muted) AudioServer.ServerMutedClientIDs.Add(connectionId);
            else AudioServer.ServerMutedClientIDs.Remove(connectionId);
        }

        /// <summary>
        /// Mutes a client 
        /// </summary>
        [LuauAPI(LuauContext.Game)]
        public static void MutePeer(int peerConnectionId, bool muted) {
            var mutedPeers = ClientSession.Client.YourVoiceSettings.mutedPeers;
            var muteListUpdated = false;
            if (muted) {
                // Add to muted client list if not already in
                if (!mutedPeers.Contains(peerConnectionId)) {
                    mutedPeers.Add(peerConnectionId);
                    muteListUpdated = true;
                }
            } else {
                // Unmute by removing from mutedPeers list
                if (mutedPeers.Remove(peerConnectionId)) {
                    muteListUpdated = true;
                }
            }
            if (muteListUpdated) ClientSession.Client.SubmitVoiceSettings();
        }

        /// <summary>
        /// Only can be run on client, returns true if a peer is muted
        /// </summary>
        [LuauAPI(LuauContext.Game)]
        public static bool IsPeerMuted(int peerConnectionId) {
            return ClientSession.Client.YourVoiceSettings.mutedPeers.Contains(peerConnectionId);
        }

        /// <summary>
        /// Called from the client, sets self deafened status (and sends settings change to server)
        /// </summary>
        public static void ClientSetDeafened(bool deafened) {
            // muteAll is UniVoice way of muting all incoming audio
            if (deafened == ClientSession.Client.YourVoiceSettings.muteAll) return;
            ClientSession.Client.YourVoiceSettings.muteAll = deafened;
            ClientSession.Client.SubmitVoiceSettings();
        }

        public static void StopRecording() {
            if (ClientSession.Input is UniMicInput uniMicInput) {
                uniMicInput.Device.StopRecording();
            }
        }
        
        public static bool IsRecording() {
            if (ClientSession.Input is UniMicInput uniMicInput) {
                return uniMicInput.Device.IsRecording;
            }
            return false;
        }
    }
}