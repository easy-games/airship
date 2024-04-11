#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace MTAssets.SkinnedMeshCombiner
{
    /*
     *  This class is responsible for the functioning of the "Skinned Mesh Converter" component, and all its functions.
     */
    /*
     * The Skinned Mesh Combiner was developed by Marcos Tomaz in 2019.
     * Need help? Contact me (mtassets@windsoft.xyz)
     */

    [AddComponentMenu("MT Assets/Skinned Mesh Combiner/Skinned Mesh Converter")] //Add this component in a category of addComponent menu
    public class SkinnedMeshConverter : MonoBehaviour
    {
        //Private constants
        private const int MAX_VERTICES_FOR_16BITS_MESH = 50000; //NOT change this

        //Important private variables from Script (Filled after a conversion been done)
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public GameObject resultOfConversion = null;
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public string resultOfConversionAssetSaved = "";

        //Variables of Script (Conversion)
        [HideInInspector]
        public Transform boneThatWillMove = null;
        [HideInInspector]
        public bool flipNormals = false;
        [HideInInspector]
        public bool compatibilityMode = true;
        [HideInInspector]
        public bool saveDataInAssets = true;

#if UNITY_EDITOR
        //Public variables of Interface
        private bool gizmosOfThisComponentIsDisabled = false;
        private SkinnedMeshRenderer convertedMeshRenderer = null;

        //Variables of interface in Editor
        [HideInInspector]
        [SerializeField]
        private bool alreadyRunnedSetup = false;

        //The UI of this component
        #region INTERFACE_CODE
        [UnityEditor.CustomEditor(typeof(SkinnedMeshConverter))]
        public class CustomInspector : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                //Start the undo event support, draw default inspector and monitor of changes
                SkinnedMeshConverter script = (SkinnedMeshConverter)target;
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(target, "Undo Event");
                script.gizmosOfThisComponentIsDisabled = MTAssetsEditorUi.DisableGizmosInSceneView("SkinnedMeshConverter", script.gizmosOfThisComponentIsDisabled);

                //If have errors, cancel the UI
                string errorsFound = script.ValidateThisComponentAndGetCauseIfIsNotValid(false);
                if (errorsFound != "")
                {
                    EditorGUILayout.HelpBox(errorsFound, MessageType.Error);
                    return;
                }

                //Run the setup, if is not runned
                if (script.alreadyRunnedSetup == false)
                {
                    script.boneThatWillMove = script.gameObject.transform.parent;
                    script.alreadyRunnedSetup = true;
                }

                //Support reminder
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Remember to read the Skinned Mesh Combiner documentation to understand how to use it.\nGet support at: mtassets@windsoft.xyz", MessageType.None);
                GUILayout.Space(10);

                //Show the main resume
                if (script.resultOfConversion == null)
                {
                    GUIStyle titulo = new GUIStyle();
                    titulo.fontSize = 16;
                    titulo.normal.textColor = Color.red;
                    titulo.alignment = TextAnchor.MiddleCenter;
                    EditorGUILayout.LabelField("This Static Mesh Is Not Converted", titulo);
                }
                if (script.resultOfConversion != null)
                {
                    GUIStyle titulo = new GUIStyle();
                    titulo.fontSize = 16;
                    titulo.normal.textColor = new Color(0, 79.0f / 250.0f, 3.0f / 250.0f);
                    titulo.alignment = TextAnchor.MiddleCenter;
                    EditorGUILayout.LabelField("This Static Mesh Is Converted", titulo);
                }

                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Please keep in mind that after the conversion, the original static mesh (Mesh Renderer) will be disabled and only the mesh converted to Skinned Mesh Renderer will be displayed. After the conversion, the only way to move the mesh is to move the bone that was associated with it, before the conversion in \"Bone That Will Move Mesh\". Also note that the purpose of this component is to convert static meshes (such as swords, hats, glasses, etc.) to Skinned Mesh Renderers, so that it is possible to combine them with the other meshes of the character. After the conversion, all the vertices of this mesh will be moved by just one bone (bone in \"Bone That Will Move Mesh\")! You can always check the documentation for more details.", MessageType.Info);
                GUILayout.Space(10);

                //If conversion not made
                if (script.resultOfConversion == null)
                {
                    EditorGUILayout.LabelField("Settings For Conversion", EditorStyles.boldLabel);
                    GUILayout.Space(10);

                    script.boneThatWillMove = (Transform)EditorGUILayout.ObjectField(new GUIContent("Bone That Will Move Mesh",
                                        "Insert here the Transform of the bone that will move the mesh resulting from the conversion."),
                                        script.boneThatWillMove, typeof(Transform), true, GUILayout.Height(16));

                    script.flipNormals = (bool)EditorGUILayout.Toggle(new GUIContent("Flip Normals And Triangles",
                        "Some meshes, after conversion, may show inverted normals, where only the inside faces are rendered. If this happens, enable this option."),
                        script.flipNormals);

                    script.compatibilityMode = (bool)EditorGUILayout.Toggle(new GUIContent("Compatibility Mode",
                        "If the compatibility mode is enabled, the Skinned Mesh Converter will calculate the positions (of the array) in a way that it will reach more types of models (such as .blend, .fbx and others). Enabling compatibility mode will ensure that your animations are not deformed on most types of models.\n\nIf any model is displaying deformed animations, after merge, try disabling this option.\n\n**Disabling this option may cause the distortion of your model's animation. Only disable this option if you believe your model will benefit from this."),
                        script.compatibilityMode);

                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Conversion In Editor", EditorStyles.boldLabel);
                    GUILayout.Space(10);

                    script.saveDataInAssets = (bool)EditorGUILayout.Toggle(new GUIContent("Save Data In Assets",
                                        "Activate this option, so that the Skinned Mesh Converter saves the resulting mesh in your project files."),
                                        script.saveDataInAssets);

                    GUILayout.Space(10);
                    //----- Run simple verifications to guarantee the quality of conversion -----//
                    //Try to found meshes without read/write enabled
                    script.RunReadWriteCheckerInThisModel();
                    //------- -------- -------//
                    GUILayout.Space(10);

                    //Button to convert
                    if (GUILayout.Button("Convert Mesh To Skinned!", GUILayout.Height(40)))
                        script.DoConvertMesh();
                }
                //If conversion have made
                if (script.resultOfConversion != null)
                {
                    //Material and submeshes count
                    if (script.convertedMeshRenderer == null)
                        script.convertedMeshRenderer = script.resultOfConversion.GetComponent<SkinnedMeshRenderer>();
                    if (script.convertedMeshRenderer.sharedMesh == null)
                        EditorGUILayout.HelpBox("It appears that the mesh resulting from the conversion no longer exists. Please try to redo the conversion to correct this problem.", MessageType.Error);
                    if (script.convertedMeshRenderer.sharedMesh != null && script.convertedMeshRenderer.sharedMesh.subMeshCount != script.convertedMeshRenderer.sharedMaterials.Length)
                        EditorGUILayout.HelpBox("It appears that the converted mesh has a material count, different from the sub-mesh count. This can cause the converted mesh to be ignored during the merging process in the Skinned Mesh Combiner. Check if the converted mesh really needs these materials.", MessageType.Warning);

                    //Button to go to conversion mesh
                    if (GUILayout.Button("Go To Mesh Result Of Conversion", GUILayout.Height(40)))
                        Selection.objects = new UnityEngine.Object[] { script.resultOfConversion };

                    //Button to undo convert
                    if (GUILayout.Button("Undo Conversion Of Skinned", GUILayout.Height(40)))
                        script.DoUndoConvertMesh(true, true);
                }

                GUILayout.Space(10);

                //Apply changes on script, case is not playing in editor
                if (GUI.changed == true && Application.isPlaying == false)
                {
                    EditorUtility.SetDirty(script);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(script.gameObject.scene);
                }
                if (EditorGUI.EndChangeCheck() == true)
                {

                }
            }
        }
        #endregion

        #region INTERFACE_CODE_TOOLS_METHODS
        //Tools methods ONLY for editor interface

        private void RunReadWriteCheckerInThisModel()
        {
            //Check if read/write of this model is enabled
            bool isReadable = true;
            Mesh mesh = this.gameObject.GetComponent<MeshFilter>().sharedMesh;
            try
            {
                if (mesh.isReadable == false)
                    isReadable = false;
                Vector3 vertices = mesh.vertices[0];
                int triangles = mesh.triangles[0];
                Vector3 normals = mesh.normals[0];
                Vector4 tangents = mesh.tangents[0];
            }
            catch (UnityException e)
            {
                if (e.Message.StartsWith("Not allowed to access vertices on mesh '" + mesh.name + "'"))
                    isReadable = false;
            }

            //If model is not readable
            if (isReadable == false)
            {
                EditorGUILayout.HelpBox("It appears that the Model \"" + mesh.name + "\" does not have the \"R/W Enabled\" option enabled in its import settings. This makes it impossible for this component to read its vertices data and so on. You can fix this by activating the \"R/W Enabled\" option in the model's import settings, or by clicking the button below.", MessageType.Warning);
                if (GUILayout.Button("Fix Model And Enable R/W In Import Settings"))
                {
                    EditorUtility.DisplayProgressBar("Processing", "Enabling Read/Write...", 1);
                    ModelImporter modelImporter = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(mesh));
                    if (modelImporter.isReadable == false)
                    {
                        modelImporter.isReadable = true;
                    }
                    AssetDatabase.ImportAsset(modelImporter.assetPath);
                    AssetDatabase.Refresh();
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Done", "The model had R/W enabled in its import settings!", "Ok");
                }
            }
        }
        #endregion
