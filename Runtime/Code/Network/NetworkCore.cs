using Assets.Luau.Network;
using UnityEngine.Scripting;

[LuauAPI][Preserve]
public class NetworkCore
{
    public static Net Net;

    public static void SetNet(Net net)
    {
        Net = net;
    }
    
    // public static NetworkManager NetworkManager => InstanceFinder.NetworkManager;
    //
    // public static void Spawn(GameObject obj, int clientId) {
    //     NetworkConnection conn = null;
    //     if (clientId > -1) {
    //         conn = RunCore.IsServer() ? NetworkManager.ServerManager.Clients[clientId] : NetworkManager.ClientManager.Clients[clientId];
    //     }
    //     NetworkManager.ServerManager.Spawn(obj, conn);
    // }

    // public static void Spawn(GameObject obj) {
    //     // var nob = obj.GetComponent<NetworkObject>();
    //     // if (nob != null) {
    //         // Debug.Log($"[NetworkCore] Spawn: {nob.gameObject.name} | CollectionId: {nob.SpawnableCollectionId} | PrefabId: {nob.PrefabId}");
    //     // }
    //     NetworkManager.ServerManager.Spawn(obj);
    // }
    //
    // public static void Despawn(GameObject obj) {
    //     NetworkManager.ServerManager.Despawn(obj);
    // }

    /**
     * Gets NetworkConnection from clientId. Works on both server and client.
     */
    // public static NetworkConnection GetNetworkConnection(int clientId) {
    //     return RunCore.IsServer() ? NetworkManager.ServerManager.Clients[clientId] : NetworkManager.ClientManager.Clients[clientId];
    // }
}