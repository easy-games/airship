using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Code.Http.Internal;
using Code.Platform.Shared;
using Code.Player;
using Mirror;
using Proyecto26;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Code.Authentication {
    public struct LoginMessage : NetworkMessage {
        public string authToken;
        public int playerVersion;

        public string editorUserId;
        public string editorUsername;
        public string editorProfileImageId;
    }
    public struct LoginResponseMessage : NetworkMessage
    {
        public bool passed;
        public string disconnectMessage;
        public bool updateRequried;
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
#if UNITY_EDITOR
                if (EditorAuthManager.localUser != null) {
                    NetworkClient.Send(new LoginMessage {
                        authToken = "dummy",
                        playerVersion = AirshipConst.playerVersion,
                        editorUserId = EditorAuthManager.localUser.uid,
                        editorUsername = EditorAuthManager.localUser.username,
                        editorProfileImageId = EditorAuthManager.localUser.profileImageId,
                    });
                    return;
                }
#endif
                authToken = "dummy";
            }

            if (authToken == null) {
                Debug.Log("[Authenticator] StateManager is missing firebase_idToken. Refreshing...");
                var authSave = AuthManager.GetSavedAccount();
                if (authSave != null) {
                    var st = Stopwatch.StartNew();
                    var data = await AuthManager.LoginWithRefreshToken(authSave.refreshToken);
                    if (data != null) {
                        Debug.Log("[Authenticator] Fetched auth token in " + st.ElapsedMilliseconds + " ms.");
                        authToken = data.id_token;
                        NetworkClient.Send(new LoginMessage {
                            authToken = authToken,
                            playerVersion = AirshipConst.playerVersion,
                        });
                        return;
                    }
                }
            }

            NetworkClient.Send(new LoginMessage {
                authToken = authToken,
                playerVersion = AirshipConst.playerVersion,
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
        private async void Server_OnLoginMessage(NetworkConnectionToClient conn, LoginMessage loginData) {
            if (connectionsPendingDisconnect.Contains(conn)) return;

#if UNITY_SERVER
            Debug.Log("Authenticating " + conn);
#endif
            try {
                var userData = await LoadUserData(loginData);
                var reserved = await PlayerManagerBridge.Instance.ValidateAgonesReservation(userData.uid);
                if (!reserved) {
                    Debug.LogError("No reserved agones slot for player " + userData.username);
                    this.RejectConnection(conn, "Failed to authenticate.", false);
                    return;
                }

                if (loginData.playerVersion < AirshipConst.minAcceptedPlayerVersionOnServer) {
                    this.RejectConnection(conn, "Please update your Airship App. Your version is outdated.",  true);
                    return;
                }

                PlayerManagerBridge.Instance.AddUserData(conn.connectionId, userData);
                conn.Send(new LoginResponseMessage() {
                    passed = true,
                });
                ServerAccept(conn);
            } catch (Exception err) {
                Debug.LogError(err);
                this.RejectConnection(conn, "An error occured while authenticating.", false);
            }
        }

        private void RejectConnection(NetworkConnectionToClient conn, string message, bool updateRequired) {
            this.connectionsPendingDisconnect.Add(conn);
            conn.Send(new LoginResponseMessage() {
                passed = false,
                disconnectMessage = message,
                updateRequried = updateRequired,
            });
            StartCoroutine(DelayedDisconnect(conn, 1));
        }

        IEnumerator DelayedDisconnect(NetworkConnectionToClient conn, float waitTime) {
            yield return new WaitForSeconds(waitTime);

            // Reject the unsuccessful authentication
            ServerReject(conn);

            yield return null;

            // remove conn from pending connections
            connectionsPendingDisconnect.Remove(conn);
        }

        private async Task<UserData> LoadUserData(LoginMessage loginMessage) {
            var tcs = new TaskCompletionSource<UserData>();

            if (Application.isEditor && CrossSceneState.IsLocalServer()) {
                this.connectionCounter++;
                if (this.connectionCounter == 1 && loginMessage.editorUserId != null) {
                    tcs.SetResult(new UserData() {
                        uid = InternalHttpManager.editorUserId,
                        username = loginMessage.editorUsername,
                        profileImageId = loginMessage.editorProfileImageId
                    });
                    return await tcs.Task;
                }
                tcs.SetResult(new UserData() {
                    uid = this.connectionCounter + "",
                    username = "Player" + this.connectionCounter,
                    profileImageId = "",
                    fullTransferPacket = "{}"
                });
                return await tcs.Task;
            }

            var serverBootstrap = GameObject.FindAnyObjectByType<ServerBootstrap>();

            if (!serverBootstrap.allocatedByAgones) {
                Debug.Log("Tried to authenticate before server was allocated. Checking allocation now...");
                var gameServer = await serverBootstrap.agones.GameServer();
                serverBootstrap.OnGameServerChange(gameServer);
            }

            RestClient.Post(UnityWebRequestProxyHelper.ApplyProxySettings(new RequestHelper {
                Uri = AirshipPlatformUrl.gameCoordinator + "/transfers/transfer/validate",
                BodyString = "{\"userIdToken\": \"" + loginMessage.authToken + "\"}",
                Headers = new Dictionary<string, string>() {
                    { "Authorization", "Bearer " + serverBootstrap.airshipJWT}
                }

            })).Then((res) => {
                // print($"[Transfer Packet] userIdToken: {userIdToken}, packet response: " + res.Text);
                string fullTransferPacket = res.Text;
                TransferData transferData = JsonUtility.FromJson<TransferData>(fullTransferPacket);
                tcs.SetResult(new UserData() {
                    uid = transferData.user.uid,
                    username = transferData.user.username,
                    profileImageId = transferData.user.profileImageId,
                    fullTransferPacket = fullTransferPacket
                });
            }).Catch((err) => {
                var error = err as RequestException;
                Debug.LogError("Failed transfer validation:");
                Debug.LogError(error?.Response);
                throw err;
            });

            return await tcs.Task;
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
                if (!string.IsNullOrEmpty(rb.disconnectMessage)) {
                    CrossSceneState.kickMessage = rb.disconnectMessage;
                } else {
                    CrossSceneState.kickMessage = "Kicked from server: Failed to authenticate.";
                }
                ClientReject();
                SceneManager.LoadScene("Disconnected");
                return;
            }
            ClientAccept();
        }
    }
}