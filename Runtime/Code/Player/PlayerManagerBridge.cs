using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agones;
using Airship.DevConsole;
using JetBrains.Annotations;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Player {
    [LuauAPI]
    public class PlayerManagerBridge : Singleton<PlayerManagerBridge> {
        public delegate void PlayerChangedDelegate(PlayerInfoDto playerInfo, object entered);

        public delegate void PlayerRemovingDelegate(PlayerInfoDto playerInfo);

        private static readonly string AGONES_PLAYERS_LIST_NAME = "players";

        private static readonly string AGONES_RESERVATIONS_LIST_NAME = "reservations";

        // To implement max players, we fill in fake reservations for slots we never want to fill. This is the prefix for the fake reservations we should ignore.
        private static readonly string AGONES_RESERVATION_FILL_PREFIX = "::";
        private static readonly double MAX_RESERVATION_TIME_SEC = 60 * 5;

        [Tooltip("Prefab to spawn for the player.")] [SerializeField]
        private GameObject playerPrefab;

        public PlayerInfo localPlayer;
        public bool localPlayerReady;

        public List<PlayerInfo> players = new();

        [SerializeField] public AgonesBetaSdk agones;

        private readonly Dictionary<int, UserData> _userData = new();

        private readonly Dictionary<string, DateTime> agonesReservationMap = new();

        private int botPlayerIdCounter = 1;

        private readonly Dictionary<int, NetworkIdentity> connectionIdToPlayerNetId = new();

        private Scene coreScene;

        private ServerBootstrap serverBootstrap;

        private void Awake() {
            coreScene = SceneManager.GetSceneByName("CoreScene");
            serverBootstrap = FindFirstObjectByType<ServerBootstrap>();
        }

        private async void Start() {
            // print("PlayerManagerBridge.start");

            // print("PlayerManagerBridge: server is ready.");
            if (RunCore.IsServer() && serverBootstrap) {
                while (!serverBootstrap.isServerReady) await Awaitable.NextFrameAsync();
                foreach (var connection in NetworkServer.connections.Values) NetworkServer_OnConnected(connection);

                NetworkServer.OnConnectedEvent += NetworkServer_OnConnected;
                NetworkServer.OnDisconnectedEvent += NetworkServer_OnDisconnected;

                if (serverBootstrap && serverBootstrap.IsAgonesEnvironment()) {
                    if (agones) {
                        agones.WatchGameServer(async gs => {
                            var reservedList = await agones.GetListValues(AGONES_RESERVATIONS_LIST_NAME);
                            reservedList.ForEach(reservation => {
                                if (reservation.StartsWith(AGONES_RESERVATION_FILL_PREFIX)) return;
                                agonesReservationMap.TryAdd(reservation, DateTime.Now);
                            });
                        });

                        CleanAgonesReservationMap();
                        UpdateAgonesPlayersList();
                    } else {
                        Debug.Log("No agones on player manager start");
                    }
                }
            }

            DevConsole.AddCommand(Command.Create("players", "", "List all connected players", () => {
                Debug.Log($"Players ({players.Count}):");
                var i = 1;
                foreach (var player in players) {
                    Debug.Log(
                        $"  {i}. {player.username} - connectionId: {player.connectionId}, userId: {player.userId}, orgRole: {player.orgRoleName}");
                    i++;
                }
            }));
        }

        private void OnDestroy() {
            NetworkServer.OnConnectedEvent -= NetworkServer_OnConnected;
            NetworkServer.OnDisconnectedEvent -= NetworkServer_OnDisconnected;
            DevConsole.RemoveCommand("players");
        }

        public event Action<object> OnPlayerAdded;
        public event PlayerRemovingDelegate playerRemoved;
        public event PlayerChangedDelegate playerChanged;

        public PlayerInfo GetPlayerInfoByConnectionId(int connectionId) {
            return players.Find(p => p.connectionId == connectionId);
        }

        public void AddUserData(int connectionId, UserData userData) {
            _userData.Remove(connectionId);
            _userData.Add(connectionId, userData);
        }

       [CanBeNull]
       public UserData GetUserDataFromClientId(int connectionId) {
           if (_userData.TryGetValue(connectionId, out var userData)) {
                return userData;
           }
           return null;
        }

        /// <summary>
        ///     Removes expired entries from the reservation map.
        /// </summary>
        /// <returns></returns>
        private async void CleanAgonesReservationMap() {
            while (true) {
                // Debug.Log("---[ Agones Reservation Map ]---");
                // Debug.Log($"Players ({this.players.Count}):");
                // foreach (var player in this.players) {
                // 	Debug.Log("  - " + player.username);
                // }
                // Debug.Log("------------");
                try {
                    var toRemove = new List<string>();
                    foreach (var entry in agonesReservationMap) {
                        if (entry.Key.StartsWith(AGONES_RESERVATION_FILL_PREFIX))
                            continue; // Fake reservations should never show up, but we check just in case.
                        var seconds = DateTime.Now.Subtract(entry.Value).TotalSeconds;
                        if (seconds < MAX_RESERVATION_TIME_SEC ||
                            players.Exists(info => $"{info.userId}" == entry.Key)) continue;
                        toRemove.Add(entry.Key);
                    }

                    toRemove.ForEach(async (userId) => {
                        await agones.DeleteListValue(AGONES_RESERVATIONS_LIST_NAME, userId);
                        agonesReservationMap.Remove(userId);
                    });
                } catch (Exception err) {
                    Debug.LogWarning($"Error when cleaning reservation map:\n{err}");
                }

                await Awaitable.WaitForSecondsAsync(30);
            }
        }

        private async void UpdateAgonesPlayersList() {
            while (true) {
                try {
                    var agonesPlayerList = await agones.GetListValues(AGONES_PLAYERS_LIST_NAME);
                    foreach (var userId in agonesPlayerList)
                        if (!players.Exists(info => $"{info.userId}" == userId))
                            await agones.DeleteListValue(AGONES_PLAYERS_LIST_NAME, userId);
                } catch (Exception err) {
                    Debug.LogWarning($"Error when updating active player list:\n{err}");
                }

                await Awaitable.WaitForSecondsAsync(30);
            }
        }

        public void AddBotPlayer(string username, string userId, string profilePictureId) {
            var connectionId = botPlayerIdCounter;
            botPlayerIdCounter++;
            var go = Instantiate(playerPrefab, Instance.transform.parent);

            var playerInfo = go.GetComponent<PlayerInfo>();
            playerInfo.Init(connectionId, userId, username, profilePictureId, string.Empty, string.Empty);

            // var identity = go.GetComponent<NetworkIdentity>();
            NetworkServer.Spawn(go);

            var playerInfoDto = playerInfo.BuildDto();
            // this.players.Add(playerInfo);

            OnPlayerAdded?.Invoke(playerInfoDto);
            playerChanged?.Invoke(playerInfoDto, true);
        }

        /**
         * Server side logic for when a new client joins.
         */
        private async void NetworkServer_OnConnected(NetworkConnectionToClient conn) {
            if (playerPrefab == null) {
                Debug.LogWarning($"Player prefab is empty and cannot be spawned for {conn}.");
                return;
            }

            var startPollingTime = Time.time;
            var sentFailedToReadyMsg = false;
            while (!conn.isAuthenticated || !conn.isReady) {
                if (!sentFailedToReadyMsg && Time.time - startPollingTime > 10) {
                    sentFailedToReadyMsg = true;
                    Debug.LogError(
                        $"Failed to setup player for connection id {conn.connectionId}: isAuthenticated={conn.isAuthenticated} isReady={conn.isReady}");
                }

                // print($"Waiting for {conn.connectionId} to be ready.");
                await Awaitable.NextFrameAsync();
            }

            var go = Instantiate(playerPrefab, Instance.transform.parent);
            var identity = go.GetComponent<NetworkIdentity>();
            connectionIdToPlayerNetId[conn.connectionId] = identity;
            var playerInfo = go.GetComponent<PlayerInfo>();
            var userData = GetUserDataFromClientId(conn.connectionId);
            if (userData != null) {
// #if UNITY_SERVER || true
// 				Debug.Log($"Initializing Player as {userData.username} owned by " + conn);
// #endif
                playerInfo.Init(conn.connectionId, userData.uid, userData.username, userData.profileImageId,
                    userData.orgRoleName, userData.fullTransferPacket);
            } else {
#if UNITY_SERVER || true
                Debug.Log("Missing UserData for " + conn);
#endif
            }

            // NetworkServer.Spawn(go, conn);
            NetworkServer.AddPlayerForConnection(conn, go);

            var playerInfoDto = playerInfo.BuildDto();
            // this.players.Add(playerInfo);

            OnPlayerAdded?.Invoke(playerInfoDto);
            playerChanged?.Invoke(playerInfoDto, true);

            if (RunCore.IsServer() && !RunCore.IsClient())
                Debug.Log($"{playerInfo.username} joined the server. orgRole: {playerInfo.orgRoleName}");

            if (agones) await agones.AppendListValue(AGONES_PLAYERS_LIST_NAME, $"{playerInfo.userId}");
        }

        // Handler for on disconnect of any connection, including connections that failed to validate
        // or didn't get fully set up. Occurs after HandlePlayerLeave
        private async void NetworkServer_OnDisconnected(NetworkConnectionToClient conn) {
            var user = GetUserDataFromClientId(conn.connectionId);
            if (user != null) {
                Debug.Log($"Cleaning up {user.username}'s connection.");
#if UNITY_SERVER
				if (this.agones) {
					await this.agones.DeleteListValue(AGONES_PLAYERS_LIST_NAME, $"{user.uid}");
					await this.agones.DeleteListValue(AGONES_RESERVATIONS_LIST_NAME, $"{user.uid}");
				}
#endif
                _userData.Remove(conn.connectionId);
            }
        }

        public void AddPlayer(PlayerInfo playerInfo) {
            if (!players.Contains(playerInfo)) players.Add(playerInfo);
            connectionIdToPlayerNetId[playerInfo.connectionId] = playerInfo.gameObject.GetComponent<NetworkIdentity>();
        }

        public async Task<PlayerInfo> GetPlayerInfoFromConnectionIdAsync(int clientId) {
            var playerInfo = GetPlayerInfoByConnectionId(clientId);
            while (playerInfo == null) {
                await Awaitable.NextFrameAsync();
                playerInfo = GetPlayerInfoByConnectionId(clientId);
            }

            return playerInfo;
        }

        // Handler for anything related to the PlayerInfo object. This only gets fired if the PlayerInfo object
        // actually got created for the connection.
        public async void HandlePlayerLeave(PlayerInfo playerInfo) {
            Debug.Log(playerInfo.username + " disconnected.");

            // Dispatch an event that the player has left:
            var dto = playerInfo.BuildDto();
            players.Remove(playerInfo);
            playerRemoved?.Invoke(dto);
            playerChanged?.Invoke(dto, false);
            connectionIdToPlayerNetId.Remove(playerInfo.connectionId);
        }

        public PlayerInfoDto[] GetPlayers() {
            List<PlayerInfoDto> list = new();
            foreach (var playerInfo in players) list.Add(playerInfo.BuildDto());

            return list.ToArray();
        }

        /// <summary>
        ///     Validates that user has a reservation on a slot for this server
        /// </summary>
        /// <param name="firebaseId"></param>
        /// <returns></returns>
        public async Task<bool> ValidateAgonesReservation(string firebaseId) {
            if (serverBootstrap.IsAgonesEnvironment())
                return await agones.ListContains(AGONES_RESERVATIONS_LIST_NAME, firebaseId);
            return true;
        }
    }
}