using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MaterialInfo : MonoBehaviour
{
    // An array to store the materials
    [HideInInspector]
    public Material[] Materials;
 

#if UNITY_EDITOR
    [CustomEditor(typeof(MaterialInfo))]
    public class MaterialInfoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            MaterialInfo materialInfo = (MaterialInfo)target;
            
            Renderer renderer = materialInfo.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material[] Materials = renderer.sharedMaterials;
            
                foreach (Material mat in Materials)
                {
                    if (mat != null)
                    {
                        EditorGUILayout.LabelField("Shader keywords for material " + mat.name + ":");
                        foreach (string keyword in mat.shaderKeywords)
                        {
                            EditorGUILayout.LabelField(keyword);
                        }
                    }
                }
            }
        }
    }
#endif
}
