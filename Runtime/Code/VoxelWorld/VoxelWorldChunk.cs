using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using System.Runtime.CompilerServices;
using UnityEditor.Rendering;
using UnityEngine.Profiling;

namespace VoxelWorldStuff {
    public enum SurfaceBits : byte {
        Up = 1,
        Down = 2,
        Left = 4,
        Right = 8,
        Forward = 16,
        Back = 32,
        Solid = 0xFF,
    }


    // 16 bit layout of all voxelData
    // blockId (12bits) Rot (3bits) solid (1bit)
    // 000000000000     000         0

    public class Chunk {
        
        private static readonly Vector3Int[] searchOffsets =
        {
            new Vector3Int(1, 1, 1),
            new Vector3Int(1, 1, 2), new Vector3Int(1, 1, 0),
            new Vector3Int(1, 2, 1), new Vector3Int(1, 0, 1),
            new Vector3Int(2, 1, 1), new Vector3Int(0, 1, 1),
            new Vector3Int(2, 2, 2), new Vector3Int(0, 0, 0),
            new Vector3Int(2, 2, 0), new Vector3Int(0, 0, 2),
            new Vector3Int(2, 0, 0), new Vector3Int(0, 2, 2),
            new Vector3Int(2, 0, 2), new Vector3Int(0, 2, 0),
            new Vector3Int(2, 2, 1), new Vector3Int(0, 0, 1),
            new Vector3Int(2, 1, 2), new Vector3Int(0, 1, 0),
            new Vector3Int(1, 2, 2), new Vector3Int(1, 0, 0),
            new Vector3Int(2, 1, 0), new Vector3Int(0, 1, 2),
            new Vector3Int(1, 2, 0), new Vector3Int(1, 0, 2)
        };
         
        static int chunkSize = VoxelWorld.chunkSize;

        //Permanent data
        public UInt16[] readWriteVoxel = new UInt16[chunkSize * chunkSize * chunkSize];

        //Currently instantiated prefabs
        private Dictionary<Vector3Int,GameObject> prefabObjects;

        public bool materialPropertiesDirty = true;

        public VoxelWorld world;

        public Vector3Int bottomLeftInt;
        public Bounds bounds;
        public int numUpdates = 0;

        private bool geometryDirty = true;
        private bool geometryDirtyPriorityUpdate = false;
 
        public Camera currentCamera = null;
                
        private Vector3Int _chunkKey;
        public Vector3Int chunkKey {
            get => _chunkKey;
            set {
                _chunkKey = value;
     
                 
                bottomLeftInt = (_chunkKey * chunkSize);

                Vector3 bottomLeft = (_chunkKey * chunkSize);
                Vector3 topRight = bottomLeft + new Vector3(chunkSize, chunkSize, chunkSize);

                bounds = new Bounds();
                bounds.SetMinMax(bottomLeft, topRight);
            }
        }


        private GameObject obj;

        private Mesh mesh;
        private MeshFilter filter;
        private MeshRenderer renderer;

        private GameObject[] detailGameObjects;
        private Mesh[] detailMeshes;
        private MeshFilter[] detailFilters;
        private MeshRenderer[] detailRenderers;
        

        private GameObject parent;
        public List<BoxCollider> colliders = new();

        private MeshProcessor meshProcessor;

        public Vector3Int GetKey() {
            return chunkKey;
        }

        public bool GetPriorityUpdate() {
            return geometryDirtyPriorityUpdate;
        }

        public void SetWorld(VoxelWorld world) {
            this.world = world;
            parent = world.chunksFolder.gameObject;
        }

        public void SetGeometryDirty(bool dirty, bool priority = false) {
            geometryDirty = dirty;
            if (dirty) {
           
                if (priority) {
                    geometryDirtyPriorityUpdate = true;
                }
            }
        }
 
