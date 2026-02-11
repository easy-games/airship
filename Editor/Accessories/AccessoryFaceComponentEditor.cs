using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AccessoryFaceComponent))]
public class AccessoryFaceComponentEditor : UnityEditor.Editor {

    private AccessoryFaceComponent.AirshipFaceDecal tempFace = AccessoryFaceComponent.AirshipFaceDecal.None;
    private bool failed = false;
        
    public override void OnInspectorGUI() {
        base.OnInspectorGUI();
        
        var face = (AccessoryFaceComponent)target;
        // Don't do extra GUI if we don't have the renderer
        if (face.faceRenderer == null) {
            return;
        }

        if (face.faceMat == null) {
            face.faceMat = face.faceRenderer.sharedMaterial;
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Editor Tooling");
        // Let users see different faces on the character in editor
        var faceIndex = (AccessoryFaceComponent.AirshipFaceDecal) EditorGUILayout.EnumPopup("Face Type", tempFace);
        if (faceIndex != tempFace) {
            tempFace = faceIndex;
            face.SetFace(tempFace, AccessoryFaceComponent.FaceRenderMode.Temporary);
        }
    }
}
