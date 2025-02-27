using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

public class CoreAssetReferences : MonoBehaviour
{
    [SerializeField]
    public UnityEngine.Object[] AssetReferences;
}


#if UNITY_EDITOR
[CustomEditor(typeof(CoreAssetReferences))]
public class CoreAssetReferencesEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        CoreAssetReferences coreAssetReferences = (CoreAssetReferences)target;

        if (GUILayout.Button("Update Asset References"))
        {
            //Grab all the shaders in the project
            Debug.Log("Searching...");
            string[] shaderGUIDs = AssetDatabase.FindAssets("t:Shader");
            Debug.Log("Found " + shaderGUIDs.Length);
            //List<UnityEngine.Object> shaderObjects = new List<UnityEngine.Object>();
            int count = 0;
            
            //Appent them if they dont exist
            foreach (string shaderGUID in shaderGUIDs)
            {
                string shaderPath = AssetDatabase.GUIDToAssetPath(shaderGUID);
               
                
                if (shaderPath.Contains("com.unity.") == false)
                {
                    UnityEngine.Object shaderObject = AssetDatabase.LoadAssetAtPath(shaderPath, typeof(UnityEngine.Object));
                    if (!coreAssetReferences.AssetReferences.Contains(shaderObject))
                    {
                        //Add to assetReferences
                        Debug.Log("Adding "+ shaderPath +" "+ shaderObject);
                        count += 1;            
                        coreAssetReferences.AssetReferences = coreAssetReferences.AssetReferences.Append(shaderObject).ToArray();
                    }
                }
            }
            if (count > 0)
            {
                //Mark scene as dirty
                EditorUtility.SetDirty(coreAssetReferences);
            }
            Debug.Log("Added " + count + " shaders");


        }
    }
}
#endif
