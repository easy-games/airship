using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;


using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Debug = UnityEngine.Debug;

public class VoxelWorldNetworker : NetworkBehaviour
{
    [SerializeField] public VoxelWorld world;
    private Stopwatch spawnTimer = new();
    private Stopwatch replicationTimer = new();

    private void Awake()
    {
        if (RunCore.IsClient())
        {
            this.spawnTimer.Start();
            world.renderingDisabled = true;
        }
    }

    public override void OnSpawnServer(NetworkConnection connection)
    {
        base.OnSpawnServer(connection);

        // Send chunks
        List<Chunk> chunks = new(world.chunks.Count);
        List<Vector3Int> chunkPositions = new(world.chunks.Count);
        var keys = world.chunks.Keys.ToArray();
        for (int i = 0; i < 900 && i < world.chunks.Count; i++) {
            var pos = keys[i];
            var chunk = world.chunks[pos];
            chunks.Add(chunk);
            chunkPositions.Add(pos);
        }
        TargetWriteChunksRpc(connection, chunkPositions.ToArray(), chunks.ToArray());
        
        TargetSetLightingProperties(
            connection
         
        );

        var pointLights = world.GetChildPointLights();
        List<PointLightDto> pointLightDtos = new(pointLights.Count);
        foreach (var pointlight in pointLights) {
            pointLightDtos.Add(pointlight.BuildDto());
        }
        TargetAddPointLights(connection, pointLightDtos.ToArray());
        
        TargetFinishedSendingWorldRpc(connection);

        // StartCoroutine(SlowlySendChunks(connection, chunkPositions));

        /* TargetDirtyLights(connection); */
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

    public override void OnStartClient()
    {
        base.OnStartClient();
        this.replicationTimer.Start();
        // print($"VoxelWorldNetworker.OnStartClient. Spawned on net after {this.spawnTimer.ElapsedMilliseconds}ms");
        // world.FullWorldUpdate();
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetWriteVoxelRpc(NetworkConnection conn, Vector3Int pos, VoxelData voxel) {
        world.WriteVoxelAt(pos, voxel, true);
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetWriteVoxelGroupRpc(NetworkConnection conn,Vector3[] positions, double[] nums, bool priority) {
        world.WriteVoxelGroupAt(positions, nums, priority);
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetWriteChunksRpc(NetworkConnection conn, Vector3Int[] positions, Chunk[] chunks)
    {
        Profiler.BeginSample("TargetWriteChunkRpc");
        for (int i = 0; i < positions.Length; i++) {
            world.WriteChunkAt(positions[i], chunks[i]);
        }
        Profiler.EndSample();
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetSetLightingProperties(
        NetworkConnection conn
    
    ) 
    {
        //TODO: Lighting settings - do we want a string here or a file path?
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetAddPointLights(NetworkConnection conn, PointLightDto[] dtos) {
        foreach (var dto in dtos) {
            world.AddPointLight(
                dto.color,
                dto.position,
                dto.rotation,
                dto.intensity,
                dto.range,
                dto.castShadows,
                dto.highQualityLight
            );
        }
    }
    
    [ObserversRpc]
    [TargetRpc]
    public void TargetDirtyLights(NetworkConnection conn) {
        world.UpdateSceneLights();
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetFinishedSendingWorldRpc(NetworkConnection conn) {
        // print($"VoxelWorldNetworker.TargetFinishedSendingWorldRpc: {this.replicationTimer.ElapsedMilliseconds}ms");
        foreach (var chunk in world.chunks.Values)
        {
            world.InitializeLightingForChunk(chunk);
        }
        world.renderingDisabled = false;
        Profiler.BeginSample("FinishedSendingWorldRpc.RegenMeshes");
        world.RegenerateAllMeshes();
        Profiler.EndSample();
        world.InvokeOnFinishedReplicatingChunksFromServer();
        // Debug.Log($"Finished chunk replication in {this.replicationTimer.ElapsedMilliseconds}ms");
    }
}