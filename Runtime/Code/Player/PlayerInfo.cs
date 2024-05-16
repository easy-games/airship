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
	public GameObject gameObject;
}

[LuauAPI]
public class PlayerInfo : NetworkBehaviour {
	public readonly SyncVar<string> userId = new();
	public readonly SyncVar<string> username = new();
	public readonly SyncVar<string> usernameTag = new();
	public readonly SyncVar<int> clientId = new();
	public AudioSource voiceChatAudioSource;

	private void Start() {
		this.transform.parent = InputBridge.Instance.transform.parent;

		PlayerManagerBridge.Instance.AddPlayer(this);
	}

	public void Init(int clientId, string userId, string username, string usernameTag) {
		this.gameObject.name = "Player_" + username;
		this.clientId.Value = clientId;
		this.userId.Value = userId;
		this.username.Value = username;
		this.usernameTag.Value = usernameTag;

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
			clientId = (int)this.clientId.Value,
			userId = (string)this.userId.Value,
			username = (string)this.username.Value,
			gameObject = gameObject,
		};
	}
}
