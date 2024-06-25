using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agones;
using Airship;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Player {
	[LuauAPI]
	public class PlayerManagerBridge : Singleton<PlayerManagerBridge> {
		[Tooltip("Prefab to spawn for the player.")]
		[SerializeField]
		private NetworkObject playerPrefab;

		private NetworkManager networkManager;

		private Dictionary<int, UserData> _userData = new();

		private Dictionary<int, NetworkObject> _clientIdToObject = new();
		public delegate void PlayerRemovingDelegate(PlayerInfoDto playerInfo);

		public delegate void PlayerChangedDelegate(PlayerInfoDto playerInfo, object entered);

		public event Action<object> OnPlayerAdded;
		public event PlayerRemovingDelegate playerRemoved;
		public event PlayerChangedDelegate playerChanged;

		public PlayerInfo localPlayer;
		public bool localPlayerReady = false;

		private Dictionary<int, GameObject> clientToPlayerGO = new();
		private List<PlayerInfo> players = new();

		private int botPlayerIdCounter = -1;

		private Scene coreScene;

		[SerializeField] public AgonesSdk agones;
		[SerializeField] public AgonesBetaSdk agonesBeta;
		private static string AGONES_PLAYERS_LIST_NAME = "players";
		private static string AGONES_RESERVATIONS_LIST_NAME = "reservations";
		private static double MAX_RESERVATION_TIME_SEC = 60 * 5;
		private Dictionary<string, DateTime> agonesReservationMap = new();

		private GameObject FindLocalObjectByTag(string objectTag) {
			var objects = networkManager.ClientManager.Connection.Objects;
			foreach (var obj in objects) {
				if (obj.CompareTag(objectTag)) {
					return obj.gameObject;
				}
			}
			return null;
		}

		public PlayerInfo GetPlayerInfoByClientId(int clientId) {
			return this.players.Find((p) => p.clientId.Value == clientId);
		}

		public void AddUserData(int clientId, UserData userData) {
			_userData.Remove(clientId);
			_userData.Add(clientId, userData);
		}

		public UserData GetUserDataFromClientId(int clientId)
		{
			return _userData[clientId];
		}

		private void Awake()
		{
			coreScene = SceneManager.GetSceneByName("CoreScene");
		}

		private void Start() {
			networkManager = InstanceFinder.NetworkManager;
			var serverBootstrap = FindFirstObjectByType<ServerBootstrap>();

			if (RunCore.IsServer() && networkManager) {
				networkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
				networkManager.ServerManager.OnRemoteConnectionState += OnClientNetworkStateChanged;

				if (serverBootstrap && serverBootstrap.IsAgonesEnvironment())
				{
					if (this.agones)
					{
						this.agones.WatchGameServer(async (gs) =>
						{
							var reservedList = await this.agonesBeta.GetListValues(AGONES_RESERVATIONS_LIST_NAME);
							reservedList.ForEach((reservation) =>
							{
								agonesReservationMap.TryAdd(reservation, DateTime.Now);
							});
						});
					
						CleanReservationMap();
					}
					else
					{
						Debug.Log("No agones on player manager start");
					}
				}
			}
		}

		/// <summary>
		/// Removes expired entries from the reservation map.
		/// </summary>
		/// <returns></returns>
		private async void CleanReservationMap()
		{
			while (true)
			{
				foreach (var entry in agonesReservationMap)
				{
					double seconds = DateTime.Now.Subtract(entry.Value).TotalSeconds;
					if (seconds < MAX_RESERVATION_TIME_SEC || players.Find((info) => $"{info.userId.Value}" == entry.Key)) continue;
					await this.agonesBeta.DeleteListValue(AGONES_RESERVATIONS_LIST_NAME, entry.Key);
				}
				await Awaitable.WaitForSecondsAsync(25);
			}
		}
        
		private void OnDestroy() {
			if (networkManager != null && RunCore.IsServer() && networkManager) {
				networkManager.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
				networkManager.ServerManager.OnRemoteConnectionState -= OnClientNetworkStateChanged;
			}
		}

		public void AddBotPlayer(string username, string tag, string userId) {
			int clientId = this.botPlayerIdCounter;
			this.botPlayerIdCounter--;
			NetworkObject playerNob = networkManager.GetPooledInstantiated(this.playerPrefab, true);
			playerNob.transform.parent = PlayerManagerBridge.Instance.transform.parent; // GameReadAccess
			this.networkManager.ServerManager.Spawn(playerNob);

			var playerInfo = playerNob.GetComponent<PlayerInfo>();
			playerInfo.Init(clientId, userId, username, tag);

			var playerInfoDto = playerInfo.BuildDto();
			// this.players.Add(playerInfo);

			this.OnPlayerAdded?.Invoke(playerInfoDto);
			this.playerChanged?.Invoke(playerInfoDto, (object)true);
		}

		/**
		 * Client side logic for when a new client joins.
		 */
		private async void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
		{
			if (!asServer)
				return;
			if (playerPrefab == null)
			{
				Debug.LogWarning($"Player prefab is empty and cannot be spawned for connection {conn.ClientId}.");
				return;
			}

			NetworkObject nob = networkManager.GetPooledInstantiated(this.playerPrefab, true);
			nob.transform.parent = PlayerManagerBridge.Instance.transform.parent; // GameReadAccess

			_clientIdToObject[conn.ClientId] = nob;
			var playerInfo = nob.GetComponent<PlayerInfo>();
			var userData = GetUserDataFromClientId(conn.ClientId);
			if (userData != null) {
				playerInfo.Init(conn.ClientId, userData.uid, userData.username, userData.profileImageId);
			}

			networkManager.ServerManager.Spawn(nob, conn);
			playerInfo.TargetRpc_SetLocalPlayer(conn);

			var playerInfoDto = playerInfo.BuildDto();
			this.clientToPlayerGO.Add(conn.ClientId, nob.gameObject);
			// this.players.Add(playerInfo);

			// Add to scene
			this.networkManager.SceneManager.AddOwnerToDefaultScene(nob);

			OnPlayerAdded?.Invoke(playerInfoDto);
			playerChanged?.Invoke(playerInfoDto, (object)true);

			if (this.agonesBeta) {
				await this.agonesBeta.AppendListValue(AGONES_PLAYERS_LIST_NAME, $"{playerInfo.userId.Value}");
			}
		}

		public void AddPlayer(PlayerInfo playerInfo) {
			if (!this.players.Contains(playerInfo)) {
				this.players.Add(playerInfo);
			}
		}

		public async Task<PlayerInfo> GetPlayerInfoFromClientIdAsync(int clientId) {
			PlayerInfo playerInfo = this.GetPlayerInfoByClientId(clientId);
			while (playerInfo == null) {
				await Awaitable.NextFrameAsync();
				playerInfo = this.GetPlayerInfoByClientId(clientId);
			}

			return playerInfo;
		}

		private async void OnClientNetworkStateChanged(NetworkConnection conn, RemoteConnectionStateArgs args) {
			if (args.ConnectionState == RemoteConnectionState.Stopped) {
				if (!_clientIdToObject.ContainsKey(conn.ClientId)) return;

				// Dispatch an event that the player has left:
				var networkObj = _clientIdToObject[conn.ClientId];
				var playerInfo = networkObj.GetComponent<PlayerInfo>();
				
				if (this.agonesBeta) {
					Debug.Log($"Removing {playerInfo.userId.Value} from player lists.");
					await this.agonesBeta.DeleteListValue(AGONES_PLAYERS_LIST_NAME, $"{playerInfo.userId.Value}");
					await this.agonesBeta.DeleteListValue(AGONES_RESERVATIONS_LIST_NAME, $"{playerInfo.userId.Value}");
				}
				
				var dto = playerInfo.BuildDto();
				this.clientToPlayerGO.Remove(dto.clientId);
				this.players.Remove(playerInfo);
				playerRemoved?.Invoke(dto);
				playerChanged?.Invoke(dto, (object)false);
				NetworkCore.Despawn(networkObj.gameObject);
				_clientIdToObject.Remove(conn.ClientId);
			}
		}

		public PlayerInfoDto[] GetPlayers()
		{
			List<PlayerInfoDto> list = new();
			foreach (var playerInfo in this.players)
			{
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
			return await this.agonesBeta.ListContains(AGONES_RESERVATIONS_LIST_NAME, firebaseId);
		}
	}
}
