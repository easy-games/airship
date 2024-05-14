using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using Adrenak.UniVoice;
using Adrenak.UniVoice.AudioSourceOutput;
using Adrenak.UniVoice.UniMicInput;
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
        public event Action<short> OnPeerJoinedChatroom;
        public event Action<short> OnPeerLeftChatroom;

        // Audio events
        public event Action<short, ChatroomAudioSegment> OnAudioReceived;
        public event Action<short, ChatroomAudioSegment> OnAudioSent;

        // Peer ID management
        public short OwnID { get; private set; } = -1;
        public List<short> PeerIDs { get; private set; } = new List<short>();

        // UniVoice peer ID <-> FishNet connection ID mapping
        short peerCount = 0;
        readonly Dictionary<short, int> clientMap = new Dictionary<short, int>();

        private void Awake() {
            var agent = new ChatroomAgent(
                this,
                new UniVoiceUniMicInput(0, 16000, 100),
                new UniVoiceAudioSourceOutput.Factory()
            );
        }

        public override void OnStartNetwork() {
            base.OnStartNetwork();

            PeerIDs.Clear();
            clientMap.Clear();

            if (base.IsServerOnlyStarted) {
                OwnID = -1;
                OnClosedChatroom?.Invoke();
            }
        }

        public override void OnStartClient() {
            base.OnStartClient();
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
                clientMap.Clear();
                OnLeftChatroom?.Invoke();
            }
        }

        [ObserversRpc]
        void ObserversNewClientInit(int peerId, int[] existingPeers) {
            // Get self ID and fire that joined chatroom event
            OwnID = (short)peerId;
            OnJoinedChatroom?.Invoke(OwnID);

            // Get the existing peer IDs from the message and fire
            // the peer joined event for each of them
            PeerIDs = existingPeers.Select(x => (short)x).ToList();
            PeerIDs.ForEach(x => OnPeerJoinedChatroom?.Invoke(x));

            this.Log($"Initialized self with ID {OwnID} and peers {string.Join(", ", PeerIDs)}");
        }

        [ObserversRpc]
        void ObserversClientJoined(int peerId) {
            var joinedId = (short)peerId;
            if (!PeerIDs.Contains(joinedId))
                PeerIDs.Add(joinedId);
            OnPeerJoinedChatroom?.Invoke(joinedId);
        }

        [ObserversRpc]
        void ObserversClientLeft(int peerId) {
            var leftId = (short)peerId;
            if (PeerIDs.Contains(leftId))
                PeerIDs.Remove(leftId);
            OnPeerLeftChatroom?.Invoke(leftId);
        }

        public override void OnSpawnServer(NetworkConnection conn) {
            base.OnSpawnServer(conn);

            // TODO: This causes the chatroom is to detected as created only when
            // the first peer joins. While this doesn't cause any bugs, it isn't right.
            if (InstanceFinder.IsServerStarted) {
                OwnID = 0;
                OnCreatedChatroom?.Invoke();
            }

            // Connection ID 0 is the server connecting to itself with a client instance.
            // We do not need this.
            // if (conn.ClientId == 0) return;

            // We get a peer ID for this connection id
            var peerId = RegisterConnectionId(conn.ClientId);

            // We go through each the peer that the server has registered
            foreach (var peer in PeerIDs) {
                // To the new peer, we send data to initialize it with.
                // This includes the following:
                // - peer Id: short: This tells the new peer its ID in the chatroom
                // - existing peers: short[]: This tells the new peer the IDs of the
                // peers that are already in the chatroom
                if (peer == peerId) {
                    // Get all the existing peer IDs except that of the newly joined peer
                    var existingPeersInitPacket = PeerIDs
                        .Where(x => x != peer)
                        .Select(x => (int)x)
                        .ToList();

                    // Server is ID 0, we add outselves to the peer list
                    // for the newly joined client
                    existingPeersInitPacket.Add(0);

                    ObserversNewClientInit(peerId, existingPeersInitPacket.ToArray());

                    // Server_OnClientConnected gets invoked as soon as a client connects
                    // to the server. But we use NetworkServer.SendToAll to send our packets
                    // and it seems the new Mirror Connection ID is not added to the KcpTransport
                    // immediately, so we send this with an artificial delay of 100ms.
                    // SendToClient(-1, newClientPacket.GetBuffer(), 100);

                    string peerListString = string.Join(", ", existingPeersInitPacket);
                    this.Log($"Initializing new client with ID {peerId} and peer list {peerListString}");
                }
                // To the already existing peers, we let them know a new peer has joined
                else {
                    ObserversClientJoined(peer);
                }
            }
            OnPeerJoinedChatroom?.Invoke(peerId);
        }

        void Log(string msg) {
            if (!Application.isEditor) {
                Debug.Log(msg);
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
            if (clientMap.ContainsKey(leftPeerId))
                clientMap.Remove(leftPeerId);

            // Notify all remaining peers that a peer has left
            // so they can update their peer lists
            ObserversClientLeft(leftPeerId);
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
        void RpcSendAudioToServer(short senderPeerId, short recipientPeerId, byte[] bytes, Channel channel = Channel.Unreliable) {
            if (recipientPeerId == OwnID) {
                var segment = FromByteArray<ChatroomAudioSegment>(bytes);
                OnAudioReceived?.Invoke(senderPeerId, segment);
            } else if (PeerIDs.Contains(recipientPeerId)) {
                var conn = GetNetworkConnectionFromPeerId(recipientPeerId);
                if (conn != null) {
                    RpcSendAudioToClient(conn, senderPeerId, recipientPeerId, bytes);
                }
            }
        }

        [TargetRpc]
        void RpcSendAudioToClient(NetworkConnection conn, short senderPeerId, short recipientPeerId, byte[] bytes, Channel channel = Channel.Unreliable) {
            if (recipientPeerId == OwnID) {
                var segment = FromByteArray<ChatroomAudioSegment>(bytes);
                OnAudioReceived?.Invoke(senderPeerId, segment);
            }
        }

        public void SendAudioSegment(short recipientPeerId, ChatroomAudioSegment data) {
            if (IsOffline) return;

            if (IsServerStarted) {
                var conn = GetNetworkConnectionFromPeerId(recipientPeerId);
                if (conn != null) {
                    RpcSendAudioToClient(conn, OwnID, recipientPeerId, ToByteArray(data));
                } else {
                    Debug.LogError("[VoiceChat]: Recipient network connection not found for PeerId: " + recipientPeerId);
                }
            } else if (IsClientStarted) {
                RpcSendAudioToServer(OwnID, recipientPeerId, ToByteArray(data));
            }

            OnAudioSent?.Invoke(recipientPeerId, data);
        }

        /// <summary>
        /// Returns the UniVoice peer Id corresponding to a previously
        /// registered Mirror connection Id
        /// </summary>
        /// <param name="connId">The connection Id to lookup</param>
        /// <returns>THe UniVoice Peer ID</returns>
        short GetPeerIdFromConnectionId(int connId) {
            foreach (var pair in clientMap) {
                if (pair.Value == connId)
                    return pair.Key;
            }
            return -1;
        }

        NetworkConnection GetNetworkConnectionFromPeerId(short peerId) {
            if (IsServerStarted) {
                if (InstanceFinder.ServerManager.Clients.TryGetValue(peerId, out var connection)) {
                    return connection;
                }
                return null;
            } else {
                if (InstanceFinder.ClientManager.Clients.TryGetValue(clientMap[peerId], out var connection)) {
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
            if (connId == 0) {
                // the server
                clientMap.Add(0, 0);
                PeerIDs.Add(0);
                return 0;
            }

            peerCount++;
            clientMap.Add(peerCount, connId);
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
            print("Dispose");
        }
    }
}