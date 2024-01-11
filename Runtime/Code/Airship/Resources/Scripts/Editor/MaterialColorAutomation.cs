#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using Airship;

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

    private static void CheckMaterials()
    {
        //Add a delay on updates, only once a second since last update
        if (EditorApplication.timeSinceStartup < nextUpdate)
        {
            return;
        }


        if (!Application.isPlaying)
        {
            // Check all renderers in the scene
            Renderer[] renderers = GameObject.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
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