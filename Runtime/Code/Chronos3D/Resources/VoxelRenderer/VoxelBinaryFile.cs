using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;


[System.Serializable]
public class VoxelBinaryFile : ScriptableObject
{
    public List<SaveChunk> chunks = new List<SaveChunk>();
    public List<SaveObject> mapObjects = new List<SaveObject>();
    public List<SavePointlight> pointLights = new List<SavePointlight>();
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
    public struct SaveObject {
        public string name;
        public Vector3 position;
        public Quaternion rotation;

        public SaveObject(string name, Vector3 position, Quaternion rotation) {
            this.name = name;
            this.position = position;
            this.rotation = rotation;
        }
    }

    [System.Serializable]
    public struct SavePointlight {
        public Color color;
        public Vector3 position;
        public Quaternion rotation;
        public float intensity;
        public float range;
        public bool castShadows;
        public bool highQualityLight;

        public SavePointlight(Color color, Vector3 position, Quaternion rotation, float intensity, float range, bool castShadows, bool highQualityLight) {
            this.color = color;
            this.position = position;
            this.rotation = rotation;
            this.intensity = intensity;
            this.range = range;
            this.castShadows = castShadows;
            this.highQualityLight = highQualityLight;
        }
        
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
        
        this.mapObjects.Clear();
        this.pointLights.Clear();

        foreach (var mapObject in world.mapObjects) {
            var saveObject = new SaveObject(mapObject.Key, mapObject.Value.position, mapObject.Value.rotation);
            this.mapObjects.Add(saveObject);
        }

        foreach (var pl in world.pointlights) {
            var pointlight = pl.GetComponent<PointLight>();
            var savePointlight = new SavePointlight(
                pointlight.color,
                pl.transform.position,
                pl.transform.rotation, 
                pointlight.intensity,
                pointlight.range,
                pointlight.castShadows,
                pointlight.highQualityLight
                );
            this.pointLights.Add(savePointlight);
        }
        
        Debug.Log("Saved " + counter + " chunks");
        Debug.Log("Saved " + mapObjects.Count + " map objects");
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
                writeChunk.readWriteVoxel[i] = world.blocks.AddSolidMaskToVoxelValue( data[i]);
            }

            world.chunks[key] = writeChunk;
        }
        Debug.Log("Loaded chunks: " + counter);
        Profiler.EndSample();
    }
    

    public void PlaceWorldObjects(VoxelWorld world) {
        foreach (var mapObject in mapObjects) {
            world.PlaceWorldObject(mapObject.name, mapObject.position, mapObject.rotation);
        }
    }

    public void PlacePointlight(VoxelWorld world) {
        foreach (var pointlight in pointLights) {
            world.PlacePointlight(
                pointlight.color, 
                pointlight.position, 
                pointlight.rotation, 
                pointlight.intensity, 
                pointlight.range, 
                pointlight.castShadows, 
                pointlight.highQualityLight);
        }
    }
    
    public SaveChunk[] GetChunks() {
        return this.chunks.ToArray();
    }

    public SaveObject[] GetMapObjects() {
        return this.mapObjects.ToArray();
    }

    public SavePointlight[] GetPointlights() {
        return this.pointLights.ToArray();
    }
}