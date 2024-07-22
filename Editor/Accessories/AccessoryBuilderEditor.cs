using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AccessoryBuilder))]
public class AccessoryBuilderEditor : UnityEditor.Editor{
    public override void OnInspectorGUI() {
        AccessoryBuilder builder = (AccessoryBuilder)target;
        EditorGUILayout.LabelField("Requried Setup");
        DrawDefaultInspector();

        EditorGUILayout.Space(30);

        EditorGUILayout.LabelField("Editor Setup");
        builder.currentOutfit = (AccessoryOutfit)EditorGUILayout.ObjectField("Outfit", builder.currentOutfit, typeof(AccessoryOutfit), true);
        
        if (GUILayout.Button("Equip Outfit") && builder.currentOutfit != null) {
            Debug.Log("Equipping outfit " + builder.currentOutfit.name);
            builder.EquipAccessoryOutfit(builder.currentOutfit, true);
        }

        if (GUILayout.Button("Clear Outfit")) {
            Debug.Log("Clearing outfit.");
            builder.RemoveClothingAccessories();
            builder.SetSkinColor(new Color(0.7169812f, 0.5064722f, 0.3754005f), true);
        }

        if(GUI.changed){
            EditorUtility.SetDirty(builder);
        }
    }
}