#endif

        //Core methods

        private string ValidateThisComponentAndGetCauseIfIsNotValid(bool sendLogsOnConsole)
        {
            //Store the response
            string response = "";

            //If is not in a Mesh Renderer + Mesh Filter GameObject
            MeshRenderer meshRenderer = this.GetComponent<MeshRenderer>();
            MeshFilter meshFilter = this.GetComponent<MeshFilter>();
            if (meshFilter == null || meshRenderer == null)
                response = "Apparently the GameObject the Skinned Mesh Converter is in does not contain a valid static mesh. Please make sure that the component is in a GameObject that contains a Mesh Renderer and Mesh Filter with mesh.";
            if (meshFilter != null && meshFilter.sharedMesh == null)
                response = "Apparently the GameObject the Skinned Mesh Converter is in does not contain a valid static mesh. Please make sure that the component is in a GameObject that contains a Mesh Renderer and Mesh Filter with mesh.";

            //If is not in a Character
            Animator parentAnimator = this.GetComponentInParent<Animator>();
            Animation parentAnimation = this.GetComponentInParent<Animation>();
            if (parentAnimation == null && parentAnimator == null)
                response = "Apparently the Skinned Mesh Converter is not in a character with an Animator or Animation. The Skinned Mesh Converter must be inside a character, in the GameObject of a static mesh (sword, hat, glasses, etc.) derived from a character's bone.";

            //If is not in a Character with skinned mesh combiner
            SkinnedMeshCombiner meshCombiner = this.GetComponentInParent<SkinnedMeshCombiner>();
            if (meshCombiner == null)
                response = "The Skinned Mesh Converter must be inside a GameObject that contains a static mesh (sword, hat, glasses, etc.) that is the child of a character's bone. This character must contain an Animator or Animation and must also contain the Skinned Mesh Combiner component at its root GameObject.";

            //Return the response
            if (sendLogsOnConsole == true && response != "")
                Debug.LogError(response);
            return response;
        }

        private void DoConvertMesh()
        {
            //Validate this component position
            if (ValidateThisComponentAndGetCauseIfIsNotValid(true) != "")
                return;

            //If already converted
            if (isMeshConverted() == true)
            {
                Debug.LogError("The mesh could not be converted to Skinned, the mesh is already converted.");
                return;
            }

            //Start the conversion
#if UNITY_EDITOR
            if (Application.isPlaying == false)
                EditorUtility.DisplayProgressBar("Converting", "Converting to Skinned...", 1.0f);
#endif

            //Create the holder GameObject
            resultOfConversion = new GameObject(this.gameObject.name + " (Converted To Skinned)");
            resultOfConversion.transform.parent = this.gameObject.transform;
            SkinnedMeshRenderer meshRenderer = resultOfConversion.AddComponent<SkinnedMeshRenderer>();

            //Get the original mesh renderer, mesh filter and mesh
            MeshRenderer originalMeshRenderer = this.GetComponent<MeshRenderer>();
            MeshFilter originalMeshFilter = this.GetComponent<MeshFilter>();
            Mesh originalMesh = null;
            if (originalMeshFilter != null)
                originalMesh = originalMeshFilter.sharedMesh;

            //Prepare the storage of combine instances for each submesh of original mesh
            List<CombineInstance> submeshesOfOriginalMesh = new List<CombineInstance>();
            //Fill the list of submeshes converted
            for (int i = 0; i < originalMesh.subMeshCount; i++)
            {
                //Create a new submesh result of conversion
                Mesh originalSubmesh = originalMesh.SMCGetSubmesh(i);
                Mesh convertedSubmesh = new Mesh();

                //Prepare to copy the data from original submesh
                Vector3[] copyVertices = new Vector3[originalSubmesh.vertices.Length];
                int[] copyTriangles = new int[originalSubmesh.triangles.Length];
                Vector3[] copyNormals = new Vector3[originalSubmesh.normals.Length];
                Vector4[] copyTangents = new Vector4[originalSubmesh.tangents.Length];
                Vector2[] copyUv1 = new Vector2[originalSubmesh.uv.Length];
                Vector2[] copyUv2 = new Vector2[originalSubmesh.uv2.Length];
                Vector2[] copyUv3 = new Vector2[originalSubmesh.uv3.Length];
                Vector2[] copyUv4 = new Vector2[originalSubmesh.uv4.Length];
                Vector2[] copyUv5 = new Vector2[originalSubmesh.uv5.Length];
                Vector2[] copyUv6 = new Vector2[originalSubmesh.uv6.Length];
                Vector2[] copyUv7 = new Vector2[originalSubmesh.uv7.Length];
                Vector2[] copyUv8 = new Vector2[originalSubmesh.uv8.Length];

                //Copy the data from original submesh
                for (int j = 0; j < copyVertices.Length; j++)
                    copyVertices[j] = originalSubmesh.vertices[j];
                if (flipNormals == false)
                {
                    for (int j = 0; j < copyTriangles.Length; j++)
                        copyTriangles[j] = originalSubmesh.triangles[j];
                    for (int j = 0; j < copyNormals.Length; j++)
                        copyNormals[j] = originalSubmesh.normals[j];
                }
                if (flipNormals == true)
                {
                    for (int j = 0; j < copyTriangles.Length; j++)
                        copyTriangles[j] = originalSubmesh.triangles[(copyTriangles.Length - 1) - j];
                    for (int j = 0; j < copyNormals.Length; j++)
                        copyNormals[j] = originalSubmesh.normals[j] * -1.0f;
                }
                for (int j = 0; j < copyTangents.Length; j++)
                    copyTangents[j] = originalSubmesh.tangents[j];
                for (int j = 0; j < copyUv1.Length; j++)
                    copyUv1[j] = originalSubmesh.uv[j];
                for (int j = 0; j < copyUv2.Length; j++)
                    copyUv2[j] = originalSubmesh.uv2[j];
                for (int j = 0; j < copyUv3.Length; j++)
                    copyUv3[j] = originalSubmesh.uv3[j];
                for (int j = 0; j < copyUv4.Length; j++)
                    copyUv4[j] = originalSubmesh.uv4[j];
                for (int j = 0; j < copyUv5.Length; j++)
                    copyUv5[j] = originalSubmesh.uv5[j];
                for (int j = 0; j < copyUv6.Length; j++)
                    copyUv6[j] = originalSubmesh.uv6[j];
                for (int j = 0; j < copyUv7.Length; j++)
                    copyUv7[j] = originalSubmesh.uv7[j];
                for (int j = 0; j < copyUv8.Length; j++)
                    copyUv8[j] = originalSubmesh.uv8[j];

                //Fill the new submesh created, with the data
                convertedSubmesh.vertices = copyVertices;
                convertedSubmesh.uv = copyUv1;
                convertedSubmesh.uv2 = copyUv2;
                convertedSubmesh.uv3 = copyUv3;
                convertedSubmesh.uv4 = copyUv4;
                convertedSubmesh.uv5 = copyUv5;
                convertedSubmesh.uv6 = copyUv6;
                convertedSubmesh.uv7 = copyUv7;
                convertedSubmesh.uv8 = copyUv8;
                convertedSubmesh.triangles = copyTriangles;
                convertedSubmesh.normals = copyNormals;
                convertedSubmesh.tangents = copyTangents;

                //Add this new converted submesh to list of converted submeshes
                CombineInstance combineInstance = new CombineInstance();
                combineInstance.mesh = convertedSubmesh;
                combineInstance.subMeshIndex = 0;
                combineInstance.transform = originalMeshRenderer.transform.localToWorldMatrix;
                submeshesOfOriginalMesh.Add(combineInstance);
            }

            //Create the new final mesh
            Mesh convertedMesh = new Mesh();
            if (originalMesh.vertexCount <= MAX_VERTICES_FOR_16BITS_MESH)
                convertedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            if (originalMesh.vertexCount > MAX_VERTICES_FOR_16BITS_MESH)
                convertedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            convertedMesh.name = originalMesh.name + " (Converted To Skinned)";
            convertedMesh.CombineMeshes(submeshesOfOriginalMesh.ToArray(), false, true);

            //Do recalculations where is desired
            convertedMesh.RecalculateBounds();

            //Create a boneweight for each vertice
            BoneWeight[] boneWeights = new BoneWeight[convertedMesh.vertices.Length];
            for (int i = 0; i < boneWeights.Length; i++)
            {
                boneWeights[i].boneIndex0 = 0;
                boneWeights[i].weight0 = 1.0f;
            }

            //Create the reference to bone transform
            Transform[] bonesTransforms = new Transform[1];
            bonesTransforms[0] = boneThatWillMove;

            //Create the bindpose for mesh
            Matrix4x4[] bindPoses = new Matrix4x4[1];
            if (compatibilityMode == false)
                bindPoses[0] = bonesTransforms[0].worldToLocalMatrix * this.transform.localToWorldMatrix;
            if (compatibilityMode == true)
                bindPoses[0] = bonesTransforms[0].worldToLocalMatrix;

            //Link the bindposes and boneweights to converted mesh
            convertedMesh.bindposes = bindPoses;
            convertedMesh.boneWeights = boneWeights;

            //Fill the new skinned mesh renderer with the data
            meshRenderer.sharedMesh = convertedMesh;
            meshRenderer.bones = bonesTransforms;
            meshRenderer.sharedMaterials = originalMeshRenderer.sharedMaterials;
            meshRenderer.rootBone = boneThatWillMove;

            //If is desired, save the data
#if UNITY_EDITOR
            if (Application.isPlaying == false && saveDataInAssets == true)
            {
                //Create the directory in project
                if (!AssetDatabase.IsValidFolder("Assets/MT Assets"))
                    AssetDatabase.CreateFolder("Assets", "MT Assets");
                if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                    AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
                if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                    AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
                if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData/Meshes"))
                    AssetDatabase.CreateFolder("Assets/MT Assets/_AssetsData", "Meshes");
                if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData/Meshes/ConvertedToSkinned"))
                    AssetDatabase.CreateFolder("Assets/MT Assets/_AssetsData/Meshes", "ConvertedToSkinned");

                //Get current date
                DateTime dateNow = DateTime.Now;
                string dateNowStr = dateNow.Year.ToString() + dateNow.Month.ToString() + dateNow.Day.ToString() + dateNow.Hour.ToString() + dateNow.Minute.ToString() + dateNow.Second.ToString() + dateNow.Millisecond.ToString();

                //Save the asset
                string fileDirectory = "Assets/MT Assets/_AssetsData/Meshes/ConvertedToSkinned/" + originalMesh.name + " (" + dateNowStr + ").asset";
                AssetDatabase.CreateAsset(meshRenderer.sharedMesh, fileDirectory);
                resultOfConversionAssetSaved = fileDirectory;

                //Save all data and reload
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
#endif

            //Add the Converted Meshes Manager to converted mesh result
            ConvertedMeshesManager convertedMeshesManager = resultOfConversion.AddComponent<ConvertedMeshesManager>();
            convertedMeshesManager.converterOriginalGameObject = this.gameObject;

            //Disable the original mesh renderer
            originalMeshRenderer.enabled = false;

            //Clear progress bar
#if UNITY_EDITOR
            if (Application.isPlaying == false)
                EditorUtility.ClearProgressBar();
#endif

            //Show alert
            if (Application.isPlaying == false)
                Debug.Log("The mesh was successfully converted to a Skinned mesh.");
        }

        private void DoUndoConvertMesh(bool runUnityGc, bool runMonoIl2CppGc)
        {
            //Get the parent Skinned Mesh Combiner
            SkinnedMeshCombiner[] meshCombiners = this.GetComponentsInParent<SkinnedMeshCombiner>();
            if (meshCombiners.Length == 0)
            {
                Debug.LogError("It was not possible to undo the mesh conversion back to static as it was not possible to find a Skinned Mesh Combiner component linked to this hierarchy of bones or character.");
                return;
            }
            //If the mesh is in a skinned mesh combiner that is merged
            bool haveMeshesMerged = false;
            foreach (SkinnedMeshCombiner meshCombiner in meshCombiners)
                if (meshCombiner.isMeshesCombined() == true)
                    haveMeshesMerged = true;
            if (haveMeshesMerged == true)
            {
                Debug.LogError("It was not possible to undo the mesh conversion back to static. Before undoing the conversion, you need to undo any merge that are currently working on this character's Skinned Mesh Combiner component.");
                return;
            }
            //If already undo conversion
            if (isMeshConverted() == false)
            {
                Debug.LogError("It was not possible to undo the conversion of the mesh to static. The mesh has not yet been converted.");
                return;
            }

            //Start the undo

            //Enable the original mesh renderer
            MeshRenderer meshRenderer = this.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.enabled = true;

            //Delete the gameobject result from conversion
#if UNITY_EDITOR
            DestroyImmediate(resultOfConversion);
#endif
#if !UNITY_EDITOR
            Destroy(resultOfConversion);
#endif

#if UNITY_EDITOR
            //Delete the asset saved
            if (AssetDatabase.LoadAssetAtPath(resultOfConversionAssetSaved, typeof(object)) != null)
                AssetDatabase.DeleteAsset(resultOfConversionAssetSaved);
#endif

            //Run the GC if is activated
            if (runMonoIl2CppGc == true)
                System.GC.Collect();
            if (runUnityGc == true)
                Resources.UnloadUnusedAssets();

            //Show alert
            if (Application.isPlaying == false)
                Debug.Log("The conversion of the mesh, to Skinned, was undone.");
        }

        //Public methods

        public bool isMeshConverted()
        {
            //Return true if result of conversion is different from null
            if (resultOfConversion != null)
                return true;
            return false;
        }

        public void ConvertMesh()
        {
            //Start the conversion
            DoConvertMesh();
        }

        public void UndoMeshConversion(bool runUnityGc, bool runMonoIl2CppGc)
        {
            //Undo the conversion and run GC if is desired
            DoUndoConvertMesh(runUnityGc, runMonoIl2CppGc);
        }

        public SkinnedMeshRenderer GetMeshConvertedToSkinned()
        {
            //If the mesh is not converted, cancel
            if (isMeshConverted() == false)
                ConvertMesh();

            //Return all renderer of converted mesh
            return resultOfConversion.GetComponent<SkinnedMeshRenderer>();
        }
    }
}