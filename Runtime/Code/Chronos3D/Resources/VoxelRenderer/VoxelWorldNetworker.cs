using System;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;


using VoxelData = System.UInt16;
using BlockId = System.UInt16;
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
        foreach (var pair in world.chunks)
        {
            TargetWriteChunkRpc(connection, pair.Key, pair.Value);
        }
        
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
        
        TargetFinishedSendingWorldRpc(connection);
        
        foreach (var pl in world.GetChildPointLights()) {
            TargetCreatePointlight(connection, pl.color, pl.transform.position, pl.transform.rotation, pl.intensity, pl.range, pl.castShadows, pl.highQualityLight);
        }
        
        /* TargetDirtyLights(connection); */
    }
    
    public override void OnStartClient()
    {
        base.OnStartClient();
        world.FullWorldUpdate();
    }
    
    [ObserversRpc]
    [TargetRpc]
    public void TargetWriteVoxelRpc(NetworkConnection conn, Vector3Int pos, VoxelData voxel)
    {
        world.WriteVoxelAt(pos, voxel, true);
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetWriteChunkRpc(NetworkConnection conn, Vector3Int pos, Chunk chunk)
    {
        Profiler.BeginSample("TargetWriteChunkRpc");
        world.WriteChunkAt(pos, chunk);
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
    )
    {
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
    public void TargetCreatePointlight(NetworkConnection conn, Color color, Vector3 position, Quaternion rotation, float intensity, float range, bool castShadows, bool highQualityLight) {
        world.PlacePointlightGame(
            color,
            position,
            rotation,
            intensity,
            range,
            castShadows,
            highQualityLight);
    }
    
    [ObserversRpc]
    [TargetRpc]
    public void TargetDirtyLights(NetworkConnection conn) {
        world.UpdateSceneLights();
    }

    [ObserversRpc]
    [TargetRpc]
    public void TargetFinishedSendingWorldRpc(NetworkConnection conn)
    {
        foreach (var chunk in world.chunks.Values)
        {
            world.InitializeLightingForChunk(chunk);
        }
        world.renderingDisabled = false;
        world.RegenerateAllMeshes();
    }
}