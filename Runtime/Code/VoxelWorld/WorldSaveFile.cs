using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;


[System.Serializable]
public class WorldSaveFile : ScriptableObject
{
    public List<SaveChunk> chunks = new List<SaveChunk>();
    public List<BlockIdToScopedName> blockIdToScopeName = new();

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
     
    private void CreateScopedBlockDictionaryFromVoxelWorld(VoxelWorld world)
    {
        var blockMap = world.voxelBlocks.loadedBlocks;
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

            VoxelWorldStuff.Chunk writeChunk = VoxelWorld.CreateChunk(key);
            writeChunk.SetWorld(world);
             
            for (int i = 0; i < data.Length;i++)
            {
                var blockId = VoxelWorld.VoxelDataToBlockId(data[i]);
                var blockTypeId = this.GetFileScopedBlockTypeId(blockId); // e.g. @Easy/Core:grass - if that's what's in the dict at blockId 1 (as an example)
                
                var worldBlockDefinition = world.voxelBlocks.GetBlockDefinitionByStringId(blockTypeId);

                if (worldBlockDefinition == null) {
                    Debug.LogError("Failed to find block with blockId " + blockId);
                    writeChunk.readWriteVoxel[i] = world.voxelBlocks.AddSolidMaskToVoxelValue(1);
                    continue;
                }

                var worldBlockId = worldBlockDefinition.blockId;
                var updatedVoxelData = world.voxelBlocks.UpdateVoxelBlockId(data[i], worldBlockId);
                updatedVoxelData = world.voxelBlocks.AddSolidMaskToVoxelValue(updatedVoxelData);

                writeChunk.readWriteVoxel[i] = updatedVoxelData;
            }
            world.chunks[key] = writeChunk;
        }
        Debug.Log("[Voxel World]: Loaded " + counter + " chunks.");
 
        Profiler.EndSample();
    }

    public SaveChunk[] GetChunks() {
        return this.chunks.ToArray();
    }
 
}
 