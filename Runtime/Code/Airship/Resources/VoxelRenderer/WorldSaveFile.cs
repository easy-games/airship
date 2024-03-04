using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;

[System.Serializable]
public class WorldSaveFile : ScriptableObject
{
    public List<SaveChunk> chunks = new List<SaveChunk>();
    // public List<WorldPosition> worldPositions = new List<WorldPosition>();
    public List<SavePointLight> pointLights = new List<SavePointLight>();
    public List<BlockIdToScopedName> blockIdToScopeName = new();

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
    public struct BlockIdToScopedName
    {
        public BlockId id;
        public string name;
    }

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

    private void CreateLightingFromVoxelWorld(VoxelWorld world)
    {
        //TODO: Path to file? file contents as string? Not sure what we want here atm
        
    }

    private void CreateScopedBlockDictionaryFromVoxelWorld(VoxelWorld world)
    {
        var blockMap = world.blocks.loadedBlocks;
        foreach (var block in blockMap)
        {
            blockIdToScopeName.Add(new BlockIdToScopedName()
            {
                id = block.Key,
                name = block.Value.blockTypeId,
            });
        }
    }

    public BlockId GetFileBlockIdFromStringId(string blockTypeId)
    {
        foreach (var pair in this.blockIdToScopeName)
        {
            if (pair.name == blockTypeId)
            {
                return pair.id;
            }
        }

        return 0;
    }

    public void CreateFromVoxelWorld(VoxelWorld world)
    {
        
        // Add used blocks + their ids to file
        this.CreateScopedBlockDictionaryFromVoxelWorld(world);
        
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

        this.pointLights.Clear();

        // this.worldPositions.Clear();
        // foreach (var pos in world.worldPositions) {
        //     this.worldPositions.Add(pos);
        // }

        foreach (var pl in world.pointLights) {
            var pointlight = pl.GetComponent<AirshipPointLight>();
            var savePointlight = new SavePointLight() {
                name = pl.name,
                color = pointlight.color,
                position = pl.transform.position,
                rotation = pl.transform.rotation,
                intensity = pointlight.intensity,
                range = pointlight.range,
                castShadows = pointlight.castShadows,
            };
            this.pointLights.Add(savePointlight);
        }
        
        Debug.Log("Saved " + counter + " chunks.");
        // Debug.Log("Saved " + worldPositions.Count + " world positions.");
    }

    /// <summary>
    /// Gets the scoped string id for the given block id declared in this file
    /// </summary>
    /// <param name="fileBlockId"></param>
    /// <returns></returns>
    public string GetFileScopedBlockTypeId(BlockId fileBlockId)
    {
        foreach (var blockDef in this.blockIdToScopeName)
        {
            if (blockDef.id == fileBlockId)
            {
                return blockDef.name;
            }
        }
        return null;
    }

    public void LoadIntoVoxelWorld(VoxelWorld world)
    {
        Profiler.BeginSample("CreateVoxelWorld");
        
        //Todo: Load lighting settings
        

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
                var blockTypeId = this.GetFileScopedBlockTypeId(blockId); // e.g. @Easy/Core:grass - if that's what's in the dict at blockId 1 (as an example)
                
                var worldBlockDefinition = world.blocks.GetBlockDefinitionByStringId(blockTypeId);

                if (worldBlockDefinition == null) {
                    Debug.LogError("Failed to find block with blockId " + blockId);
                    writeChunk.readWriteVoxel[i] = world.blocks.AddSolidMaskToVoxelValue(1);
                    continue;
                }

                var worldBlockId = worldBlockDefinition.blockId;
                var updatedVoxelData = world.blocks.UpdateVoxelBlockId(data[i], worldBlockId);
                updatedVoxelData = world.blocks.AddSolidMaskToVoxelValue(updatedVoxelData);

                writeChunk.readWriteVoxel[i] = updatedVoxelData;
            }
            world.chunks[key] = writeChunk;
        }
        Debug.Log("[Voxel World]: Loaded " + counter + " chunks.");

        // foreach (var worldPosition in this.worldPositions)
        // {
        //     world.AddWorldPosition(worldPosition);
        // }

        foreach (var pointlight in pointLights) {
            var pl = world.AddPointLight(
                pointlight.color,
                pointlight.position,
                pointlight.rotation,
                pointlight.intensity,
                pointlight.range,
                pointlight.castShadows
                );
            pl.name = pointlight.name;
        }

        Profiler.EndSample();
    }

    public SaveChunk[] GetChunks() {
        return this.chunks.ToArray();
    }

    // public WorldPosition[] GetMapObjects() {
    //     return this.worldPositions.ToArray();
    // }

    public SavePointLight[] GetPointlights() {
        return this.pointLights.ToArray();
    }
}