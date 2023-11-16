using UnityEditor;
using UnityEngine;

public class ModelSplitter : MonoBehaviour
{
    [MenuItem("Assets/Split Model into Prefabs")]
    private static void SplitModelIntoPrefabs()
    {
        GameObject selectedObject = Selection.activeGameObject;

        if (selectedObject == null || !(selectedObject is GameObject))
        {
            Debug.LogError("Please select a model asset in the project window.");
            return;
        }

        MeshFilter[] meshFilters = selectedObject.GetComponentsInChildren<MeshFilter>();
        if (meshFilters == null || meshFilters.Length == 0)
        {
            Debug.LogError("The selected model does not contain any MeshFilter components.");
            return;
        }

        string modelPath = AssetDatabase.GetAssetPath(selectedObject);
        string modelDirectory = System.IO.Path.GetDirectoryName(modelPath);
        string modelName = selectedObject.name;

        Material modelMaterial = null;
        string materialPath = modelDirectory + "/" + modelName + ".mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) is Material foundMaterial)
        {
            modelMaterial = foundMaterial;
        }

        foreach (MeshFilter meshFilter in meshFilters)
        {
            GameObject newPrefab = new GameObject(meshFilter.name);
            newPrefab.transform.position = Vector3.zero;
            newPrefab.transform.rotation = meshFilter.transform.rotation;
            newPrefab.transform.localScale = meshFilter.transform.localScale;

            MeshRenderer renderer = newPrefab.AddComponent<MeshRenderer>();
            if (modelMaterial != null)
            {
                renderer.sharedMaterial = modelMaterial;
            }
            else
            {
                renderer.sharedMaterials = meshFilter.GetComponent<MeshRenderer>().sharedMaterials;
            }

            MeshFilter newMeshFilter = newPrefab.AddComponent<MeshFilter>();
            newMeshFilter.sharedMesh = meshFilter.sharedMesh;

            string prefabPath = modelDirectory + "/" + meshFilter.name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(newPrefab, prefabPath);
            DestroyImmediate(newPrefab);
        }

        Debug.Log("Model split into prefabs successfully.");
    }
}
