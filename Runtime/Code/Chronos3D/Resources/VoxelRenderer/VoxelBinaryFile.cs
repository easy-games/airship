using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;

[System.Serializable]
public class VoxelBinaryFile : ScriptableObject
{
    public List<SaveChunk> chunks = new List<SaveChunk>();
    public List<WorldPosition> worldPositions = new List<WorldPosition>();
    public List<SavePointLight> pointLights = new List<SavePointLight>();
    public string cubeMapPath = "";
    
    // Lighting
    public float globalSkySaturation = 1;
    public Color globalSunColor = new Color(1, 1, 0.9f);
    public float globalSunBrightness = 1f;
    public Color globalAmbientLight = new Color(0.2f, 0.2f, 0.2f);
    public float globalAmbientBrightness = 1.0f;
    public float globalAmbientOcclusion = 0.25f;
    public float globalRadiosityScale = 0.25f;
    public float globalRadiosityDirectLightAmp = 1.0f;
    public float globalFogStart = 40.0f;
    public float globalFogEnd = 500.0f;
    public Color globalFogColor = Color.white;

    [System.Serializable]
    public struct SaveChunk
    {
        public Vector3Int key;
        public VoxelData[] data;
        public SaveChunk(Vector3Int key, VoxelData[] data)
        {
            this.key = key;
            this.data = data; 
        }
    }

    [System.Serializable]
    public struct WorldPosition {
        public string name;
        public Vector3 position;
        public Quaternion rotation;

        public WorldPosition(string name, Vector3 position, Quaternion rotation) {
            this.name = name;
            this.position = position;
            this.rotation = rotation;
        }
    }

    [System.Serializable]
    public struct SavePointLight
    {
        public string name;
        public Color color;
        public Vector3 position;
        public Quaternion rotation;
        public float intensity;
        public float range;
        public bool castShadows;
        public bool highQualityLight;
    }

    public void CreateFromVoxelWorld(VoxelWorld world)
    {
        this.cubeMapPath = world.cubeMapPath;
        
        // Lighting
        this.globalSkySaturation = world.globalSkySaturation;
        this.globalSunColor = world.globalSunColor;
        this.globalSunBrightness = world.globalSunBrightness;
        this.globalAmbientLight = world.globalAmbientLight;
        this.globalAmbientBrightness = world.globalAmbientBrightness;
        this.globalAmbientOcclusion = world.globalAmbientOcclusion;
        this.globalRadiosityScale = world.globalRadiosityScale;
        this.globalRadiosityDirectLightAmp = world.globalRadiosityDirectLightAmp;
        this.globalFogStart = world.globalFogStart;
        this.globalFogEnd = world.globalFogEnd;
        this.globalFogColor = world.globalFogColor;
        
        var chunks = world.chunks;
        int counter = 0;
        foreach (var chunk in chunks)
        {
            var key = chunk.Key;
            var data = chunk.Value.readWriteVoxel;

            int count = 0;
            foreach (var voxel in data)
            {
                if (voxel != 0)
                {
                    count += 1;
                    
                }
            }
            if (count > 0)
            {
              
                counter++;
                var chunkData = new SaveChunk(key, data);
                this.chunks.Add(chunkData);
            }
        }
        
        this.worldPositions.Clear();
        this.pointLights.Clear();

        foreach (var pair in world.worldPositionEditorIndicators) {
            var saveObject = new WorldPosition(pair.Key, pair.Value.position, pair.Value.rotation);
            this.worldPositions.Add(saveObject);
        }

        foreach (var pl in world.pointLights) {
            var pointlight = pl.GetComponent<PointLight>();
            var savePointlight = new SavePointLight() {
                name = pl.name,
                color = pointlight.color,
                position = pl.transform.position,
                rotation = pl.transform.rotation,
                intensity = pointlight.intensity,
                range = pointlight.range,
                castShadows = pointlight.castShadows,
                highQualityLight = pointlight.highQualityLight
            };
            this.pointLights.Add(savePointlight);
        }
        
        Debug.Log("Saved " + counter + " chunks.");
        Debug.Log("Saved " + worldPositions.Count + " world positions.");
    } 

    public void CreateVoxelWorld(VoxelWorld world)
    {
        Profiler.BeginSample("CreateVoxelWorld");
        world.cubeMapPath = this.cubeMapPath;
        
        // Lighting
        world.globalSkySaturation = this.globalSkySaturation;
        world.globalSunColor = this.globalSunColor;
        world.globalSunBrightness = this.globalSunBrightness;
        world.globalAmbientLight = this.globalAmbientLight;
        world.globalAmbientBrightness = this.globalAmbientBrightness;
        world.globalAmbientOcclusion = this.globalAmbientOcclusion;
        world.globalRadiosityScale = this.globalRadiosityScale;
        world.globalRadiosityDirectLightAmp = this.globalRadiosityDirectLightAmp;
        world.globalFogStart = this.globalFogStart;
        world.globalFogEnd = this.globalFogEnd;
        world.globalFogColor = this.globalFogColor;


        world.chunks.Clear();
        int counter = 0;
        foreach (var chunk in chunks)
        {
            counter += 1;
            var key = chunk.key;
            var data = chunk.data;

            VoxelWorldStuff.Chunk writeChunk = new VoxelWorldStuff.Chunk(key);
            writeChunk.SetWorld(world);
             
            for (int i = 0; i < data.Length;i++)
            {
                var blockId = VoxelWorld.VoxelDataToBlockId(data[i]);
                if (world.blocks.GetBlock(blockId) == null)
                {
                    Debug.LogError("Failed to find block with blockId " + blockId);
                    writeChunk.readWriteVoxel[i] = world.blocks.AddSolidMaskToVoxelValue(1);
                    continue;
                }
                VoxelData val = world.blocks.AddSolidMaskToVoxelValue(data[i]);
                
                writeChunk.readWriteVoxel[i] = val;
            }
            world.chunks[key] = writeChunk;
        }
        Debug.Log("Loaded chunks: " + counter);

        foreach (var worldPosition in this.worldPositions)
        {
            world.AddWorldPosition(worldPosition);
        }

        foreach (var pointlight in pointLights) {
            var pl = world.AddPointLight(
                pointlight.color,
                pointlight.position,
                pointlight.rotation,
                pointlight.intensity,
                pointlight.range,
                pointlight.castShadows,
                pointlight.highQualityLight);
            pl.name = pointlight.name;
        }

        Profiler.EndSample();
    }

    public SaveChunk[] GetChunks() {
        return this.chunks.ToArray();
    }

    public WorldPosition[] GetMapObjects() {
        return this.worldPositions.ToArray();
    }

    public SavePointLight[] GetPointlights() {
        return this.pointLights.ToArray();
    }
}