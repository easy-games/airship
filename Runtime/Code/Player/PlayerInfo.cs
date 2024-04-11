using Code.Player;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerInfoDto {
	public int clientId;
	public string userId;
	public string username;
	public string usernameTag;
	public GameObject gameObject;
}

public class PlayerInfo : NetworkBehaviour {
	public readonly SyncVar<string> userId;
	public readonly SyncVar<string> username;
	public readonly SyncVar<string> usernameTag;
	public readonly SyncVar<int> clientId;

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
		this.gameObject.name = "Player_" + username;
	}



	public PlayerInfoDto BuildDto() {
		return new PlayerInfoDto {
			clientId = this.clientId.Value,
			userId = this.userId.Value,
			username = this.username.Value,
			usernameTag = this.usernameTag.Value,
			gameObject = gameObject,
		};
	}
}
