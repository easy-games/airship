using System;
using FishNet;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

public struct LoginBroadcast : IBroadcast
{
    public string Username;
}

public struct LoginResponseBroadcast : IBroadcast
{
    public bool Passed;
}

public class EasyAuthenticator : Authenticator
{
    public override event Action<NetworkConnection, bool> OnAuthenticationResult;
    private int _playersLoadedCounter = 0;
    private PlayerCore _playerCore;

    private void Awake()
    {
        _playerCore = GameObject.Find("Players").GetComponent<PlayerCore>();
    }

    public override void InitializeOnce(NetworkManager networkManager)
        {
            base.InitializeOnce(networkManager);

            //Listen for connection state change as client.
            base.NetworkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            //Listen for broadcast from client. Be sure to set requireAuthentication to false.
            base.NetworkManager.ServerManager.RegisterBroadcast<LoginBroadcast>(OnPasswordBroadcast, false);
            //Listen to response from server.
            base.NetworkManager.ClientManager.RegisterBroadcast<LoginResponseBroadcast>(OnResponseBroadcast);
        }

        /// <summary>
        /// Called when a connection state changes for the local client.
        /// </summary>
        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs args)
        {
            /* If anything but the started state then exit early.
             * Only try to authenticate on started state. The server
            * doesn't have to send an authentication request before client
            * can authenticate, that is entirely optional and up to you. In this
            * example the client tries to authenticate soon as they connect. */
            if (args.ConnectionState != LocalConnectionState.Started)
                return;

            LoginBroadcast pb = new LoginBroadcast()
            {
                Username = CrossSceneState.Username
            };

            base.NetworkManager.ClientManager.Broadcast(pb);
        }


        /// <summary>
        /// Received on server when a client sends the password broadcast message.
        /// </summary>
        /// <param name="conn">Connection sending broadcast.</param>
        /// <param name="loginData"></param>
        private void OnPasswordBroadcast(NetworkConnection conn, LoginBroadcast loginData)
        {
            /* If client is already authenticated this could be an attack. Connections
             * are removed when a client disconnects so there is no reason they should
             * already be considered authenticated. */
            if (conn.Authenticated)
            {
                conn.Disconnect(true);
                return;
            }

            var userData = LoadUserData(loginData);
            if (userData == null)
            {
                SendAuthenticationResponse(conn, false);
                OnAuthenticationResult?.Invoke(conn, false);
                return;
            }

            _playerCore.AddUserData(conn.ClientId, userData);
            SendAuthenticationResponse(conn, true);
            /* Invoke result. This is handled internally to complete the connection or kick client.
             * It's important to call this after sending the broadcast so that the broadcast
             * makes it out to the client before the kick. */
            OnAuthenticationResult?.Invoke(conn, true);
        }

        private UserData LoadUserData(LoginBroadcast loginData)
        {
            _playersLoadedCounter++;
            return new UserData()
            {
                Username = loginData.Username,
                UsernameTag = "easy",
                UserId = _playersLoadedCounter + "",
            };
        }

        /// <summary>
        /// Received on client after server sends an authentication response.
        /// </summary>
        /// <param name="rb"></param>
        private void OnResponseBroadcast(LoginResponseBroadcast rb)
        {
            string result = (rb.Passed) ? "Authentication complete." : "Authentication failed.";
            NetworkManager.Log(result);
        }

        /// <summary>
        /// Sends an authentication result to a connection.
        /// </summary>
        private void SendAuthenticationResponse(NetworkConnection conn, bool authenticated)
        {
            /* Tell client if they authenticated or not. This is
            * entirely optional but does demonstrate that you can send
            * broadcasts to client on pass or fail. */
            LoginResponseBroadcast rb = new LoginResponseBroadcast()
            {
                Passed = authenticated
            };
            base.NetworkManager.ServerManager.Broadcast(conn, rb, false);
        }
}