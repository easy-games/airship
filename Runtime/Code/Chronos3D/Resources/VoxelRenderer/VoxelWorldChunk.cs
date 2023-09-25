using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using System.Runtime.CompilerServices;
using UnityEditor.Rendering;

namespace VoxelWorldStuff
{
    public enum SurfaceBits : byte
    {
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

    public class Chunk
    {
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
        System.DateTime timeOfLastRadiosityUpdate = new System.DateTime(0);

        public RadiosityProbe[] probes;
        public int probeCount = 0;

        public int numUpdates = 0;
        public bool updatingRadiosity = false;

        private bool geometryDirty = true;
        private bool geometryDirtyPriorityUpdate = false;

        public bool bakedLightingDirty = true;
        public int lightingConverged = 0; //if this over a set value, no need to keep running radiosity

        public float previousEnergy = 999999;

        public Camera currentCamera = null;
 
        //Private stuff
        public Vector3Int chunkKey;

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

        private Dictionary<int, VoxelWorld.LightReference> lights = new();
        private VoxelWorld.LightReference[] highQualityLightArray = new VoxelWorld.LightReference[0];
        private VoxelWorld.LightReference[] detailLightArray = new VoxelWorld.LightReference[0];

        private MeshProcessor meshProcessor;

        public Vector3Int GetKey()
        {
            return chunkKey;
        }

        public bool GetPriorityUpdate()
        {
            return geometryDirtyPriorityUpdate;
        }

        //Caching some stuff to allow radiosity to process faster
        public MeshProcessor.PersistantData meshPersistantData = new MeshProcessor.PersistantData();

        public void SetWorld(VoxelWorld world)
        {
            this.world = world;
            parent = world.chunksFolder.gameObject;
        }

        public void SetGeometryDirty(bool dirty, bool priority = false)
        {
            geometryDirty = dirty;
            if (dirty)
            {
                lightingConverged = 0;
                if (priority)
                {
                    geometryDirtyPriorityUpdate = true;
                }
            }
        }

        public System.DateTime GetTimeOfLastRadiosityUpdate()
        {
            return timeOfLastRadiosityUpdate;
        }

