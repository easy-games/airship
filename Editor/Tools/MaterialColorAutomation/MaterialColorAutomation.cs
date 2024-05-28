#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using Airship;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class MaterialMonitor
{
    private static double nextUpdate = 0;
    
    // List of shader names to check for
    private static readonly string[] ShaderNames = {
        "Airship/WorldShaderPBR",
        "Airship/WorldShaderPBRTransparent",
        // Add more shader names as needed
    };

    static MaterialMonitor()
    {
        EditorApplication.update += CheckMaterials;
    }

    [MenuItem("Airship/Rendering/Reset All Material Colors")]
    public static void ResetAllMaterialColors() {

        if (ARPConfig.IsDisabled) return;
        var materialColors = GameObject.FindObjectsByType<MaterialColor>(FindObjectsSortMode.None);
        if (!EditorUtility.DisplayDialog(
                "Reset All Material Colors?",
                $"Are you sure you want to reset {materialColors.Length} Material Color components?",
                "Reset All",
                "Cancel")
            ) return;

        List<GameObject> gameObjects = new List<GameObject>();
        foreach (var materialColor in materialColors) {
            gameObjects.Add(materialColor.gameObject);
        }

        foreach (var go in gameObjects) {
            var comps = go.GetComponents<MaterialColor>();
            foreach (var comp in comps) {
                Object.DestroyImmediate(comp);
            }
            var matColor = go.AddComponent<MaterialColor>();
            matColor.addedByEditorScript = true;
            matColor.EditorFirstTimeSetup();
        }
    }

    private static void CheckMaterials()
    {
        if (ARPConfig.IsDisabled) return;
        if (EditorIntegrationsConfig.instance.autoAddMaterialColor == false)
        {
            return;
        }
        
        //Add a delay on updates, only once a second since last update
        if (EditorApplication.timeSinceStartup < nextUpdate)
        {
            return;
        }
        
        if (!Application.isPlaying)
        {
            //Check the current stage for all materials
            Renderer[] renderers = StageUtility.GetCurrentStageHandle().FindComponentsOfType<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                //make sure it doesnt have a MeshCombiner component
                if (renderer.gameObject.name == "MeshCombinerSkinned")
                {
                    continue;
                }
                if (renderer.gameObject.name == "MeshCombinerStatic")
                {
                    continue;
                }
                if (renderer.gameObject.name == "Chunk")
                {
                    continue;
                }


                bool hasTargetedShader = renderer.sharedMaterials.Any(mat => mat != null && ShaderNames.Contains(mat.shader.name));

                MaterialColor matColorComponent = renderer.gameObject.GetComponent<MaterialColor>();

                if (renderer.gameObject.name == "Chunk") {
                    continue;
                }

                if (hasTargetedShader)
                {
                    if (matColorComponent == null)
                    {
                        matColorComponent = Undo.AddComponent<MaterialColor>(renderer.gameObject);
                        matColorComponent.addedByEditorScript = true;
                        matColorComponent.EditorFirstTimeSetup();
                    }
                }
                else if (matColorComponent && matColorComponent.addedByEditorScript)
                {
                    Undo.DestroyObjectImmediate(matColorComponent);
                }
            }

            nextUpdate = EditorApplication.timeSinceStartup + 1.0;
        }
    }
}
#endif