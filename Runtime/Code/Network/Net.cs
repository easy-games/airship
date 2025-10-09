using System;
using System.Collections.Generic;
using Code.Luau;
using Mirror;
using UnityEngine;
using UnityEngine.Scripting;

namespace Assets.Luau.Network {
    public struct NetBroadcast : NetworkMessage {
	    /// <summary>
	    /// This is a bool to try to minimize data size. Could be swapped to a LuauContext if
	    /// needed in the future.
	    /// </summary>
	    public bool FromProtectedContext;
        public BinaryBlob Blob;
    }

    [LuauAPI][Preserve]
    public class Net : MonoBehaviour {
	    private const bool RequireAuth = false;
	    
        public delegate void BroadcastFromServerAction(object context, BinaryBlob blob);
        [AttachContext]
        public event BroadcastFromServerAction broadcastFromServerAction;
		
        public delegate void BroadcastFromClientAction(object context, object clientId, BinaryBlob blob);
        [AttachContext]
        public event BroadcastFromClientAction broadcastFromClientAction;

        private readonly Dictionary<int, ThrottleTrack> _throttle = new();

        // Client-to-server data throttle parameters:
        private const float ThrottleResetPeriod = 1f;
        private const ulong MaxBytesPerSecond = 1024 * 1024 * 50; // 50 MB
        private const ulong MaxBytesAtOnce = 1024 * 1024 * 5; // 5 MB
        
        private const ulong MaxBytesPerPeriod = (ulong)((double)MaxBytesPerSecond * (double)ThrottleResetPeriod);

        private void OnEnable() {
		    NetworkCore.SetNet(this);
        }

	    private void OnClientConnected(NetworkConnectionToClient conn) {
		    _throttle[conn.connectionId] = new ThrottleTrack();
	    }

	    private void OnClientDisconnected(NetworkConnectionToClient conn) {
		    _throttle.Remove(conn.connectionId);
	    }

	    public void OnStartServer() {
		    if (RunCore.IsServer()) {
			    NetworkServer.RegisterHandler<NetBroadcast>(OnBroadcastFromClient, RequireAuth);
			    NetworkServer.OnConnectedEvent += OnClientConnected;
			    NetworkServer.OnDisconnectedEvent += OnClientDisconnected;
		    }
	    }
	    
	    public void OnStartClient() {
		    if (RunCore.IsClient()) {
			    NetworkClient.RegisterHandler<NetBroadcast>(OnBroadcastFromServer, RequireAuth);
		    }
	    }

	    private void OnDisable() {
		    if (RunCore.IsServer()) {
			    NetworkServer.UnregisterHandler<NetBroadcast>();
			    NetworkServer.OnConnectedEvent -= OnClientConnected;
			    NetworkServer.OnDisconnectedEvent -= OnClientDisconnected;
		    }
		    if (RunCore.IsClient()) {
			    NetworkClient.UnregisterHandler<NetBroadcast>();
		    }
	    }

	    private void OnBroadcastFromClient(NetworkConnectionToClient conn, NetBroadcast msg) {
		    // Runs on the server, when the client broadcasts a message
		    if ((ulong)msg.Blob.uncompressedDataSize >= MaxBytesAtOnce) {
			    Debug.LogWarning($"Dropping message from client connection {conn.connectionId} due to exceeding max data size.");
			    return;
		    }
		    
		    var now = Time.realtimeSinceStartup;
		    if (!_throttle.TryGetValue(conn.connectionId, out var throttle)) {
			    // If we don't have a throttle key, that means the client isn't connected anymore and we are processing an old message.
			    print("hiu");
			    return;
		    }
		    
		    if (now >= throttle.nextClear) {
			    throttle.dataAmount = 0;
			    throttle.nextClear = now + ThrottleResetPeriod;
		    }

		    throttle.dataAmount += (ulong)msg.Blob.uncompressedDataSize;
		    if (throttle.dataAmount >= MaxBytesPerPeriod) {
			    Debug.LogWarning(
				    $"Disconnecting connection {conn.connectionId} because it's sending too much data. Data total {throttle.dataAmount} > {MaxBytesPerPeriod}");
			    conn.Disconnect();
			    return;
		    }

		    var targetContext = msg.FromProtectedContext ? LuauContext.Protected : LuauContext.Game;
		    broadcastFromClientAction?.Invoke((object) targetContext, (object)conn.connectionId, msg.Blob);
		}

		private void OnBroadcastFromServer(NetBroadcast msg) {
			// Runs on the client, when the server broadcasts a message
			var targetContext = msg.FromProtectedContext ? LuauContext.Protected : LuauContext.Game;
			broadcastFromServerAction?.Invoke(targetContext, msg.Blob);
		}

		[AttachContext]
		public void BroadcastToAllClients(LuauContext context, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { FromProtectedContext = context == LuauContext.Protected, Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			NetworkServer.SendToReady(msg, channel);
		}

		[AttachContext]
		public void BroadcastToClient(LuauContext context, int clientId, BinaryBlob blob, int reliable) {
			// if (clientId < 0) return;
			var msg = new NetBroadcast { FromProtectedContext = context == LuauContext.Protected, Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			if (NetworkServer.connections.TryGetValue(clientId, out var connection)) {
				if (!connection.isReady) return;
				connection.Send(msg, channel);
			} else {
				throw new Exception(
					"Tried to send send network packet to a client that isn't connected to the server. ClientId: " +
					clientId);
			}
		}

		[AttachContext]
		public void BroadcastToClients(LuauContext context, IEnumerable<int> clientIds, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { FromProtectedContext = context == LuauContext.Protected, Blob = blob };
			HashSet<NetworkConnection> connections = new();
			foreach (var clientId in clientIds) {
				// if (clientId < 0) continue;
				if (NetworkServer.connections.TryGetValue(clientId, out var connection)) {
					connections.Add(connection);
				} else {
					throw new Exception(
						"Tried to send send network packet to a client that isn't connected to the server. ClientId: " +
						clientId);
				}
			}
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			foreach (var connection in connections) {
				if (!connection.isReady) continue;
				connection.Send(msg, channel);
			}
		}

		[AttachContext]
		public void BroadcastToAllExceptClient(LuauContext context, int ignoredClientId, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { FromProtectedContext = context == LuauContext.Protected, Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			foreach (var connection in NetworkServer.connections.Values) {
				if (connection.connectionId != ignoredClientId) {
					if (!connection.isReady) continue;
					connection.Send(msg, channel);
				}
			}
		}

		[AttachContext]
		public void BroadcastToServer(LuauContext context, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { FromProtectedContext = context == LuauContext.Protected, Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			NetworkClient.Send(msg, channel);
		}
    }

    internal class ThrottleTrack {
	    internal ulong dataAmount;
	    internal float nextClear;
    }
}
