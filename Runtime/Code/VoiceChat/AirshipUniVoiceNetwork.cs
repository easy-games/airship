using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
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
    [LuauAPI(LuauContext.Protected)]
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

        public ChatroomAgent agent;
        
        private void OnDisable() {
            if (this.agent != null) {
                this.agent.Dispose();
            }
        }

        public override void OnStartServer() {
            base.OnStartServer();

            OnCreatedChatroom?.Invoke();
        }

        public override void OnStartNetwork() {
            base.OnStartNetwork();

            this.agent = new ChatroomAgent(
                this,
                new UniVoiceUniMicInput(0, 16000, 100),
                new UniVoiceAudioSourceOutput.Factory()
            );

            PeerIDs.Clear();
            peerIdToClientIdMap.Clear();
            // peerIdToClientIdMap.Add(0, 0); // server
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
            if (OwnID >= 0) {
                OwnID = -1;
                PeerIDs.Clear();
                peerIdToClientIdMap.Clear();
                OnLeftChatroom?.Invoke();
            }
        }

        [TargetRpc]
        void TargetNewClientInit(NetworkConnection connection, short peerId, int clientId, short[] existingPeers, int[] existingPeerClientIds) {
            this.Log($"Initialized self with PeerId {peerId} and peers: {string.Join(", ", existingPeers)}");

            // Get self ID and fire that joined chatroom event
            OwnID = peerId;
            OnJoinedChatroom?.Invoke(OwnID);

            for (int i = 0; i < existingPeers.Length; i++) {
                peerIdToClientIdMap.TryAdd(existingPeers[i], existingPeerClientIds[i]);
            }

            // Get the existing peer IDs from the message and fire
            // the peer joined event for each of them
            PeerIDs = existingPeers.ToList();
            PeerIDs.ForEach(async x => {
                var conn = GetNetworkConnectionFromPeerId(x);;
                if (conn != null) {
                    var playerInfo = await PlayerManagerBridge.Instance.GetPlayerInfoFromClientIdAsync(conn.ClientId);
                    await Awaitable.MainThreadAsync();
                    if (playerInfo != null) {
                        OnPeerJoinedChatroom?.Invoke(x, conn.ClientId, playerInfo.voiceChatAudioSource);
                    }
                }
            });
        }

        [TargetRpc]
        void ObserversClientJoined(NetworkConnection targetConn, int peerId, int clientId) {
            this.Log($"New peer joined with PeerId: {peerId}, ClientId: {clientId}");

            var joinedId = (short)peerId;
            if (!PeerIDs.Contains(joinedId)) {
                PeerIDs.Add(joinedId);
            }
            peerIdToClientIdMap.TryAdd(joinedId, clientId);

            var _ = Task.Run(() => PlayerManagerBridge.Instance.GetPlayerInfoFromClientIdAsync(clientId).ContinueWith(
                async result => {
                    await Awaitable.MainThreadAsync();
                    print("Firing OnPeerJoinedChatroom for peer: " + joinedId + " with playerInfo: " + result.Result.username.Value + " clientId=" + result.Result.clientId.Value);
                    OnPeerJoinedChatroom?.Invoke(joinedId, clientId, result.Result.voiceChatAudioSource);
                }));
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

            // We get a peer ID for this connection id
            var peerId = RegisterConnectionId(conn.ClientId);
            var existingPeersInitPacket = PeerIDs
                        .Where(x => x != peerId)
                        .ToList();
            var clientIds = PeerIDs.Select((pId) => {
                var conn = GetNetworkConnectionFromPeerId(pId);

                if (conn == null) {
                    Debug.LogError("Unable to find NetworkConnection for PeerId: " + pId);
                    return -1;
                }

                return conn.ClientId;
            }).Where((x) => x != -1).ToList();

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
            this.Log($"Initializing new client with ID {peerId} and peers: {peerListString}");

            foreach (var otherConn in InstanceFinder.ServerManager.Clients.Values) {
                if (otherConn != conn) {
                    ObserversClientJoined(otherConn, peerId, conn.ClientId);
                }
            }

            var playerInfo = await PlayerManagerBridge.Instance.GetPlayerInfoFromClientIdAsync(conn.ClientId);
            await Awaitable.MainThreadAsync();
            OnPeerJoinedChatroom?.Invoke(peerId, conn.ClientId, playerInfo.voiceChatAudioSource);
        }

        void Log(string msg) {
            if (!Application.isEditor || RunCore.IsInternal()) {
                Debug.Log("[VoiceChat] " + msg);
            }
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

        [ServerRpc(RequireOwnership = false)]
        void RpcSendAudioToServer(byte[] bytes, Channel channel = Channel.Unreliable, NetworkConnection conn = null) {
            var senderPeerId = this.GetPeerIdFromConnectionId(conn.ClientId);
            // print("[server] received audio from peer " + senderPeerId);
            RpcSendAudioToClient(null, senderPeerId, bytes);

            // var segment = FromByteArray<ChatroomAudioSegment>(bytes);
            // OnAudioReceived?.Invoke(senderPeerId, segment);
        }

        [TargetRpc][ObserversRpc]
        void RpcSendAudioToClient(NetworkConnection conn, short senderPeerId, byte[] bytes, Channel channel = Channel.Unreliable) {
            // print("[client] received audio from server for peer " + senderPeerId);
            var segment = FromByteArray<ChatroomAudioSegment>(bytes);
            OnAudioReceived?.Invoke(senderPeerId, segment);
        }

        public void BroadcastAudioSegment(ChatroomAudioSegment data) {
            if (IsOffline) return;

            if (IsClientStarted) {
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
            if (!peerIdToClientIdMap.ContainsKey(peerId)) {
                return null;
            }
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