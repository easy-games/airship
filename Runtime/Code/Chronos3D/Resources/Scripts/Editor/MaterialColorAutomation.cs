#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;

[InitializeOnLoad]
public class MaterialMonitor
{
    private static double nextUpdate = 0;
    
    // List of shader names to check for
    private static readonly string[] ShaderNames = {
        "Chronos/WorldShaderPBR",
        "Chronos/WorldShaderPBRTransparent",
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
            Renderer[] renderers = GameObject.FindObjectsOfType<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                bool hasTargetedShader = renderer.sharedMaterials.Any(mat => mat != null && ShaderNames.Contains(mat.shader.name));

                MaterialColor matColorComponent = renderer.gameObject.GetComponent<MaterialColor>();

                if (hasTargetedShader)
                {
                    if (matColorComponent == null)
                    {
                        matColorComponent = Undo.AddComponent<MaterialColor>(renderer.gameObject);
                        matColorComponent.addedByEditorScript = true;
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