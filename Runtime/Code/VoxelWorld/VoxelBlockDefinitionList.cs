//write a scriptable object that acts as a container and editor for VoxelBlockDefines called VoxelBlockDefinion
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "VoxelBlockDefinionList", menuName = "Airship/VoxelWorld/VoxelBlockDefinionList", order = 2)]
public class VoxelBlockDefinionList : ScriptableObject {

    public string scope = "@Easy/Default";
    public List<VoxelBlockDefinition> blockDefinitions = new List<VoxelBlockDefinition>();

}


#if UNITY_EDITOR
//Create an editor for it

[CustomEditor(typeof(VoxelBlockDefinionList))]
public class VoxelBlockDefinionListEditor : Editor {

 /*   public override void OnInspectorGUI() {
        VoxelBlockDefinionList list = (VoxelBlockDefinionList)target;

        EditorGUILayout.LabelField("Context", list.context);

        if (GUILayout.Button("Add Block")) {
            list.blockDefinitions.Add(new VoxelBlockDefinition());
        }

    }*/
}
        


#endif