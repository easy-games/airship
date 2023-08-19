using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkinnedMeshBoneSetter))]
public class SkinnedMeshBoneSetterEditor : UnityEditor.Editor {

    public override void OnInspectorGUI() {
        var boneSetter = (SkinnedMeshBoneSetter)target;
        //Refresh serialized properties
        serializedObject.Update();
        
        EditorGUILayout.LabelField("SOURCE");
        var useSource = serializedObject.FindProperty("remappingMode");
        EditorGUILayout.PropertyField(useSource);
        var remappingMode = (SkinnedMeshBoneSetter.RemappingMode)useSource.enumValueIndex;
        if (remappingMode == SkinnedMeshBoneSetter.RemappingMode.EXISTING_SKINNED_MESH) {
            var sourceRen = serializedObject.FindProperty("sourceSkinnedMeshRenderer");
            EditorGUILayout.PropertyField(sourceRen);
        } 
        
        EditorGUILayout.LabelField("TARGET");
        if (remappingMode == SkinnedMeshBoneSetter.RemappingMode.EXISTING_ARMATURE) {
            var targetRootBone = serializedObject.FindProperty("targetRootBone");
            EditorGUILayout.PropertyField(targetRootBone);
        }
        var targetRen = serializedObject.FindProperty("targetSkinnedMeshRenderers");
        EditorGUILayout.PropertyField(targetRen);
        
        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (GUILayout.Button("SET TARGET BONES")) {
            boneSetter.ApplyBones();
        }
        
        //Apply any changes
        serializedObject.ApplyModifiedProperties();
    }
}
