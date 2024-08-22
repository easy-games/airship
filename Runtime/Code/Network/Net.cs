using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Scripting;

namespace Assets.Luau.Network {
    public struct NetBroadcast : NetworkMessage {
        public BinaryBlob Blob;
    }

    [LuauAPI][Preserve]
    public class Net : MonoBehaviour {
	    private const bool RequireAuth = false;
	    
        public delegate void BroadcastFromServerAction(BinaryBlob blob);
        public event BroadcastFromServerAction broadcastFromServerAction;
		
        public delegate void BroadcastFromClientAction(object clientId, BinaryBlob blob);
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

	    // private void ServerObjects_OnPreDestroyClientObjects(NetworkConnection conn)
	    // {
		   //  // Prevent despawning when a player leaves.
		   //  var clone = conn.Objects.ToArray();
		   //  foreach (NetworkObject nob in clone)
		   //  {
			  //   nob.RemoveOwnership();
		   //  }
	    // }

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
			broadcastFromClientAction?.Invoke((object)conn.connectionId, msg.Blob);
		}

		private void OnBroadcastFromServer(NetBroadcast msg) {
			// Runs on the client, when the server broadcasts a message
			broadcastFromServerAction?.Invoke(msg.Blob);
		}

		public void BroadcastToAllClients(BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			NetworkServer.SendToReady(msg, channel);
		}

		public void BroadcastToClient(int clientId, BinaryBlob blob, int reliable) {
			// if (clientId < 0) return;
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			var connection = NetworkServer.connections[clientId];
			if (!connection.isReady) return;
			connection.Send(msg, channel);
		}

		public void BroadcastToClients(IEnumerable<int> clientIds, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
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

		public void BroadcastToAllExceptClient(int ignoredClientId, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			foreach (var connection in NetworkServer.connections.Values) {
				if (connection.connectionId != ignoredClientId) {
					if (!connection.isReady) continue;
					connection.Send(msg, channel);
				}
			}
		}

		public void BroadcastToServer(BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channels.Reliable : Channels.Unreliable;
			NetworkClient.Send(msg, channel);
		}
    }
}
