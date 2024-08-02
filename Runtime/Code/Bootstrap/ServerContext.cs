using Mirror;

public class ServerContext : NetworkBehaviour {
    [SyncVar] public string serverId;
    [SyncVar] public string gameId;
    [SyncVar] public string organizationId;
}