using System;
using System.Collections.Generic;
using System.Diagnostics;
using Code.Player;
using Mirror;
using Proyecto26;
using RSG;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace Code.Authentication {
    public struct LoginBroadcast : NetworkMessage {
        public string authToken;
    }
    public struct LoginResponseBroadcast : NetworkMessage
    {
        public bool passed;
    }

    public struct KickBroadcast : NetworkMessage {
        public string reason;
    }

    public class EasyAuthenticator : NetworkAuthenticator
    {
        private readonly string apiKey = "AIzaSyB04k_2lvM2VxcJqLKD6bfwdqelh6Juj2o";

        public int connectionCounter = 0;

        public override void OnStartServer() {
            base.OnStartServer();

            this.connectionCounter = 0;

            //Listen for broadcast from client. Be sure to set requireAuthentication to false.
            NetworkServer.RegisterHandler<LoginBroadcast>(OnPasswordBroadcast, false);
            //Listen to response from server.
            NetworkServer.RegisterHandler<LoginResponseBroadcast>(OnResponseBroadcast, false);
        }

        public override async void OnStartClient() {
            base.OnStartClient();

            NetworkClient.RegisterHandler<KickBroadcast>(OnKickBroadcast, false);

            string authToken = StateManager.GetString("firebase_idToken");

            if (Application.isEditor && CrossSceneState.IsLocalServer()) {
                authToken = "dummy";
            }

            if (authToken == null) {
                Debug.Log("StateManager is missing firebase_idToken. Refreshing...");
                var authSave = AuthManager.GetSavedAccount();
                if (authSave != null) {
                    var st = Stopwatch.StartNew();
                    var data = await AuthManager.LoginWithRefreshToken(this.apiKey, authSave.refreshToken);
                    if (data != null) {
                        Debug.Log("Login took " + st.ElapsedMilliseconds + " ms.");
                        authToken = data.id_token;
                        LoginBroadcast loginBroadcast = new LoginBroadcast {
                            authToken = authToken
                        };
                        NetworkClient.Send(loginBroadcast);
                        return;
                    }
                }
            }

            LoginBroadcast pb = new LoginBroadcast {
                authToken = authToken
            };
            NetworkClient.Send(pb);
        }

        private void OnKickBroadcast(KickBroadcast kickBroadcast) {
            TransferManager.Instance.Disconnect(true, kickBroadcast.reason);
        }

        /// <summary>
        /// Received on server when a client sends the password broadcast message.
        /// </summary>
        /// <param name="conn">Connection sending broadcast.</param>
        /// <param name="loginData"></param>
        private void OnPasswordBroadcast(NetworkConnectionToClient conn, LoginBroadcast loginData)
        {
            /* If client is already authenticated this could be an attack. Connections
             * are removed when a client disconnects so there is no reason they should
             * already be considered authenticated. */
            if (conn.isAuthenticated)
            {
                conn.Disconnect();
                return;
            }

            LoadUserData(loginData).Then(async (userData) => {
                var reserved = await PlayerManagerBridge.Instance.ValidateAgonesReservation(userData.uid);
                if (!reserved) throw new Exception("No reserved slot.");
                PlayerManagerBridge.Instance.AddUserData(conn.connectionId, userData);
                ServerAccept(conn);
            }).Catch((err) => {
                Debug.LogError(err);
                ServerReject(conn);
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
                        profileImageId = "",
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
                print("transfer: " + res.Text);
                string fullTransferPacket = res.Text;
                TransferData transferData = JsonUtility.FromJson<TransferData>(fullTransferPacket);
                return new UserData() {
                    uid = transferData.user.uid,
                    username = transferData.user.username,
                    profileImageId = transferData.user.profileImageId,
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
        private void OnResponseBroadcast(NetworkConnectionToClient conn, LoginResponseBroadcast rb)
        {
            if (!Application.isEditor) {
                string result = (rb.passed) ? "Authentication complete." : "Authentication failed.";
                // UnityEngine.Debug.Log(result);
            }

            if (!rb.passed) {
                CrossSceneState.kickMessage = "Kicked from server: Failed to authenticate.";
                SceneManager.LoadScene("Disconnected");
            }
        }
    }
}