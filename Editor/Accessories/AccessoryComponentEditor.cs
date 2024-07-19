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
        myTarget.accessorySlot = (AccessorySlot)EditorGUILayout.EnumPopup("Slot", myTarget.accessorySlot);

        //Visibility Mode
        myTarget.visibilityMode = (AccessoryComponent.VisibilityMode)EditorGUILayout.EnumPopup("Visibility", myTarget.visibilityMode);

        //Skinned To Character
        myTarget.skinnedToCharacter = EditorGUILayout.Toggle("Skinned", myTarget.skinnedToCharacter);

        if(GUI.changed){
            EditorUtility.SetDirty(myTarget);
        }
    }
}