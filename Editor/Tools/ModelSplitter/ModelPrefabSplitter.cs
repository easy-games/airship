using NUnit.Framework.Internal;
using System.Collections.Generic;
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

        Dictionary<string, List<MeshFilter>> combinedList = new();
        foreach (MeshFilter meshFilter in meshFilters){

            string meshName = meshFilter.name;
            if (meshName.Contains("-")) {
                meshName = meshName.Substring(0, meshName.IndexOf("-"));
            }

            if (!combinedList.ContainsKey(meshName)) {
                combinedList.Add(meshName, new List<MeshFilter>());
            }

            combinedList[meshName].Add(meshFilter);
        }


        foreach (var rec in combinedList) {
            //Skip if meshFilter.Name begins with a _
            if (rec.Key.StartsWith("_"))
            {
                continue;
            }

            GameObject newPrefab = new GameObject(rec.Key);
            newPrefab.transform.position = Vector3.zero;
            newPrefab.transform.rotation = rec.Value[0].transform.rotation;
            newPrefab.transform.localScale = rec.Value[0].transform.localScale;
            
            if (rec.Value.Count == 1) {
              
                MeshRenderer renderer = newPrefab.AddComponent<MeshRenderer>();
                if (modelMaterial != null)
                {
                    renderer.sharedMaterial = modelMaterial;
                }
                else
                {
                    renderer.sharedMaterials = rec.Value[0].GetComponent<MeshRenderer>().sharedMaterials;
                }

                MeshFilter newMeshFilter = newPrefab.AddComponent<MeshFilter>();
                newMeshFilter.sharedMesh = rec.Value[0].sharedMesh;
            }
            else {
               

                //Add each meshfilter as its own subobject
                foreach (MeshFilter meshFilter in rec.Value) {
                    GameObject subObject = new GameObject(meshFilter.name);
                    subObject.transform.parent = newPrefab.transform;
                    subObject.transform.localPosition = Vector3.zero;
                    subObject.transform.localRotation = Quaternion.identity;
                    subObject.transform.localScale = Vector3.one;

                    MeshRenderer renderer = subObject.AddComponent<MeshRenderer>();
                    if (modelMaterial != null) {
                        renderer.sharedMaterial = modelMaterial;
                    }
                    else {
                        renderer.sharedMaterials = meshFilter.GetComponent<MeshRenderer>().sharedMaterials;
                    }

                    MeshFilter newMeshFilter = subObject.AddComponent<MeshFilter>();
                    newMeshFilter.sharedMesh = meshFilter.sharedMesh;
                }
            }
            

            string prefabPath = modelDirectory + "/" + rec.Key + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(newPrefab, prefabPath);
            DestroyImmediate(newPrefab);
        }

        Debug.Log("Model split into prefabs successfully.");
    }
}
