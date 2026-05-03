using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Debug = UnityEngine.Debug;

public class VoxelWorldNetworker : NetworkBehaviour {
    [SerializeField] public VoxelWorld world;

    [Tooltip(
        "If set to true all written voxels will sync from server to clients. If false only the initial load will be networked.")]
    public bool networkWriteVoxels = true;

    private Stopwatch spawnTimer = new();
    private Stopwatch replicationTimer = new();
    private Dictionary<int, int> clientVersions = new ();

    private void Awake() {
        if (!RunCore.IsServer()) {
            spawnTimer.Start();
            world.renderingDisabled = true;
        }
    }

    private async void ClientSendReadyWhenAble() {
        while (!NetworkClient.ready) {
            await Awaitable.NextFrameAsync();
        }

        // Init this client on the server
        OnReadyCommand(25);
    }

    [Command(requiresAuthority = false)]
    public void OnReadyCommand(NetworkConnectionToClient connection = null) {
        // Save Client Version
        if (connection != null) {
            clientVersions[connection.connectionId] = 24;
        }
        
        if (RunCore.IsClient() && RunCore.IsServer()) {
            // Running in shared editor
            return;
        }
        SendAllChunks(connection);
    }
    
    [Command(requiresAuthority = false)]
    public void OnReadyCommand(int clientVersion, NetworkConnectionToClient connection = null) {
        // Save Client Version
        if (connection != null) {
            clientVersions[connection.connectionId] = clientVersion;
        }
            
        if (RunCore.IsClient() && RunCore.IsServer()) {
            // Running in shared editor
            return;
        }
        // SendAllChunks(client);
        StartCoroutine(SlowlySendChunks(connection, new List<Vector3Int>()));
    }

    private void SendAllChunks(NetworkConnectionToClient connection = null) {
        // Send chunks
        List<Chunk> chunks = new(world.chunks.Count);
        List<Vector3Int> chunkPositions = new(world.chunks.Count);
        var keys = world.chunks.Keys.ToArray();
        // Send whole world
        for (var i = 0; i < world.chunks.Count; i++) {
            var pos = keys[i];
            var chunk = world.chunks[pos];
            chunks.Add(chunk);
            chunkPositions.Add(pos);
        }
        
        // Use client version to determine which RPC to call
        if (clientVersions.TryGetValue(connection.connectionId, out int userVersion) && userVersion >= 25) {
            RpcWriteChunks(connection, chunkPositions.ToArray(), chunks.ToArray(), true);
        } else {
            RpcWriteChunks(connection, chunkPositions.ToArray(), chunks.ToArray());
        }
    }

    private IEnumerator SlowlySendChunks(NetworkConnection connection, List<Vector3Int> skipChunks) {
        var keys = world.chunks.Keys.ToArray();
        HashSet<Vector3Int> sentPositions = new();
        List<Vector3Int> packetPositions = new();
        List<Chunk> packetChunks = new();
        const int chunksPerFrame = 5;
        bool newestVersion = false;
        if (clientVersions.TryGetValue(connection.connectionId, out int userVersion) && userVersion >= 25) {
            newestVersion = true;
        }
        for (var i = 0; i < world.chunks.Count; i++) {
            var pos = keys[i];
            if (skipChunks.Contains(pos)) {
                continue;
            }

            packetPositions.Add(pos);
            packetChunks.Add(world.chunks[pos]);
            sentPositions.Add(pos);

            if (i % chunksPerFrame == chunksPerFrame-1 || i == world.chunks.Count - 1) {
                if (newestVersion) {
                    RpcWriteChunks(connection, packetPositions.ToArray(), packetChunks.ToArray(), false);
                } else {
                    RpcWriteChunks(connection, packetPositions.ToArray(), packetChunks.ToArray());
                }

                packetPositions.Clear();
                packetChunks.Clear();
                yield return new WaitForSeconds(.05f);
            }
        }

        if (newestVersion) {
            RpcFinishedSendingAllChunks(connection);
        } else {
            RpcFinishedSendingWorld(connection);
        }
    }

    public override void OnStartClient() {
        base.OnStartClient();
        // If we ever want to load a different definition file specified by server this will
        // need to be swapped to an rpc. But right now we always load the definition file attached
        // to the VW.
        if (!RunCore.IsServer()) {
            // Don't run in shared
            SetupClientVoxelWorld();
        }

        replicationTimer.Start();
        // print($"VoxelWorldNetworker.OnStartClient. Spawned on net after {this.spawnTimer.ElapsedMilliseconds}ms");
        // world.FullWorldUpdate();

        if (RunCore.IsClient()) {
            ClientSendReadyWhenAble();
        }
    }

    private void SetupClientVoxelWorld() {
        world.voxelBlocks.Reload(world.useSimplifiedVoxels);
    }

    //Voxel Changes write to all clients
    [ClientRpc]
    public void RpcWriteVoxel(Vector3Int pos, ushort voxel) {
        world.WriteVoxelAt(pos, voxel, true);
    }

    [ClientRpc]
    public void RpcWriteVoxelGroup(Vector3[] positions, double[] voxelData, bool priority) {
        world.WriteVoxelGroupAt(positions, voxelData, priority);
    }

    //Sending chunks happens to specific clients when they initialize
    [TargetRpc]
    public void RpcWriteChunks(NetworkConnection connection, Vector3Int[] positions, Chunk[] chunks, bool containsAllChunks) {
        Profiler.BeginSample("TargetWriteChunkRpcRpcWriteChunks");
        WriteChunks(positions, chunks, containsAllChunks);
        Profiler.EndSample();
    }

    [TargetRpc]
    [Obsolete("Use overload with 'ContainsAllChunks' bool", false)]
    public void RpcWriteChunks(
        NetworkConnection conn,
        Vector3Int[] positions,
        Chunk[] chunks) {
        WriteChunks(positions, chunks, true);
    }

    private void WriteChunks(Vector3Int[] positions, Chunk[] chunks, bool containsAllChunks) {
        // Needed for shared mode to ensure that processed chunks do not stay in the HashSet after replication.
        world.ClearProcessingMeshChunks();
        for (var i = 0; i < positions.Length; i++) {
            world.WriteChunkAt(positions[i], chunks[i]);
        }

        if (containsAllChunks) {
            world.renderingDisabled = false;
            world.DeleteRenderedGameObjects();
            world.RegenerateAllMeshes();
            world.InvokeOnFinishedReplicatingChunksFromServer();
        }
    }

    // Remove when all clients are playerVersion 25 or above
    [TargetRpc]
    [Obsolete("Use 'RpcFinishedSendingAllChunks'", false)]
    public void RpcFinishedSendingWorld(NetworkConnection conn) {
        FinishedSendingAllChunks();
    }

    [TargetRpc]
    public void RpcFinishedSendingAllChunks(NetworkConnection connection) {
        FinishedSendingAllChunks();
    }

    private void FinishedSendingAllChunks() {
        world.renderingDisabled = false;
        Profiler.BeginSample("RpcFinishedSendingWorld.RegenMeshes");
        world.DeleteRenderedGameObjects();
        world.RegenerateAllMeshes();
        Profiler.EndSample();
        world.InvokeOnFinishedReplicatingChunksFromServer();
        // Debug.Log($"Finished chunk replication in {this.replicationTimer.ElapsedMilliseconds}ms");
    }

}