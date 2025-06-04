using System.Collections.Generic;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace VoxelWorldStuff {
    public struct GreedyMeshRegion {
        public Vector3 minCorner;
        public Vector3 size;
        public ushort value;
    }
    
    public static class VoxelWorldCollision {
        // Pre-allocated array to check for overlap results
        private static Collider[] chunkOverlapResults = new Collider[1];
        
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
            bool[] used = new bool[VoxelWorld.chunkSize * VoxelWorld.chunkSize * VoxelWorld.chunkSize];
            
            //greedily convert collision into box colliders
            for (int x = 0; x < VoxelWorld.chunkSize; x++)
            {
                for (int y = 0; y < VoxelWorld.chunkSize; y++)
                {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++) {
                        var voxelAtPos = src.GetLocalVoxelAt(x, y, z);
                        if (!IsVoxelUsed(x, y, z, used) && voxelAtPos > 0) {
                            if (src.world.GetCollisionType(voxelAtPos) != VoxelBlocks.CollisionType.Solid) continue; // No collision for this block
                            
                            //grow a box from this point
                            Vector3Int size = new Vector3Int(1, 1, 1);
                            Vector3Int origin = new Vector3Int(x, y, z);
                                                        
                            while (GrowY(origin, size, src, 0, used) == true) { size.y += 1; } //Grow y Axis first for tall blocks
                            while (GrowX(origin, size, src, 0, used) == true) { size.x += 1; }
                            while (GrowZ(origin, size, src, 0, used) == true) { size.z += 1; }
                            
                            MarkAllVoxelsUsed(origin, size, used);

                            //Output a collider
                            MakeCollider(src, src.bottomLeftInt + new Vector3(origin.x + size.x * 0.5f, origin.y + size.y * 0.5f, origin.z + size.z * 0.5f), size);
                        }
                    }
                }
            }
        }

        public static List<GreedyMeshRegion> GreedyMesh(Chunk src) {
            var meshes = new List<GreedyMeshRegion>();
            GameObject obj = src.GetGameObject();
            if (obj == null) {
                return meshes;
            }
            
            var usedVoxels = new bool[VoxelWorld.chunkSize * VoxelWorld.chunkSize * VoxelWorld.chunkSize];

            //greedily convert collision into box colliders
            /*
            for (int x = 0; x < VoxelWorld.chunkSize; x++) {
                for (int y = 0; y < VoxelWorld.chunkSize; y++) {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++) {
                        var localVoxel = src.GetLocalVoxelAt(x, y, z);
                        var used = IsVoxelUsed(x, y, z, usedVoxels);
                        if (used) continue;
                        
                        if (localVoxel > 0) {
                            //grow a box from this point
                            var size = new Vector3Int(1, 1, 1);
                            var origin = new Vector3Int(x, y, z);

                            while (GrowY(origin, size, src, localVoxel, usedVoxels)) size.y++; // Grow y Axis first for tall blocks
                            while (GrowX(origin, size, src, localVoxel, usedVoxels)) size.x++;
                            while (GrowZ(origin, size, src, localVoxel, usedVoxels)) size.z++;

                            //Was all good, clear these voxels and continue
                            MarkAllVoxelsUsed(origin, size, usedVoxels);

                            //Output a collider
                            MakeCollider(src,
                                src.bottomLeftInt + new Vector3(origin.x + size.x * 0.5f, origin.y + size.y * 0.5f,
                                    origin.z + size.z * 0.5f), size);
                        }
                    }
                }
            }
            */
            return meshes;
        }
        private static void MarkAllVoxelsUsed(Vector3Int origin, Vector3Int size, bool[] usedVoxels)
        {
            for (int x = 0; x < size.x; x++) {
                for (int y = 0; y < size.y; y++) {
                    for (int z = 0; z < size.z; z++) {
                        int xx = origin.x + x;
                        int yy = origin.y + y;
                        int zz = origin.z + z;

                        SetVoxelUsed(xx, yy, zz, usedVoxels, true);
                    }
                }
            }
        }

        private static bool IsVoxelUsed(int x, int y, int z, bool[] usedVoxels) {
            return usedVoxels[GetUsedVoxelIndex(x, y, z)];
        }
        
        private static void SetVoxelUsed(int x, int y, int z, bool[] usedVoxels, bool used) {
            usedVoxels[GetUsedVoxelIndex(x, y, z)] = used;
        }

        private static int GetUsedVoxelIndex(int x, int y, int z) {
            return x + y * VoxelWorld.chunkSize + z * VoxelWorld.chunkSize * VoxelWorld.chunkSize;
        }

        /// <param name="targetVoxel">If target voxel is 0 we will not target a specific voxel type for growth, instead we'll just check that a voxel exists (non-zero)</param>
        private static bool GrowX(Vector3Int origin, Vector3Int size, Chunk src, ushort targetVoxel, bool[] usedVoxels)
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
                    
                    if (IsVoxelUsed(xx, yy, zz, usedVoxels)) return false;

                    var voxelAt = src.GetLocalVoxelAt(xx, yy, zz);
                    
                    if (src.world.GetCollisionType(voxelAt) != VoxelBlocks.CollisionType.Solid) return false; // No collision for this block
                    if (targetVoxel == 0 && voxelAt == 0) return false; // We're targeting any block but no block is found
                    if (targetVoxel > 0 && targetVoxel != voxelAt) return false; // This is not our target voxel
                }
            }
            return true;
        }

  

        /// <param name="targetVoxel">If target voxel is 0 we will not target a specific voxel type for growth, instead we'll just check that a voxel exists (non-zero)</param>
        private static bool GrowY(Vector3Int origin, Vector3Int size, Chunk src, ushort targetVoxel, bool[] usedVoxels) {
            if (origin.y + size.y + 1 > VoxelWorld.chunkSize) return false;
            for (int x = 0; x < size.x; x++) {
                for (int z = 0; z < size.z; z++) {
                    int xx = origin.x + x;
                    int yy = origin.y + size.y;
                    int zz = origin.z + z;

                    if (IsVoxelUsed(xx, yy, zz, usedVoxels)) return false;
                    var voxelAt = src.GetLocalVoxelAt(xx, yy, zz);
                    
                    if (src.world.GetCollisionType(voxelAt) != VoxelBlocks.CollisionType.Solid) return false; // No collision for this block
                    if (targetVoxel == 0 && voxelAt == 0) return false; // We're targeting any block but no block is found
                    if (targetVoxel > 0 && targetVoxel != voxelAt) return false; // This is not our target voxel
                }
            }
            return true;
        }

        /// <param name="targetVoxel">If target voxel is 0 we will not target a specific voxel type for growth, instead we'll just check that a voxel exists (non-zero)</param>
        private static bool GrowZ(Vector3Int origin, Vector3Int size, Chunk src, ushort targetVoxel, bool[] usedVoxels)
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
                    
                    if (IsVoxelUsed(xx, yy, zz, usedVoxels)) return false;

                    var voxelAt = src.GetLocalVoxelAt(xx, yy, zz);
                    
                    if (src.world.GetCollisionType(voxelAt) != VoxelBlocks.CollisionType.Solid) return false; // No collision for this block
                    if (targetVoxel == 0 && voxelAt == 0) return false; // We're targeting any block but no block is found
                    if (targetVoxel > 0 && targetVoxel != voxelAt) return false; // This is not our target voxel
                }
            }
            return true;
        }

        public static void RemoveSingleVoxelCollision(Chunk chunk, Vector3 pos) {
            var chunkGO = chunk.GetGameObject();
            var resultCount = Physics.OverlapBoxNonAlloc(pos, Vector3.one / 3, chunkOverlapResults, chunkGO.transform.rotation, 1 << chunkGO.layer, QueryTriggerInteraction.Ignore);
            if (resultCount == 0) return; // Already no collider here

            var colliderToSplit = chunkOverlapResults[0];
            if (colliderToSplit is not BoxCollider bc) return;

            var bcSize = bc.size;
            var bcCenter = bc.center;
            
            // Pos adjusted so 0,0,0 is the min corner of the size of the collider
            var minCorner = (bcCenter - bcSize / 2);
            var posRelativeToSize = Vector3Int.FloorToInt(pos - minCorner);
            
            // Debug.Log($"Splitting! posRelativeToSize={posRelativeToSize} bcSize={bcSize} minCorner={minCorner} bcCenter={bcCenter}");
            
            var bcComp = new GameObject("ComponentTrue");
            bcComp.transform.localScale = bcSize;
            bcComp.transform.position = bcCenter;
            
            // Create 6 new colliders split off
            if (posRelativeToSize.x > 0) {
                var size = new Vector3(posRelativeToSize.x, bcSize.y, bcSize.z);
                var center = new Vector3(minCorner.x + posRelativeToSize.x / 2f, bcCenter.y, bcCenter.z);
                MakeCollider(chunk, center, Vector3Int.FloorToInt(size));
            }
            if (posRelativeToSize.x < bcSize.x - 1) {
                var size = new Vector3(bcSize.x - posRelativeToSize.x - 1, bcSize.y, bcSize.z);
                var center = new Vector3(minCorner.x + (posRelativeToSize.x + 1) + (bcSize.x - (posRelativeToSize.x + 1)) / 2, bcCenter.y, bcCenter.z);
                MakeCollider(chunk, center, Vector3Int.FloorToInt(size));
            }
            if (posRelativeToSize.y > 0) {
                var size = new Vector3(1, posRelativeToSize.y, bcSize.z);
                var center = new Vector3(minCorner.x + posRelativeToSize.x + 0.5f, minCorner.y + posRelativeToSize.y / 2f, bcCenter.z);
                MakeCollider(chunk, center, Vector3Int.FloorToInt(size));
            }
            if (posRelativeToSize.y < bcSize.y - 1) {
                var size = new Vector3(1, bcSize.y - posRelativeToSize.y - 1, bcSize.z);
                var center = new Vector3(minCorner.x + posRelativeToSize.x + 0.5f, minCorner.y + (posRelativeToSize.y + 1) + (bcSize.y - (posRelativeToSize.y + 1)) / 2, bcCenter.z);
                MakeCollider(chunk, center, Vector3Int.FloorToInt(size));
            }
            if (posRelativeToSize.z > 0) {
                var size = new Vector3(1, 1, posRelativeToSize.z);
                var center = new Vector3(minCorner.x + posRelativeToSize.x + 0.5f, minCorner.y + posRelativeToSize.y + 0.5f, minCorner.z + posRelativeToSize.z / 2f);
                MakeCollider(chunk, center, Vector3Int.FloorToInt(size));
            }
            if (posRelativeToSize.z < bcSize.z - 1) {
                var size = new Vector3(1, 1, bcSize.z - posRelativeToSize.z - 1);
                var center = new Vector3(minCorner.x + posRelativeToSize.x + 0.5f, minCorner.y + posRelativeToSize.y + 0.5f, minCorner.z + (posRelativeToSize.z + 1) + (bcSize.z - (posRelativeToSize.z + 1)) / 2);
                MakeCollider(chunk, center, Vector3Int.FloorToInt(size));
            }
            Object.Destroy(bc);
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
