using System.Collections.Generic;
using UnityEngine;

public class AirshipRendererManager : Singleton<AirshipRendererManager>
{
    private Dictionary<Renderer, RendererReference> rendererReferences = new Dictionary<Renderer, RendererReference>();
    private Renderer[] allRenderers;
 
    public void PerFrameUpdate()
    {
        UpdateRendererReferences();
    }

    private void UpdateRendererReferences()
    {
        allRenderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
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
        
        private Dictionary<Material, MaterialPropertyBlock> propertyBlock;
        private HashSet<MaterialPropertyBlock> dirtyPropertyBlocks;

        //Lighting stuff
        Vector4[] lightsPositions = new Vector4[2];
        Vector4[] lightColors = new Vector4[2];
        float[] lightRadius = new float[2];
        bool hasBeenAffectedByLight = false; //Sets to true once this has been lit, so we know when to unlight it when lights drop to 0

        List<AirshipPointLight> lightsInRange = new(8);
        
        bool usingUniqueMaterials = false;
        Material[] previousMaterialArray;
        


        //Call this to enforce this renderer to use a set of locally unique material
        public void EnableUniqueMaterials()
        {
            if (usingUniqueMaterials == true)
            {
                return;
            }
            usingUniqueMaterials = true;

            Material[] newMats = new Material[renderer.sharedMaterials.Length];
            previousMaterialArray = renderer.sharedMaterials;


            for (int j = 0; j < previousMaterialArray.Length; j++)
            {
                Material mat = previousMaterialArray[j];
                Material clonedMaterial = new Material(mat);
                //Fun debug check - shows where materials came from
                //clonedMaterial.name = mat.name + "_" + renderer.gameObject.name;
                newMats[j] = clonedMaterial;
            }

            //Swap the propertyBlocks too
            if (propertyBlock != null)
            {
                Dictionary<Material, MaterialPropertyBlock> newPropertyBlock = new();
                for (int j = 0; j < previousMaterialArray.Length; j++)
                {
                    Material mat = previousMaterialArray[j];
                    bool found = propertyBlock.TryGetValue(mat, out MaterialPropertyBlock oldBlock);
                    if (found == true)
                    {
                        newPropertyBlock.Add(newMats[j], oldBlock);
                    }
                }
                propertyBlock = newPropertyBlock;
            }
            renderer.sharedMaterials = newMats;
        }

        //Call this to go back to whatever the materials were before you called it
        public void DisableUniqueMaterials()
        {
            if (usingUniqueMaterials == false)
            {
                return;
            }
            usingUniqueMaterials = false;
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

        public MaterialPropertyBlock GetPropertyBlock(Material mat)
        {
            if (propertyBlock == null)
            {
                propertyBlock = new();
            }

            //Find a propertyBlock for this material
            bool found = propertyBlock.TryGetValue(mat, out MaterialPropertyBlock block);
            if (found == false)
            {
                //Stash it in a dictionary
                MaterialPropertyBlock newBlock = new MaterialPropertyBlock();
                propertyBlock.Add(mat, newBlock);
                block = newBlock;

                //The actual fetch of the propertyBlock.
                //This should be the only place GetPropertyBlock should be called
                renderer.GetPropertyBlock(block);
            }


            //Mark it as "dirty" this frame
            if (dirtyPropertyBlocks == null)
            {
                dirtyPropertyBlocks = new();
            }
            dirtyPropertyBlocks.Add(block);

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

            //Set the propertyblock for all materials if someone accessed the propertyBlock this frame
            foreach (MaterialPropertyBlock block in dirtyPropertyBlocks)
            {
                renderer.SetPropertyBlock(block);
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

                //lightRec.lightRef.TryGetTarget(out PointLight light);

                //if (light)
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

            }

            //AirshipRendererManager.RendererReference rendererReference = AirshipRendererManager.Instance.GetRendererReference(meshRenderer);

            if (numHighQualityLights > 0)
            {
                hasBeenAffectedByLight = true;

                //Call this before setting any keywords
                EnableUniqueMaterials();

                Material[] mats = renderer.sharedMaterials;
            
                foreach (Material mat in mats)
                {
                    MaterialPropertyBlock block = GetPropertyBlock(mat);
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

                    foreach (Material mat in mats)
                    {
                        mat.EnableKeyword("NUM_LIGHTS_LIGHTS0");
                        mat.DisableKeyword("NUM_LIGHTS_LIGHTS1");
                        mat.DisableKeyword("NUM_LIGHTS_LIGHTS2");
                    }
                }
            }

        }
    }
}
