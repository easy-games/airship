using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "VoxelBlockDefinition", menuName = "Airship/VoxelWorld/VoxelBlockDefinition")]
public class VoxelBlockDefinition : ScriptableObject {

    public string blockName = "undefined";
    public string description;

    [System.Serializable]
    public class TextureSet {

        public Material material;
        
        public Texture2D diffuse;
        public Texture2D normal;
        public Texture2D smooth;
        public Texture2D metallic;
        public Texture2D emissive;
    }

    [System.Serializable]
    public class MeshSet {
        public GameObject mesh_LOD0;
        public GameObject mesh_LOD1;
        public GameObject mesh_LOD2;
      
    }

    public VoxelBlocks.ContextStyle contextStyle = VoxelBlocks.ContextStyle.Block;
    public Material meshMaterial; //Used by quarterBlocks, Pipes, StaticMesh and regular blocks

    public TextureSet topTexture = new();
    public TextureSet sideTexture = new();
    public TextureSet bottomTexture = new();
    
    
    public VoxelQuarterBlockMeshDefinition[] quarterBlockMeshes;
       

    //ContextStyle.Prefab
    public GameObject prefab;
    
    //For use with ContextStyle.StaticMesh
    public GameObject staticMeshLOD0;
    public GameObject staticMeshLOD1;
    public GameObject staticMeshLOD2;

    
    //For use with ContextStyle.meshTiles
    public MeshSet meshTile1x1x1;
    public MeshSet meshTile2x2x2;
    public MeshSet meshTile3x3x3;
    public MeshSet meshTile4x4x4;


    ///////////////////////////

    public float metallic = 0;
    public float smoothness = 0;
    public float normalScale = 1;
    public float emissive = 0;
    public float brightness = 1;

    public bool solid = true;  //Blocks all rendering behind it eg: stone.  leafs would be false

    public VoxelBlocks.CollisionType collisionType = VoxelBlocks.CollisionType.Solid;

    public bool randomRotation = false; //Object gets flipped on the x or z axis "randomly" (always the same per coordinate)

    public string minecraftIds = ""; //For automatic conversion from minecraft maps
}

#if UNITY_EDITOR
//Create an editor for it

[CustomEditor(typeof(VoxelBlockDefinition))]
public class VoxelBlockDefinitionEditor : Editor {

