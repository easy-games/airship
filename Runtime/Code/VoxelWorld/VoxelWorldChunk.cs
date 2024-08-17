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

        private float[] detailMeshAlpha = new float[3];
        private bool[] wantsToBeVisible = new bool[3];
        private float[] detailMeshPrevAlpha = new float[3];
        private bool skipLodAnimation = true;

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
        public static int WorldPosToProbeIndex(Vector3Int globalCoord) {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            int localX = (globalCoord.x - chunkCoordinate.x * chunkSize) / 4;
            int localY = (globalCoord.y - chunkCoordinate.y * chunkSize) / 4;
            int localZ = (globalCoord.z - chunkCoordinate.z * chunkSize) / 4;

            return localX + (localY * 4) + (localZ * 4 * 4);
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
            if (VoxelWorld.doVisuals == false) {
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
                    obj.name = "Chunk";
              

                    renderer = obj.AddComponent<MeshRenderer>();
                }

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
                        obj.name = "Chunk";
                        mesh = new Mesh();
                        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //Big boys

                        filter = obj.AddComponent<MeshFilter>();
                        filter.mesh = mesh;

                        renderer = obj.AddComponent<MeshRenderer>();
                        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

                        //See if this mesh has detail meshes
                        if (meshProcessor.GetHasDetailMeshes() == true) {
                            //Debug.Log("Got a mesh");
                            skipLodAnimation = true;

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
                                detailGameObjects[i].layer = 6;
                                detailFilters[i] = detailGameObjects[i].AddComponent<MeshFilter>();
                                detailRenderers[i] = detailGameObjects[i].AddComponent<MeshRenderer>();

                                detailMeshes[i] = new Mesh();
                                detailMeshes[i].indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //Big boys

                                detailFilters[i].mesh = detailMeshes[i];
                            }
                        }
                    }
                    
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
                UpdateMaterialPropertiesForChunk();
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
        public void UpdateMaterialPropertiesForChunk() {
            if (renderer == null) return;
            if (mesh == null) return;

            //Update the detail meshes if they're there
            if (detailRenderers != null && currentCamera != null) {
                Vector3 pos = currentCamera.transform.position;
                Vector3 chunkPos = (chunkKey * chunkSize) + new Vector3(chunkSize / 2, chunkSize / 2, chunkSize / 2);
                float distance = Vector3.Distance(pos, chunkPos);
                wantsToBeVisible[0] = false;
                wantsToBeVisible[1] = false;
                wantsToBeVisible[2] = false;
                bool somethingChanged = false;
                float speed = world.lodTransitionSpeed * Time.deltaTime;


                //mark what we saw
                for (int i = 0; i < 3; i++) {
                    detailMeshPrevAlpha[i] = detailMeshAlpha[i];
                }

                //If the near one should be visible
                if (distance < world.lodNearDistance) {
                    //Blend it in
                    if (detailMeshAlpha[0] < 1.0f) {
                        detailMeshAlpha[0] += speed;
                    }
                    else {
                        //Once the near one is fully visible, we can fade 1 and 2 out
                        detailMeshAlpha[1] -= speed;
                        detailMeshAlpha[2] -= speed;
                    }
                }
                else
                if (distance < world.lodFarDistance) {
                    //If the far one should be visible
                    //Blend it in
                    if (detailMeshAlpha[1] < 1.0f) {
                        detailMeshAlpha[1] += speed;
                    }
                    else {
                        //Once the far one is fully visible, we can fade 0+2 out
                        detailMeshAlpha[0] -= speed;
                        detailMeshAlpha[2] -= speed;
                    }
                }
                else {
                    if (detailMeshAlpha[2] < 1.0f) {
                        detailMeshAlpha[2] += speed;
                    }
                    else {
                        detailMeshAlpha[0] -= speed;
                        detailMeshAlpha[1] -= speed;
                    }
                }

                /*
                for (int i = 0; i < 2; i++)
                {
                    //Becoming more visible?
                    if (wantsToBeVisible[i] == true)
                    {
                        detailRenderers[i].enabled = true;
                        
                        if (detailMeshAlpha[i] < 1)
                        {
                            detailMeshAlpha[i] += speed;
                            somethingChanged = true;
                            if (detailMeshAlpha[i] >= 1)
                            {
                                detailMeshAlpha[i] = 1;
                            }
                        }
                    }
                    //And backwards
                    if (wantsToBeVisible[i] == false && detailMeshAlpha[i] > 0)
                    {
                        detailMeshAlpha[i] -= speed;
                        somethingChanged = true;
                        if (detailMeshAlpha[i] <= 0)
                        {
                            detailMeshAlpha[i] = 0;
                            detailRenderers[i].enabled = false;
                        }
                    }
                }*/

                for (int i = 0; i < 3; i++) {
                    //Did we hit zero from a higher value?
                    if (detailMeshPrevAlpha[i] > 0 && detailMeshAlpha[i] <= 0) {
                        detailRenderers[i].enabled = false;

                    }
                    //Did we move away from zero?
                    if (detailMeshPrevAlpha[i] <= 0 && detailMeshAlpha[i] > 0) {
                        detailRenderers[i].enabled = true;
                    }
                    //clamp it to 0, 1
                    detailMeshAlpha[i] = Mathf.Clamp01(detailMeshAlpha[i]);

                    //mark the change
                    if (detailMeshPrevAlpha[i] - detailMeshAlpha[i] != 0) {
                        somethingChanged = true;
                    }
                }
                
                //On initial creation we want lod stuff to be in its correct state, so this skips the animation and just sets things directly
                //Note this block of code doesn't run every frame!
                if (skipLodAnimation == true) {
                    //Hot start! This isnt the moment to moment logic
                    skipLodAnimation = false;
                    somethingChanged = true;
                    if (distance < world.lodNearDistance) {
                        detailRenderers[0].enabled = true;
                        detailMeshAlpha[0] = 1;
                        detailRenderers[1].enabled = false;
                        detailMeshAlpha[1] = 0;
                        detailRenderers[2].enabled = false;
                        detailMeshAlpha[2] = 0;
                    }
                    else
                    if (distance < world.lodFarDistance) {
                        detailRenderers[0].enabled = false;
                        detailMeshAlpha[0] = 0;
                        detailRenderers[1].enabled = true;
                        detailMeshAlpha[1] = 1;
                        detailRenderers[2].enabled = false;
                        detailMeshAlpha[2] = 0;
                    }
                    else {
                        detailRenderers[0].enabled = false;
                        detailMeshAlpha[0] = 0;
                        detailRenderers[1].enabled = false;
                        detailMeshAlpha[1] = 0;
                        detailRenderers[2].enabled = true;
                        detailMeshAlpha[2] = 1;
                    }
                }


                //Set the alpha of the detail meshes
                if (somethingChanged == true) {

                    //Create the material property blocks and store them 
                    /*if (detailPropertyBlocks == null)
                    {
                        detailPropertyBlocks = new List<MaterialPropertyBlock>[3];
                        for (int i = 0; i < 3; i++)
                        {
                            detailPropertyBlocks[i] = new List<MaterialPropertyBlock>();
                            for (int materialIndex = 0; materialIndex < detailRenderers[i].sharedMaterials.Length; materialIndex++)
                            {
                                detailPropertyBlocks[i].Add(new MaterialPropertyBlock());
                            }
                        }
                    }*/

                    //Adjust the alpha in the property block
                    for (int i = 0; i < 3; i++) {
                        for (int materialIndex = 0; materialIndex < detailRenderers[i].sharedMaterials.Length; materialIndex++) {
                            Material mat = detailRenderers[i].sharedMaterials[materialIndex];
                            if (mat == null) {
                                continue;
                            }

                            /*
                            var rendererRef = AirshipRendererManager.Instance.GetRendererReference(detailRenderers[i]);

                            if (rendererRef != null) {
                                MaterialPropertyBlock propertyBlock = rendererRef.GetPropertyBlock(mat, materialIndex);
                                propertyBlock.SetFloat("_Alpha", detailMeshAlpha[i]);
                            }
                            */

                        }
                    }
                }
            }

            if (materialPropertiesDirty == true) {
                //get all the point lights in the scene
                /*
                int numHighQualityLights = 0;
                
                foreach (var lightRec in lights)
                {
                    lightRec.Value.lightRef.TryGetTarget(out AirshipPointLight light);
                    if (light == null)
                    {
                        continue;
                    }

                    lightsPositions[numHighQualityLights] = light.transform.position;
                    lightColors[numHighQualityLights] = light.color * light.intensity;
                    lightRadius[numHighQualityLights] = light.range;
                    numHighQualityLights++;
                    if (numHighQualityLights == 2)
                    {
                        break;
                    }
                }
                if (numHighQualityLights > 0)
                {
                    usingLocallyClonedMaterials = true;
                }*/


            }
        }

        public GameObject GetGameObject() {
            return obj;
        }
    }



}
