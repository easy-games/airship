using System;
using System.Buffers;
using System.Collections.Generic;
using Assets.Luau;
using Code.Zstd;
using Mirror;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;

public static class ChunkSerializer {
    private static Zstd zstd = new Zstd(1024 * 4);

    public static void WriteChunk(this NetworkWriter writer, Chunk value) {
        Profiler.BeginSample("WriteChunk");
        Vector3Int key = value.GetKey();

        writer.WriteVector3Int(key);

        var voxelDataLengthBytes = value.readWriteVoxel.Length * sizeof(short);
        var colDataLengthBytes = value.color.Length * sizeof(uint);
        
        // Keep track of uncompressed byte size of voxels and colors:
        writer.WriteInt(voxelDataLengthBytes);
        writer.WriteInt(colDataLengthBytes);
        
        // Input byte array
        byte[] voxelByteAndColorArray = ArrayPool<byte>.Shared.Rent(voxelDataLengthBytes + colDataLengthBytes);
        Buffer.BlockCopy(value.readWriteVoxel, 0, voxelByteAndColorArray, 0, voxelDataLengthBytes);
        Buffer.BlockCopy(value.color, 0, voxelByteAndColorArray, voxelDataLengthBytes, colDataLengthBytes);
        
        // Compress the byte array
        Profiler.BeginSample("WriteChunk.Compress");

        var maxCompressionSize = Zstd.GetCompressionBound(voxelByteAndColorArray);
        var compressionBuffer = ArrayPool<byte>.Shared.Rent(maxCompressionSize);
        var voxelDataCompressedSize = zstd.Compress(voxelByteAndColorArray, compressionBuffer);
        writer.WriteInt(voxelDataCompressedSize);
        writer.WriteBytes(compressionBuffer, 0, voxelDataCompressedSize);
        
        ArrayPool<byte>.Shared.Return(voxelByteAndColorArray);
        ArrayPool<byte>.Shared.Return(compressionBuffer);
        
        // Custom Data
        writer.WriteUInt((uint)value.customDataMap.Count);
        foreach (var kvp in value.customDataMap) {
            writer.WriteUShort(kvp.Key);
            writer.WriteBinaryBlob(kvp.Value);
        }
        
        // Damage Map
        writer.WriteUInt((uint)value.damageMap.Count);
        foreach (var kvp in value.damageMap) {
            writer.WriteUShort(kvp.Key);
            writer.WriteFloat(kvp.Value);
        }
        
        Profiler.EndSample();
        Profiler.EndSample();
    }

    public static Chunk ReadChunk(this NetworkReader reader) {
        //create it from the reader
        Vector3Int key = reader.ReadVector3Int();

        var voxelDataLength = reader.ReadInt();
        var colorDataLength = reader.ReadInt();
        var compressedBytesLen = reader.ReadInt();

        Chunk chunk = VoxelWorld.CreateChunk(key);

        byte[] voxelByteAndColorArray = ArrayPool<byte>.Shared.Rent(compressedBytesLen);
        
        reader.ReadBytes(voxelByteAndColorArray, compressedBytesLen);
        var decompressedData = ArrayPool<byte>.Shared.Rent(Zstd.GetDecompressionBound(voxelByteAndColorArray));
        zstd.Decompress(new ReadOnlySpan<byte>(voxelByteAndColorArray, 0, compressedBytesLen), decompressedData);
        
        Buffer.BlockCopy(decompressedData, 0, chunk.readWriteVoxel, 0, voxelDataLength);
        Buffer.BlockCopy(decompressedData, voxelDataLength, chunk.color, 0, colorDataLength);
        
        ArrayPool<byte>.Shared.Return(voxelByteAndColorArray);
        ArrayPool<byte>.Shared.Return(decompressedData);
        
        // Custom Data
        var customData = new Dictionary<ushort, BinaryBlob>();
        int customCount = (int)reader.ReadUInt();
        for (int i = 0; i < customCount; i++) {
            customData.Add(reader.ReadUShort(), reader.ReadBinaryBlob());
        }
        chunk.customDataMap = customData;
        
        // Damage Map
        var damage = new Dictionary<ushort, float>();
        int damageCount = (int)reader.ReadUInt();
        for (int i = 0; i < damageCount; i++) {
            damage.Add(reader.ReadUShort(), reader.ReadFloat());
        }
        chunk.damageMap = damage;
        
        chunk.MarkKeysWithVoxelsDirty();
        return chunk;
    }
}
