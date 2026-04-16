using System.Collections.Generic;
using System.Numerics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using Vector3 = UnityEngine.Vector3;

namespace VoxelWorldStuff {
    public struct GreedyMeshRegion {
        public Vector3 minCorner;
        public Vector3 size;
        public ushort value;
    }
    
    public static class VoxelWorldCollision {
        private struct CollisionDescriptor {
            public Vector3 position;
            public Vector3Int size;
        }
        
        private static readonly List<Vector3> meshVertices = new();
        private static readonly List<int> meshTriangles = new();

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

            if (src.collisionMeshCollider != null) src.collisionMeshCollider.sharedMesh = null;
            if (src.collisionMesh != null) src.collisionMesh.Clear();
        }

        private static bool[] used = new bool[VoxelWorld.chunkSize * VoxelWorld.chunkSize * VoxelWorld.chunkSize];
        public static void MakeCollision(Chunk src, bool temporary = false) {
            GameObject obj = src.GetGameObject();
            if (obj == null)
            {
                return;
            }

            // Normal rebuilds clear any resim suppressions. Temporary rebuilds preserve them
            // (used by RemoveSingleVoxelCollision after adding a new suppression).
            if (!temporary) src.suppressedCollisionPositions.Clear();

            // Clear used array
            unsafe {
                fixed (bool* usedPtr = used) UnsafeUtility.MemSet(usedPtr, 0, used.Length);
            }

            List<CollisionDescriptor> collisions = new List<CollisionDescriptor>();

            //greedily convert collision into box colliders
            for (int x = 0; x < VoxelWorld.chunkSize; x++) {
                for (int y = 0; y < VoxelWorld.chunkSize; y++) {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++) {
                        var voxelAtPos = src.GetLocalVoxelDataAt(x, y, z);
                        if (!IsVoxelUsed(x, y, z, used) && voxelAtPos > 0) {
                            if (src.world.GetCollisionType(voxelAtPos) != VoxelBlocks.CollisionType.Solid) continue; // No collision for this block
                            if (src.suppressedCollisionPositions.Count > 0 &&
                                src.suppressedCollisionPositions.Contains(new Vector3Int(x, y, z))) continue;

                            //grow a box from this point
                            Vector3Int size = new Vector3Int(1, 1, 1);
                            Vector3Int origin = new Vector3Int(x, y, z);

                            while (GrowY(origin, size, src, 0, used) == true) { size.y += 1; } //Grow y Axis first for tall blocks
                            while (GrowX(origin, size, src, 0, used) == true) { size.x += 1; }
                            while (GrowZ(origin, size, src, 0, used) == true) { size.z += 1; }
                            
                            MarkAllVoxelsUsed(origin, size, used);

                            //Output a collider
                            collisions.Add(new CollisionDescriptor() {
                                position = src.bottomLeftInt + new Vector3(origin.x + size.x * 0.5f, origin.y + size.y * 0.5f, origin.z + size.z * 0.5f),
                                size = size,
                            });
                        }
                    }
                }
            }

