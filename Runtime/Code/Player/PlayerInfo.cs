using Code.Player;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

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
		print(
			$"Building dto. clientId={this.clientId.Value} ({this.clientId.Value.GetType()}) userId={this.userId.Value} ({this.userId.Value.GetType()}) username={this.username.Value}");
		return new PlayerInfoDto {
			clientId = (int)this.clientId.Value,
			userId = (string)this.userId.Value,
			username = (string)this.username.Value,
			gameObject = gameObject,
		};
	}
}
