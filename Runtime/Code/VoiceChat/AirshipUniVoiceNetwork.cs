using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Adrenak.UniMic;
using Adrenak.UniVoice;
using Adrenak.UniVoice.AudioSourceOutput;
using Adrenak.UniVoice.UniMicInput;
using Code.Player;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Code.VoiceChat {
    public class AirshipUniVoiceNetwork : NetworkBehaviour, IChatroomNetwork
     {
         // Hosting events
        public event Action OnCreatedChatroom;
        public event Action<Exception> OnChatroomCreationFailed;
        public event Action OnClosedChatroom;

        // Joining events
        public event Action<short> OnJoinedChatroom;
        public event Action<Exception> OnChatroomJoinFailed;
        public event Action OnLeftChatroom;

        // Peer events
        public event Action<short, int, AudioSource> OnPeerJoinedChatroom;
        public event Action<short> OnPeerLeftChatroom;

        // Audio events
        public event Action<short, ChatroomAudioSegment> OnAudioReceived;
        public event Action<ChatroomAudioSegment> OnAudioBroadcasted;

        // Peer ID management
        public short OwnID { get; private set; } = -1;
        public List<short> PeerIDs { get; private set; } = new List<short>();

        // UniVoice peer ID <-> FishNet connection ID mapping
        short peerCount = 0;
        private readonly Dictionary<short, int> peerIdToClientIdMap = new Dictionary<short, int>();

        private ChatroomAgent agent;
        
        private void OnDisable() {
            if (this.agent != null) {
                this.agent.Dispose();
            }
        }

        public override void OnStartServer() {
            base.OnStartServer();

            OwnID = 0;
            OnCreatedChatroom?.Invoke();
        }

        public override void OnStartNetwork() {
            base.OnStartNetwork();

            this.Log("Creating VoiceChat agent..");
            this.agent = new ChatroomAgent(
                this,
                new UniVoiceUniMicInput(0, 16000, 100),
                new UniVoiceAudioSourceOutput.Factory()
            );

            PeerIDs.Clear();
            peerIdToClientIdMap.Clear();
            peerIdToClientIdMap.Add(0, 0); // server

            if (base.IsServerOnlyStarted) {
                OwnID = -1;
                OnClosedChatroom?.Invoke();
            }
        }

        public override void OnStopNetwork() {
            base.OnStopNetwork();

            // If the client disconnects while own ID is -1, that means
            // it haven't connected earlier and the connection attempt has failed.
            if (OwnID == -1) {
                OnChatroomJoinFailed?.Invoke(new Exception("Could not join chatroom"));
                return;
            }

            // This method is *also* called on the server when the server is shutdown.
            // So we check peer ID to ensure that we're running this only on a peer.
            if (OwnID > 0) {
                OwnID = -1;
                PeerIDs.Clear();
                peerIdToClientIdMap.Clear();
                OnLeftChatroom?.Invoke();
            }
        }

        [TargetRpc]
        void TargetNewClientInit(NetworkConnection connection, short peerId, int clientId, short[] existingPeers, int[] existingPeerClientIds) {
            // Get self ID and fire that joined chatroom event
            OwnID = peerId;
            OnJoinedChatroom?.Invoke(OwnID);

            var playerInfo = PlayerManagerBridge.Instance.GetPlayerInfoByClientId(clientId);

            for (int i = 0; i < existingPeers.Length; i++) {
                peerIdToClientIdMap.TryAdd(existingPeers[i], existingPeerClientIds[i]);
            }

            // Get the existing peer IDs from the message and fire
            // the peer joined event for each of them
            PeerIDs = existingPeers.ToList();
            PeerIDs.ForEach(x => {
                var conn = GetNetworkConnectionFromPeerId(peerId);
                if (conn != null) {
                    OnPeerJoinedChatroom?.Invoke(x, conn.ClientId, playerInfo.voiceChatAudioSource);
                }
            });

            this.Log($"Initialized self with ID {OwnID} and peers {string.Join(", ", PeerIDs)}");
        }

        [ObserversRpc]
        void ObserversClientJoined(int peerId, int clientId) {
            if (peerId == OwnID) return;

            var joinedId = (short)peerId;
            if (!PeerIDs.Contains(joinedId))
                PeerIDs.Add(joinedId);
            var playerInfo = PlayerManagerBridge.Instance.GetPlayerInfoByClientId(clientId);
            OnPeerJoinedChatroom?.Invoke(joinedId, clientId, playerInfo.voiceChatAudioSource);
        }

        [ObserversRpc]
        void ObserversClientLeft(int peerId, int clientId) {
            var leftId = (short)peerId;
            if (PeerIDs.Contains(leftId))
                PeerIDs.Remove(leftId);
            OnPeerLeftChatroom?.Invoke(leftId);
        }

        public override async void OnSpawnServer(NetworkConnection conn) {
            base.OnSpawnServer(conn);

            // Connection ID 0 is the server connecting to itself with a client instance.
            // We do not need this.
            // if (conn.ClientId == 0) return;

            // We get a peer ID for this connection id
            var peerId = RegisterConnectionId(conn.ClientId);
            var existingPeersInitPacket = PeerIDs
                        .Where(x => x != peerId)
                        .ToList();
            var clientIds = PeerIDs.Select((pId) => GetNetworkConnectionFromPeerId(pId).ClientId).ToList();

            // Server is ID 0, we add ourselves to the peer list
            // for the newly joined client
            existingPeersInitPacket.Add(0);
            clientIds.Add(0);

            TargetNewClientInit(conn, peerId, conn.ClientId, existingPeersInitPacket.ToArray(), clientIds.ToArray());

            // Server_OnClientConnected gets invoked as soon as a client connects
            // to the server. But we use NetworkServer.SendToAll to send our packets
            // and it seems the new Mirror Connection ID is not added to the KcpTransport
            // immediately, so we send this with an artificial delay of 100ms.
            // SendToClient(-1, newClientPacket.GetBuffer(), 100);

            string peerListString = string.Join(", ", existingPeersInitPacket);
            this.Log($"Initializing new client with ID {peerId} and peer list {peerListString}");

            ObserversClientJoined(peerId, conn.ClientId);

            var playerInfo = await PlayerManagerBridge.Instance.GetPlayerInfoFromClientIdAsync(conn.ClientId);
            OnPeerJoinedChatroom?.Invoke(peerId, conn.ClientId, playerInfo.voiceChatAudioSource);
        }

        void Log(string msg) {
            // if (!Application.isEditor) {
                Debug.Log(msg);
            // }
        }

        public override void OnDespawnServer(NetworkConnection connection) {
            base.OnDespawnServer(connection);

            // We use the peer map to get the peer ID for this connection ID
            var leftPeerId = GetPeerIdFromConnectionId(connection.ClientId);

            // We now go ahead with the server handling a client leaving
            // Remove the peer from our peer list
            if (PeerIDs.Contains(leftPeerId))
                PeerIDs.Remove(leftPeerId);

            // Remove the peer-connection ID pair from the map
            if (peerIdToClientIdMap.ContainsKey(leftPeerId))
                peerIdToClientIdMap.Remove(leftPeerId);

            // Notify all remaining peers that a peer has left
            // so they can update their peer lists
            ObserversClientLeft(leftPeerId, connection.ClientId);
            OnPeerLeftChatroom?.Invoke(leftPeerId);
        }

        public void HostChatroom(object data = null) {
            throw new NotImplementedException();
        }

        public void CloseChatroom(object data = null) {
            throw new NotImplementedException();
        }

        public void JoinChatroom(object data = null) {
            throw new NotImplementedException();
        }

        public void LeaveChatroom(object data = null) {
            throw new NotImplementedException();
        }

        [ServerRpc]
        void RpcSendAudioToServer(byte[] bytes, Channel channel = Channel.Unreliable, NetworkConnection conn = null) {
            var segment = FromByteArray<ChatroomAudioSegment>(bytes);

            var senderPeerId = this.GetPeerIdFromConnectionId(conn.ClientId);
            RpcSendAudioToClient(null, senderPeerId, bytes);
            OnAudioReceived?.Invoke(senderPeerId, segment);
        }

        [TargetRpc][ObserversRpc]
        void RpcSendAudioToClient(NetworkConnection conn, short senderPeerId, byte[] bytes, Channel channel = Channel.Unreliable) {
            var segment = FromByteArray<ChatroomAudioSegment>(bytes);
            OnAudioReceived?.Invoke(senderPeerId, segment);
        }

        public void BroadcastAudioSegment(ChatroomAudioSegment data) {
            if (IsOffline) return;

            if (IsServerStarted) {
                RpcSendAudioToClient(null, 0, ToByteArray(data));
            } else if (IsClientStarted) {
                RpcSendAudioToServer(ToByteArray(data));
            }

            OnAudioBroadcasted?.Invoke(data);
        }

        /// <summary>
        /// Returns the UniVoice peer Id corresponding to a previously
        /// registered Mirror connection Id
        /// </summary>
        /// <param name="connId">The connection Id to lookup</param>
        /// <returns>THe UniVoice Peer ID</returns>
        short GetPeerIdFromConnectionId(int connId) {
            foreach (var pair in peerIdToClientIdMap) {
                if (pair.Value == connId)
                    return pair.Key;
            }
            return -1;
        }

        NetworkConnection GetNetworkConnectionFromPeerId(short peerId) {
            if (IsServerStarted) {
                if (InstanceFinder.ServerManager.Clients.TryGetValue(peerIdToClientIdMap[peerId], out var connection)) {
                    return connection;
                }
                return null;
            } else {
                if (InstanceFinder.ClientManager.Clients.TryGetValue(peerIdToClientIdMap[peerId], out var connection)) {
                    return connection;
                }

                return null;
            }
        }

        /// <summary>
        /// Connection ID need not be a short type. In Mirror, it can also be a very large
        /// number, for exmaple KcpTransport connection Ids can be something like 390231886
        /// Since UniVoice uses sequential short values to store peers, we generate a peer ID
        /// from any int connection Id and use a dictionary to store them in pairs.
        /// </summary>
        /// <param name="connId">The Mirror connection ID to be registered</param>
        /// <returns>The UniVoice Peer ID after registration</returns>
        short RegisterConnectionId(int connId) {
            // if (connId == 0) {
            //     // the server
            //     clientMap.Add(0, 0);
            //     PeerIDs.Add(0);
            //     return 0;
            // }

            peerCount++;
            peerIdToClientIdMap.Add(peerCount, connId);
            PeerIDs.Add(peerCount);
            return peerCount;
        }

        public byte[] ToByteArray<T>(T obj) {
            if (obj == null)
                return null;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream()) {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        public T FromByteArray<T>(byte[] data) {
            if (data == null)
                return default;
            BinaryFormatter bf = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream(data)) {
                object obj = bf.Deserialize(ms);
                return (T)obj;
            }
        }

        public void Dispose() {

        }
    }
}