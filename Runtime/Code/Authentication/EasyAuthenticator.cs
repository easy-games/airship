using System;
using System.Collections.Generic;
using Code.Player;
using FishNet.Authenticating;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using Proyecto26;
using RSG;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Authentication {
public struct LoginBroadcast : IBroadcast {
    public string authToken;
}
public struct LoginResponseBroadcast : IBroadcast
{
    public bool passed;
}

    public class EasyAuthenticator : Authenticator
    {
        public override event Action<NetworkConnection, bool> OnAuthenticationResult;

        private readonly string apiKey = "AIzaSyB04k_2lvM2VxcJqLKD6bfwdqelh6Juj2o";

        public int connectionCounter = 0;

        public override void InitializeOnce(NetworkManager networkManager) {
            base.InitializeOnce(networkManager);
            this.connectionCounter = 0;

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
            if (args.ConnectionState != LocalConnectionState.Started) {
                return;
            }

            string authToken = StateManager.GetString("firebase_idToken");

            if (Application.isEditor && CrossSceneState.IsLocalServer()) {
                authToken = "dummy";
            }

            if (authToken == null) {
                Debug.Log("StateManager is missing firebase_idToken. Refreshing...");
                var authSave = AuthManager.GetSavedAccount();
                if (authSave != null) {
                    AuthManager.LoginWithRefreshToken(this.apiKey, authSave.refreshToken).Then((data) => {
                        authToken = data.id_token;
                        LoginBroadcast pb = new LoginBroadcast {
                            authToken = authToken
                        };
                        base.NetworkManager.ClientManager.Broadcast(pb);
                    }).Catch(Debug.LogError);
                    return;
                }
            }

            LoginBroadcast pb = new LoginBroadcast {
                authToken = authToken
            };
            base.NetworkManager.ClientManager.Broadcast(pb);
        }


        /// <summary>
        /// Received on server when a client sends the password broadcast message.
        /// </summary>
        /// <param name="conn">Connection sending broadcast.</param>
        /// <param name="loginData"></param>
        private void OnPasswordBroadcast(NetworkConnection conn, LoginBroadcast loginData, Channel channel)
        {
            /* If client is already authenticated this could be an attack. Connections
             * are removed when a client disconnects so there is no reason they should
             * already be considered authenticated. */
            if (conn.Authenticated)
            {
                conn.Disconnect(true);
                return;
            }

            LoadUserData(loginData).Then((userData) => {
                PlayerManagerBridge.Instance.AddUserData(conn.ClientId, userData);
                SendAuthenticationResponse(conn, true);
                /* Invoke result. This is handled internally to complete the connection or kick client.
                 * It's important to call this after sending the broadcast so that the broadcast
                 * makes it out to the client before the kick. */
                OnAuthenticationResult?.Invoke(conn, true);
            }).Catch((err) => {
                Debug.LogError(err);
                SendAuthenticationResponse(conn, false);
                OnAuthenticationResult?.Invoke(conn, false);
            });
        }

        private IPromise<UserData> LoadUserData(LoginBroadcast loginData) {
            if (Application.isEditor && CrossSceneState.IsLocalServer()) {
                this.connectionCounter++;
                var promise = new Promise<UserData>();
                promise.Resolve(
                    new UserData() {
                        uid = this.connectionCounter + "",
                        username = "Player" + this.connectionCounter,
                        discriminator = "0000",
                        discriminatedUsername = "Player" + this.connectionCounter + "#0000",
                        fullTransferPacket = "{}"
                    }
                );
                return promise;
            }

            var serverBootstrap = GameObject.FindAnyObjectByType<ServerBootstrap>();
            return RestClient.Post(new RequestHelper {
                Uri = AirshipApp.gameCoordinatorUrl + "/transfers/transfer/validate",
                BodyString = "{\"userIdToken\": \"" + loginData.authToken + "\"}",
                Headers = new Dictionary<string, string>() {
                    { "Authorization", "Bearer " + serverBootstrap.airshipJWT}
                }
            }).Then((res) => {
                string fullTransferPacket = res.Text;
                TransferData transferData = JsonUtility.FromJson<TransferData>(fullTransferPacket);
                return new UserData() {
                    uid = transferData.user.uid,
                    username = transferData.user.username,
                    discriminator = transferData.user.discriminator,
                    discriminatedUsername = transferData.user.discriminatedUsername,
                    fullTransferPacket = fullTransferPacket
                };
            }).Catch((err) => {
                var error = err as RequestException;
                Debug.LogError("Failed transfer validation:");
                Debug.LogError(error?.Response);
                throw err;
            });
        }

        /// <summary>
        /// Received on client after server sends an authentication response.
        /// </summary>
        /// <param name="rb"></param>
        private void OnResponseBroadcast(LoginResponseBroadcast rb, Channel channel)
        {
            if (!Application.isEditor) {
                string result = (rb.passed) ? "Authentication complete." : "Authentication failed.";
                NetworkManager.Log(result);
            }
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
                passed = authenticated
            };
            base.NetworkManager.ServerManager.Broadcast(conn, rb, false);
        }
    }
}