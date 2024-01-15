using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AvatarCollection))]
public class AvatarCollectionEditor : UnityEditor.Editor {
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        if (GUILayout.Button("FILL ARRAY WITH ACCESSORIES")) {
            FillAccessories((AvatarCollection)target);
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void FillAccessories(AvatarCollection collection) {
        var allAccessoryGuids = AssetDatabase.FindAssets("t: pref ref:{t:Scripts/AccessoryComponent.cs");
        var allAccessories = new List<AccessoryComponent>();
        foreach (var guid in allAccessoryGuids) {
            var accessory = AssetDatabase.LoadAssetAtPath<AccessoryComponent>(AssetDatabase.GUIDToAssetPath(guid));
            Debug.Log("Found Accessory: " + accessory.name);
            allAccessories.Add(accessory);
        }

        if (allAccessories.Count > 0) {
            collection.generalAccessories = allAccessories.ToArray();
        }
    }
}