        private int CountAirVoxelsAround(VoxelWorld world, Vector3Int checkPos)
        {
            int count = 0;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector3Int pos = checkPos + new Vector3Int(x, y, z);
                        if (VoxelWorld.VoxelIsSolid(world.ReadVoxelAtInternal(pos)) == false)
                        {
                            count++;
                        }
                    }
                }
            }
            return count;
        }

        private (Vector3, bool) FindFirstEmpty(Vector3 pos)
        {
            //Check a 3x3x3 grid around pos, return the one with the fewest full voxels nearby

            Vector3Int intPos = VoxelWorld.FloorInt(pos);

            Vector3 bestPos = Vector3.zero;
            int bestCount = 0;

            foreach (Vector3Int offset in searchOffsets)
            {
                Vector3Int checkPos = intPos + offset;
                if (world.ReadVoxelAtInternal(checkPos) == 0)
                {
                    //count how many air voxels are around it, 
                    int count = CountAirVoxelsAround(world, checkPos);

                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestPos = checkPos;

                        if (count == 26)
                        {
                            //found a perfect spot
                            break;
                        }
                    }
                }
           
            }

            if (bestCount == 0)
            {
                return (Vector3.zero, false);
            }
            else
            {
                return (bestPos + new Vector3(0.5f, 0.5f, 0.5f), true);

            }
        }


        //Get the nearest 4x4x4 probe
        //Assumes you're in the bounds of this chunk, which is why you're asking
        private RadiosityProbe GetProbeForVoxel(Vector3Int voxel)
        {
            int index = WorldPosToProbeIndex(voxel);
            RadiosityProbe probe = probes[index];

            return probe;
        }

        private bool AnyVoxelsNearby(int x, int y, int z)
        {
            //Todo: we could use more memory and make this a much quicker check
            for (int x2 = x-4; x2 <= x+8; x2++)
            {
                for (int y2 = y-4; y2 <= y+8; y2++)
                {
                    for (int z2 = z-4; z2 <= z+8; z2++)
                    {
                        
                        if (world.ReadVoxelAtInternal(new Vector3Int(x2, y2, z2)) != 0)
                        {
                            return true;
                            
                        }
                    }
                }
            }
            return false;
        }


        private void SetupProbes()
        {
            probes = new RadiosityProbe[64];


            Vector3Int origin = chunkKey * chunkSize;


            //place probes evenly inside the chunk, every 4 units
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int z = 0; z < 4; z++)
                    {
                        int i = x + (y * 4) + (z * 4 * 4);

                        Vector3Int posStart = origin + new Vector3Int(x * 4, y * 4, z * 4);
                        Vector3 pos = posStart + new Vector3(1.5f, 1.5f, 1.5f);
                        bool enabled = true;


                        //See if there are any voxels that need lighting within -1 to +5 of this probe
                        //If not, disable it
                        if (AnyVoxelsNearby(posStart.x, posStart.y, posStart.z) == false)
                        {
                            enabled = false;
                        }
                        
                        else
                        {

                            //Check in this 4x4x4 grid if the center isnt empty
                            VoxelData vox = world.GetVoxelAt(pos);
                            if (vox > 0)
                            {
                                (Vector3 res, bool valid) = FindFirstEmpty(posStart);
                                enabled = valid;
                                pos = res;
                            }
                        }
                        RadiosityProbeSample sample = world.GetOrMakeRadiosityProbeFor(VoxelWorld.FloorInt(pos));

                        probes[i] = new RadiosityProbe(world, pos, this, sample, enabled);
                        if (enabled == true)
                        {
                            probeCount += 1;
                        }

                    }
                }
            }
        }
        
        public bool HasVoxels()
        {
            foreach(UInt16 vox in readWriteVoxel)
            {
                if (vox > 0)
                {
                    return true;
                }
            }
            return false;
        }

        public void MainThreadAddSamplesToProbes()
        {
            if (updatingRadiosity == true || meshProcessor != null)
            {
                return;
            }

            numUpdates += 1;
            timeOfLastRadiosityUpdate = System.DateTime.Now;
            updatingRadiosity = true;

            if (probes == null)
            {
                SetupProbes();
            }

#pragma warning disable CS0162
            if (VoxelWorld.runThreaded)
            {
                ThreadPool.QueueUserWorkItem(ThreadedAddSamples, world);
            }
            else
            {
                ThreadedAddSamples(world);
            }
#pragma warning restore CS0162
        }

        public bool Busy()
        {
            if (updatingRadiosity == true || meshProcessor != null)
            {
                return true;
            }
            return false;
        }

        private void ThreadedAddSamples(object state)
        {
            int count = 0;
            foreach (RadiosityProbe p in probes)
            {
                
                count += p.AddSamples();
            }

            float energy = 0;
            foreach (RadiosityProbe p in probes)
            {
                energy += p.CalculateDirectColor();
            }

            foreach (RadiosityProbe p in probes)
            {
                energy += p.CalculateIndirectColor();
            }

            updatingRadiosity = false;

            //Flag for the chunk to know the baked lighting has changed
            bakedLightingDirty = true;

            //Debug.Log("Energy change: " + energy);

            if (energy < 10)
            {
                //Good enough, its converged
                lightingConverged += 1;
            }
            previousEnergy = energy;

        }

   

        public VoxelWorld.LightReference[] GetHighQualityLightArray()
        {
            return highQualityLightArray;
        }
        public VoxelWorld.LightReference[] GetDetailLightArray()
        {
            return detailLightArray;
        }



        public void AddLight(int id, VoxelWorld.LightReference light)
        {
            lights.Add(id, light);
            materialPropertiesDirty = true;
            
            UpdateLightList();
        }

        public void RemoveLight(int id)
        {
            lights.Remove(id);
            UpdateLightList();
        }

        public void ForceRemoveAllLightReferences()
        {
            lights.Clear();
            UpdateLightList();
        }
        private void UpdateLightList()
        {

            int heroIndex = 0;
            int detailIndex = 0;
            foreach (var lightRef in lights)
            {
                lightRef.Value.lightRef.TryGetTarget(out PointLight light);
                if (light == null)
                {
                    continue;
                }

                if (lightRef.Value.highQualityLight == true)
                {
                    if (heroIndex < 2)
                    {
                        heroIndex++;
                    }

                }
                else
                {
                    detailIndex++;
                }
            }
            highQualityLightArray = new VoxelWorld.LightReference[heroIndex];
            detailLightArray = new VoxelWorld.LightReference[detailIndex];

            heroIndex = 0;
            detailIndex = 0;
            foreach (var lightRef in lights)
            {
                lightRef.Value.lightRef.TryGetTarget(out PointLight light);
                if (light == null)
                {
                    continue;
                }

                if (lightRef.Value.highQualityLight == true)
                {
                    if (heroIndex < 2)
                    {
                        highQualityLightArray[heroIndex] = lightRef.Value;
                        heroIndex++;
                    }

                }
                else
                {
                    detailLightArray[detailIndex] = lightRef.Value;
                    detailIndex++;
                }
            }
        }



        public Chunk(Vector3Int srcPosition)
        {
            chunkKey = srcPosition;

            bottomLeftInt = (chunkKey * chunkSize);

            Vector3 bottomLeft = (chunkKey * chunkSize);
            Vector3 topRight = bottomLeft + new Vector3(chunkSize, chunkSize, chunkSize);

            bounds = new Bounds();
            bounds.SetMinMax(bottomLeft, topRight);
        }
        
        ~Chunk()
        {

            Free();
        }

        public void Free()
        {
            //if (voxel.IsCreated)
            {
                //  voxel.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldPosToProbeIndex(Vector3Int globalCoord)
        {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            int localX = (globalCoord.x - chunkCoordinate.x * chunkSize) / 4;
            int localY = (globalCoord.y - chunkCoordinate.y * chunkSize) / 4;
            int localZ = (globalCoord.z - chunkCoordinate.z * chunkSize) / 4;

            return localX + (localY * 4) + (localZ * 4 * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldPosToVoxelIndex(Vector3Int globalCoord)
        {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            int localX = globalCoord.x - chunkCoordinate.x * chunkSize;
            int localY = globalCoord.y - chunkCoordinate.y * chunkSize;
            int localZ = globalCoord.z - chunkCoordinate.z * chunkSize;

            return localX + localY * chunkSize + localZ * chunkSize * chunkSize;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 WorldPosToLocalPos(Vector3 globalCoord)
        {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            float localX = globalCoord.x - chunkCoordinate.x * chunkSize;
            float localY = globalCoord.y - chunkCoordinate.y * chunkSize;
            float localZ = globalCoord.z - chunkCoordinate.z * chunkSize;
            return new Vector3(localX, localY, localZ);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3Int WorldPosToLocalPos(Vector3Int globalCoord)
        {
            Vector3Int chunkCoordinate = VoxelWorld.WorldPosToChunkKey(globalCoord);
            int localX = globalCoord.x - chunkCoordinate.x * chunkSize;
            int localY = globalCoord.y - chunkCoordinate.y * chunkSize;
            int localZ = globalCoord.z - chunkCoordinate.z * chunkSize;
            return new Vector3Int(localX, localY, localZ);
        }

        //Assumes you've already identified if this the right chunk
        public void WriteVoxel(Vector3Int worldPos, UInt16 num)
        {
            int key = WorldPosToVoxelIndex(worldPos);

            if (key < 0 || key >= chunkSize * chunkSize * chunkSize)
            {
                return;
            }

            readWriteVoxel[key] = num;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt16 GetVoxelAt(Vector3Int worldPos)
        {
            int key = WorldPosToVoxelIndex(worldPos);
            
            return readWriteVoxel[key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VoxelData GetLocalVoxelAt(Vector3Int localPos)
        {
            int key = localPos.x + localPos.y * chunkSize + localPos.z * chunkSize * chunkSize;
            return readWriteVoxel[key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt16 GetLocalVoxelAt(int localX, int localY, int localZ)
        {
            int key = localX + localY * chunkSize + localZ * chunkSize * chunkSize;

            return readWriteVoxel[key];

        }

        public void Clear()
        {
            if (obj != null)
            {
                colliders.Clear();
                GameObject.DestroyImmediate(obj);
                if (detailGameObjects != null && detailGameObjects[0] != null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        GameObject.DestroyImmediate(detailGameObjects[i]);
                        detailGameObjects[i] = null;
                    }
                }
                obj = null;
            }
        }

        public void MainthreadForceCollisionForVoxel(Vector3 pos)
        {
            //Clear all of the BoxColliders off this gameObject
            VoxelWorldCollision.ClearCollision(this);
            VoxelWorldCollision.MakeCollision(this);
        }

        public bool MainthreadUpdateMesh(VoxelWorld world)
        {
#pragma warning disable CS0162
            if (VoxelWorld.doVisuals == false)
            {
                return DoHeadlessUpdate(world);
            }
            else
            {
                return DoVisualUpdate(world);
            }
#pragma warning restore CS0162            
        }

        private bool DoHeadlessUpdate(VoxelWorld world)
        {
            if (geometryDirty == true)
            {
                if (obj != null)
                {
                    Clear();
                }

                if (obj == null)
                {
                    obj = new GameObject();
                    obj.transform.parent = parent.transform;
                    obj.name = "Chunk";
                    obj.layer = 6;
               
                    renderer = obj.AddComponent<MeshRenderer>();
                }

                //Fill the collision out
                VoxelWorldCollision.MakeCollision(this);
                geometryDirty = false;
                return true;
            }

            return false;
        }

        public bool IsGeometryDirty()
        {
            return this.geometryDirty;
        }

        public bool NeedsToRunUpdate() 
        {
            if (meshProcessor != null && meshProcessor.GetFinishedProcessing() == true)
            {
                return true;
            }
                    
            if (geometryDirty == false && bakedLightingDirty == false)
            {
                return false;
            }

            if (meshProcessor != null) //already processing a mesh
            {
                return false;
            }

            return true;
        }

        private bool DoVisualUpdate(VoxelWorld world)
        {
            if (meshProcessor != null && meshProcessor.GetFinishedProcessing() == true)
            {
                //This runs on the main thread, so we can do unity scene manipulation here (and only here)
                if (meshProcessor.GetGeometryDirty() == true)
                {
                    if (obj != null)
                    {
                        Clear();
                    }
                    
                    if (obj == null)
                    {
                        
                        obj = new GameObject();
                        obj.transform.parent = parent.transform;
                        obj.name = "Chunk";
                        obj.layer = 6;
                        mesh = new Mesh();
                        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; //Big boys

                        filter = obj.AddComponent<MeshFilter>();
                        filter.mesh = mesh;

                        renderer = obj.AddComponent<MeshRenderer>();

                        //See if this mesh has detail meshes
                        if (meshProcessor.GetHasDetailMeshes() == true)
                        {
                            //Debug.Log("Got a mesh");
                            skipLodAnimation = true;
                                
                            if (detailGameObjects == null)
                            {
                                detailGameObjects = new GameObject[3];
                                
                                detailMeshes = new Mesh[3];
                                detailFilters = new MeshFilter[3];
                                detailRenderers = new MeshRenderer[3];
                            }
                            
                            for(int i = 0; i < 3; i++)
                            {
                                detailGameObjects[i] = new GameObject();
                                detailGameObjects[i].transform.parent = obj.transform;
                                if (i == 0)
                                {
                                    detailGameObjects[i].name = "DetailMeshNear";
                                }
                                if (i == 1)
                                {
                                    detailGameObjects[i].name = "DetailMeshFar";
                                }
                                if (i == 2)
                                {
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

                    //Fill the collision out
                    //Greedy mesh time!
                    VoxelWorldCollision.MakeCollision(this);

#pragma warning disable CS0162
                    //Create a gameobject with a "WireCube" component, and set it to the bounds of this chunk
                    //This is for debugging purposes only
                    if (VoxelWorld.showDebugBounds == true)
                    {
                        GameObject debugBounds = new GameObject();
                        debugBounds.transform.parent = obj.transform;
                        debugBounds.transform.localPosition = (chunkKey * chunkSize) + new Vector3(chunkSize / 2, chunkSize / 2, chunkSize / 2);
                        debugBounds.transform.localScale = new Vector3(4, 4, 4);
                        WireCube cube = debugBounds.AddComponent<WireCube>();
                    }
#pragma warning restore CS0162
                  
                }

                //Debug.Log("(Light time: " + meshProcessor.lastLightUpdateDuration + " ms)");
                //Debug.Log("(Mesh time: " + meshProcessor.lastMeshUpdateDuration + " ms)");
                
                meshProcessor.FinalizeMesh(mesh, renderer, detailMeshes, detailRenderers);
                meshProcessor = null; //clear it

                materialPropertiesDirty = true;
                UpdateMaterialPropertiesForChunk();
                
                //Print out the total time taken
                //Debug.Log("Mesh processing time: " + (int)((Time.realtimeSinceStartup - timeOfStartOfUpdate)*1000.0f) + " ms");
            }
            
            if (geometryDirty == false && bakedLightingDirty == false)
            {
                return false;
            }

            if (meshProcessor != null) //already processing a mesh
            {
                return false;
            }
 
            //kick off an update!

            //If we're being asked to update the baked lighting, we need to do a full mesh update if it hasn't run before
            if (mesh == null && bakedLightingDirty == true)
            {
                geometryDirty = true;
            }

            //Time to go launch a new mesh process!
            if (bakedLightingDirty == true && geometryDirty == false)
            {
                //Just update baked lighting
                meshProcessor = new MeshProcessor(this, true);
            }
            else
            {
                //Update everything
                meshProcessor = new MeshProcessor(this, false);
            }
            
            //Note this is not cleared while there is still a processing mesh (earlier in this method) because it makes sure the mesh always captures new updates
            geometryDirty = false;
            geometryDirtyPriorityUpdate = false;
            bakedLightingDirty = false;
            return true;
        }

        public static bool TestAABBSphere(Bounds aabb, Vector3 sphereCenter, float sphereRadius)
        {
            Vector3 closestPoint = Vector3.Max(Vector3.Min(sphereCenter, aabb.max), aabb.min);
            float distanceSquared = (sphereCenter - closestPoint).sqrMagnitude;
            return distanceSquared < (sphereRadius * sphereRadius);
        }

        static Vector4[] lightsPositions = new Vector4[2];
        static Vector4[] lightColors = new Vector4[2];
        static float[] lightRadius = new float[2];
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnStartup()
        {
            Array.Clear(lightsPositions, 0, lightsPositions.Length);
            Array.Clear(lightColors, 0, lightColors.Length);
            Array.Clear(lightRadius, 0, lightRadius.Length);
        }
        public void UpdateMaterialPropertiesForChunk() 
        {
            if (renderer == null) return;
            if (mesh == null) return;

            //Update the detail meshes if they're there
            if (detailRenderers != null && currentCamera != null)
            {
                Vector3 pos = currentCamera.transform.position;
                Vector3 chunkPos = (chunkKey * chunkSize) + new Vector3(chunkSize / 2, chunkSize / 2, chunkSize / 2);
                float distance = Vector3.Distance(pos, chunkPos);
                wantsToBeVisible[0] = false;
                wantsToBeVisible[1] = false;
                wantsToBeVisible[2] = false;
                bool somethingChanged = false;
                float speed = world.lodTransitionSpeed * Time.deltaTime;
                

                //mark what we saw
                for (int i = 0; i < 3; i++)
                { 
                    detailMeshPrevAlpha[i] = detailMeshAlpha[i];
                }

                //If the near one should be visible
                if (distance < world.lodNearDistance)
                {
                    //Blend it in
                    if (detailMeshAlpha[0] < 1.0f)
                    {
                        detailMeshAlpha[0] += speed;
                    }
                    else
                    {
                        //Once the near one is fully visible, we can fade 1 and 2 out
                        detailMeshAlpha[1] -= speed;
                        detailMeshAlpha[2] -= speed;
                    }
                }
                else
                if (distance < world.lodFarDistance)
                {
                    //If the far one should be visible
                    //Blend it in
                    if (detailMeshAlpha[1] < 1.0f)
                    {
                        detailMeshAlpha[1] += speed;
                    }
                    else
                    {
                        //Once the far one is fully visible, we can fade 0+2 out
                        detailMeshAlpha[0] -= speed;
                        detailMeshAlpha[2] -= speed;
                    }
                }
                else
                {
                    if (detailMeshAlpha[2] < 1.0f)
                    {
                        detailMeshAlpha[2] += speed;
                    }
                    else
                    {
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

                
                for (int i = 0; i < 3; i++)
                {
                    //Did we hit zero from a higher value?
                    if (detailMeshPrevAlpha[i] > 0 && detailMeshAlpha[i] <= 0)
                    {
                        detailRenderers[i].enabled = false;
                        
                    }
                    //Did we move away from zero?
                    if (detailMeshPrevAlpha[i] <= 0 && detailMeshAlpha[i] > 0)
                    {
                        detailRenderers[i].enabled = true;
                    }
                    //clamp it to 0, 1
                    detailMeshAlpha[i] = Mathf.Clamp01(detailMeshAlpha[i]);

                    //mark the change
                    if (detailMeshPrevAlpha[i] - detailMeshAlpha[i] != 0)
                    {
                        somethingChanged = true;
                    }
                }
                
                
                
                //On initial creation we want lod stuff to be in its correct state, so this skips the animation and just sets things directly
                //Note this block of code doesn't run every frame!
                if (skipLodAnimation == true)
                {
                    //Hot start! This isnt the moment to moment logic
                    skipLodAnimation = false;
                    somethingChanged = true;
                    if (distance < world.lodNearDistance)
                    {
                        detailRenderers[0].enabled = true;
                        detailMeshAlpha[0] = 1;
                        detailRenderers[1].enabled = false;
                        detailMeshAlpha[1] = 0;
                        detailRenderers[2].enabled = false;
                        detailMeshAlpha[2] = 0;
                    }
                    else
                    if (distance < world.lodFarDistance)
                    {
                        detailRenderers[0].enabled = false;
                        detailMeshAlpha[0] = 0;
                        detailRenderers[1].enabled = true;
                        detailMeshAlpha[1] = 1;
                        detailRenderers[2].enabled = false;
                        detailMeshAlpha[2] = 0;
                    }
                    else
                    {
                        detailRenderers[0].enabled = false;
                        detailMeshAlpha[0] = 0;
                        detailRenderers[1].enabled = false;
                        detailMeshAlpha[1] = 0;
                        detailRenderers[2].enabled = true;
                        detailMeshAlpha[2] = 1;
                    }
                }

                
                //Set the alpha of the detail meshes
                if (somethingChanged == true)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        foreach (Material material in detailRenderers[i].sharedMaterials)
                        {
                            if (material != null)
                            {
                                material.SetFloat("_Alpha", detailMeshAlpha[i]);
                            }
                        }
                    }
                }
            }


            if (materialPropertiesDirty == true)
            {
                //get all the point lights in the scene
                int numHighQualityLights = 0;
                foreach (var lightRec in lights)
                {
                    lightRec.Value.lightRef.TryGetTarget(out PointLight light);
                    if (light == null || light.highQualityLight == false)
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
                
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (lightsPositions != null)
                    {
                        material.SetVectorArray("globalDynamicLightPos", lightsPositions);
                        material.SetVectorArray("globalDynamicLightColor", lightColors);
                        material.SetFloatArray("globalDynamicLightRadius", lightRadius);
                    }
               
                    if (numHighQualityLights == 0)
                    {
                        material.EnableKeyword("NUM_LIGHTS_LIGHTS0");
                        material.DisableKeyword("NUM_LIGHTS_LIGHTS1");
                        material.DisableKeyword("NUM_LIGHTS_LIGHTS2");
                    }
                    if (numHighQualityLights == 1)
                    {
                        material.DisableKeyword("NUM_LIGHTS_LIGHTS0");
                        material.EnableKeyword("NUM_LIGHTS_LIGHTS1");
                        material.DisableKeyword("NUM_LIGHTS_LIGHTS2");
                    }
                    if (numHighQualityLights == 2)
                    {
                        material.DisableKeyword("NUM_LIGHTS_LIGHTS0");
                        material.DisableKeyword("NUM_LIGHTS_LIGHTS1");
                        material.EnableKeyword("NUM_LIGHTS_LIGHTS2");
                    }
                   
                }
                materialPropertiesDirty = false;
            }
        }

        public GameObject GetGameObject()
        {
            return obj;
        }
    }



}
