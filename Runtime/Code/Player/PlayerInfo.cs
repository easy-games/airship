using System;
using Code.Player;
using Mirror;
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
	public string userId;
	public string username;
	public int clientId;
	public string profileImageId;
	public AudioSource voiceChatAudioSource;

	private void Start() {
		this.transform.parent = InputBridge.Instance.transform.parent;
		PlayerManagerBridge.Instance.AddPlayer(this);
	}

	public void Init(int clientId, string userId, string username, string profileImageId) {
		this.gameObject.name = "Player_" + username;
		this.clientId = clientId;
		this.userId = userId;
		this.username = username;
		this.profileImageId = profileImageId;

		this.InitVoiceChat();
	}

	private void InitVoiceChat() {
		var voiceChatGO = new GameObject(
			$"{this.username}_VoiceChatAudioSourceOutput");
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

		if (isClient) {
			this.InitVoiceChat();
		}
	}


	public PlayerInfoDto BuildDto() {
		return new PlayerInfoDto {
			clientId = this.clientId,
			userId = this.userId,
			username = this.username,
			profileImageId = this.profileImageId,
			gameObject = gameObject,
		};
	}
}
