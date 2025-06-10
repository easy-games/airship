using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
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
        
        // Input byte array
        byte[] byteArray = ArrayPool<byte>.Shared.Rent(voxelDataLengthBytes);
        byte[] colorArray = ArrayPool<byte>.Shared.Rent(colDataLengthBytes);
        Buffer.BlockCopy(value.readWriteVoxel, 0, byteArray, 0, voxelDataLengthBytes);
        Buffer.BlockCopy(value.color, 0, colorArray, 0, colDataLengthBytes);

        // Compress the byte array
        Profiler.BeginSample("WriteChunk.Compress");

        var byteArrayCompressed = zstd.Compress(byteArray, 0, voxelDataLengthBytes);
        var colorArrayCompressed = zstd.Compress(colorArray, 0, colDataLengthBytes);
        
        writer.WriteInt(byteArrayCompressed.Length);
        writer.WriteInt(colorArrayCompressed.Length);
        
        writer.WriteBytes(byteArrayCompressed, 0, byteArrayCompressed.Length);
        writer.WriteBytes(colorArrayCompressed, 0, colorArrayCompressed.Length);
        
        ArrayPool<byte>.Shared.Return(byteArray);
        ArrayPool<byte>.Shared.Return(colorArray);
        
        Profiler.EndSample();
        Profiler.EndSample();
    }

    public static Chunk ReadChunk(this NetworkReader reader) {
        //create it from the reader
        Vector3Int key = reader.ReadVector3Int();

        var voxelDataLength = reader.ReadInt();
        var colorDataLength = reader.ReadInt();

        Chunk chunk = VoxelWorld.CreateChunk(key);

        byte[] voxelByteAndColorArray = ArrayPool<byte>.Shared.Rent(voxelDataLength + colorDataLength);
        
        reader.ReadBytes(voxelByteAndColorArray, voxelDataLength + colorDataLength);
        
        var decompressedByteArray = zstd.Decompress(voxelByteAndColorArray, 0, voxelDataLength);
        var decompressedColorArray = zstd.Decompress(voxelByteAndColorArray, voxelDataLength, colorDataLength);
        
        Buffer.BlockCopy(decompressedByteArray, 0, chunk.readWriteVoxel, 0, decompressedByteArray.Length);
        Buffer.BlockCopy(decompressedColorArray, 0, chunk.color, 0, decompressedColorArray.Length);
        
        ArrayPool<byte>.Shared.Return(voxelByteAndColorArray);
        
        chunk.MarkKeysWithVoxelsDirty();
        return chunk;
    }
}