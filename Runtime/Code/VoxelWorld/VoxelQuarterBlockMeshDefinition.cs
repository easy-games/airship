using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "VoxelQuarterBlockMeshDefinition", menuName = "Airship/VoxelWorld/QuarterBlock/VoxelQuarterBlockMeshDefinition")]
public class VoxelQuarterBlockMeshDefinition : ScriptableObject {
    public GameObject UA;
    public GameObject UB;
    public GameObject UC;
    public GameObject UD;
    public GameObject UE;
    public GameObject UF;
    public GameObject UG;
    public GameObject UH;
    public GameObject UI;
    public GameObject UJ;
    public GameObject UK;
    public GameObject UL;
    public GameObject UM;
    public GameObject UN;
    
    public GameObject DA;
    public GameObject DB;
    public GameObject DC;
    public GameObject DD;
    public GameObject DE;
    public GameObject DF;
    public GameObject DG;
    public GameObject DH;
    public GameObject DI;
    public GameObject DJ;
    public GameObject DK;
    public GameObject DL;
    public GameObject DM;
    public GameObject DN;

    [HideInInspector]
    public float probablity = 1;

    //Accessor to get them by enum
    public GameObject GetQuarterBlockMesh(string blockName) {
        //use GetField
        return (GameObject)this.GetType().GetField(blockName).GetValue(this);
    }
       
}


#if UNITY_EDITOR
[CustomEditor(typeof(VoxelQuarterBlockMeshDefinition))]
public class VoxelQuarterBlockMeshDefinitionEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();

        //Draw a slider for probability
        VoxelQuarterBlockMeshDefinition myScript = (VoxelQuarterBlockMeshDefinition)target;

        myScript.probablity = EditorGUILayout.Slider("Probability %", myScript.probablity * 100.0f, 0, 100) / 100.0f;
                
        
        if (GUILayout.Button("Load")) { 
            //Get the path of this asset
            string path = AssetDatabase.GetAssetPath(myScript);
            //Get the path of the folder containing this asset
            string folderPath = path.Substring(0, path.LastIndexOf("/"));
            //Get all the assets in the folder
            string[] assets = AssetDatabase.FindAssets("", new string[] { folderPath });
            //Iterate over all the assets matching the names to our slots
            foreach (string asset in assets) {
                string assetPath = AssetDatabase.GUIDToAssetPath(asset);
                
                //FIx this line
                foreach(string name in VoxelBlocks.QuarterBlockNames) {
                    if (assetPath.Contains(name)) {
                        GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        myScript.GetType().GetField(name).SetValue(myScript, obj);
                    }
                }
            }

            EditorUtility.SetDirty(myScript);
            AssetDatabase.SaveAssets();
        }
    }

}
#endif