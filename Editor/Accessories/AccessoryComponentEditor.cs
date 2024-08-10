using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Editor.Accessories;
using System.Linq;
using Code.Player.Accessories;

[CustomEditor(typeof(AccessoryComponent))]
public class AccessoryComponentEditor : UnityEditor.Editor {

    private bool foldout = false; // Variable to handle foldout state

    private void OnEnable() {
        var accessoryComponent = (AccessoryComponent)target;
        foldout = accessoryComponent.bodyMask > 0;
    }

    [MenuItem("Airship/Internal/UpdateClassIds")]
    public static void UpdateIds() {
        var collection = AssetDatabase.LoadAssetAtPath<AvatarAccessoryCollection>(
            "Assets/AirshipPackages/@Easy/Core/Prefabs/Accessories/AvatarItems/EntireAvatarCollection.asset");
        if (!collection) {
            Debug.LogError("Failed to find collection.");
            return;
        }
        foreach (var accessory in collection.accessories) {
            accessory.serverClassId = "";
            EditorUtility.SetDirty(accessory);
        }
        foreach (var accessory in collection.faces) {
            accessory.serverClassId = "";
            EditorUtility.SetDirty(accessory);
        }
        AssetDatabase.SaveAssets();
    }

    public override void OnInspectorGUI() {
        AccessoryComponent myTarget = (AccessoryComponent)target;

        EditorGUILayout.LabelField("Single Character Accessory");
        #if AIRSHIP_INTERNAL
        myTarget.serverClassId = EditorGUILayout.TextField("Class Id", myTarget.serverClassId);
        myTarget.serverClassIdStaging = EditorGUILayout.TextField("Class Id (Staging)", myTarget.serverClassIdStaging);
        #endif

        //Accessory Slot
        myTarget.accessorySlot = (AccessorySlot)EditorGUILayout.EnumPopup("Slot", myTarget.accessorySlot);

        //Visibility Mode
        myTarget.visibilityMode = (AccessoryComponent.VisibilityMode)EditorGUILayout.EnumPopup("Visibility", myTarget.visibilityMode);

        //Skinned To Character
        myTarget.skinnedToCharacter = EditorGUILayout.Toggle("Skinned", myTarget.skinnedToCharacter);

        //Allow mesh combine
        myTarget.canMeshCombine = EditorGUILayout.Toggle("Can Mesh Combine", myTarget.canMeshCombine);

        // Add the Open Editor button:
            EditorGUILayout.Space();
            if (RunCore.IsClone()) {
                GUILayout.Label("Accessory Editor disabled in clone window.");
                return;
            }
            if (GUILayout.Button("Open Editor")) {
                var accessory = targets?.First((obj) => obj is AccessoryComponent) as AccessoryComponent;
                if (accessory != null) {
                    AccessoryEditorWindow.OpenWithAccessory(accessory);
                }
            }

            // Start a foldout
            EditorGUILayout.Space();
            foldout = EditorGUILayout.Foldout(foldout, "Hide Body Parts");

            if (foldout) {
                EditorGUI.indentLevel++;

                // Show bools for all the hide bits
                var accessoryComponent = (AccessoryComponent)target;
                int hideBits = accessoryComponent.bodyMask;

                // Display them based on the sort order in BodyMaskInspectorData
                foreach (var maskData in AccessoryComponent.BodyMaskInspectorDatas) {
                    if (maskData.bodyMask == AccessoryComponent.BodyMask.NONE) {
                        continue;
                    }

                    bool isHidden = (hideBits & (int)maskData.bodyMask) != 0;
                    bool newIsHidden = EditorGUILayout.Toggle(maskData.name, isHidden);
                    if (newIsHidden != isHidden) {
                        if (newIsHidden) {
                            hideBits |= (int)maskData.bodyMask;
                        }
                        else {
                            hideBits &= ~(int)maskData.bodyMask;
                        }
                    }
                }

                if (hideBits != accessoryComponent.bodyMask) {
                    accessoryComponent.bodyMask = hideBits;
                    EditorUtility.SetDirty(accessoryComponent);
                }
                               
                EditorGUI.indentLevel--;
            }

        if(GUI.changed){
            EditorUtility.SetDirty(myTarget);
        }
    }

    [MenuItem("Airship/Avatar/Accessory Editor")]
    public static void OpenAccessoryEditor() {
        AccessoryEditorWindow.OpenOrCreateWindow();
    }
}