using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;


[System.Serializable]
[LuauAPI]
public class WorldSaveFile : ScriptableObject {
    public List<SaveChunk> chunks = new List<SaveChunk>();
    public List<BlockIdToScopedName> blockIdToScopeName = new();

    [System.Serializable]
    public struct BlockIdToScopedName {
        public BlockId id;
        public string name;
    }

    [System.Serializable]
    public struct SaveChunk {
        public Vector3Int key;
        public VoxelData[] data;
        public uint[] color;
        public SaveChunk(Vector3Int key, VoxelData[] data, uint[] color) {
            this.key = key;
            this.data = data;
            this.color = color;
        }
    }

    private void CreateScopedBlockDictionaryFromVoxelWorld(VoxelWorld world) {
        blockIdToScopeName.Clear();
        var blockMap = world.voxelBlocks.loadedBlocks;
        foreach (var block in blockMap) {
            blockIdToScopeName.Add(new BlockIdToScopedName() {
                id = block.Key,
                name = block.Value.blockTypeId,
            });
        }
    }

    //This is unusable - for some reason it seems to be causing errors
    private void CreateScopedBlockDictionaryFromVoxelWorldTight(VoxelWorld world) {

        HashSet<int> UsedIds = new();
        foreach (var chunk in chunks) {
            var data = chunk.data;
            for (int j = 0; j < data.Length; j++) {
                UsedIds.Add(VoxelWorld.VoxelDataToBlockId(data[j]));
            }
        }

        blockIdToScopeName.Clear();
        var blockMap = world.voxelBlocks.loadedBlocks;
        foreach (var block in blockMap) {

            if (UsedIds.Contains(block.Value.blockId)==true) {

                blockIdToScopeName.Add(new BlockIdToScopedName() {
                    id = block.Key,
                    name = block.Value.blockTypeId,
                });
            }
        }
    }




    public BlockId GetFileBlockIdFromStringId(string blockTypeId) {
        foreach (var pair in this.blockIdToScopeName) {
            if (pair.name == blockTypeId) {
                return pair.id;
            }
        }

        return 0;
    }

    public void CreateFromVoxelWorld(VoxelWorld world) {

        // Add used blocks + their ids to file
        this.CreateScopedBlockDictionaryFromVoxelWorld(world);

        var chunks = world.chunks;
        int counter = 0;
        this.chunks.Clear();
        Dictionary<Vector3Int, VoxelWorldStuff.Chunk> finalChunks = new();

        //Merge all chunks (how?!)
        foreach (var chunk in chunks) {
            var key = chunk.Key;
            var data = chunk.Value.readWriteVoxel;
            var color = chunk.Value.color;

            finalChunks.TryGetValue(key, out var finalChunk);
            if (finalChunk == null) {
                finalChunk = new();
                finalChunks.Add(key, finalChunk);
            }

            for (int j = 0; j < data.Length; j++) {
                if (data[j] != 0) {
                    finalChunk.readWriteVoxel[j] = data[j];
                    finalChunk.color[j] = color[j];
                }
            }
        }

        //Discard any empty chunks
        foreach (var chunk in finalChunks) {
            var key = chunk.Key;
            var data = chunk.Value.readWriteVoxel;
            var color = chunk.Value.color;

            var foundVoxel = false;
            foreach (var voxel in data) {
                if (voxel != 0) {
                    foundVoxel = true;
                    break;
                }
            }

            if (!foundVoxel) continue;

            counter++;
            var chunkData = new SaveChunk(key, data, color);
            this.chunks.Add(chunkData);
        }

        Debug.Log("Saved " + counter + " chunks.");
        // Debug.Log("Saved " + worldPositions.Count + " world positions.");
    }

    /// <summary>
    /// Gets the scoped string id for the given block id declared in this file
    /// </summary>
    /// <param name="fileBlockId"></param>
    /// <returns></returns>
    public string GetFileScopedBlockTypeId(BlockId fileBlockId) {
        foreach (var blockDef in this.blockIdToScopeName) {
            if (blockDef.id == fileBlockId) {
                return blockDef.name;
            }
        }
        return null;
    }

    public void LoadIntoVoxelWorld(VoxelWorld world) {
        Profiler.BeginSample("CreateVoxelWorld");

        //Todo: Load lighting settings
        world.chunks.Clear();
        int counter = 0;

        Dictionary<BlockId, BlockId> blockRemapping = new();

        foreach (var blockIdToScopeName in this.blockIdToScopeName) {

            //see if this block exists in the world blockfiles
            var definition = world.voxelBlocks.GetBlockDefinitionByStringId(blockIdToScopeName.name);
            if (definition == null) {
                Debug.LogWarning($"Warning: Block {blockIdToScopeName.name} not found in world block definitions - Creating a placeholder. You can still safely save this file.");

                definition = world.voxelBlocks.CreateTemporaryBlockDefinition(blockIdToScopeName.name);
                blockRemapping[blockIdToScopeName.id] = definition.blockId;
            }
            else {
                blockRemapping[blockIdToScopeName.id] = definition.blockId;
            }
        }


        foreach (var chunk in chunks) {
            counter += 1;
            var key = chunk.key;
            var data = chunk.data;
            var color = chunk.color;

            VoxelWorldStuff.Chunk writeChunk = VoxelWorld.CreateChunk(key);
            writeChunk.SetWorld(world);

            for (int i = 0; i < data.Length; i++) {
                BlockId fileBlockId = VoxelWorld.VoxelDataToBlockId(data[i]);
                ushort extraBits = VoxelWorld.VoxelDataToExtraBits(data[i]);
                                
                bool found = blockRemapping.TryGetValue(fileBlockId, out var updatedBlockId);
                if (found) {

                    VoxelData vox = ((VoxelData)(updatedBlockId | extraBits));

#if UNITY_EDITOR
                    //Fix the solid bit - we have to do this in case someone has already placed a bunch of blocks and then changes their solid bit, which is usually only set when the voxel is written
                    var definition = world.voxelBlocks.GetBlockDefinitionFromBlockId(updatedBlockId);
                    vox = VoxelWorld.SetVoxelSolidBit(vox, definition.definition.solid);
#endif

                    writeChunk.readWriteVoxel[i] = vox;
                    if (color != null && color.Length > 0) {
                        writeChunk.color[i] = color[i];
                    }
                } else {
                    Debug.LogWarning(
                        $"Warning: Block {fileBlockId} not found in world block definitions - Replacing with air.");
                }
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
