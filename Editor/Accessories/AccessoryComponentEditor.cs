using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AccessoryComponent))]
public class AccessoryComponentEditor : UnityEditor.Editor {
    public override void OnInspectorGUI() {
        AccessoryComponent myTarget = (AccessoryComponent)target;

        EditorGUILayout.LabelField("Single Character Accessory");
        #if AIRSHIP_INTERNAL
        myTarget.serverClassId = EditorGUILayout.TextField("Server Class Id", myTarget.serverClassId);
        #endif

        //Accessory Slot
        myTarget.accessorySlot = (AccessorySlot)EditorGUILayout.EnumFlagsField("Slot", myTarget.accessorySlot);

        //Visibility Mode
        myTarget.visibilityMode = (AccessoryComponent.VisibilityMode)EditorGUILayout.EnumFlagsField("Visibility", myTarget.visibilityMode);

        //Skinned To Character
        myTarget.skinnedToCharacter = EditorGUILayout.Toggle("Skinned", myTarget.skinnedToCharacter);
    }
}