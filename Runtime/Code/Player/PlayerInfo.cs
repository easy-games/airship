using System;
using Code.Player;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

[LuauAPI]
public class PlayerInfoDto {
	public int clientId;
	public string userId;
	public string username;
	public string profileImageId;
	public GameObject gameObject;
}

[LuauAPI]
public class PlayerInfo : NetworkBehaviour {
	public readonly SyncVar<string> userId = new();
	public readonly SyncVar<string> username = new();
	public readonly SyncVar<int> clientId = new();
	public readonly SyncVar<string> profileImageId = new();
	public AudioSource voiceChatAudioSource;

	private void Start() {
		this.transform.parent = InputBridge.Instance.transform.parent;
		PlayerManagerBridge.Instance.AddPlayer(this);
	}

	public void Init(int clientId, string userId, string username, string profileImageId) {
		this.gameObject.name = "Player_" + username;
		this.clientId.Value = clientId;
		this.userId.Value = userId;
		this.username.Value = username;
		this.profileImageId.Value = profileImageId;

		this.InitVoiceChat();
	}

	private void InitVoiceChat() {
		var voiceChatGO = new GameObject(
			$"{username.Value}_VoiceChatAudioSourceOutput");
		this.voiceChatAudioSource = voiceChatGO.AddComponent<AudioSource>();
		voiceChatGO.transform.SetParent(this.transform);
	}

	[LuauAPI(LuauContext.Protected)]
	[TargetRpc]
	public void TargetRpc_SetLocalPlayer(NetworkConnection connection) {
		PlayerManagerBridge.Instance.localPlayer = this;
		PlayerManagerBridge.Instance.localPlayerReady = true;
	}

	public override void OnStartClient() {
		base.OnStartClient();

		if (IsClientOnlyStarted) {
			this.InitVoiceChat();
		}
	}

	public override void OnStartNetwork() {
		base.OnStartNetwork();
	}


	public PlayerInfoDto BuildDto() {
		return new PlayerInfoDto {
			clientId = this.clientId.Value,
			userId = this.userId.Value,
			username = this.username.Value,
			profileImageId = this.profileImageId.Value,
			gameObject = gameObject,
		};
	}
}
