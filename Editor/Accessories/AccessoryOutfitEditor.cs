using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AccessoryOutfit))]
public class AccessoryOutfitEditor : UnityEditor.Editor{
    public override void OnInspectorGUI() {
        AccessoryOutfit myTarget = (AccessoryOutfit)target;
        DrawDefaultInspector();

        //TODO: let user select accessories via a thumbnail list

        if(GUI.changed){
            EditorUtility.SetDirty(myTarget);
        }
    }

    [MenuItem("Airship/Open Asset Bundle")]
    public static void TestOpenAssetBundle() {
        string path = "Assets/TestBundles/CoreMaterials";
        AssetBundle bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null) {
            Debug.LogError("Failed to load asset bundle.");
        }

        Debug.Log("<color=green>Success!</color>");

        foreach (var name in bundle.GetAllAssetNames()) {
            Debug.Log("  - " + name);
        }

        Debug.Log("------------");
        GameObject prefab = bundle.LoadAsset<GameObject>("assets/airshippackages/@easy/corematerials/materiallibrary/codestrip/__decal projector.prefab");
        GameObject go = Object.Instantiate(prefab);
        Debug.Log("Instantiated " + go.name);

        Debug.Log("Unloading bundle...");
        bundle.Unload(true);
        Debug.Log("Unloaded.");
    }
}
