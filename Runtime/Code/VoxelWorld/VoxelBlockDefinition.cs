using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "VoxelBlockDefinition", menuName = "Airship/VoxelBlockDefinition")]
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

    public VoxelBlocks.ContextStyle contextStyle = VoxelBlocks.ContextStyle.Block;
 

    public TextureSet topTexture = new();
    public TextureSet sideTexture = new();
    public TextureSet bottomTexture = new();

    public Material meshMaterial;
    public VoxelQuarterBlockMeshDefinition quarterBlockMesh;
    public string meshPathLod;

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
        }
        else {
            block.meshMaterial = (Material)EditorGUILayout.ObjectField("QuarterBlock Mesh Material", block.meshMaterial, typeof(Material), false);
            block.quarterBlockMesh = (VoxelQuarterBlockMeshDefinition)EditorGUILayout.ObjectField("QuarterBlock Mesh", block.quarterBlockMesh, typeof(VoxelQuarterBlockMeshDefinition), false);

            block.meshPathLod = EditorGUILayout.TextField("Mesh Path LOD", block.meshPathLod);
        }
 
        block.metallic = EditorGUILayout.FloatField("Metallic", block.metallic);
        block.smoothness = EditorGUILayout.FloatField("Smoothness", block.smoothness);
        block.normalScale = EditorGUILayout.FloatField("Normal Scale", block.normalScale);
        block.emissive = EditorGUILayout.FloatField("Emissive", block.emissive);
        block.brightness = EditorGUILayout.FloatField("Brightness", block.brightness);

        block.solid = EditorGUILayout.Toggle("Solid", block.solid);
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