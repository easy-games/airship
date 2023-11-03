using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.VisualScripting;
using UnityEngine;
using static VoxelWorld;

//Update in editor
[ExecuteInEditMode]
public class VoxelWorldMeshUpdater : MonoBehaviour
{ 
    public string warningMessage = "";
     
    VoxelWorld world = null;
    MeshFilter meshFilter = null;
    Renderer meshRenderer = null;
    private SkinnedMeshRenderer skinnedMeshRenderer = null;
    List<LightReference> lightsInRange = new(16);
    
    static Vector4[] lightsPositions = new Vector4[2];
    static Vector4[] lightColors = new Vector4[2];
    static float[] lightRadius = new float[2];

    MaterialPropertyBlock block = null;

    public Renderer GetRenderer()
    {
        return meshRenderer;
    }

    private uint storedFilter = 0;

    //add setters and getters for storedFilter
    public uint GetStoredFilter()
    {
        return storedFilter;
    }
    public void SetStoredFilter(uint filter)
    {
        storedFilter = filter;
    }

    private void LateUpdate()
    {
        
        //get the voxelWorld
        if (world == null)
        {
            world = GameObject.FindObjectOfType<VoxelWorld>();
        }

        if (RunCore.IsServer() && Application.isPlaying) {
            return;
        }

        if (skinnedMeshRenderer == null || meshFilter == null)
        {
            if (skinnedMeshRenderer == null) {
                skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
                meshRenderer = skinnedMeshRenderer;
            }
            
            //Grab the voxelWorld material on the meshRenderer
            if (meshFilter == null) {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null) {
                meshRenderer = GetComponent<MeshRenderer>();
            }
            
        }

        if (world == null )
        {
#if UNITY_EDITOR
            warningMessage = "No available voxel world";
#endif
            return;
        }

        if (skinnedMeshRenderer == null && meshFilter == null)
        {
            if (meshFilter == null) {
#if UNITY_EDITOR
            warningMessage = "No available meshFilter";
#endif
                return;
            }

            if (meshRenderer == null) {
#if UNITY_EDITOR
            warningMessage = "No available meshRenderer";
#endif
                return;
            }
            
#if UNITY_EDITOR
            warningMessage = "No available skinnedMeshRender or meshFilter or meshRenderer";
#endif
            return;
        }

        Material mat = meshRenderer.sharedMaterial;
        if (mat == null || !mat.IsKeywordEnabled("NUM_LIGHTS"))
        {
#if UNITY_EDITOR
            warningMessage = "No available material that supports dynamic lights ";
#endif
            return;    
        }
        //Allgood
        warningMessage = "";

        lightsInRange.Clear();
        
        //calculate the radius of the mesh
        Mesh sharedMesh = meshFilter ? meshFilter.sharedMesh : skinnedMeshRenderer.sharedMesh;
        if (!sharedMesh) {
            return;
        }
        float boundingSphereRadius = sharedMesh.bounds.extents.magnitude;
        
        //Grab all the light references out of the world, and see if they're in range of this objects bounding box
        foreach (var referenceKV in world.sceneLights)
        {
            LightReference reference = referenceKV.Value;

            Vector3 vec = (reference.position - transform.position);
            float dist = vec.magnitude;

           
            if (dist < reference.radius + boundingSphereRadius)
            {
                //If the light is in range, add it to the list of lights to use for this object
                lightsInRange.Add(reference);
            }


        }
        int numHighQualityLights = 0;
        foreach (var lightRec in lightsInRange)
        {

            lightRec.lightRef.TryGetTarget(out PointLight light);

            if (light)
            {
                lightsPositions[numHighQualityLights] = light.transform.position;
                lightColors[numHighQualityLights] = light.color * light.intensity;
                lightRadius[numHighQualityLights] = light.range;
                numHighQualityLights++;
                if (numHighQualityLights == 2)
                {
                    break;
                }
            }
                
        }

        //calculate the sun at this models origin
        //Update: Use shadows instead
        //loat localSun = 1.0f - world.CalculateSunShadowAtPoint(transform.position, 1, Vector3.up);

        if (block == null)
        {
            block = new MaterialPropertyBlock();
        }
        meshRenderer.GetPropertyBlock(block);
                
        if (numHighQualityLights > 0)
        {
            block.SetVectorArray("globalDynamicLightPos", lightsPositions);
            block.SetVectorArray("globalDynamicLightColor", lightColors);
            block.SetFloatArray("globalDynamicLightRadius", lightRadius);
        }
        
        if (numHighQualityLights == 0)
        {
            mat.EnableKeyword("NUM_LIGHTS_LIGHTS0");
            mat.DisableKeyword("NUM_LIGHTS_LIGHTS1");
            mat.DisableKeyword("NUM_LIGHTS_LIGHTS2");
        }
        if (numHighQualityLights == 1)
        {
            mat.DisableKeyword("NUM_LIGHTS_LIGHTS0");
            mat.EnableKeyword("NUM_LIGHTS_LIGHTS1");
            mat.DisableKeyword("NUM_LIGHTS_LIGHTS2");
        }
        if (numHighQualityLights == 2)
        {
            mat.DisableKeyword("NUM_LIGHTS_LIGHTS0");
            mat.DisableKeyword("NUM_LIGHTS_LIGHTS1");
            mat.EnableKeyword("NUM_LIGHTS_LIGHTS2");
        }

        if (world.cubeMap) {
            if (mat.HasProperty("_CubeTex")) {
                block.SetTexture("_CubeTex", world.cubeMap);
            }
        }
        
        //Update: Use shadows instead
        //block.SetFloat("_SunScale", localSun);
        meshRenderer.SetPropertyBlock(block);
        
    }

    private void Awake()
    {
        Register();
    }

    private void OnEnable()
    {
        Register();
    }

#if(UNITY_EDITOR)
    private void OnValidate()
    {
        Register();
    }
#endif

    private void OnDestroy()
    {
        Unregister();
    }
    
    private void Register()
    {
        var manager = VoxelWorldStuff.SingletonClassManager<VoxelWorldMeshUpdater>.Instance;
        manager.RegisterItem(this);
    }

    private void Unregister()
    {
        var manager = VoxelWorldStuff.SingletonClassManager<VoxelWorldMeshUpdater>.Instance;
        manager.UnregisterItem(this);
    }
}

