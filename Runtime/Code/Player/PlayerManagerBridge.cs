using System.Collections.Generic;
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

		public delegate void PlayerAddedDelegate(PlayerInfoDto playerInfo);
		public delegate void PlayerRemovingDelegate(PlayerInfoDto playerInfo);

		public delegate void PlayerChangedDelegate(PlayerInfoDto playerInfo, object entered);

		public event PlayerAddedDelegate playerAdded;
		public event PlayerRemovingDelegate playerRemoved;
		public event PlayerChangedDelegate playerChanged;

		public PlayerInfo localPlayer;

		private Dictionary<int, GameObject> clientToPlayerGO = new();
		private List<PlayerInfo> players = new();

		private int botPlayerIdCounter = -1;

		private Scene coreScene;

		[SerializeField] public AgonesAlphaSdk agones;

		private GameObject FindLocalObjectByTag(string objectTag) {
			var objects = networkManager.ClientManager.Connection.Objects;
			foreach (var obj in objects) {
				if (obj.CompareTag(objectTag)) {
					return obj.gameObject;
				}

				InstanceFinder.PredictionManager.IsReplaying();
			}
			return null;
		}

		public PlayerInfo GetPlayerInfoByClientId(int clientId) {
			return this.players.Find((p) => p.clientId == clientId);
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

			if (RunCore.IsServer() && networkManager) {
				networkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
				networkManager.ServerManager.OnRemoteConnectionState += OnClientNetworkStateChanged;
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
			SceneManager.MoveGameObjectToScene(playerNob.gameObject, this.coreScene);
			this.networkManager.ServerManager.Spawn(playerNob);

			var playerInfo = playerNob.GetComponent<PlayerInfo>();
			playerInfo.Init(clientId, userId, username, tag);

			var playerInfoDto = playerInfo.BuildDto();
			this.players.Add(playerInfo);

			this.playerAdded?.Invoke(playerInfoDto);
			this.playerChanged?.Invoke(playerInfoDto, (object)true);
		}

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

			_clientIdToObject[conn.ClientId] = nob;
			var playerInfo = nob.GetComponent<PlayerInfo>();
			var userData = GetUserDataFromClientId(conn.ClientId);
			if (userData != null) {
				playerInfo.Init(conn.ClientId, userData.uid, userData.username, userData.discriminator);
			}

			SceneManager.MoveGameObjectToScene(nob.gameObject, this.coreScene);
			networkManager.ServerManager.Spawn(nob, conn);

			var playerInfoDto = playerInfo.BuildDto();
			this.clientToPlayerGO.Add(conn.ClientId, nob.gameObject);
			this.players.Add(playerInfo);

			// Add to scene
			this.networkManager.SceneManager.AddOwnerToDefaultScene(nob);

			playerAdded?.Invoke(playerInfoDto);
			playerChanged?.Invoke(playerInfoDto, (object)true);

			if (this.agones) {
				await this.agones.PlayerConnect(playerInfo.userId);
			}
		}

		private async void OnClientNetworkStateChanged(NetworkConnection conn, RemoteConnectionStateArgs args) {
			if (args.ConnectionState == RemoteConnectionState.Stopped) {
				if (!_clientIdToObject.ContainsKey(conn.ClientId)) return;

				// Dispatch an event that the player has left:
				var networkObj = _clientIdToObject[conn.ClientId];
				var playerInfo = networkObj.GetComponent<PlayerInfo>();
				var dto = playerInfo.BuildDto();
				this.clientToPlayerGO.Remove(dto.clientId);
				this.players.Remove(playerInfo);
				playerRemoved?.Invoke(dto);
				playerChanged?.Invoke(dto, (object)false);
				NetworkCore.Despawn(networkObj.gameObject);
				_clientIdToObject.Remove(conn.ClientId);

				if (this.agones) {
					await this.agones.PlayerDisconnect(playerInfo.userId);
				}
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
	}
}