        private int CountAirVoxelsAround(VoxelWorld world, Vector3Int checkPos) {
            int count = 0;
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    for (int z = -1; z <= 1; z++) {
                        Vector3Int pos = checkPos + new Vector3Int(x, y, z);
                        if (VoxelWorld.VoxelIsSolid(world.ReadVoxelAtInternal(pos)) == false) {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private void ClearPrefabsMainThread() {
            //If its the editor detroy

            if (prefabObjects == null) {
                return; 
            }
#if UNITY_EDITOR
            if (Application.isPlaying == false) {
                foreach (var obj in prefabObjects) {
               
                    GameObject.DestroyImmediate(obj.Value);
                }
            }
            else {
                foreach (var obj in prefabObjects) {
                    GameObject.Destroy(obj.Value);
                }
            }
#else
            foreach (var obj in prefabObjects) {
                GameObject.Destroy(obj.Value);
            }
#endif
            
            prefabObjects = null;
        }

        private void FullInstatiatePrefabsMainThread() {

            ClearPrefabsMainThread();

            if (prefabObjects == null) {
                prefabObjects = new Dictionary<Vector3Int, GameObject>();
            }

            if (obj == null) {
                Debug.LogWarning("Chunk obj is null, can't instantiate prefabs.");
                return;
            }
            Vector3 origin = (chunkKey * chunkSize) + new Vector3(0.5f,0.5f,0.5f);
            for (int x = 0; x < chunkSize; x++) {
                for (int y = 0; y < chunkSize; y++) {
                    for (int z = 0; z < chunkSize; z++) {
                        
                        BlockId blockId = VoxelWorld.VoxelDataToBlockId(GetLocalVoxelAt(x, y, z));
                        
                        if (blockId > 0) {
                            var blockDefinition = world.voxelBlocks.GetBlockDefinitionFromBlockId(blockId);
                            if (blockDefinition.definition.contextStyle == VoxelBlocks.ContextStyle.Prefab) {
                                GameObject prefabDef = blockDefinition.definition.prefab;
                                GameObject prefab = GameObject.Instantiate(prefabDef);
                                Vector3Int pos = new Vector3Int(x, y, z);
                                prefab.transform.parent = obj.transform;
                                prefab.transform.localPosition = origin + pos;
                                prefab.transform.localRotation = Quaternion.identity;
                                prefab.transform.localScale = Vector3.one;

                                if (blockDefinition.definition.randomRotation) {
                                    float angle = VoxelWorld.HashCoordinates(x,y,z) % 4;
                                    prefab.transform.localRotation = Quaternion.Euler(0, angle * 90, 0);
                                }
                                prefabObjects.Add(pos, prefab);
                            }
                        }
                      
                    }
                }
            }
        }

        private (Vector3, bool) FindFirstEmpty(Vector3 pos) {
            //Check a 3x3x3 grid around pos, return the one with the fewest full voxels nearby

            Vector3Int intPos = VoxelWorld.FloorInt(pos);

            Vector3 bestPos = Vector3.zero;
            int bestCount = 0;

            foreach (Vector3Int offset in searchOffsets) {
                Vector3Int checkPos = intPos + offset;
                if (world.ReadVoxelAtInternal(checkPos) == 0) {
                    //count how many air voxels are around it, 
                    int count = CountAirVoxelsAround(world, checkPos);

                    if (count > bestCount) {
                        bestCount = count;
                        bestPos = checkPos;

                        if (count == 26) {
                            //found a perfect spot
                            break;
                        }
                    }
                }
            }

            if (bestCount == 0) {
                return (Vector3.zero, false);
            }
            else {
                return (bestPos + new Vector3(0.5f, 0.5f, 0.5f), true);

            }
        }

        private bool AnyVoxelsNearby(int x, int y, int z) {
            //Todo: we could use more memory and make this a much quicker check
            for (int x2 = x - 4; x2 <= x + 8; x2++) {
                for (int y2 = y - 4; y2 <= y + 8; y2++) {
                    for (int z2 = z - 4; z2 <= z + 8; z2++) {
                        if (world.ReadVoxelAtInternal(new Vector3Int(x2, y2, z2)) != 0) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool HasVoxels() {
            foreach (UInt16 vox in readWriteVoxel) {
                if (vox > 0) {
                    return true;
                }
            }
            return false;
        }
        public bool Busy() {
            if (meshProcessor != null) {
                return true;
            }
            return false;
        }
        
        ~Chunk() {

            Free();
        }

        public void Free() {
            //if (voxel.IsCreated)
            {
                //  voxel.Dispose();
            }
        }
 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldPosToVoxelIndex(Vector3Int globalCoord) {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            int localX = globalCoord.x - chunkCoordinate.x * chunkSize;
            int localY = globalCoord.y - chunkCoordinate.y * chunkSize;
            int localZ = globalCoord.z - chunkCoordinate.z * chunkSize;

            return localX + localY * chunkSize + localZ * chunkSize * chunkSize;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WorldPosToLocalPos(Vector3 globalCoord) {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            float localX = globalCoord.x - chunkCoordinate.x * chunkSize;
            float localY = globalCoord.y - chunkCoordinate.y * chunkSize;
            float localZ = globalCoord.z - chunkCoordinate.z * chunkSize;
            return new Vector3(localX, localY, localZ);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Int WorldPosToLocalPos(Vector3Int globalCoord) {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            int localX = globalCoord.x - chunkCoordinate.x * chunkSize;
            int localY = globalCoord.y - chunkCoordinate.y * chunkSize;
            int localZ = globalCoord.z - chunkCoordinate.z * chunkSize;
            return new Vector3Int(localX, localY, localZ);
        }

        //Assumes you've already identified if this the right chunk
        public void WriteVoxel(Vector3Int worldPos, UInt16 num) {
            int key = WorldPosToVoxelIndex(worldPos);

            if (key < 0 || key >= chunkSize * chunkSize * chunkSize) {
                return;
            }

            readWriteVoxel[key] = num;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt16 GetVoxelAt(Vector3Int worldPos) {
            int key = WorldPosToVoxelIndex(worldPos);

            return readWriteVoxel[key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelData GetLocalVoxelAt(Vector3Int localPos) {
            int key = localPos.x + localPos.y * chunkSize + localPos.z * chunkSize * chunkSize;
            return readWriteVoxel[key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt16 GetLocalVoxelAt(int localX, int localY, int localZ) {
            int key = localX + localY * chunkSize + localZ * chunkSize * chunkSize;
            return readWriteVoxel[key];
        }

        public void Clear() {
            if (obj != null) {
                colliders.Clear();
                GameObject.DestroyImmediate(obj);
                if (detailGameObjects != null && detailGameObjects[0] != null) {
                    for (int i = 0; i < 2; i++) {
                        GameObject.DestroyImmediate(detailGameObjects[i]);
                        detailGameObjects[i] = null;
                    }
                }
                obj = null;
            }
        }

        public void MainthreadForceCollisionRebuild() {
            //Clear all of the BoxColliders off this gameObject
            VoxelWorldCollision.ClearCollision(this);
            VoxelWorldCollision.MakeCollision(this);
        }

        public bool MainthreadUpdateMesh(VoxelWorld world) {
#pragma warning disable CS0162
            if (this.world.doVisuals == false) {
                return DoHeadlessUpdate(world);
            }
            else {
                return DoVisualUpdate(world);
            }
#pragma warning restore CS0162
        }

        private bool DoHeadlessUpdate(VoxelWorld world) {
            if (IsGeometryDirty() == true) {
                if (obj != null) {
                    Clear();
                }

                if (obj == null) {
                    obj = new GameObject();
                    obj.transform.parent = parent.transform;
                    obj.transform.localRotation = Quaternion.identity;
                    obj.transform.localScale = Vector3.one;
                    obj.name = "Chunk";

                    renderer = obj.AddComponent<MeshRenderer>();
                }

                //Fill the prefabs out
                FullInstatiatePrefabsMainThread();
                
                //Fill the collision out
                VoxelWorldCollision.MakeCollision(this);
                geometryDirty = false;
                return true;
            }

            return false;
        }

        public bool IsGeometryDirty() {
            return this.geometryDirty;
        }

        public bool NeedsToCopyMeshToScene() {
            if (meshProcessor != null) {
                if (meshProcessor.GetFinishedProcessing() == true) {
                    return true;
                }
            }

            return false;
        }

        public bool NeedsToGenerateMesh() {
            if (meshProcessor != null) {
                return false;
            }
            if (geometryDirty == true || geometryDirtyPriorityUpdate == true) {
                return true;
            }

            return false;
        }


        private bool DoVisualUpdate(VoxelWorld world) {
            if (meshProcessor != null && meshProcessor.GetFinishedProcessing() == true) {
                //This runs on the main thread, so we can do unity scene manipulation here (and only here)
                if (meshProcessor.GetGeometryReady() == true) {
                     
                    Profiler.BeginSample("ChunkMainThread");
                    if (obj != null) {
                        Clear();
                    }

                    if (obj == null) {

                        obj = new GameObject();
                        obj.transform.parent = parent.transform;
                        obj.transform.localRotation = Quaternion.identity;
                        obj.transform.localScale = Vector3.one;
                        obj.name = "Chunk";
                        
                        mesh = new Mesh();
                        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //Big boys

                        filter = obj.AddComponent<MeshFilter>();
                        filter.mesh = mesh;

                        renderer = obj.AddComponent<MeshRenderer>();
                        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

                        //See if this mesh has detail meshes
                        if (meshProcessor.GetHasDetailMeshes() == true) {
                            
                            if (detailGameObjects == null) {
                                detailGameObjects = new GameObject[3];

                                detailMeshes = new Mesh[3];
                                detailFilters = new MeshFilter[3];
                                detailRenderers = new MeshRenderer[3];
                            }

                            for (int i = 0; i < 3; i++) {
                                detailGameObjects[i] = new GameObject();
                                detailGameObjects[i].transform.parent = obj.transform;
                                if (i == 0) {
                                    detailGameObjects[i].name = "DetailMeshNear";
                                }
                                if (i == 1) {
                                    detailGameObjects[i].name = "DetailMeshFar";
                                }
                                if (i == 2) {
                                    detailGameObjects[i].name = "DetailMeshVeryFar";
                                }
                                
                                detailFilters[i] = detailGameObjects[i].AddComponent<MeshFilter>();
                                detailRenderers[i] = detailGameObjects[i].AddComponent<MeshRenderer>();

                                detailMeshes[i] = new Mesh();
                                detailMeshes[i].indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //Big boys

                                detailFilters[i].mesh = detailMeshes[i];
                            }

                            LODGroup lodSystem = detailGameObjects[0].AddComponent<LODGroup>();

                            // Enable crossfade
                            lodSystem.fadeMode = LODFadeMode.CrossFade;
                            lodSystem.animateCrossFading = true;

                            // Configure LODs with the last LOD2 as the lowest and no "culled" LOD
                            LOD[] lods = new LOD[3] {
                                new LOD(0.07f, new Renderer[] { detailRenderers[0] }), //The distance is actually for the next group eg: this one sets LOD1 to 10%
                                new LOD(0.03f, new Renderer[] { detailRenderers[1] }),
                                new LOD(0.0f, new Renderer[] { detailRenderers[2] })  
                            };
                       
                            lodSystem.SetLODs(lods);
                        }
                    }
                    
                    Profiler.EndSample();

                    Profiler.BeginSample("SpawnPrefabs");
                    FullInstatiatePrefabsMainThread();
                    Profiler.EndSample();

                    Profiler.BeginSample("RebuildCollision");
                    //Fill the collision out
                    //Greedy mesh time!
                    VoxelWorldCollision.MakeCollision(this);
                    Profiler.EndSample();

#pragma warning disable CS0162
                    //Create a gameobject with a "WireCube" component, and set it to the bounds of this chunk
                    //This is for debugging purposes only
                    if (VoxelWorld.showDebugBounds == true) {
                        GameObject debugBounds = new GameObject();
                        debugBounds.transform.parent = obj.transform;
                        debugBounds.transform.localPosition = (chunkKey * chunkSize) + new Vector3(chunkSize / 2, chunkSize / 2, chunkSize / 2);
                        debugBounds.transform.localScale = new Vector3(4, 4, 4);
                        WireCube cube = debugBounds.AddComponent<WireCube>();
                    }
#pragma warning restore CS0162

                }

                Profiler.BeginSample("FinalizeMesh");
                meshProcessor.FinalizeMesh(obj, mesh, renderer, detailMeshes, detailRenderers, world);
                meshProcessor = null; //clear it
                Profiler.EndSample();

                Profiler.BeginSample("UpdatePropertiesForChunk");
                materialPropertiesDirty = true;
         
                Profiler.EndSample();

                //Print out the total time taken
                //Debug.Log("Mesh processing time: " + (int)((Time.realtimeSinceStartup - timeOfStartOfUpdate)*1000.0f) + " ms");
            }

            if (geometryDirty == false) {
                return false;
            }

            if (meshProcessor != null) //already processing a mesh
            {
                return false;
            }

            //Update everything
            meshProcessor = new MeshProcessor(this);

            //Note this is not cleared while there is still a processing mesh (earlier in this method) because it makes sure the mesh always captures new updates
            geometryDirty = false;
            geometryDirtyPriorityUpdate = false;

            return true;
        }

        public static bool TestAABBSphere(Bounds aabb, Vector3 sphereCenter, float sphereRadius) {
            Vector3 closestPoint = Vector3.Max(Vector3.Min(sphereCenter, aabb.max), aabb.min);
            float distanceSquared = (sphereCenter - closestPoint).sqrMagnitude;
            return distanceSquared < (sphereRadius * sphereRadius);
        }

        static Vector4[] lightsPositions = new Vector4[2];
        static Vector4[] lightColors = new Vector4[2];
        static float[] lightRadius = new float[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnStartup() {
            Array.Clear(lightsPositions, 0, lightsPositions.Length);
            Array.Clear(lightColors, 0, lightColors.Length);
            Array.Clear(lightRadius, 0, lightRadius.Length);
        }
      

        public GameObject GetGameObject() {
            return obj;
        }
    }



}
