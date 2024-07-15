using FishNet.Serializing;
using UnityEngine;
using VoxelWorldStuff;


//Designed to copy the data from a VoxelWorld to fishnet
public static class VoxelWorldSerializer
{
    public static void SerializeVoxelWorld(Writer writer, VoxelWorld world)
    {

        writer.Write(world.chunks.Count);

        foreach (var chunkKV in world.chunks)
        {
            ChunkSerializer.WriteChunk(writer, chunkKV.Value);
        }
               
    }

    public static void DeserializeVoxelWorld(this Reader reader, VoxelWorld world)
    {
        //Deserialize it and pack it into world
        world.chunks.Clear();

        int count = reader.Read<int>();

        for (int j = 0; j < count; j++)
        {
            Chunk chunk = ChunkSerializer.ReadChunk(reader);
            chunk.SetWorld(world);
            world.AddChunk(chunk.chunkKey, chunk);
        }
    }

}
