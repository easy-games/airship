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
	[SyncVar] public string userId;
	[SyncVar] public string username;
	[SyncVar] public string usernameTag;
	[SyncVar] public int clientId;

	public void Init(int clientId, string userId, string username, string usernameTag) {
		this.clientId = clientId;
		this.userId = userId;
		this.username = username;
		this.usernameTag = usernameTag;
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
			clientId = this.clientId,
			userId = this.userId,
			username = this.username,
			usernameTag = this.usernameTag,
			gameObject = gameObject,
		};
	}
}
