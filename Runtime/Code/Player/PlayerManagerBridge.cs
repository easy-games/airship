using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agones;
using Airship.DevConsole;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Player {
	[LuauAPI]
	public class PlayerManagerBridge : Singleton<PlayerManagerBridge> {
		[Tooltip("Prefab to spawn for the player.")]
		[SerializeField]
		private GameObject playerPrefab;

		private Dictionary<int, UserData> _userData = new();

		private Dictionary<int, NetworkIdentity> connectionIdToPlayerNetId = new();
		public delegate void PlayerRemovingDelegate(PlayerInfoDto playerInfo);

		public delegate void PlayerChangedDelegate(PlayerInfoDto playerInfo, object entered);

		public event Action<object> OnPlayerAdded;
		public event PlayerRemovingDelegate playerRemoved;
		public event PlayerChangedDelegate playerChanged;

		public PlayerInfo localPlayer;
		public bool localPlayerReady = false;

		public List<PlayerInfo> players = new();

		private int botPlayerIdCounter = 1;

		private Scene coreScene;

		[SerializeField] public AgonesBetaSdk agones;
		private static string AGONES_PLAYERS_LIST_NAME = "players";
		private static string AGONES_RESERVATIONS_LIST_NAME = "reservations";
		// To implement max players, we fill in fake reservations for slots we never want to fill. This is the prefix for the fake reservations we should ignore.
		private static string AGONES_RESERVATION_FILL_PREFIX = "::";
		private static double MAX_RESERVATION_TIME_SEC = 60 * 5;
	
		private Dictionary<string, DateTime> agonesReservationMap = new();

		private ServerBootstrap serverBootstrap;

		public PlayerInfo GetPlayerInfoByConnectionId(int connectionId) {
			return this.players.Find((p) => p.connectionId == connectionId);
		}

		public void AddUserData(int connectionId, UserData userData) {
			_userData.Remove(connectionId);
			_userData.Add(connectionId, userData);
		}

		public UserData GetUserDataFromClientId(int connectionId) {
			// var data = new UserData() {
			// 	uid = "1",
			// 	username = "Player1",
			// 	fullTransferPacket = "{}",
			// 	profileImageId = "",
			// };
			// _userData.Remove(connectionId);
			// _userData[connectionId] = data;

			return _userData[connectionId];
		}

		private void Awake()
		{
			coreScene = SceneManager.GetSceneByName("CoreScene");
			this.serverBootstrap = FindFirstObjectByType<ServerBootstrap>();
		}

		private async void Start() {
			// print("PlayerManagerBridge.start");

			// print("PlayerManagerBridge: server is ready.");
			if (RunCore.IsServer() && this.serverBootstrap) {
				while (!this.serverBootstrap.isServerReady) {
					await Awaitable.NextFrameAsync();
				}
				foreach (var connection in NetworkServer.connections.Values) {
					NetworkServer_OnConnected(connection);
				}

				NetworkServer.OnConnectedEvent += NetworkServer_OnConnected;

				if (this.serverBootstrap && this.serverBootstrap.IsAgonesEnvironment())
				{
					if (this.agones)
					{
						this.agones.WatchGameServer(async (gs) =>
						{
							var reservedList = await this.agones.GetListValues(AGONES_RESERVATIONS_LIST_NAME);
							reservedList.ForEach((reservation) =>
							{
								if (reservation.StartsWith(AGONES_RESERVATION_FILL_PREFIX)) return;
								agonesReservationMap.TryAdd(reservation, DateTime.Now);
							});
						});

						CleanAgonesReservationMap();
						UpdateAgonesPlayersList();
					}
					else
					{
						Debug.Log("No agones on player manager start");
					}
				}
			}

			DevConsole.AddCommand(Command.Create("players", "", "List all connected players", () => {
				Debug.Log($"Players ({this.players.Count}):");
				int i = 1;
				foreach (var player in this.players) {
					Debug.Log($"  {i}. {player.username} - connectionId: {player.connectionId}, userId: {player.userId}, orgRole: ${player.orgRoleName}");
					i++;
				}
			}));
		}

		/// <summary>
		/// Removes expired entries from the reservation map.
		/// </summary>
		/// <returns></returns>
		private async void CleanAgonesReservationMap()
		{
			while (true)
			{
				// Debug.Log("---[ Agones Reservation Map ]---");
				// Debug.Log($"Players ({this.players.Count}):");
				// foreach (var player in this.players) {
				// 	Debug.Log("  - " + player.username);
				// }
				// Debug.Log("------------");
				var toRemove = new List<string>();
				foreach (var entry in agonesReservationMap)
				{
					if (entry.Key.StartsWith(AGONES_RESERVATION_FILL_PREFIX)) continue; // Fake reservations should never show up, but we check just in case.
					double seconds = DateTime.Now.Subtract(entry.Value).TotalSeconds;
					if (seconds < MAX_RESERVATION_TIME_SEC || players.Find((info) => $"{info.userId}" == entry.Key)) continue;
					await this.agones.DeleteListValue(AGONES_RESERVATIONS_LIST_NAME, entry.Key);
					toRemove.Add(entry.Key);
				}
				toRemove.ForEach((userId) => agonesReservationMap.Remove(userId));
				await Awaitable.WaitForSecondsAsync(30);
			}
		}

		private async void UpdateAgonesPlayersList()
		{
			while (true)
			{
				var agonesPlayerList = await this.agones.GetListValues(AGONES_PLAYERS_LIST_NAME);
				foreach (var userId in agonesPlayerList)
				{
					if (!players.Find((info) => $"{info.userId}" == userId))
					{
						await this.agones.DeleteListValue(AGONES_PLAYERS_LIST_NAME, userId);
					}
				}
				await Awaitable.WaitForSecondsAsync(30);
			}
		}
        
		private void OnDestroy() {
			NetworkServer.OnConnectedEvent -= NetworkServer_OnConnected;
		}

		public void AddBotPlayer(string username, string userId, string profilePictureId) {
			int connectionId = this.botPlayerIdCounter;
			this.botPlayerIdCounter++;
			var go = Instantiate(this.playerPrefab, Instance.transform.parent);

			var playerInfo = go.GetComponent<PlayerInfo>();
			playerInfo.Init(connectionId, userId, username, profilePictureId, string.Empty);

			// var identity = go.GetComponent<NetworkIdentity>();
			NetworkServer.Spawn(go);

			var playerInfoDto = playerInfo.BuildDto();
			// this.players.Add(playerInfo);

			this.OnPlayerAdded?.Invoke(playerInfoDto);
			this.playerChanged?.Invoke(playerInfoDto, (object)true);
		}

		/**
		 * Client side logic for when a new client joins.
		 */
		private async void NetworkServer_OnConnected(NetworkConnectionToClient conn) {
			if (playerPrefab == null) {
				Debug.LogWarning($"Player prefab is empty and cannot be spawned for {conn}.");
				return;
			}

			var startPollingTime = Time.time;
			var sentFailedToReadyMsg = false;
			while (!conn.isAuthenticated || !conn.isReady) {
				if (!sentFailedToReadyMsg && (Time.time - startPollingTime) > 10) {
					sentFailedToReadyMsg = true;
					Debug.LogError($"Failed to setup player for connection id {conn.connectionId}: isAuthenticated={conn.isAuthenticated} isReady={conn.isReady}");
				}
				// print($"Waiting for {conn.connectionId} to be ready.");
				await Awaitable.NextFrameAsync();
			}

			var go = GameObject.Instantiate(this.playerPrefab, PlayerManagerBridge.Instance.transform.parent);
			var identity = go.GetComponent<NetworkIdentity>();
			this.connectionIdToPlayerNetId[conn.connectionId] = identity;
			var playerInfo = go.GetComponent<PlayerInfo>();
			var userData = GetUserDataFromClientId(conn.connectionId);
			if (userData != null) {
// #if UNITY_SERVER || true
// 				Debug.Log($"Initializing Player as {userData.username} owned by " + conn);
// #endif
				playerInfo.Init(conn.connectionId, userData.uid, userData.username, userData.profileImageId, userData.orgRoleName);
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
			playerChanged?.Invoke(playerInfoDto, (object)true);

			if (RunCore.IsServer() && !RunCore.IsClient()) {
				Debug.Log(playerInfo.username + " joined the server.");
			}

			if (this.agones) {
				await this.agones.AppendListValue(AGONES_PLAYERS_LIST_NAME, $"{playerInfo.userId}");
			}
		}

		public void AddPlayer(PlayerInfo playerInfo) {
			if (!this.players.Contains(playerInfo)) {
				this.players.Add(playerInfo);
			}
			this.connectionIdToPlayerNetId[playerInfo.connectionId] = playerInfo.gameObject.GetComponent<NetworkIdentity>();
		}

		public async Task<PlayerInfo> GetPlayerInfoFromConnectionIdAsync(int clientId) {
			PlayerInfo playerInfo = this.GetPlayerInfoByConnectionId(clientId);
			while (playerInfo == null) {
				await Awaitable.NextFrameAsync();
				playerInfo = this.GetPlayerInfoByConnectionId(clientId);
			}

			return playerInfo;
		}

		public async void HandlePlayerLeave(PlayerInfo playerInfo) {
			Debug.Log(playerInfo.username + " disconnected.");

			// Dispatch an event that the player has left:
			var dto = playerInfo.BuildDto();
			this.players.Remove(playerInfo);
			playerRemoved?.Invoke(dto);
			playerChanged?.Invoke(dto, (object)false);
			this.connectionIdToPlayerNetId.Remove(playerInfo.connectionId);

#if UNITY_SERVER
			if (this.agones) {
				await this.agones.DeleteListValue(AGONES_PLAYERS_LIST_NAME, $"{dto.userId}");
				await this.agones.DeleteListValue(AGONES_RESERVATIONS_LIST_NAME, $"{dto.userId}");
			}
#endif
		}

		public PlayerInfoDto[] GetPlayers() {
			List<PlayerInfoDto> list = new();
			foreach (var playerInfo in this.players) {
				list.Add(playerInfo.BuildDto());
			}

			return list.ToArray();
		}

		/// <summary>
		/// Validates that user has a reservation on a slot for this server
		/// </summary>
		/// <param name="firebaseId"></param>
		/// <returns></returns>
		public async Task<bool> ValidateAgonesReservation(string firebaseId)
		{
			if (this.serverBootstrap.IsAgonesEnvironment()) {
				return await this.agones.ListContains(AGONES_RESERVATIONS_LIST_NAME, firebaseId);
			} else {
				return true;
			}
		}
	}
}
