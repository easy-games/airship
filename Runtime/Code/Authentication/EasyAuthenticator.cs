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
    public struct LoginMessage : NetworkMessage {
        public string authToken;
    }
    public struct LoginResponseMessage : NetworkMessage
    {
        public bool passed;
    }

    public struct KickMessage : NetworkMessage {
        public string reason;
    }

    public class EasyAuthenticator : NetworkAuthenticator {
        private readonly string apiKey = "AIzaSyB04k_2lvM2VxcJqLKD6bfwdqelh6Juj2o";

        public int connectionCounter = 0;

        public override void OnStartServer() {
            print("OnStartServer");

            this.connectionCounter = 0;

            //Listen for broadcast from client. Be sure to set requireAuthentication to false.
            NetworkServer.RegisterHandler<LoginMessage>(OnLogin, false);
        }

        public override async void OnStartClient() {
            print("OnStartClient");

            NetworkClient.RegisterHandler<KickMessage>(OnKickBroadcast, false);

            //Listen to response from server.
            NetworkServer.RegisterHandler<LoginResponseMessage>(OnResponseBroadcast, false);

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
                        LoginMessage loginMessage = new LoginMessage {
                            authToken = authToken
                        };
                        print("client.send 1");
                        NetworkClient.Send(loginMessage);
                        return;
                    }
                }
            }

            print("client.send 2");
            LoginMessage pb = new LoginMessage {
                authToken = authToken
            };
            NetworkClient.Send(pb);
        }

        private void OnKickBroadcast(KickMessage kickMessage) {
            TransferManager.Instance.Disconnect(true, kickMessage.reason);
        }

        /// <summary>
        /// Received on server when a client sends the password broadcast message.
        /// </summary>
        /// <param name="conn">Connection sending broadcast.</param>
        /// <param name="loginData"></param>
        private void OnLogin(NetworkConnectionToClient conn, LoginMessage loginData)
        {
            /* If client is already authenticated this could be an attack. Connections
             * are removed when a client disconnects so there is no reason they should
             * already be considered authenticated. */
            print("password.1");
            if (conn.isAuthenticated) {
                conn.Disconnect();
                return;
            }

            print("password.2");
            LoadUserData(loginData).Then(async (userData) => {
                print("password.3");
                var reserved = await PlayerManagerBridge.Instance.ValidateAgonesReservation(userData.uid);
                if (!reserved) throw new Exception("No reserved slot.");
                print("password.4");
                PlayerManagerBridge.Instance.AddUserData(conn.connectionId, userData);
                print("password.5");
                ServerAccept(conn);
                print("password.6");
            }).Catch((err) => {
                print("password reject");
                Debug.LogError(err);
                ServerReject(conn);
            });
        }

        private IPromise<UserData> LoadUserData(LoginMessage loginData) {
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
        private void OnResponseBroadcast(NetworkConnectionToClient conn, LoginResponseMessage rb)
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