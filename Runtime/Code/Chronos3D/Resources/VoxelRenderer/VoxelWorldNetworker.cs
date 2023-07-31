using System;
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

    private void Awake()
    {
        if (RunCore.IsClient())
        {
            world.renderingDisabled = true;
        }
    }

    public override void OnSpawnServer(NetworkConnection connection)
    {
        base.OnSpawnServer(connection);

        // Send chunks
        List<Chunk> chunks = new(world.chunks.Count);
        List<Vector3Int> chunkPositions = new(world.chunks.Count);
        foreach (var chunk in world.chunks) {
            chunks.Add(chunk.Value);
            chunkPositions.Add(chunk.Key);
        }
        TargetWriteChunksRpc(connection, chunkPositions.ToArray(), chunks.ToArray());
        
        TargetSetLightingProperties(
            connection,
            world.globalSunBrightness,
            world.globalSkyBrightness,
            world.globalSkySaturation,
            world.globalSunColor,
            world.globalAmbientLight,
            world.globalAmbientBrightness,
            world.globalAmbientOcclusion,
            world.globalRadiosityScale,
            world.globalRadiosityDirectLightAmp,
            world.globalFogStart,
            world.globalFogEnd,
            world.globalFogColor
        );

        foreach (var pl in world.GetChildPointLights()) {
            TargetAddPointLight(connection, pl.color, pl.transform.position, pl.transform.rotation, pl.intensity, pl.range, pl.castShadows, pl.highQualityLight);
        }
        
        TargetFinishedSendingWorldRpc(connection);

        /* TargetDirtyLights(connection); */
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        print("VoxelWorldNetworker.OnStartClient");
        // world.FullWorldUpdate();
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetWriteVoxelRpc(NetworkConnection conn, Vector3Int pos, VoxelData voxel) {
        world.WriteVoxelAt(pos, voxel, true);
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetWriteChunksRpc(NetworkConnection conn, Vector3Int[] positions, Chunk[] chunks)
    {
        print("VoxelWorldNetworker.TargetWriteChunksRpc");
        Profiler.BeginSample("TargetWriteChunkRpc");
        for (int i = 0; i < positions.Length; i++) {
            world.WriteChunkAt(positions[i], chunks[i]);
        }
        Profiler.EndSample();
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetSetLightingProperties(
        NetworkConnection conn,
        float globalSunBrightness,
        float globalSkyBrightness,
        float globalSkySaturation,
        Color globalSunColor,
        Color globalAmbientLight,
        float globalAmbientBrightness,
        float globalAmbientOcclusion,
        float globalRadiosityScale,
        float globalRadiosityDirectLightAmp,
        float globalFogStart,
        float globalFogEnd,
        Color globalFogColor
    ) {
        print("VoxelWorldNetworker.TargetSetLightingProperties");
        world.globalAmbientBrightness = globalAmbientBrightness;
        world.globalSunBrightness = globalSunBrightness;
        world.globalSkyBrightness = globalSkyBrightness;
        world.globalSkySaturation = globalSkySaturation;
        world.globalSunColor = globalSunColor;
        world.globalAmbientLight = globalAmbientLight;
        world.globalAmbientOcclusion = globalAmbientOcclusion;
        world.globalRadiosityScale = globalRadiosityScale;
        world.globalRadiosityDirectLightAmp = globalRadiosityDirectLightAmp;
        world.globalFogStart = globalFogStart;
        world.globalFogEnd = globalFogEnd;
        world.globalFogColor = globalFogColor;
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetAddPointLight(NetworkConnection conn, Color color, Vector3 position, Quaternion rotation, float intensity, float range, bool castShadows, bool highQualityLight) {
        print("VoxelWorldNetworker.TargetAddPointLight");
        world.AddPointLight(
            color,
            position,
            rotation,
            intensity,
            range,
            castShadows,
            highQualityLight
        );
    }
    
    [ObserversRpc]
    [TargetRpc]
    public void TargetDirtyLights(NetworkConnection conn) {
        world.UpdateSceneLights();
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetFinishedSendingWorldRpc(NetworkConnection conn) {
        print("VoxelWorldNetworker.TargetFinishedSendingWorldRpc");
        var st = Stopwatch.StartNew();
        foreach (var chunk in world.chunks.Values)
        {
            world.InitializeLightingForChunk(chunk);
        }
        world.renderingDisabled = false;
        world.RegenerateAllMeshes();
        world.InvokeOnFinishedReplicatingChunksFromServer();
        Debug.Log($"Finished chunk replication in {st.ElapsedMilliseconds}ms");
    }
}