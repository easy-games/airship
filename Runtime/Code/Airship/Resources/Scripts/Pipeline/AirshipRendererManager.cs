using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

[ExecuteInEditMode]
public class AirshipRendererManager : Singleton<AirshipRendererManager>
{
    [NonSerialized]
    private Dictionary<Renderer, RendererReference> rendererReferences = new Dictionary<Renderer, RendererReference>();
    [NonSerialized]
    private Renderer[] allRenderers;
 
    public void PerFrameUpdate()
    {
        UpdateRendererReferences();
    }
    void Awake()
    {
        // Set the HideFlags for this component to be hidden and not saved.
        this.hideFlags = HideFlags.HideAndDontSave;
    }

    private void FindAllRenderers()
    {
#if UNITY_EDITOR
        allRenderers = StageUtility.GetCurrentStageHandle().FindComponentsOfType<Renderer>();
#else
        allRenderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#endif
    }


    private void UpdateRendererReferences()
    {
        FindAllRenderers();
        var foundRenderers = new HashSet<Renderer>(allRenderers);

        // Add new renderers.
        foreach (var renderer in foundRenderers)
        {
            if (!rendererReferences.ContainsKey(renderer))
            {
                rendererReferences[renderer] = new RendererReference { renderer = renderer };
            }
        }

        // Remove missing or destroyed renderers.
        var keysToRemove = new List<Renderer>();
        foreach (var pair in rendererReferences)
        {
            if (pair.Key == null || !foundRenderers.Contains(pair.Key))
            {
                pair.Value.OnCleanup();
                keysToRemove.Add(pair.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            rendererReferences.Remove(key);
        }
    }

    // Returns all currently active Renderers.
    public Renderer[] GetRenderers()
    {
        return allRenderers;
    }

    // Returns the RendererReference for a given Renderer, if it exists, and adds it if not.
    public RendererReference GetRendererReference(Renderer renderer)
    {
        if (renderer == null)
        {
            return null; // Or throw an exception, depending on your policy.
        }

        if (!rendererReferences.TryGetValue(renderer, out RendererReference rendererReference))
        {
            // Renderer is not tracked yet, add it immediately.
            rendererReference = new RendererReference { renderer = renderer };
            rendererReferences[renderer] = rendererReference;
            // Note: This does not handle the removal of this renderer if it's destroyed before the next Update.
            // You might need additional logic to handle such cases if they can occur in your application.
        }

        return rendererReference;
    }

    public void PreRender()
    {
        if (RunCore.IsServer() && Application.isPlaying)
        {
            return;
        }
        
        foreach (var renderer in rendererReferences.Values)
        {
            renderer.UpdateLights();
            renderer.PreRender();
        }
    }

    public class RendererReference
    {
        public Renderer renderer;
        
        private Dictionary<KeyValuePair<Material, int>, MaterialPropertyBlock> propertyBlock;
        private HashSet<KeyValuePair<MaterialPropertyBlock, int>> dirtyPropertyBlocks;

        //Lighting stuff
        Vector4[] lightsPositions = new Vector4[2];
        Vector4[] lightColors = new Vector4[2];
        float[] lightRadius = new float[2];
        bool hasBeenAffectedByLight = false; //Sets to true once this has been lit, so we know when to unlight it when lights drop to 0

        List<AirshipPointLight> lightsInRange = new(8);
        
        bool usingEngineShaderVariants = false;
        Material[] previousMaterialArray;
        


        //Call this to enforce this renderer to use a set of locally unique material
        public void EnableEngineShaderVariants()
        {
            if (usingEngineShaderVariants == true)
            {
                return;
            }
            usingEngineShaderVariants = true;

            Material[] newMats = new Material[renderer.sharedMaterials.Length];
            previousMaterialArray = renderer.sharedMaterials;

           

            for (int j = 0; j < previousMaterialArray.Length; j++)
            {
                Material mat = previousMaterialArray[j];
                Material clonedMaterial = new Material(mat);
                
                if (clonedMaterial.name.EndsWith("_engine") == false)
                {
                    clonedMaterial.name += "_engine";
                }
                
                newMats[j] = clonedMaterial;
            }

            //Swap the propertyBlocks too
            if (propertyBlock != null)
            {
                Dictionary<KeyValuePair<Material, int>, MaterialPropertyBlock> newPropertyBlock = new();
                for (int j = 0; j < previousMaterialArray.Length; j++)
                {
                    Material mat = previousMaterialArray[j];
                    KeyValuePair<Material, int> key = new(mat, j);
                    bool found = propertyBlock.TryGetValue(key, out MaterialPropertyBlock oldBlock);
                    if (found == true)
                    {
                        newPropertyBlock.Add(new(newMats[j],j), oldBlock);
                    }
                }
                propertyBlock = newPropertyBlock;
            }
            renderer.sharedMaterials = newMats;
        }

        //Call this to go back to whatever the materials were before you called it
        public void DisableEngineShaderVariants()
        {
            if (usingEngineShaderVariants == false)
            {
                return;
            }
            usingEngineShaderVariants = false;
            renderer.sharedMaterials = previousMaterialArray;
            previousMaterialArray = null;
        }

        //Add a destructor
        public void OnCleanup()
        {
            // Do any cleanup here.
            if (propertyBlock != null)
            {
                propertyBlock.Clear();
                propertyBlock = null;
            }
        }
 

        public MaterialPropertyBlock GetPropertyBlock(Material mat, int subMaterialIndex = 0)
        {
            if (propertyBlock == null)
            {
                propertyBlock = new();
            }

            if (mat == null)
            {
                return null;
            }

            KeyValuePair<Material, int> key = new(mat, subMaterialIndex);

            //Find a propertyBlock for this material
            bool found = propertyBlock.TryGetValue(key, out MaterialPropertyBlock block);
                       
            if (found == false)
            {
                //Stash it in a dictionary
                MaterialPropertyBlock newBlock = new MaterialPropertyBlock();
                propertyBlock.Add(key, newBlock);
                block = newBlock;

                //The actual fetch of the propertyBlock.
                //This should be the only place GetPropertyBlock should be called
                renderer.GetPropertyBlock(block, subMaterialIndex);
            }


            //Mark it as "dirty" this frame
            if (dirtyPropertyBlocks == null)
            {
                dirtyPropertyBlocks = new();
            }
            dirtyPropertyBlocks.Add(new(block, subMaterialIndex));

            return block;
        }

        public void PreRender()
        {
            if (dirtyPropertyBlocks == null)
            {
                return;
            }
            if (dirtyPropertyBlocks.Count == 0)
            {
                return;
            }
            //Debug.Log("Num props" + dirtyPropertyBlocks.Count + " (" + renderer.name + ")");
            //Set the propertyblock for all materials if someone accessed the propertyBlock this frame
            foreach (var blockKey in dirtyPropertyBlocks)
            {
                renderer.SetPropertyBlock(blockKey.Key,blockKey.Value);
            }
            dirtyPropertyBlocks.Clear();
        }


        public void UpdateLights()
        {
            lightsInRange.Clear();

            //get the bounds of this meshRenderer
            Bounds bounds = renderer.bounds;
            float boundingSphereRadius = bounds.extents.magnitude;

            Vector3 position = bounds.center;

            for (int i = 0; i < 2; i++)
            {
                lightColors[i] = Vector4.zero;
            }

            //Grab all the light references out of the world, and see if they're in range of this objects bounding box
            List<AirshipPointLight> pointLights = AirshipPointLight.GetAllPointLights();
            foreach (AirshipPointLight reference in pointLights)
            {
                Vector3 vec = (reference.transform.position - position);
                float dist = vec.magnitude;

                if (dist < reference.range + boundingSphereRadius)
                {
                    //If the light is in range, add it to the list of lights to use for this object
                    lightsInRange.Add(reference);
                }
            }
            int numHighQualityLights = 0;
            foreach (var light in lightsInRange)
            {
                //This could be much nicer
                lightsPositions[numHighQualityLights] = light.transform.position;
                lightColors[numHighQualityLights] = light.color * light.intensity;
                lightColors[numHighQualityLights].w = light.intensity;
                lightRadius[numHighQualityLights] = light.range;
                numHighQualityLights++;
                if (numHighQualityLights == 2)
                {
                    break;
                }
            }

            //Check if we're in the editor
            if (RunCore.IsEditor() == false)
            {             
                //If we're in runtime we're free to modify the materials as we see fit
                if (numHighQualityLights > 0)
                {
                    hasBeenAffectedByLight = true;

                    //Call this before setting any keywords
                    EnableEngineShaderVariants();

                    Material[] mats = renderer.sharedMaterials;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material mat = mats[i];
                        if (mat == null)
                        {
                            continue;
                        }
                        
                        MaterialPropertyBlock block = GetPropertyBlock(mat,i);
                        block.SetVectorArray("globalDynamicLightPos", lightsPositions);
                        block.SetVectorArray("globalDynamicLightColor", lightColors);
                        block.SetFloatArray("globalDynamicLightRadius", lightRadius);

                        switch (numHighQualityLights)
                        {
                            case 0:
                        
                                mat.EnableKeyword("NUM_LIGHTS_LIGHTS0");
                                mat.DisableKeyword("NUM_LIGHTS_LIGHTS1");
                                mat.DisableKeyword("NUM_LIGHTS_LIGHTS2");
                                break;
                            case 1:
                                mat.DisableKeyword("NUM_LIGHTS_LIGHTS0");
                                mat.EnableKeyword("NUM_LIGHTS_LIGHTS1");
                                mat.DisableKeyword("NUM_LIGHTS_LIGHTS2");
                                break;
                            case 2:
                            default:
                                mat.DisableKeyword("NUM_LIGHTS_LIGHTS0");
                                mat.DisableKeyword("NUM_LIGHTS_LIGHTS1");
                                mat.EnableKeyword("NUM_LIGHTS_LIGHTS2");
                                break;
                        }
                    }
                }
                else
                {
                    if (hasBeenAffectedByLight == true)
                    {
                        //Was lit, but now is not
                        hasBeenAffectedByLight = false;
                        Material[] mats = renderer.sharedMaterials;

                        for (int i = 0; i < mats.Length; i++)
                        {
                            Material mat = mats[i];
                            if (mat == null)
                            {
                                continue;
                            }
                            mat.EnableKeyword("NUM_LIGHTS_LIGHTS0");
                            mat.DisableKeyword("NUM_LIGHTS_LIGHTS1");
                            mat.DisableKeyword("NUM_LIGHTS_LIGHTS2");
                        }
                    }
                }

            }
            else
            {
                //In the editor we have to do it in a way that modifies all materials to act like the have maximum lights affecting them
                //This seems inefficient but it means that we can see the effect of the lights in the editor
                //without having to use unique materials all over the place (which we can do at runtime no fuss)

                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material mat = mats[i];
                    if (mat == null)
                    {
                        continue;
                    }
                    MaterialPropertyBlock block = GetPropertyBlock(mat, i);
                    
                    block.SetVectorArray("globalDynamicLightPos", lightsPositions);
                    block.SetVectorArray("globalDynamicLightColor", lightColors);
                    block.SetFloatArray("globalDynamicLightRadius", lightRadius);

                    mat.DisableKeyword("NUM_LIGHTS_LIGHTS0");
                    mat.DisableKeyword("NUM_LIGHTS_LIGHTS1");
                    mat.EnableKeyword("NUM_LIGHTS_LIGHTS2");
                }

            }
        }
    }
}
