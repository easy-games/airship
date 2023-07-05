using FishNet.Object;
using UnityEngine;

[LuauAPI]
public class ClientInfoDto {
	public int ClientId;
	public string UserId;
	public string Username;
	public string UsernameTag;
	public GameObject GameObject;
}

[LuauAPI]
public class PlayerInfo : MonoBehaviour {
	private NetworkObject _networkObject;

	/// <summary>
	/// ClientId refers to the network ID for the given client in the
	/// current game server. It is <i>not</i> a unique ID for the client
	/// within the global game.
	///
	/// Use UserId instead for a unique ID.
	/// </summary>
	public int ClientId => _networkObject.OwnerId;
	public string UserId => _userId;
	public string Username => _username;
	public string UsernameTag => _usernameTag;

	private string _userId;
	private string _username;
	private string _usernameTag;

	private void Awake() {
		_networkObject = GetComponent<NetworkObject>();
	}

	public void SetIdentity(string userId, string username, string usernameTag) {
		_userId = userId;
		_username = username;
		_usernameTag = usernameTag;
	}

	public ClientInfoDto BuildDto() {
		return new ClientInfoDto {
			ClientId = ClientId,
			UserId = UserId,
			Username = Username,
			UsernameTag = UsernameTag,
			GameObject = gameObject,
		};
	}
}
