using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AccessoryOutfit))]
public class AccessoryOutfitEditor : UnityEditor.Editor {
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        /*if (GUILayout.Button("FILL ARRAY WITH ACCESSORIES")) {
            FillAccessories((AccessoryOutfit)target);
        }*/
        serializedObject.ApplyModifiedProperties();
    }
    [MenuItem("Airship/Avatar/Fill All Avatar Accessories")]
    private static void FillAvatarOutfit() {
        Debug.Log("Grabbing all avatar accessories");
        string folderPath = Application.dataPath + "/Bundles/@Easy/Core/Shared/Resources/Accessories/AvatarItems";
        string allItemsPath
            = "Assets/Bundles/@Easy/Core/Shared/Resources/Accessories/AvatarItems/AllAvatarItems.asset";
        AccessoryOutfit allAccessories = AssetDatabase.LoadAssetAtPath<AccessoryOutfit>(allItemsPath);
        List<AccessoryComponent> accs = new List<AccessoryComponent>();
        int count = 0;
        GetAccessoriesInFolder(ref count, ref accs, folderPath);

        allAccessories.accessories = accs.ToArray();
        EditorUtility.SetDirty(allAccessories);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void GetAccessoriesInFolder(ref int count, ref List<AccessoryComponent> allAccessories, string folderPath) {
        if (!Directory.Exists(folderPath)) {
            Debug.LogWarning("No folder found: " + folderPath);
            return;
        }

        count++;
        if (count > 1000) {
            Debug.LogError("INFINITE FOLDER CHECK!");
            return;
        }
        
        var filePaths = Directory.GetFiles(folderPath);
        foreach (var filePath in filePaths) {
            if (Path.GetExtension(filePath) == ".prefab") {
                string relativePath =  "Assets" + filePath.Substring(Application.dataPath.Length);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(relativePath);
                var acc = go.GetComponent<AccessoryComponent>();
                if (acc) {
                    Debug.Log("Found Accessory: " + relativePath);
                    allAccessories.Add(acc);
                }
            }
        }

        foreach (var directory in Directory.GetDirectories(folderPath)) {
            GetAccessoriesInFolder(ref count, ref allAccessories, directory);
        }
    }
}
