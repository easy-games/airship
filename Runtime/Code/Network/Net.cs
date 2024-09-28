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
        
	    private void OnEnable() {
		    NetworkCore.SetNet(this);
	    }

	    public void OnStartServer() {
		    if (RunCore.IsServer()) {
			    NetworkServer.RegisterHandler<NetBroadcast>(OnBroadcastFromClient, RequireAuth);
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
		    }
		    if (RunCore.IsClient()) {
			    NetworkClient.UnregisterHandler<NetBroadcast>();
		    }
	    }

	    private void OnBroadcastFromClient(NetworkConnectionToClient conn, NetBroadcast msg) {
		    // Runs on the server, when the client broadcasts a message
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
			var connection = NetworkServer.connections[clientId];
			if (!connection.isReady) return;
			connection.Send(msg, channel);
		}

		[AttachContext]
		public void BroadcastToClients(LuauContext context, IEnumerable<int> clientIds, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { FromProtectedContext = context == LuauContext.Protected, Blob = blob };
			HashSet<NetworkConnection> connections = new();
			foreach (var clientId in clientIds) {
				// if (clientId < 0) continue;
				var connection = NetworkServer.connections[clientId];
				connections.Add(connection);
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
}
