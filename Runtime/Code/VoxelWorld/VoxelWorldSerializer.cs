using Mirror;
using VoxelWorldStuff;


//Designed to copy the data from a VoxelWorld to mirror
public static class VoxelWorldSerializer
{
    public static void SerializeVoxelWorld(this NetworkWriter writer, VoxelWorld world)
    {

        writer.Write(world.chunks.Count);

        foreach (var chunkKV in world.chunks)
        {
            ChunkSerializer.WriteChunk(writer, chunkKV.Value);
        }
               
    }

    public static void DeserializeVoxelWorld(this NetworkReader reader, VoxelWorld world)
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