    private void ShowDrawerForTextureSetProperty(string labelName, string propertyName, VoxelBlockDefinition block) {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        if (prop != null) {
            EditorGUILayout.LabelField(labelName, EditorStyles.boldLabel);

            object diffuseValue = prop.FindPropertyRelative("diffuse").objectReferenceValue;
            object materialValue = prop.FindPropertyRelative("material").objectReferenceValue;

            if (materialValue == null && diffuseValue == null) {
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("material"));

                EditorGUILayout.PropertyField(prop.FindPropertyRelative("diffuse"), new GUIContent("BaseColor"));
                
                object newValue = prop.FindPropertyRelative("diffuse").objectReferenceValue;
                if (diffuseValue != newValue && newValue != null) {
                    string diffusePath = AssetDatabase.GetAssetPath(prop.FindPropertyRelative("diffuse").objectReferenceValue);

                    //Remove the extension from the path
                    string path = diffusePath.Substring(0, diffusePath.LastIndexOf('.'));

                    //Check to see if path+"_n" exists
                    if (AssetDatabase.LoadAssetAtPath(path + "_n.png", typeof(Texture2D)) != null) {
                        prop.FindPropertyRelative("normal").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_n.png");
                    }
                    //Check to see if the path+"_s" exists
                    if (AssetDatabase.LoadAssetAtPath(path + "_s.png", typeof(Texture2D)) != null) {
                        prop.FindPropertyRelative("smooth").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_s.png");
                    }
                    //Check to see if the path+"_m" exists
                    if (AssetDatabase.LoadAssetAtPath(path + "_m.png", typeof(Texture2D)) != null) {
                        prop.FindPropertyRelative("metallic").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_m.png");
                    }
                    //Check to see if the path+"_e" exists
                    if (AssetDatabase.LoadAssetAtPath(path + "_e.png", typeof(Texture2D)) != null) {
                        prop.FindPropertyRelative("emissive").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(path + "_e.png");
                    }
                }
            }

            //We either show material here, or diffuse texture
            if (materialValue != null) {
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("material"));
            }
            else if (materialValue == null && diffuseValue != null) {
                EditorGUILayout.PropertyField(prop.FindPropertyRelative("diffuse"), new GUIContent("BaseColor"));

                object newValue = prop.FindPropertyRelative("diffuse").objectReferenceValue;
                if (newValue != null) {
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("normal"));

                    Object normalmap = prop.FindPropertyRelative("normal").objectReferenceValue;
                    if (normalmap != null) {
                        //Display a warning if the normal texture isn't a normalmap

                        TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normalmap)) as TextureImporter;
                        if (importer != null && importer.textureType != TextureImporterType.NormalMap) {
                            DisplayNormalMapFixHelp(importer);
                        }
                    }

                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("smooth"));
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("metallic"));
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative("emissive"));
                }
            }
            EditorGUILayout.Space();
        }
    }

    private void DisplayNormalMapFixHelp(TextureImporter importer) {

        //Lay them out horizontally
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.HelpBox("This texture needs to be a Normalmap", MessageType.Warning );
        if (GUILayout.Button("Fix", GUILayout.Height(38))) {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
        
        EditorGUILayout.EndHorizontal();
    }

    private VoxelBlockDefinition.MeshSet ShowMeshEditor(VoxelBlockDefinition.MeshSet meshSet, string label) {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        meshSet.mesh_LOD0 = (GameObject)EditorGUILayout.ObjectField("LOD0", meshSet.mesh_LOD0, typeof(GameObject), false);
        if (meshSet.mesh_LOD0 != null) {
            meshSet.mesh_LOD1 = (GameObject)EditorGUILayout.ObjectField("LOD1", meshSet.mesh_LOD1, typeof(GameObject), false);
        }
        if (meshSet.mesh_LOD1 != null) {
            meshSet.mesh_LOD2 = (GameObject)EditorGUILayout.ObjectField("LOD2", meshSet.mesh_LOD2, typeof(GameObject), false);
        }
        return meshSet;
    }
    
    public override void OnInspectorGUI() {

        serializedObject.Update(); // Sync serialized object with target object
        VoxelBlockDefinition block = (VoxelBlockDefinition)target;
                
        //If the name is "Undefined" look at the foldername of this asset and use that instead
        if (block.blockName == "undefined") {
            try
            {
                string path = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(block));
                //Get the last folder name
                string[] folders = path.Split('\\');
                block.blockName = folders[folders.Length - 1];
            }
            catch {
                block.blockName = "undefined";
            }
        }

        EditorGUILayout.LabelField("Block Name", block.blockName);
        block.blockName = EditorGUILayout.TextField("Block Name", block.blockName);
        block.description = EditorGUILayout.TextField("Description", block.description);
        block.contextStyle = (VoxelBlocks.ContextStyle)EditorGUILayout.EnumPopup("Context Style", block.contextStyle);

        if (block.contextStyle == VoxelBlocks.ContextStyle.Block) {
            ShowDrawerForTextureSetProperty("Top Texture", "topTexture", block);
            ShowDrawerForTextureSetProperty("Side Texture", "sideTexture", block);
            ShowDrawerForTextureSetProperty("Bottom Texture", "bottomTexture", block);


            //helpbox
            bool hasTopFaces = false;
            string topInfo = "";
            bool hasSideFaces = false;
            string sideInfo = "";
            bool hasBottomFaces = false;
            string bottomInfo = "";

            if (block.topTexture.diffuse != null) {
                hasTopFaces = true;
                topInfo = "texture (" + block.topTexture.diffuse.name + ")";
            }

            if (block.topTexture.material != null) {
                hasTopFaces = true;
                topInfo = "material (" + block.topTexture.material.name + ")";
            }

            if (block.sideTexture.diffuse != null) {
                hasSideFaces = true;
                sideInfo = "texture (" + block.sideTexture.diffuse.name + ")";
            }

            if (block.sideTexture.material != null) {
                hasSideFaces = true;
                sideInfo = "material (" + block.sideTexture.material.name + ")";
            }

            if (block.bottomTexture.diffuse != null) {
                hasBottomFaces = true;
                bottomInfo = "texture (" + block.bottomTexture.diffuse.name + ")";
            }

            if (block.bottomTexture.material != null) {
                hasBottomFaces = true;
                bottomInfo = "material (" + block.bottomTexture.material.name + ")";
            }

            if (hasTopFaces == true) {
                if (hasSideFaces == true && hasBottomFaces == true) {
                    EditorGUILayout.HelpBox("The top " + topInfo + " will be used on the top face. The side " + sideInfo + " will be used on the side faces. The bottom " + bottomInfo + " will be used on the bottom face.", MessageType.Info);
                }
                if (hasSideFaces == false && hasBottomFaces == false) {
                    EditorGUILayout.HelpBox("The top " + topInfo + " will be used on all faces.", MessageType.Info);
                }
                if (hasSideFaces == false && hasBottomFaces == true) {
                    EditorGUILayout.HelpBox("The top " + topInfo + " will be used on the top face and side faces. The bottom " + bottomInfo + " will be used on the bottom face.", MessageType.Info);
                }
                if (hasSideFaces == true && hasBottomFaces == false) {
                    EditorGUILayout.HelpBox("The top " + topInfo + " will be used on the top face and bottom face. The side " + sideInfo + " will be used on the side faces.", MessageType.Info);
                }
            }
            else {
                EditorGUILayout.HelpBox("Assign a texture or material for the top face.", MessageType.Info);
            }

            block.metallic = EditorGUILayout.FloatField("Metallic", block.metallic);
            block.smoothness = EditorGUILayout.FloatField("Smoothness", block.smoothness);
            block.normalScale = EditorGUILayout.FloatField("Normal Scale", block.normalScale);
            block.emissive = EditorGUILayout.FloatField("Emissive", block.emissive);
            block.brightness = EditorGUILayout.FloatField("Brightness", block.brightness);

        }
        if (block.contextStyle == VoxelBlocks.ContextStyle.QuarterBlocks) {
            block.meshMaterial = (Material)EditorGUILayout.ObjectField("QuarterBlock Mesh Material", block.meshMaterial, typeof(Material), false);
            
            SerializedProperty quarterBlockMeshesProp = serializedObject.FindProperty("quarterBlockMeshes");
            EditorGUILayout.PropertyField(quarterBlockMeshesProp, new GUIContent("QuarterBlock Meshes"), true);
        }
        if (block.contextStyle == VoxelBlocks.ContextStyle.Prefab) {
            block.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", block.prefab, typeof(GameObject), false);
        }
        if (block.contextStyle == VoxelBlocks.ContextStyle.StaticMesh) {
            block.meshMaterial = (Material)EditorGUILayout.ObjectField("Static Mesh Material", block.meshMaterial, typeof(Material), false);
            block.staticMeshLOD0 = (GameObject)EditorGUILayout.ObjectField("LOD0", block.staticMeshLOD0, typeof(GameObject), false);
            block.staticMeshLOD1 = (GameObject)EditorGUILayout.ObjectField("LOD1", block.staticMeshLOD1, typeof(GameObject), false);
            block.staticMeshLOD2 = (GameObject)EditorGUILayout.ObjectField("LOD2", block.staticMeshLOD2, typeof(GameObject), false);
            EditorGUILayout.HelpBox("LOD1 and LOD2 are optional.", MessageType.Info);
        }

        if (block.contextStyle == VoxelBlocks.ContextStyle.GreedyMeshingTiles) {
            block.meshMaterial = (Material)EditorGUILayout.ObjectField("Greedy Mesh Material", block.meshMaterial, typeof(Material), false);
            block.meshTile1x1x1 = ShowMeshEditor(block.meshTile1x1x1, "1x1x1");
            block.meshTile2x2x2 = ShowMeshEditor(block.meshTile2x2x2, "2x2x2");
            block.meshTile3x3x3 = ShowMeshEditor(block.meshTile3x3x3, "3x3x3");
            block.meshTile4x4x4 = ShowMeshEditor(block.meshTile4x4x4, "4x4x4");

            EditorGUILayout.HelpBox("LOD1 and LOD2 are optional.", MessageType.Info);
        }

        //Small gap
        EditorGUILayout.Space();
        block.solid = EditorGUILayout.Toggle("Solid Visibility", block.solid);
        block.collisionType = (VoxelBlocks.CollisionType)EditorGUILayout.EnumPopup("Collision Type", block.collisionType);

        block.randomRotation = EditorGUILayout.Toggle("Random Rotation", block.randomRotation);

        block.minecraftIds = EditorGUILayout.TextField("Minecraft Ids", block.minecraftIds);
        
        serializedObject.ApplyModifiedProperties();

        if (GUI.changed) {
            EditorUtility.SetDirty(block);
        }
    }
}
#endif