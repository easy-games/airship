using FishNet.Serializing;
using UnityEngine;
using VoxelWorldStuff;

public static class ChunkSerializer
{
    public static void WriteChunk(this Writer writer, Chunk value)
    {
        Vector3Int key = value.GetKey();

        writer.WriteInt32(key.x);
        writer.WriteInt32(key.y);
        writer.WriteInt32(key.z);

        writer.WriteInt32(value.readWriteVoxel.Length);
        for (int i = 0; i < value.readWriteVoxel.Length; i++)
        {
            byte upperByte = (byte)(value.readWriteVoxel[i] & 0xFF00 >> 8);
            byte lowerByte = (byte)(value.readWriteVoxel[i] & 0x00FF);
            writer.WriteByte(upperByte);
            writer.WriteByte(lowerByte);
        }
    }

    public static Chunk ReadChunk(this Reader reader)
    {
        //create it from the reader
        int x = reader.ReadInt32();
        int y = reader.ReadInt32();
        int z = reader.ReadInt32();
        Vector3Int key = new Vector3Int(x, y, z);

        Chunk chunk = new Chunk(key);

        //parse the bytes
        int length = reader.ReadInt32();
        chunk.readWriteVoxel = new ushort[length];
        for (int i = 0; i < length; i++)
        {
            byte upperByte = reader.ReadByte();
            byte lowerByte = reader.ReadByte();
            chunk.readWriteVoxel[i] = (ushort)((upperByte << 8) | lowerByte);
        }
        return chunk;
    }
}