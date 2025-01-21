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
    [Tooltip("If set to true all written voxels will sync from server to clients. If false only the initial load will be networked.")]
    public bool networkWriteVoxels = true;
    private Stopwatch spawnTimer = new();
    private Stopwatch replicationTimer = new();

    private void Awake() {
        if (!RunCore.IsServer()) {
            this.spawnTimer.Start();
            world.renderingDisabled = true;
        }
    }

    private void Start() {
        OnReadyCommand();
    }

    [Command(requiresAuthority = false)]
    public void OnReadyCommand(NetworkConnectionToClient connection = null) {
        // Send chunks
        List<Chunk> chunks = new(world.chunks.Count);
        List<Vector3Int> chunkPositions = new(world.chunks.Count);
        var keys = world.chunks.Keys.ToArray();
        // Send whole world
        for (int i = 0; i < world.chunks.Count; i++) {
            var pos = keys[i];
            var chunk = world.chunks[pos];
            chunks.Add(chunk);
            chunkPositions.Add(pos);
        }
        TargetWriteChunksRpc(connection, chunkPositions.ToArray(), chunks.ToArray());
        TargetFinishedSendingWorldRpc(connection);
    }

    private IEnumerator SlowlySendChunks(NetworkConnection connection, List<Vector3Int> skipChunks) {
        var keys = this.world.chunks.Keys.ToArray();
        HashSet<Vector3Int> sentPositions = new();
        List<Vector3Int> packetPositions = new();
        List<Chunk> packetChunks = new();
        const int chunksPerFrame = 5;
        for (int i = 0; i < this.world.chunks.Count; i++) {
            var pos = keys[i];
            if (skipChunks.Contains(pos)) continue;

            packetPositions.Add(pos);
            packetChunks.Add(this.world.chunks[pos]);
            sentPositions.Add(pos);

            if (i % chunksPerFrame == 0) {
                TargetWriteChunksRpc(connection, packetPositions.ToArray(), packetChunks.ToArray());
                packetPositions.Clear();
                packetChunks.Clear();
                yield return null;
            }
        }
    }

    public override void OnStartClient() {
        base.OnStartClient();
        // If we ever want to load a different definition file specified by server this will
        // need to be swapped to an rpc. But right now we always load the definition file attached
        // to the VW.
        if (!RunCore.IsServer()) { // Don't run in shared
            SetupClientVoxelWorld();
        }

        this.replicationTimer.Start();
        // print($"VoxelWorldNetworker.OnStartClient. Spawned on net after {this.spawnTimer.ElapsedMilliseconds}ms");
        // world.FullWorldUpdate();
    }

    private void SetupClientVoxelWorld() {
        this.world.voxelBlocks.Reload();
    }

    [TargetRpc]
    public void TargetWriteVoxelRpc(NetworkConnection conn, Vector3Int pos, VoxelData voxel) {
        world.WriteVoxelAt(pos, voxel, true);
    }

    [TargetRpc]
    public void TargetWriteVoxelGroupRpc(NetworkConnection conn, Vector3[] positions, double[] nums, bool priority) {
        world.WriteVoxelGroupAt(positions, nums, priority);
    }

    [TargetRpc]
    public void TargetWriteChunksRpc(NetworkConnection conn, Vector3Int[] positions, Chunk[] chunks) {
        Profiler.BeginSample("TargetWriteChunkRpc");
        for (int i = 0; i < positions.Length; i++) {
            world.WriteChunkAt(positions[i], chunks[i]);
        }
        Profiler.EndSample();
    }

    [TargetRpc]
    public void TargetFinishedSendingWorldRpc(NetworkConnection conn) {
        world.renderingDisabled = false;
        Profiler.BeginSample("FinishedSendingWorldRpc.RegenMeshes");
        world.RegenerateAllMeshes();
        Profiler.EndSample();
        world.InvokeOnFinishedReplicatingChunksFromServer();
        // Debug.Log($"Finished chunk replication in {this.replicationTimer.ElapsedMilliseconds}ms");
    }
}