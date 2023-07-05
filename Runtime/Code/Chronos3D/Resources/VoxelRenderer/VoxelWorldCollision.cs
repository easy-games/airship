using FishNet.Component.ColliderRollback;
using UnityEngine;


namespace VoxelWorldStuff
{


    public static class VoxelWorldCollision
    {
        public static void ClearCollision(Chunk src)
        {
            src.colliders.Clear();
            GameObject obj = src.GetGameObject();
            if (obj == null)
            {
                return;
            }
            //clear all the boxColliders
            if (Application.isPlaying)
            {
                BoxCollider[] colliders = obj.GetComponents<BoxCollider>();
                foreach (BoxCollider collider in colliders)
                {
                    Object.Destroy(collider);
                }
            }
            else
            {
                BoxCollider[] colliders = obj.GetComponents<BoxCollider>();
                foreach (BoxCollider collider in colliders)
                {
                    Object.DestroyImmediate(collider);
                }
            }
            

        }
            
        public static void MakeCollision(Chunk src)
        {
            GameObject obj = src.GetGameObject();
            if (obj == null)
            {
                return;
            }

            //allocate new bytes
            bool[] collision = new bool[VoxelWorld.chunkSize * VoxelWorld.chunkSize * VoxelWorld.chunkSize];

            //copy 
            for (int x = 0; x < VoxelWorld.chunkSize; x++)
            {
                for (int y = 0; y < VoxelWorld.chunkSize; y++)
                {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++)
                    {
                        if (VoxelWorld.VoxelIsSolid(src.GetLocalVoxelAt(x, y, z)) == true) 
                        {
                            collision[x + y * VoxelWorld.chunkSize + z * VoxelWorld.chunkSize * VoxelWorld.chunkSize] = true;
                        }
                    }
                }
            }

            //greedily convert collision into box colliders
            for (int x = 0; x < VoxelWorld.chunkSize; x++)
            {
                for (int y = 0; y < VoxelWorld.chunkSize; y++)
                {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++)
                    {
                        if (collision[x + y * VoxelWorld.chunkSize + z * VoxelWorld.chunkSize * VoxelWorld.chunkSize] == true)
                        {
                            //grow a box from this point
                            Vector3Int size = new Vector3Int(1, 1, 1);
                            Vector3Int origin = new Vector3Int(x, y, z);
                                                        
                            while (GrowY(origin, size, collision) == true) { size.y += 1; } //Grow y Axis first for tall blocks
                            while (GrowX(origin, size, collision) == true) { size.x += 1; }
                            while (GrowZ(origin, size, collision) == true) { size.z += 1; }
                            
                            //Was all good, clear these voxels and continue
                            ClearVoxels(origin, size, collision);

                            //Output a collider
                            MakeCollider(src, src.bottomLeftInt + new Vector3(origin.x + size.x * 0.5f, origin.y + size.y * 0.5f, origin.z + size.z * 0.5f), size);
                        }
                    }
                }
            }
        }
        private static bool ClearVoxels(Vector3Int origin, Vector3Int size, bool[] voxels)
        {
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        int xx = origin.x + x;
                        int yy = origin.y + y;
                        int zz = origin.z + z;

                        voxels[xx + yy * VoxelWorld.chunkSize + zz * VoxelWorld.chunkSize * VoxelWorld.chunkSize] = false;
                    }
                }
            }
            return true;
        }

        private static bool GrowX(Vector3Int origin, Vector3Int size, bool[] voxels)
        {
            //check the x face
         
            //Done?
            if (origin.x + size.x + 1 > VoxelWorld.chunkSize)
            {
                return false;
            }

            for (int y = 0; y < size.y; y++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    int xx = origin.x + size.x;
                    int yy = origin.y + y;
                    int zz = origin.z + z;

                    //Check if any voxels are empty
                    if (voxels[xx + yy * VoxelWorld.chunkSize + zz * VoxelWorld.chunkSize * VoxelWorld.chunkSize] == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

  
        private static bool GrowY(Vector3Int origin, Vector3Int size, bool[] voxels)
        {
            //check the y face
            //Done?
            if (origin.y + size.y + 1 > VoxelWorld.chunkSize)
            {
                return false;
            }
            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    int xx = origin.x + x;
                    int yy = origin.y + size.y;
                    int zz = origin.z + z;

                    //Check if any voxels are empty
                    if (voxels[xx + yy * VoxelWorld.chunkSize + zz * VoxelWorld.chunkSize * VoxelWorld.chunkSize] == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool GrowZ(Vector3Int origin, Vector3Int size, bool[] voxels)
        {
            //check the z face
            //Done?
            if (origin.z + size.z + 1> VoxelWorld.chunkSize)
            {
                return false;
            }
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    int xx = origin.x + x;
                    int yy = origin.y + y;
                    int zz = origin.z + size.z;

                    //Check if any voxels are empty
                    if (voxels[xx + yy * VoxelWorld.chunkSize + zz * VoxelWorld.chunkSize * VoxelWorld.chunkSize] == false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static void MakeCollider(Chunk chunk, Vector3 pos, Vector3Int size)
        {
            BoxCollider col = chunk.GetGameObject().AddComponent<BoxCollider>();
            col.size = size;
            col.center = pos;
            chunk.colliders.Add(col);
        }


    }

}
