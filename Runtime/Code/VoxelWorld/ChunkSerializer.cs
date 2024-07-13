using System;
using System.IO;
using System.IO.Compression;
using FishNet.Serializing;
using UnityEngine;
using VoxelWorldStuff;

public static class ChunkSerializer {

    public static void WriteChunk(this Writer writer, Chunk value)
    {
        Vector3Int key = value.GetKey();

        writer.WriteVector3Int(key);

        // Input byte array
        byte[] byteArray = new byte[value.readWriteVoxel.Length * sizeof(short)];
        Buffer.BlockCopy(value.readWriteVoxel, 0, byteArray, 0, byteArray.Length);

        // Compress the byte array
        byte[] compressedBytes;
        using (MemoryStream ms = new MemoryStream())
        {
            using (DeflateStream deflateStream = new DeflateStream(ms, CompressionMode.Compress))
            {
                deflateStream.Write(byteArray, 0, byteArray.Length);
            }
            compressedBytes = ms.ToArray();
        }
        writer.WriteArray(compressedBytes);
    }

    public static Chunk ReadChunk(this Reader reader)
    {
        //create it from the reader
        Vector3Int key = reader.ReadVector3Int();

        Chunk chunk = new Chunk(key);

        byte[] byteArray = new byte[16 * 16 * 16 * 2]; // 2 because they are shorts
        reader.ReadArray(ref byteArray);
        using (MemoryStream compressedStream = new MemoryStream(byteArray))
        {
            using (DeflateStream deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                using (MemoryStream outputStream = new MemoryStream()) {
                    deflateStream.CopyTo(outputStream);
                    // chunk.readWriteVoxel = outputStream.ToArray();
                    var output = outputStream.ToArray();
                    Buffer.BlockCopy(output, 0, chunk.readWriteVoxel, 0, output.Length);
                }
            }
        }
        return chunk;
    }
}