//write a scriptable object that acts as a container and editor for VoxelBlockDefines called VoxelBlockDefinition
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "VoxelBlockDefinitionList", menuName = "Airship/VoxelWorld/VoxelBlockDefinitionList", order = 2)]
public class VoxelBlockDefinitionList : ScriptableObject {

    public string scope = "@Easy/Default";
    [FormerlySerializedAs("blockDefinions")]
    public List<VoxelBlockDefinition> blockDefinitions = new List<VoxelBlockDefinition>();

}


#if UNITY_EDITOR
//Create an editor for it

[CustomEditor(typeof(VoxelBlockDefinitionList))]
public class VoxelBlockDefinitionListEditor : Editor {
    // public override void OnInspectorGUI() {
    //     base.OnInspectorGUI();
    //
    //     VoxelBlockDefinitionList list = (VoxelBlockDefinitionList)target;
    //
    //     EditorGUILayout.Space();
    //
    //     if (GUILayout.Button("Gather in folder")) {
    //         var parentPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(list));
    //         Debug.Log("Parent path: " + parentPath);
    //         var blockPaths = AssetDatabase.FindAssets("t:VoxelBlockDefinition", new[] { parentPath }).Select((guid) => AssetDatabase.GUIDToAssetPath(guid)).ToArray();
    //         Debug.Log($"Found {blockPaths.Length} block definitions.");
    //         int counter = 0;
    //         foreach (var path in blockPaths) {
    //             var blockDefinition = AssetDatabase.LoadAssetAtPath<VoxelBlockDefinition>(path);
    //             Debug.Log($"Loaded {blockDefinition.name} at path: {path}");
    //             if (!list.blockDefinitions.Contains(blockDefinition)) {
    //                 list.blockDefinitions.Add(blockDefinition);
    //                 counter++;
    //             }
    //         }
    //
    //         Debug.Log($"Added {counter} missing block definitions.");
    //         if (counter > 0) {
    //             EditorUtility.SetDirty(list);
    //             AssetDatabase.SaveAssets();
    //         }
    //     }
    //
    //     // EditorGUILayout.LabelField("Context", list.context);
    //     // if (GUILayout.Button("Add Block")) {
    //     //     list.blockDefinitions.Add(new VoxelBlockDefinition());
    //     // }
    //
    // }
 
}
        


#endif