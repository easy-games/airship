using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.Profiling;

public class AirshipRendererManager {
    [NonSerialized]
    private Dictionary<Renderer, RendererReference> rendererReferences = new Dictionary<Renderer, RendererReference>();
    [NonSerialized]
    private Renderer[] allRenderers;


    private static AirshipRendererManager instance;
    public static AirshipRendererManager Instance {
        get {
            if (instance == null) {
                instance = new AirshipRendererManager();
            }
            return instance;
        }
    }
     
    public void PerFrameUpdate() {
        Profiler.BeginSample("PerFrameUpdate");
        UpdateRendererReferences();
        Profiler.EndSample();
    }
    
    private void FindAllRenderers() {
#if UNITY_EDITOR
        allRenderers = StageUtility.GetCurrentStageHandle().FindComponentsOfType<Renderer>();
#else
        allRenderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#endif
    }
    
    private void UpdateRendererReferences() {
        FindAllRenderers();

        var foundRenderers = new HashSet<Renderer>(allRenderers);

        // Add new renderers.
        foreach (var renderer in foundRenderers) {
            if (!rendererReferences.ContainsKey(renderer)) {
                rendererReferences[renderer] = new RendererReference { renderer = renderer };
            }
        }

        // Remove missing or destroyed renderers.
        var keysToRemove = new List<Renderer>();
        foreach (var pair in rendererReferences) {
            if (pair.Key == null || !foundRenderers.Contains(pair.Key)) {
                pair.Value.OnCleanup();
                keysToRemove.Add(pair.Key);
            }
        }

        foreach (var key in keysToRemove) {
            rendererReferences.Remove(key);
        }
    }

    // Returns all currently active Renderers.
    public Renderer[] GetRenderers() {
        return allRenderers;
    }

    // Returns the RendererReference for a given Renderer, if it exists, and adds it if not.
    public RendererReference GetRendererReference(Renderer renderer) {
        if (renderer == null) {
            return null; // Or throw an exception, depending on your policy.
        }

        if (!rendererReferences.TryGetValue(renderer, out RendererReference rendererReference)) {
            // Renderer is not tracked yet, add it immediately.
            rendererReference = new RendererReference { renderer = renderer };
            rendererReferences[renderer] = rendererReference;
            // Note: This does not handle the removal of this renderer if it's destroyed before the next Update.
            // You might need additional logic to handle such cases if they can occur in your application.
        }

        return rendererReference;
    }

    public void PreRender() {
        foreach (var renderer in rendererReferences.Values) {
            
            renderer.PreRender();
        }
    }

    public class RendererReference {
        public Renderer renderer;

        private Dictionary<KeyValuePair<Material, int>, MaterialPropertyBlock> propertyBlock;
        private HashSet<KeyValuePair<MaterialPropertyBlock, int>> dirtyPropertyBlocks;

        //Lighting stuff
        bool usingEngineShaderVariants = false;
        Material[] previousMaterialArray;
        

        //Call this to enforce this renderer to use a set of locally unique material
        public void EnableEngineShaderVariants() {
            if (!Application.isPlaying || usingEngineShaderVariants == true) {
                return;
            }
            usingEngineShaderVariants = true;

            Material[] newMats = new Material[renderer.sharedMaterials.Length];
            previousMaterialArray = renderer.sharedMaterials;



            for (int j = 0; j < previousMaterialArray.Length; j++) {
                Material mat = previousMaterialArray[j];
                Material clonedMaterial = new Material(mat);

                if (clonedMaterial.name.EndsWith("_engine") == false) {
                    clonedMaterial.name += "_engine";
                }

                newMats[j] = clonedMaterial;
            }

            //Swap the propertyBlocks too
            if (propertyBlock != null) {
                Dictionary<KeyValuePair<Material, int>, MaterialPropertyBlock> newPropertyBlock = new();
                for (int j = 0; j < previousMaterialArray.Length; j++) {
                    Material mat = previousMaterialArray[j];
                    KeyValuePair<Material, int> key = new(mat, j);
                    bool found = propertyBlock.TryGetValue(key, out MaterialPropertyBlock oldBlock);
                    if (found == true) {
                        newPropertyBlock.Add(new(newMats[j], j), oldBlock);
                    }
                }
                propertyBlock = newPropertyBlock;
            }
            renderer.sharedMaterials = newMats;
        }

        //Call this to go back to whatever the materials were before you called it
        public void DisableEngineShaderVariants() {
            if (usingEngineShaderVariants == false) {
                return;
            }
            usingEngineShaderVariants = false;
            renderer.sharedMaterials = previousMaterialArray;
            previousMaterialArray = null;
        }

        //Add a destructor
        public void OnCleanup() {
            // Do any cleanup here.
            if (propertyBlock != null) {
                propertyBlock.Clear();
                propertyBlock = null;
            }
        }


        public MaterialPropertyBlock GetPropertyBlock(Material mat, int subMaterialIndex = 0) {
            if (propertyBlock == null) {
                propertyBlock = new();
            }

            if (mat == null) {
                return null;
            }

            KeyValuePair<Material, int> key = new(mat, subMaterialIndex);

            //Find a propertyBlock for this material
            bool found = propertyBlock.TryGetValue(key, out MaterialPropertyBlock block);

            if (found == false) {
                //Stash it in a dictionary
                MaterialPropertyBlock newBlock = new MaterialPropertyBlock();
                propertyBlock.Add(key, newBlock);
                block = newBlock;

                //The actual fetch of the propertyBlock.
                //This should be the only place GetPropertyBlock should be called
                renderer.GetPropertyBlock(block, subMaterialIndex);
            }


            //Mark it as "dirty" this frame
            if (dirtyPropertyBlocks == null) {
                dirtyPropertyBlocks = new();
            }
            dirtyPropertyBlocks.Add(new(block, subMaterialIndex));

            return block;
        }

        public void PreRender() {
            
            if (dirtyPropertyBlocks == null) {
                return;
            }
            if (dirtyPropertyBlocks.Count == 0) {
                return;
            }
            Profiler.BeginSample("UpdateDirtyPropertyBlocks");
            //Debug.Log("Num props" + dirtyPropertyBlocks.Count + " (" + renderer.name + ")");
            //Set the propertyblock for all materials if someone accessed the propertyBlock this frame
            foreach (var blockKey in dirtyPropertyBlocks) {
                renderer.SetPropertyBlock(blockKey.Key, blockKey.Value);
            }
            dirtyPropertyBlocks.Clear();
            Profiler.EndSample();
            
        }


     
    }
}
