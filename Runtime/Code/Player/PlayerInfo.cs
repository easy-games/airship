using FishNet.Object;
using UnityEngine;

[LuauAPI]
public class PlayerInfoDto {
	public int clientId;
	public string userId;
	public string username;
	public string usernameTag;
	public GameObject gameObject;
}

[LuauAPI]
public class PlayerInfo : MonoBehaviour {
	public NetworkObject networkObject;

	/// <summary>
	/// ClientId refers to the network ID for the given client in the
	/// current game server. It is <i>not</i> a unique ID for the client
	/// within the global game.
	///
	/// Use UserId instead for a unique ID.
	/// </summary>

	public string userId { get; private set; }
	public string username { get; private set; }
	public string usernameTag { get; private set; }

	public int clientId { get; private set; } = -1;

	private void Awake() {
		this.networkObject = GetComponent<NetworkObject>();
	}

	public void SetIdentity(string userId, string username, string usernameTag) {
		this.userId = userId;
		this.username = username;
		this.usernameTag = usernameTag;
	}

	public void SetClientId(int clientId) {
		this.clientId = clientId;
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
