using System;
using System.IO;
using System.IO.Compression;
using Mirror;
using UnityEngine;
using VoxelWorldStuff;

public static class ChunkSerializer {

    public static void WriteChunk(this NetworkWriter writer, Chunk value) {
        Vector3Int key = value.GetKey();

        writer.WriteVector3Int(key);

        var voxelDataLengthBytes = value.readWriteVoxel.Length * sizeof(short);
        var colDataLengthBytes = value.color.Length * sizeof(uint);
        
        writer.WriteInt(voxelDataLengthBytes);
        writer.WriteInt(colDataLengthBytes);
        
        // Input byte array
        byte[] byteArray = new byte[voxelDataLengthBytes];
        Buffer.BlockCopy(value.readWriteVoxel, 0, byteArray, 0, byteArray.Length);
        byte[] colArray = new byte[colDataLengthBytes];
        Buffer.BlockCopy(value.color, 0, colArray, 0, colArray.Length);

        // Compress the byte array
        byte[] compressedBytes;
        using (MemoryStream ms = new MemoryStream()) {
            using (DeflateStream deflateStream = new DeflateStream(ms, CompressionMode.Compress)) {
                deflateStream.Write(byteArray, 0, byteArray.Length);
                deflateStream.Write(colArray, 0, colArray.Length);
            }
            compressedBytes = ms.ToArray();
        }
        writer.WriteArray(compressedBytes);
    }

    public static Chunk ReadChunk(this NetworkReader reader) {
        //create it from the reader
        Vector3Int key = reader.ReadVector3Int();

        var voxelDataLength = reader.ReadInt();
        var colorDataLength = reader.ReadInt();

        Chunk chunk = VoxelWorld.CreateChunk(key);

        // byte[] byteArray = new byte[16 * 16 * 16 * 2]; // 2 because they are shorts
        byte[] byteArray = reader.ReadArray<byte>();
        using (MemoryStream compressedStream = new MemoryStream(byteArray)) {
            using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress)) {
                using (MemoryStream outputStream = new MemoryStream()) {
                    deflateStream.CopyTo(outputStream);
                    // chunk.readWriteVoxel = outputStream.ToArray();
                    var output = outputStream.ToArray();
                    Buffer.BlockCopy(output, 0, chunk.readWriteVoxel, 0, voxelDataLength);
                    Buffer.BlockCopy(output, voxelDataLength, chunk.color, 0, colorDataLength);
                }
            }
        }
        chunk.MarkKeysWithVoxelsDirty();
        return chunk;
    }
}