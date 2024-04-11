using System;
using FishNet.Connection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        // begin easy.gg
        /****** NETWORK ******/
        public event Action OnStartNetwork;
        public event Action OnStopNetwork;

        /****** SERVER ******/
        public event Action OnStartServer;
        /** Params: NetworkConnection */
        public event Action<object> OnOwnershipServer;
        public event Action OnStopServer;
        /** Params: NetworkConnection */
        public event Action<object> OnSpawnServer;
        /** Params: NetworkConnection */
        public event Action<object> OnDespawnServer;

        /****** CLIENT ******/
        public event Action OnStartClient;
        /** Params: NetworkConnection */
        public event Action<object> OnOwnershipClient;
        public event Action OnStopClient;
        // end easy.gg


        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
        {
            /* Note: When invoking OnOwnership here previous owner will
             * always be an empty connection, since the object is just
             * now initializing. */

            //Invoke OnStartNetwork.
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeOnNetwork(true);
            this.OnStartNetwork?.Invoke();

            //As server.
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartServer_Internal();
                this.OnStartServer?.Invoke();

                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
                this.OnOwnershipServer?.Invoke(FishNet.Managing.NetworkManager.EmptyConnection);
            }
            //As client.
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartClient_Internal();
                this.OnStartClient?.Invoke();

                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipClient_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
                this.OnOwnershipClient?.Invoke(FishNet.Managing.NetworkManager.EmptyConnection);
            }

            if (invokeSyncTypeCallbacks)
                InvokeOnStartSyncTypeCallbacks(true);
        }


        /// <summary>
        /// Invokes OnStartXXXX for synctypes, letting them know the NetworkBehaviour start cycle has been completed.
        /// </summary>
        internal void InvokeOnStartSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStartCallbacks(asServer);

            if (asServer) {
                OnStartServer?.Invoke();
            } else {
                OnStartClient?.Invoke();
            }
        }

        /// <summary>
        /// Invokes OnStopXXXX for synctypes, letting them know the NetworkBehaviour stop cycle is about to start.
        /// </summary>
        internal void InvokeOnStopSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStopCallbacks(asServer);

            if (asServer) {
                OnStopServer?.Invoke();
            } else {
                OnStopClient?.Invoke();
            }
        }

        /// <summary>
        /// Invokes events to be called after OnServerStart.
        /// This is made one method to save instruction calls.
        /// </summary>
        /// <param name=""></param>
        internal void OnSpawnServerInternal(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].SendBufferedRpcs(conn);

            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].OnSpawnServer(conn);
            this.OnSpawnServer?.Invoke(conn);
        }

        /// <summary>
        /// Called on the server before it sends a despawn message to a client.
        /// </summary>
        /// <param name="conn">Connection spawn was sent to.</param>
        internal void InvokeOnServerDespawn(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].OnDespawnServer(conn);
            this.OnDespawnServer?.Invoke(conn);
        }

        /// <summary>
        /// Invokes OnStop callbacks.
        /// </summary>
        /// <param name="asServer"></param>
        internal void InvokeStopCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStopCallbacks(asServer);

            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopServer_Internal();
                this.OnStopServer?.Invoke();
            }
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopClient_Internal();
                this.OnStopClient?.Invoke();
            }

            /* Several conditions determine if OnStopNetwork can
             * be called.
             * 
             * - If asServer and pending destroy from clientHost.
             * - If !asServer and not ServerInitialized. */
            bool callStopNetwork;
            if (asServer)
            {
                if (!IsClientStarted)
                    callStopNetwork = true;
                else
                    callStopNetwork = (ServerManager.Objects.GetFromPending(ObjectId) == null);
            }
            else
            {
                /* When not as server only perform OnStopNetwork if
                 * not initialized for the server. The object could be
                 * server initialized if it were spawned, despawned, then spawned again
                 * before client ran this method. */
                callStopNetwork = !IsServerInitialized;
            }
            if (callStopNetwork)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].InvokeOnNetwork(false);
                this.OnStopNetwork?.Invoke();
            }
        }

        /// <summary>
        /// Invokes OnOwnership callbacks when ownership changes.
        /// This is not to be called when assigning ownership during a spawn message.
        /// </summary>
        private void InvokeOwnershipChange(NetworkConnection prevOwner, bool asServer)
        {
            if (asServer)
            {
#if !PREDICTION_1
                ResetReplicateTick();
#endif
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(prevOwner);
                this.OnOwnershipServer?.Invoke(prevOwner);
                //Also write owner syncTypes if there is an owner.
                if (Owner.IsValid)
                {
                    for (int i = 0; i < NetworkBehaviours.Length; i++)
                        NetworkBehaviours[i].WriteDirtySyncTypes(true, true, true);
                }
            }
            else
            {
                /* If local client is owner and not server then only
                 * invoke if the prevOwner is different. This prevents
                 * the owner change callback from happening twice when
                 * using TakeOwnership. 
                 * 
                 * Further explained, the TakeOwnership sets local client
                 * as owner client-side, which invokes the OnOwnership method.
                 * Then when the server approves the owner change it would invoke
                 * again, which is not needed. */
                bool blockInvoke = ((IsOwner && !IsServerStarted) && (prevOwner == Owner));
                if (!blockInvoke)
                {
                    for (int i = 0; i < NetworkBehaviours.Length; i++)
                        NetworkBehaviours[i].OnOwnershipClient_Internal(prevOwner);
                    this.OnOwnershipClient?.Invoke(prevOwner);
                }
            }
        }
    }

}

