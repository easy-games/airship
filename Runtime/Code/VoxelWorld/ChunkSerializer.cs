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
        
        writer.WriteInt(voxelDataLengthBytes);
        writer.WriteInt(colDataLengthBytes);
        
        // Input byte array
        byte[] byteArray = ArrayPool<byte>.Shared.Rent(voxelDataLengthBytes);
        Buffer.BlockCopy(value.readWriteVoxel, 0, byteArray, 0, byteArray.Length);
        byte[] colorArray = ArrayPool<byte>.Shared.Rent(colDataLengthBytes);
        Buffer.BlockCopy(value.color, 0, colorArray, 0, colorArray.Length);

        // Compress the byte array
        Profiler.BeginSample("WriteChunk.Compress");

        var byteArrayCompressed = zstd.Compress(byteArray, 0, byteArray.Length);
        writer.WriteBytes(byteArrayCompressed, 0, byteArrayCompressed.Length);
            
        var colorArrayCompressed = zstd.Compress(colorArray, 0, colorArray.Length);
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

        Debug.Log("voxel data length: " + voxelDataLength);
        byte[] byteArray = ArrayPool<byte>.Shared.Rent(voxelDataLength);
        reader.ReadBytes(byteArray, voxelDataLength);
        byteArray = zstd.Decompress(byteArray, 0, voxelDataLength);
        Buffer.BlockCopy(byteArray, 0, chunk.readWriteVoxel, 0, voxelDataLength);
        ArrayPool<byte>.Shared.Return(byteArray);
        
        // todo: color array
        
        // byte[] byteArray = reader.ReadArray<byte>();
        // using (MemoryStream compressedStream = new MemoryStream(byteArray)) {
        //     using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress)) {
        //         using (MemoryStream outputStream = new MemoryStream()) {
        //             deflateStream.CopyTo(outputStream);
        //             // chunk.readWriteVoxel = outputStream.ToArray();
        //             var output = outputStream.ToArray();
        //             Buffer.BlockCopy(output, 0, chunk.readWriteVoxel, 0, voxelDataLength);
        //             Buffer.BlockCopy(output, voxelDataLength, chunk.color, 0, colorDataLength);
        //         }
        //     }
        // }
        chunk.MarkKeysWithVoxelsDirty();
        return chunk;
    }
}