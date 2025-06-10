using System;
using Code.Player;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

[LuauAPI]
public class PlayerInfoDto {
	public int connectionId;
	public string userId;
	public string username;
	public string profileImageId;
	public string orgRoleName;
	public GameObject gameObject;
}

[LuauAPI]
public class PlayerInfo : NetworkBehaviour {
	[SyncVar] public string userId;
	[SyncVar] public string username;
	[SyncVar] public int connectionId;
	[SyncVar] public string profileImageId;
	[SyncVar] public string orgRoleName;
	public AudioSource voiceChatAudioSource;

	private void Start() {
		this.transform.parent = InputBridge.Instance.transform.parent;
		PlayerManagerBridge.Instance.AddPlayer(this);
	}

	public void Init(int connectionId, string userId, string username, string profileImageId, string orgRoleName) {
		this.gameObject.name = "Player_" + username;
		this.connectionId = connectionId;
		this.userId = userId;
		this.username = username;
		this.profileImageId = profileImageId;
		this.orgRoleName = orgRoleName;
		
		this.InitVoiceChat();
	}

	private void InitVoiceChat() {
		var voiceChatGO = new GameObject(
			$"{this.username}_VoiceChatAudioSourceOutput");
		this.voiceChatAudioSource = voiceChatGO.AddComponent<AudioSource>();
		voiceChatGO.transform.SetParent(this.transform);
	}

	public override void OnStartLocalPlayer() {
		PlayerManagerBridge.Instance.localPlayer = this;
		PlayerManagerBridge.Instance.localPlayerReady = true;
	}

	public override void OnStartClient() {
		base.OnStartClient();

		if (isClient) {
			this.InitVoiceChat();
		}
	}

	public bool IsInGameOrg() {
		return !string.IsNullOrEmpty(this.orgRoleName);
	}

	public override void OnStopServer() {
		PlayerManagerBridge.Instance.HandlePlayerLeave(this);
		base.OnStopServer();
	}

	public override void OnStopClient() {
		base.OnStopClient();
		if (!RunCore.IsServer()) {
			PlayerManagerBridge.Instance.HandlePlayerLeave(this);
		}
	}
	
	public PlayerInfoDto BuildDto() {
		return new PlayerInfoDto {
			connectionId = this.connectionId,
			userId = this.userId,
			username = this.username,
			profileImageId = this.profileImageId,
			orgRoleName = this.orgRoleName,
			gameObject = this.gameObject,
		};
	}
}
