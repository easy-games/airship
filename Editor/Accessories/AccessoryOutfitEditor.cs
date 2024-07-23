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
}
