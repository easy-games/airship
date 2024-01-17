using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.Scripting;

namespace Assets.Luau.Network {
    public struct NetBroadcast : IBroadcast {
        public BinaryBlob Blob;
    }

    [LuauAPI][Preserve]
    public class Net : MonoBehaviour {
	    private const bool RequireAuth = false;
	    
        public delegate void BroadcastFromServerAction(BinaryBlob blob);
        public event BroadcastFromServerAction broadcastFromServerAction;
		
        public delegate void BroadcastFromClientAction(object clientId, BinaryBlob blob);
        public event BroadcastFromClientAction broadcastFromClientAction;
        
	    private void OnEnable()
	    {
		    NetworkCore.SetNet(this);
		    if (RunCore.IsServer())
		    {
			    InstanceFinder.ServerManager.RegisterBroadcast<NetBroadcast>(OnBroadcastFromClient, RequireAuth);
			    InstanceFinder.ServerManager.Objects.OnPreDestroyClientObjects +=
				    ServerObjects_OnPreDestroyClientObjects;
		    }
		    if (RunCore.IsClient()) {
			    InstanceFinder.ClientManager.RegisterBroadcast<NetBroadcast>(OnBroadcastFromServer);
		    }
	    }

	    private void ServerObjects_OnPreDestroyClientObjects(NetworkConnection conn)
	    {
		    // Prevent despawning when a player leaves.
		    var clone = conn.Objects.ToArray();
		    foreach (NetworkObject nob in clone)
		    {
			    nob.RemoveOwnership();
		    }
	    }

	    private void OnDisable()
	    {
		    if (RunCore.IsServer()) {
			    InstanceFinder.ServerManager.UnregisterBroadcast<NetBroadcast>(OnBroadcastFromClient);
			    InstanceFinder.ServerManager.Objects.OnPreDestroyClientObjects -=
				    ServerObjects_OnPreDestroyClientObjects;
		    }
		    if (RunCore.IsClient()) {
				InstanceFinder.ClientManager.UnregisterBroadcast<NetBroadcast>(OnBroadcastFromServer);
		    }
	    }

	    private void OnBroadcastFromClient(NetworkConnection conn, NetBroadcast msg) {
			// Runs on the server, when the client broadcasts a message
			broadcastFromClientAction?.Invoke((object)conn.ClientId, msg.Blob);
		}

		private void OnBroadcastFromServer(NetBroadcast msg) {
			// Runs on the client, when the server broadcasts a message
			broadcastFromServerAction?.Invoke(msg.Blob);
		}

		public void BroadcastToAllClients(BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channel.Reliable : Channel.Unreliable;
			InstanceFinder.ServerManager.Broadcast(msg, RequireAuth, channel);
		}

		public void BroadcastToClient(int clientId, BinaryBlob blob, int reliable) {
			if (clientId < 0) return;
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channel.Reliable : Channel.Unreliable;
			InstanceFinder.ServerManager.Broadcast(InstanceFinder.ServerManager.Clients[clientId], msg, RequireAuth, channel);
		}

		public void BroadcastToClients(IEnumerable<int> clientIds, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
			HashSet<NetworkConnection> connections = new();
			foreach (var clientId in clientIds) {
				if (clientId < 0) continue;
				var connection = InstanceFinder.ServerManager.Clients[clientId];
				connections.Add(connection);
			}
			var channel = reliable == 1 ? Channel.Reliable : Channel.Unreliable;
			InstanceFinder.ServerManager.Broadcast(connections, msg, RequireAuth, channel);
		}

		public void BroadcastToAllExceptClient(int ignoredClientId, BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channel.Reliable : Channel.Unreliable;
			if (ignoredClientId > -1) {
				InstanceFinder.ServerManager.BroadcastExcept(InstanceFinder.ServerManager.Clients[ignoredClientId], msg, RequireAuth, channel);
			} else {
				InstanceFinder.ServerManager.Broadcast(msg, RequireAuth, channel);
			}
		}

		public void BroadcastToServer(BinaryBlob blob, int reliable) {
			var msg = new NetBroadcast { Blob = blob };
			var channel = reliable == 1 ? Channel.Reliable : Channel.Unreliable;
			InstanceFinder.ClientManager.Broadcast(msg, channel);
		}
    }
}
