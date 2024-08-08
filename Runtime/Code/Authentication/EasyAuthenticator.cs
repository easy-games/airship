using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Code.Platform.Shared;
using Code.Player;
using Mirror;
using Proyecto26;
using RSG;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        readonly HashSet<NetworkConnection> connectionsPendingDisconnect = new HashSet<NetworkConnection>();

        public int connectionCounter = 0;

        public override void OnStartServer() {
            this.connectionCounter = 0;

            //Listen for broadcast from client. Be sure to set requireAuthentication to false.
            NetworkServer.RegisterHandler<LoginMessage>(Server_OnLoginMessage, false);
        }

        public override async void OnStartClient() {
            NetworkClient.RegisterHandler<KickMessage>(Client_OnKickBroadcast, false);

            //Listen to response from server.
            NetworkClient.RegisterHandler<LoginResponseMessage>(Client_OnLoginResponseMessage, false);
        }

        public override void OnStopClient() {
            NetworkClient.UnregisterHandler<KickMessage>();
            NetworkClient.UnregisterHandler<LoginResponseMessage>();
        }

        public override void OnStopServer() {
            NetworkServer.UnregisterHandler<LoginMessage>();
        }

        public override async void OnClientAuthenticate() {
            string authToken = StateManager.GetString("firebase_idToken");

            if (Application.isEditor && CrossSceneState.IsLocalServer()) {
                authToken = "dummy";
            }

            if (authToken == null) {
                Debug.Log("[Authenticator] StateManager is missing firebase_idToken. Refreshing...");
                var authSave = AuthManager.GetSavedAccount();
                if (authSave != null) {
                    var st = Stopwatch.StartNew();
                    var data = await AuthManager.LoginWithRefreshToken(AirshipPlatformUrl.firebaseApiKey, authSave.refreshToken);
                    if (data != null) {
                        Debug.Log("[Authenticator] Fetched auth token in " + st.ElapsedMilliseconds + " ms.");
                        authToken = data.id_token;
                        NetworkClient.Send(new LoginMessage {
                            authToken = authToken
                        });
                        return;
                    }
                }
            }

            NetworkClient.Send(new LoginMessage {
                authToken = authToken
            });
        }

        private void Client_OnKickBroadcast(KickMessage kickMessage) {
            TransferManager.Instance.Disconnect(true, kickMessage.reason);
        }

        /// <summary>
        /// Received on server when a client sends the password broadcast message.
        /// </summary>
        /// <param name="conn">Connection sending broadcast.</param>
        /// <param name="loginData"></param>
        private void Server_OnLoginMessage(NetworkConnectionToClient conn, LoginMessage loginData) {
            if (connectionsPendingDisconnect.Contains(conn)) return;

            LoadUserData(loginData).Then(async (userData) => {
                var reserved = await PlayerManagerBridge.Instance.ValidateAgonesReservation(userData.uid);
                if (!reserved) throw new Exception("No reserved slot.");
                PlayerManagerBridge.Instance.AddUserData(conn.connectionId, userData);

                conn.Send(new LoginResponseMessage() {
                    passed = true,
                });

                ServerAccept(conn);
            }).Catch((err) => {
                Debug.LogError(err);
                connectionsPendingDisconnect.Add(conn);
                conn.Send(new LoginResponseMessage() {
                    passed = false,
                });
                StartCoroutine(DelayedDisconnect(conn, 1));
            });
        }

        IEnumerator DelayedDisconnect(NetworkConnectionToClient conn, float waitTime) {
            yield return new WaitForSeconds(waitTime);

            // Reject the unsuccessful authentication
            ServerReject(conn);

            yield return null;

            // remove conn from pending connections
            connectionsPendingDisconnect.Remove(conn);
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
            return RestClient.Post(UnityWebRequestProxyHelper.ApplyProxySettings(new RequestHelper {
                Uri = AirshipPlatformUrl.gameCoordinator + "/transfers/transfer/validate",
                BodyString = "{\"userIdToken\": \"" + loginData.authToken + "\"}",
                Headers = new Dictionary<string, string>() {
                    { "Authorization", "Bearer " + serverBootstrap.airshipJWT}
                }

            })).Then((res) => {
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
        private void Client_OnLoginResponseMessage(LoginResponseMessage rb)
        {
            if (!Application.isEditor) {
                string result = (rb.passed) ? "Authentication complete." : "Authentication failed.";
                // UnityEngine.Debug.Log(result);
            }

            if (!rb.passed) {
                CrossSceneState.kickMessage = "Kicked from server: Failed to authenticate.";
                ClientReject();
                SceneManager.LoadScene("Disconnected");
                return;
            }
            ClientAccept();
        }
    }
}