            // Bake all merged boxes into a single MeshCollider for this chunk.
            BuildCollisionMesh(src, collisions);
        }

        private static void BuildCollisionMesh(Chunk src, List<CollisionDescriptor> collisions) {
            if (src.collisionMesh == null) {
                src.collisionMesh = new Mesh {
                    name = "VoxelWorldChunkCollision",
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                };
            }
            if (src.collisionMeshCollider == null) {
                var obj = src.GetGameObject();
                var mc = obj.GetComponent<MeshCollider>();
                if (mc == null) mc = obj.AddComponent<MeshCollider>();
                mc.convex = false;
                src.collisionMeshCollider = mc;
            }

            meshVertices.Clear();
            meshTriangles.Clear();

            for (int i = 0; i < collisions.Count; i++) {
                var d = collisions[i];
                float halfX = d.size.x * 0.5f;
                float halfY = d.size.y * 0.5f;
                float halfZ = d.size.z * 0.5f;
                float minX = d.position.x - halfX;
                float minY = d.position.y - halfY;
                float minZ = d.position.z - halfZ;
                float maxX = d.position.x + halfX;
                float maxY = d.position.y + halfY;
                float maxZ = d.position.z + halfZ;

                int v = meshVertices.Count;
                meshVertices.Add(new Vector3(minX, minY, minZ)); // 0
                meshVertices.Add(new Vector3(maxX, minY, minZ)); // 1
                meshVertices.Add(new Vector3(maxX, maxY, minZ)); // 2
                meshVertices.Add(new Vector3(minX, maxY, minZ)); // 3
                meshVertices.Add(new Vector3(minX, minY, maxZ)); // 4
                meshVertices.Add(new Vector3(maxX, minY, maxZ)); // 5
                meshVertices.Add(new Vector3(maxX, maxY, maxZ)); // 6
                meshVertices.Add(new Vector3(minX, maxY, maxZ)); // 7

                // -z face
                meshTriangles.Add(v + 0); meshTriangles.Add(v + 2); meshTriangles.Add(v + 1);
                meshTriangles.Add(v + 0); meshTriangles.Add(v + 3); meshTriangles.Add(v + 2);
                // +z face
                meshTriangles.Add(v + 5); meshTriangles.Add(v + 7); meshTriangles.Add(v + 4);
                meshTriangles.Add(v + 5); meshTriangles.Add(v + 6); meshTriangles.Add(v + 7);
                // -x face
                meshTriangles.Add(v + 4); meshTriangles.Add(v + 3); meshTriangles.Add(v + 0);
                meshTriangles.Add(v + 4); meshTriangles.Add(v + 7); meshTriangles.Add(v + 3);
                // +x face
                meshTriangles.Add(v + 1); meshTriangles.Add(v + 6); meshTriangles.Add(v + 2);
                meshTriangles.Add(v + 1); meshTriangles.Add(v + 5); meshTriangles.Add(v + 6);
                // -y face
                meshTriangles.Add(v + 4); meshTriangles.Add(v + 1); meshTriangles.Add(v + 5);
                meshTriangles.Add(v + 4); meshTriangles.Add(v + 0); meshTriangles.Add(v + 1);
                // +y face
                meshTriangles.Add(v + 3); meshTriangles.Add(v + 6); meshTriangles.Add(v + 2);
                meshTriangles.Add(v + 3); meshTriangles.Add(v + 7); meshTriangles.Add(v + 6);
            }

            var mesh = src.collisionMesh;
            mesh.Clear();

            // An empty mesh (air chunk, or every solid voxel suppressed) can't be assigned to
            // sharedMesh — Unity logs an error for zero-vertex mesh colliders. Leave sharedMesh
            // null in that case so the collider is simply inert.
            if (meshVertices.Count == 0) {
                src.collisionMeshCollider.sharedMesh = null;
                return;
            }

            // Worst case unmerged: chunkSize^3 boxes * 8 verts = 32768 (fits in UInt16).
            mesh.indexFormat = IndexFormat.UInt16;
            mesh.SetVertices(meshVertices);
            mesh.SetTriangles(meshTriangles, 0, false);
            int cs = VoxelWorld.chunkSize;
            mesh.bounds = new Bounds(
                new Vector3(src.bottomLeftInt.x + cs * 0.5f, src.bottomLeftInt.y + cs * 0.5f, src.bottomLeftInt.z + cs * 0.5f),
                new Vector3(cs, cs, cs));

            // Null-then-set forces PhysX to re-bake the mesh's collision data — Unity does
            // not always pick up geometry mutations when the reference is unchanged.
            src.collisionMeshCollider.sharedMesh = null;
            src.collisionMeshCollider.sharedMesh = mesh;
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

                    var voxelAt = src.GetLocalVoxelDataAt(xx, yy, zz);

                    if (src.world.GetCollisionType(voxelAt) != VoxelBlocks.CollisionType.Solid) return false; // No collision for this block
                    if (targetVoxel == 0 && voxelAt == 0) return false; // We're targeting any block but no block is found
                    if (targetVoxel > 0 && targetVoxel != voxelAt) return false; // This is not our target voxel
                    if (src.suppressedCollisionPositions.Count > 0 &&
                        src.suppressedCollisionPositions.Contains(new Vector3Int(xx, yy, zz))) return false;
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
                    var voxelAt = src.GetLocalVoxelDataAt(xx, yy, zz);

                    if (src.world.GetCollisionType(voxelAt) != VoxelBlocks.CollisionType.Solid) return false; // No collision for this block
                    if (targetVoxel == 0 && voxelAt == 0) return false; // We're targeting any block but no block is found
                    if (targetVoxel > 0 && targetVoxel != voxelAt) return false; // This is not our target voxel
                    if (src.suppressedCollisionPositions.Count > 0 &&
                        src.suppressedCollisionPositions.Contains(new Vector3Int(xx, yy, zz))) return false;
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

                    var voxelAt = src.GetLocalVoxelDataAt(xx, yy, zz);

                    if (src.world.GetCollisionType(voxelAt) != VoxelBlocks.CollisionType.Solid) return false; // No collision for this block
                    if (targetVoxel == 0 && voxelAt == 0) return false; // We're targeting any block but no block is found
                    if (targetVoxel > 0 && targetVoxel != voxelAt) return false; // This is not our target voxel
                    if (src.suppressedCollisionPositions.Count > 0 &&
                        src.suppressedCollisionPositions.Contains(new Vector3Int(xx, yy, zz))) return false;
                }
            }
            return true;
        }

        // With the single-MeshCollider approach the baked mesh cannot be split cheaply. If a sidecar
        // BoxCollider was added by WriteTemporaryCollision(true), destroy it. Otherwise the voxel
        // lives inside the baked mesh — add it to the suppression set and re-bake in place.
        public static void RemoveSingleVoxelCollision(Chunk chunk, Vector3 pos) {
            for (int i = 0; i < chunk.colliders.Count; i++) {
                var bc = chunk.colliders[i];
                if (bc == null) continue;
                if (bc.center == pos && bc.size == Vector3.one) {
                    if (Application.isPlaying) Object.Destroy(bc);
                    else Object.DestroyImmediate(bc);
                    chunk.colliders.RemoveAt(i);
                    return;
                }
            }

            var local = Vector3Int.FloorToInt(pos) - chunk.bottomLeftInt;
            if (local.x >= 0 && local.x < VoxelWorld.chunkSize &&
                local.y >= 0 && local.y < VoxelWorld.chunkSize &&
                local.z >= 0 && local.z < VoxelWorld.chunkSize) {
                chunk.suppressedCollisionPositions.Add(local);
            }
            MakeCollision(chunk, temporary: true);
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
