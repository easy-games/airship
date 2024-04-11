using FishNet.Object;
using FishNet.Object.Synchronizing;

public class ServerContext : NetworkBehaviour {
    public readonly SyncVar<string> serverId;
    public readonly SyncVar<string> gameId;
    public readonly SyncVar<string> organizationId;
}