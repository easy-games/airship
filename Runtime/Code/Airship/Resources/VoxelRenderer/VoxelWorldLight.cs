using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using VoxelWorldStuff;

public partial class VoxelWorld : Singleton<VoxelWorld>
{

    public class LightReference
    {
        public Vector3 position;
        public float radius;
        public float radiusSquare;
        public WeakReference<AirshipPointLight> lightRef;
        public bool dirty = true;
        public HashSet<Vector3Int> chunkKeys = new();
        public int instanceId;
        public bool highQualityLight = true;
        public Color color;
        public bool shadow = false;

        public LightReference(AirshipPointLight light)
        {

            lightRef = new WeakReference<AirshipPointLight>(light);
            instanceId = light.GetInstanceID();
            Update();
        }
        public void Update()
        {
            lightRef.TryGetTarget(out AirshipPointLight light);
            if (light == null)
            {
                return;
            }
            Vector3 newPos = light.gameObject.transform.position;
            float newRange = light.range;

            if ((newPos - position).sqrMagnitude > 0.01 || newRange != radius || light.highQualityLight != highQualityLight || light.castShadows != shadow || light.color * light.intensity != color)
            {
                dirty = true;

                position = newPos;
                radius = newRange;
                radiusSquare = radius * radius;
                color = light.color * light.intensity;
                shadow = light.castShadows;
                highQualityLight = light.highQualityLight;
            }


        }

        public void RemoveLightReferenceFromWorld(VoxelWorld world)
        {
            foreach (Vector3Int key in chunkKeys)
            {
                world.chunks.TryGetValue(key, out Chunk chunk);

                if (chunk != null)
                {
                    chunk.RemoveLight(instanceId);
                    chunk.SetGeometryDirty(true);
                }

            }

        }

        //called after a scene load in editor
        public void ForceAddAllLightReferencesToChunks(VoxelWorld world)
        {
            foreach (Vector3Int key in chunkKeys)
            {
                world.chunks.TryGetValue(key, out Chunk chunk);

                if (chunk != null)
                {
                    chunk.AddLight(instanceId, this);
                    chunk.SetGeometryDirty(true);
                }

            }
        }
    }
    
    public void UpdateLights() {
        if (RunCore.IsServer()) return;

        List<AirshipPointLight> srcLights = Airship.SingletonClassManager<AirshipPointLight>.Instance.GetAllActiveItems();

        //see if we have it?
        //Debug.Log("Num lights: " + srcLights.Length + " Tracked lights: " + srcLights.Length);
        foreach (AirshipPointLight light in srcLights)
        {
            // Check if the light is already in the dictionary
            if (!sceneLights.ContainsKey(light.GetInstanceID()))
            {
                // If not, add it to the dictionary
                sceneLights.Add(light.GetInstanceID(), new LightReference(light));
                //Debug.Log("Added light to dictionary: " + light.name);
            }
        }

        List<int> deleteKeys = new List<int>();
        foreach (var rec in sceneLights)
        {
            rec.Value.lightRef.TryGetTarget(out AirshipPointLight refLight);

            if (refLight == null)
            {
                deleteKeys.Add(rec.Key);
            }


        }
        foreach (int key in deleteKeys)
        {
            // if light is already in dictionary but destroy then remove
            sceneLights[key].RemoveLightReferenceFromWorld(this);
            sceneLights.Remove(key);

            //Debug.Log("Removed light from dictionary");
        }

        //Once we have our final list, see if they're dirty. if they're dirty, update the chunks they affect
        int updateCount = 0;
        foreach (var rec in sceneLights)
        {
            rec.Value.Update();

            if (rec.Value.dirty == true)
            {

                rec.Value.dirty = false;
                updateCount++;
                //remove this lightref from all chunkKeys
                rec.Value.RemoveLightReferenceFromWorld(this);


                int dist = Mathf.CeilToInt(rec.Value.radius);
                Vector3Int minKey = WorldPosToChunkKey(rec.Value.position - new Vector3Int(dist, dist, dist));
                Vector3Int maxKey = WorldPosToChunkKey(rec.Value.position + new Vector3Int(dist, dist, dist));

                //Write to every chunk in this box
                for (int x = minKey.x; x < maxKey.x + 1; x++)
                {
                    for (int y = minKey.y; y < maxKey.y + 1; y++)
                    {
                        for (int z = minKey.z; z < maxKey.z + 1; z++)
                        {
                            Vector3Int key = new Vector3Int(x, y, z);
                            chunks.TryGetValue(key, out Chunk lookupChunk);

                            if (lookupChunk != null)
                            {
                                //We use a sphere test so that corners get clipped off. Very useful for large lights.
                                if (Chunk.TestAABBSphere(lookupChunk.bounds, rec.Value.position, rec.Value.radius) == true)
                                {

                                    lookupChunk.AddLight(rec.Value.instanceId, rec.Value);
                                    rec.Value.chunkKeys.Add(key);
                                }
                            }

                        }
                    }
                }
            }
        }

        if (updateCount > 0)
        {
            //    Debug.Log("Updated Chunks " + updateCount);
        }


    }

    public void AddChunk(Vector3Int key, Chunk chunk)
    {
        chunks.Add(key, chunk);
        chunk.SetGeometryDirty(true);
        InitializeLightingForChunk(chunk);
    }


    //Called by newly created chunks at runtime
    public void InitializeLightingForChunk(Chunk chunk)
    {
        chunk.ForceRemoveAllLightReferences();

        foreach (var rec in sceneLights)
        {

            int dist = Mathf.CeilToInt(rec.Value.radius);
            Vector3Int minKey = WorldPosToChunkKey(rec.Value.position - new Vector3Int(dist, dist, dist));
            Vector3Int maxKey = WorldPosToChunkKey(rec.Value.position + new Vector3Int(dist, dist, dist));

            Vector3Int key = chunk.GetKey();

            if (key.x >= minKey.x && key.y >= minKey.y && key.z >= minKey.z && key.x <= maxKey.x && key.y <= maxKey.y && key.z <= maxKey.z)
            {
                if (Chunk.TestAABBSphere(chunk.bounds, rec.Value.position, rec.Value.radius) == true)
                {
                    chunk.AddLight(rec.Value.instanceId, rec.Value);
                    rec.Value.chunkKeys.Add(key);
                }
            }

        }
    }

    public Color SampleSphericalHarmonics(float3[] shMap, Vector3 unitVector)
    {
        const float c1 = 0.429043f;
        const float c2 = 0.511664f;
        const float c3 = 0.743125f;
        const float c4 = 0.886227f;
        const float c5 = 0.247708f;
        float3 f = (c1 * shMap[8] * (unitVector.x * unitVector.x - unitVector.y * unitVector.y) +
            c3 * shMap[6] * unitVector.z * unitVector.z +
            c4 * shMap[0] -
            c5 * shMap[6] +
            2.0f * c1 * shMap[4] * unitVector.x * unitVector.y +
            2.0f * c1 * shMap[7] * unitVector.x * unitVector.z +
            2.0f * c1 * shMap[5] * unitVector.y * unitVector.z +
            2.0f * c2 * shMap[3] * unitVector.x +
            2.0f * c2 * shMap[1] * unitVector.y +
            2.0f * c2 * shMap[2] * unitVector.z
            );
        return new Color(f.x, f.y, f.z);
    }
}
