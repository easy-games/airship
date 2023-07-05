using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Component.Spawning;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.SceneManagement;

[LuauAPI]
public class PlayerCore : MonoBehaviour {
	[Tooltip("Prefab to spawn for the player.")]
	[SerializeField]
	private NetworkObject _playerPrefab;
	
	private NetworkManager _networkManager;

	private Dictionary<int, UserData> _userData = new();

	private Dictionary<int, NetworkObject> _clientIdToObject = new();
	
	public delegate void PlayerAddedDelegate(ClientInfoDto clientInfo);
	public delegate void PlayerRemovingDelegate(ClientInfoDto clientInfo);

	public delegate void PlayerChangedDelegate(ClientInfoDto clientInfo, object entered);

	public event PlayerAddedDelegate playerAdded;
	public event PlayerRemovingDelegate playerRemoved;
	public event PlayerChangedDelegate playerChanged;

	private Dictionary<int, GameObject> _players = new();

	private Scene _coreScene;

	private GameObject FindLocalObjectByTag(string objectTag) {
		var objects = _networkManager.ClientManager.Connection.Objects;
		foreach (var obj in objects) {
			if (obj.CompareTag(objectTag)) {
				return obj.gameObject;
			}
		}
		return null;
	}

	public void AddUserData(int clientId, UserData userData)
	{
		_userData.Remove(clientId);
		_userData.Add(clientId, userData);
	}

	public UserData GetUserDataFromClientId(int clientId)
	{
		return _userData[clientId];
	}

	private void Awake()
	{
		DontDestroyOnLoad(gameObject);
		_coreScene = SceneManager.GetSceneByName("CoreScene");
	}

	private void Start() {
		_networkManager = InstanceFinder.NetworkManager;
		
		if (RunCore.IsServer()) {
			_networkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
			_networkManager.ServerManager.OnRemoteConnectionState += OnClientNetworkStateChanged;
		}
	}
	
	private void OnDestroy() {
		if (_networkManager != null && RunCore.IsServer()) {
			_networkManager.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;	
			_networkManager.ServerManager.OnRemoteConnectionState -= OnClientNetworkStateChanged;
		}
	}

	private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
	{
		if (!asServer)
			return;
		if (_playerPrefab == null)
		{
			Debug.LogWarning($"Player prefab is empty and cannot be spawned for connection {conn.ClientId}.");
			return;
		}

		Debug.Log("Client has finished loading scenes: " + conn.ClientId);
		NetworkObject nob = _networkManager.GetPooledInstantiated(_playerPrefab, true);
		SceneManager.MoveGameObjectToScene(nob.gameObject, _coreScene);
		_networkManager.ServerManager.Spawn(nob, conn);
		
		_clientIdToObject[nob.OwnerId] = nob;
		var clientInfo = nob.GetComponent<PlayerInfo>();
		// StartCoroutine(WaitForClientInfoReady(clientInfo));

		var userData = GetUserDataFromClientId(clientInfo.ClientId);
		if (userData != null)
		{
			clientInfo.SetIdentity(userData.UserId, userData.Username, userData.UsernameTag);
		}

		var clientInfoDto = clientInfo.BuildDto();
		_players.Add(conn.ClientId, nob.gameObject);
		
		// Add to scene
		_networkManager.SceneManager.AddOwnerToDefaultScene(nob);
		
		Debug.Log("Invoking PlayerAdded from C#...");
		playerAdded?.Invoke(clientInfoDto);
		playerChanged?.Invoke(clientInfoDto, (object)true);
		Debug.Log("Finished invoking PlayerAdded from C#!");
	}

	private void OnClientNetworkStateChanged(NetworkConnection conn, RemoteConnectionStateArgs args) {
		Debug.Log("Remote connection state: " + args.ConnectionState + ", scenes: ");
		foreach (var connScene in conn.Scenes)
		{
			Debug.Log(connScene);
		}
		
		if (args.ConnectionState == RemoteConnectionState.Stopped) {
			// Dispatch an event that the player has left:
			var networkObj = _clientIdToObject[conn.ClientId];
			var clientInfo = networkObj.GetComponent<PlayerInfo>();
			var dto = clientInfo.BuildDto();
			_players.Remove(dto.ClientId);
			playerRemoved?.Invoke(dto);
			playerChanged?.Invoke(dto, (object)false);
			NetworkCore.Despawn(networkObj.gameObject);
			_clientIdToObject.Remove(conn.ClientId);
		}
	}

	public ClientInfoDto[] GetPlayers()
	{
		List<ClientInfoDto> list = new();
		foreach (var playerGO in _players.Values)
		{
			var clientInfo = playerGO.GetComponent<PlayerInfo>();
			list.Add(clientInfo.BuildDto());
		}

		return list.ToArray();
	}
}
