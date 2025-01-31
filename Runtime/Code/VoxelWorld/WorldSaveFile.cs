using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;


[Serializable]
[LuauAPI]
[CreateAssetMenu(fileName = "VoxelWorldSaveFile", menuName = "Airship/VoxelWorld/VoxelWorldSaveFile", order = 0)]
public class WorldSaveFile : ScriptableObject {
    public List<SaveChunk> chunks = new List<SaveChunk>();
    public List<BlockIdToScopedName> blockIdToScopeName = new();

    public byte[] chunksCompressed;

    [Serializable]
    public struct BlockIdToScopedName {
        public BlockId id;
        public string name;
    }

    [Serializable]
    public struct SaveChunk {
        public Vector3Int key;
        public VoxelData[] data;
        public uint[] color;
        
        public SaveChunk(Vector3Int key, VoxelData[] data, uint[] color) {
            this.key = key;
            this.data = data;
            this.color = color;
        }

        public void Serialize(BinaryWriter writer, ushort version) {
            // Write key:
            writer.Write(key.x);
            writer.Write(key.y);
            writer.Write(key.z);
            
            // Write colors:
            writer.Write((uint)color.Length);
            foreach (var c in color) {
                writer.Write(c);
            }
            
            // Write voxel data:
            writer.Write((uint)data.Length);
            foreach (var d in data) {
                writer.Write(d);
            }
        }

        public static Vector3Int Deserialize(BinaryReader reader, ushort version, List<VoxelData> voxelData, List<uint> colors) {
            // Read key:
            var x = reader.ReadInt32();
            var y = reader.ReadInt32();
            var z = reader.ReadInt32();
            var key = new Vector3Int(x, y, z);

            // Read colors:
            var numColors = reader.ReadUInt32();
            for (uint j = 0; j < numColors; j++) {
                var c = reader.ReadUInt32();
                colors.Add(c);
            }

            // Read voxel data:
            var numVoxelData = reader.ReadUInt32();
            for (uint j = 0; j < numVoxelData; j++) {
                var d = reader.ReadUInt16();
                voxelData.Add(d);
            }

            return key;
        }
    }

    private static string FormatDataSize(long bytes) {
        if (bytes < 1024) {
            return $"{bytes} bytes";
        }
        if (bytes < 1024 * 1024) {
            var kb = bytes / 1024.0f;
            return $"{kb:F2} KB [{bytes} bytes]";
        }
        if (bytes < 1024 * 1024 * 1024) {
            var mb = bytes / (float)(1024 * 1024);
            return $"{mb:F2} MB [{bytes} bytes]";
        }

        var gb = bytes / (float)(1024 * 1024 * 1024);
        return $"{gb:F2} GB [{bytes} bytes]";
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
        var savedChunks = new List<SaveChunk>();
        
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
            savedChunks.Add(chunkData);
        }
        
        // Serialize:
        Profiler.BeginSample("CompressVoxelWorldChunks");
        
        using var memStream = new MemoryStream();
        using var writer = new BinaryWriter(memStream);
        
        // Serializer version:
        const ushort version = 1;
        writer.Write(version);
        
        // Serialize chunks:
        writer.Write((uint)savedChunks.Count);
        foreach (var chunk in savedChunks) {
            chunk.Serialize(writer, version);
        }
        
        // Compress:
        chunksCompressed = VoxelCompressUtil.CompressToByteArray(memStream);
        
        Profiler.EndSample();

        Debug.Log($"Saved {counter} chunks to {name} (raw: {FormatDataSize(memStream.Length)}) (compressed: {FormatDataSize(chunksCompressed.Length)})");
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

    private void LoadChunkIntoVoxelWorld(VoxelWorld world, Dictionary<BlockId, BlockId> blockRemapping, Vector3Int key, IList<VoxelData> data, IList<uint> color) {
        VoxelWorldStuff.Chunk writeChunk = VoxelWorld.CreateChunk(key);
        writeChunk.SetWorld(world);

        for (int i = 0; i < data.Count; i++) {
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
                if (color != null && color.Count > 0) {
                    writeChunk.color[i] = color[i];
                }
            } else {
                Debug.LogWarning(
                    $"Warning: Block {fileBlockId} not found in world block definitions - Replacing with air.");
            }
        }
        world.chunks[key] = writeChunk;
    }

    public void LoadIntoVoxelWorld(VoxelWorld world) {
        Profiler.BeginSample("CreateVoxelWorld");

        //Todo: Load lighting settings
        world.chunks.Clear();
        var counter = 0;

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

        // If compressed data is available, use that instead:
        if (chunksCompressed != null && chunksCompressed.Length > 0) {
            Profiler.BeginSample("ReadVoxelWorldChunks");
            
            // Decompress and deserialize chunks:
            Profiler.BeginSample("DecompressChunks");
            using var decompressedStream = VoxelCompressUtil.DecompressToMemoryStream(chunksCompressed);
            Profiler.EndSample();

            var reader = new BinaryReader(decompressedStream);
            var version = reader.ReadUInt16();
            var numChunks = reader.ReadUInt32();
            
            var data = new List<VoxelData>();
            var color = new List<uint>();
            for (uint i = 0; i < numChunks; i++) {
                data.Clear();
                color.Clear();
                
                Profiler.BeginSample("Deserialize");
                var key = SaveChunk.Deserialize(reader, version, data, color);
                Profiler.EndSample();
                
                counter += 1;
                
                Profiler.BeginSample("LoadChunkIntoVoxelWorld");
                LoadChunkIntoVoxelWorld(world, blockRemapping, key, data, color);
                Profiler.EndSample();
            }
            
            Profiler.EndSample();
        } else {
            // Old, non-compressed data version:
            foreach (var chunk in chunks) {
                counter += 1;
                LoadChunkIntoVoxelWorld(world, blockRemapping, chunk.key, chunk.data, chunk.color);
            }
        }

        Debug.Log($"[Voxel World]: Loaded {counter} chunks");
        
        Profiler.EndSample();
    }

    public SaveChunk[] GetChunks() {
        // TODO: Build the chunks from the serialized data
        Debug.LogWarning("[Voxel World]: GetChunks may not return any data if the data is serialized");
        return this.chunks.ToArray();
    }

}
