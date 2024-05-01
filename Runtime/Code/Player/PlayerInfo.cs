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

public class PlayerInfo : NetworkBehaviour {
	public readonly SyncVar<string> userId = new();
	public readonly SyncVar<string> username = new();
	public readonly SyncVar<string> usernameTag = new();
	public readonly SyncVar<int> clientId = new();

	private void Start() {
		this.transform.parent = InputBridge.Instance.transform.parent;
	}

	public void Init(int clientId, string userId, string username, string usernameTag) {
		this.clientId.Value = clientId;
		this.userId.Value = userId;
		this.username.Value = username;
		this.usernameTag.Value = usernameTag;
	}

	public override void OnOwnershipClient(NetworkConnection prevOwner) {
		base.OnOwnershipClient(prevOwner);
		if (base.IsOwner) {
			PlayerManagerBridge.Instance.localPlayer = this;
		}
	}

	public override void OnStartClient() {
		base.OnStartClient();
	}

	public override void OnStartNetwork() {
		base.OnStartNetwork();
		this.gameObject.name = "Player_" + username;
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
