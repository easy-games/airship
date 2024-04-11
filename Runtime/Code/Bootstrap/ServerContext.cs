using FishNet.Object;
using FishNet.Object.Synchronizing;

public class ServerContext : NetworkBehaviour {
    public readonly SyncVar<string> serverId = new();
    public readonly SyncVar<string> gameId = new();
    public readonly SyncVar<string> organizationId = new();
}