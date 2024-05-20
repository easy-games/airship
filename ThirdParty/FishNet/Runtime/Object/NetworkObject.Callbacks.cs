using System;
﻿using FishNet.Connection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FishNet.Object
{
    public partial class NetworkObject : MonoBehaviour
    {
        // Begin Airship: Interanl event all
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
        // End Airship

        #region Private.
        /// <summary>
        /// True if OnStartServer was called.
        /// </summary>
        private bool _onStartServerCalled;
        /// <summary>
        /// True if OnStartClient was called.
        /// </summary>
        private bool _onStartClientCalled;
        #endregion

        /// <summary>
        /// Called after all data is synchronized with this NetworkObject.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeStartCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
        {
            /* Note: When invoking OnOwnership here previous owner will
             * always be an empty connection, since the object is just
             * now initializing. */

            //Invoke OnStartNetwork.
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeOnNetwork(true);
            // Begin Airship: Event Call
            this.OnStartNetwork?.Invoke();
            // End Airship

            //As server.
            if (asServer)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartServer_Internal();
                // Begin Airship: Event Call
                this.OnStartServer?.Invoke();
                // End Airship

                _onStartServerCalled = true;
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipServer_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
                // Begin Airship: Event Call
                this.OnOwnershipServer?.Invoke(FishNet.Managing.NetworkManager.EmptyConnection);
                // End Airship
            }
            //As client.
            else
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStartClient_Internal();
                // Begin Airship: Event Call
                this.OnStartClient?.Invoke();
                // End Airship

                _onStartClientCalled = true;
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnOwnershipClient_Internal(FishNet.Managing.NetworkManager.EmptyConnection);
                // Begin Airship: Event Call
                this.OnOwnershipClient?.Invoke(FishNet.Managing.NetworkManager.EmptyConnection);
                // End Airship
            }

            if (invokeSyncTypeCallbacks)
                InvokeOnStartSyncTypeCallbacks(true);

#if !PREDICTION_1
            InvokeStartCallbacks_Prediction(asServer);
#endif
        }


        /// <summary>
        /// Invokes OnStartXXXX for synctypes, letting them know the NetworkBehaviour start cycle has been completed.
        /// </summary>
        internal void InvokeOnStartSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStartCallbacks(asServer);

            // Begin Airship: Event Call
            if (asServer) {
                OnStartServer?.Invoke();
            } else {
                OnStartClient?.Invoke();
            }
            // End Airship
        }

        /// <summary>
        /// Invokes OnStopXXXX for synctypes, letting them know the NetworkBehaviour stop cycle is about to start.
        /// </summary>
        internal void InvokeOnStopSyncTypeCallbacks(bool asServer)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].InvokeSyncTypeOnStopCallbacks(asServer);

            // Begin Airship: Event Call
            if (asServer) {
                OnStopServer?.Invoke();
            } else {
                OnStopClient?.Invoke();
            }
            // End Airship
        }

        // Begin Airship: Custom internal function call so we can fire an event
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
        //End Airship

        /// <summary>
        /// Called on the server before it sends a despawn message to a client.
        /// </summary>
        /// <param name="conn">Connection spawn was sent to.</param>
        internal void InvokeOnServerDespawn(NetworkConnection conn)
        {
            for (int i = 0; i < NetworkBehaviours.Length; i++)
                NetworkBehaviours[i].OnDespawnServer(conn);
            // Begin Airship: Event Call
            this.OnDespawnServer?.Invoke(conn);
            // End Airship
        }

        /// <summary>
        /// Invokes OnStop callbacks.
        /// </summary>
        /// <param name="asServer"></param>
        internal void InvokeStopCallbacks(bool asServer, bool invokeSyncTypeCallbacks)
        {
#if !PREDICTION_1
            InvokeStopCallbacks_Prediction(asServer);
#endif
            if (invokeSyncTypeCallbacks)
                InvokeOnStopSyncTypeCallbacks(asServer);

            bool invokeOnNetwork = (!asServer || (asServer && !_onStartClientCalled));
            if (asServer && _onStartServerCalled)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopServer_Internal();
                    
                // Begin Airship: Event Call
                this.OnStopServer?.Invoke();
                // End Airship

                _onStartServerCalled = false;
            }
            else if (!asServer && _onStartClientCalled)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].OnStopClient_Internal();
                // Begin Airship: Event Call
                this.OnStopClient?.Invoke();
                // End Airship
                _onStartClientCalled = false;
            }

            if (invokeOnNetwork)
            {
                for (int i = 0; i < NetworkBehaviours.Length; i++)
                    NetworkBehaviours[i].InvokeOnNetwork(false);
                // Begin Airship: Event Call
                this.OnStopNetwork?.Invoke();
                // End Airship
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
                // Begin Airship: Event Call
                this.OnOwnershipServer?.Invoke(prevOwner);
                // End Airship
                
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
                    // Begin Airship: Event Call
                    this.OnOwnershipClient?.Invoke(prevOwner);
                    // End Airship
                }
            }
        }
    }

}

