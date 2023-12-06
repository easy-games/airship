#if UNITY_EDITOR
using UnityEditor;
#endif
#if MTAssets_Anima2D_Available
using Anima2D;
#endif
using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System.IO;
using System.Text;
using System.Threading;
using System.Linq;
using System.Globalization;

namespace MTAssets.SkinnedMeshCombiner
{
    /*
     *  This class is responsible for the functioning of the "Skinned Mesh Combiner" component, and all its functions.
     */
    /*
     * The Skinned Mesh Combiner was developed by Marcos Tomaz in 2019.
     * Need help? Contact me (mtassets@windsoft.xyz)
     */

    [AddComponentMenu("MT Assets/Skinned Mesh Combiner/Skinned Mesh Combiner")] //Add this component in a category of addComponent menu
    public class SkinnedMeshCombiner : MonoBehaviour
    {
        //Private constants
        private const int MAX_VERTICES_FOR_16BITS_MESH = 50000; //NOT change this

        //Private variables
        private Vector3 thisOriginalPosition = Vector3.zero;
        private Vector3 thisOriginalRotation = Vector3.zero;
        private Vector3 thisOriginalScale = Vector3.one;

        //Enums of script
        public enum LogTypeOf
        {
            Assert,
            Error,
            Exception,
            Log,
            Warning
        }
        public enum MergeMethod
        {
            OneMeshPerMaterial,
            AllInOne,
            JustMaterialColors,
            OnlyAnima2dMeshes
        }
        public enum AtlasSize
        {
            Pixels32x32,
            Pixels64x64,
            Pixels128x128,
            Pixels256x256,
            Pixels512x512,
            Pixels1024x1024,
            Pixels2048x2048,
            Pixels4096x4096,
            Pixels8192x8192
        }
        public enum AnimQuality
        {
            UseQualitySettings,
            Bad,
            Good,
            VeryGood
        }
        public enum MipMapEdgesSize
        {
            Pixels0x0,
            Pixels16x16,
            Pixels32x32,
            Pixels64x64,
            Pixels128x128,
            Pixels256x256,
            Pixels512x512,
            Pixels1024x1024,
        }
        public enum AtlasPadding
        {
            Pixels0x0,
            Pixels2x2,
            Pixels4x4,
            Pixels8x8,
            Pixels16x16,
        }
        public enum MergeTiledTextures
        {
            SkipAll,
            ImprovedMode,
            LegacyMode
        }
        public enum BlendShapesSupport
        {
            Disabled,
            Enabled,
            FullSupport
        }
        public enum CombineOnStart
        {
            Disabled,
            OnStart,
            OnAwake
        }
        public enum RootBoneToUse
        {
            Automatic,
            Manual
        }

        //Classes of script
        [Serializable]
        public class LogOfMerge
        {
            public string content;
            public LogTypeOf logType;

            public LogOfMerge(string content, LogTypeOf logType)
            {
                this.content = content;
                this.logType = logType;
            }
        }
        [Serializable]
        public class OneMeshPerMaterialParams
        {
            public bool mergeOnlyEqualRootBones = false;
        }
        [Serializable]
        public class JustMaterialColorsParams
        {
            public Material materialToUse;
            public bool mergeOnlyEqualsRootBones = false;
            public bool useDefaultColorProperty = true;
            public string colorPropertyToFind = "_Color";
            public string mainTexturePropertyToInsert = "_MainTex";
        }
        [Serializable]
        public class AllInOneParams
        {
            public Material materialToUse;
            public AtlasSize atlasResolution = AtlasSize.Pixels512x512;
            public MipMapEdgesSize mipMapEdgesSize = MipMapEdgesSize.Pixels64x64;
            public AtlasPadding atlasPadding = AtlasPadding.Pixels0x0;
            public MergeTiledTextures mergeTiledTextures = MergeTiledTextures.LegacyMode;
            public bool mergeOnlyEqualsRootBones = false;
            public bool useDefaultMainTextureProperty = true;
            public string mainTexturePropertyToFind = "_MainTex";
            public string mainTexturePropertyToInsert = "_MainTex";
            public bool metallicMapSupport = false;
            public string metallicMapPropertyToFind = "_MetallicGlossMap";
            public string metallicMapPropertyToInsert = "_MetallicGlossMap";
            public bool specularMapSupport = false;
            public string specularMapPropertyToFind = "_SpecGlossMap";
            public string specularMapPropertyToInsert = "_SpecGlossMap";
            public bool normalMapSupport = false;
            public string normalMapPropertyToFind = "_BumpMap";
            public string normalMapPropertyToInsert = "_BumpMap";
            public bool normalMap2Support = false;
            public string normalMap2PropertyFind = "_DetailNormalMap";
            public string normalMap2PropertyToInsert = "_DetailNormalMap";
            public bool heightMapSupport = false;
            public string heightMapPropertyToFind = "_ParallaxMap";
            public string heightMapPropertyToInsert = "_ParallaxMap";
            public bool occlusionMapSupport = false;
            public string occlusionMapPropertyToFind = "_OcclusionMap";
            public string occlusionMapPropertyToInsert = "_OcclusionMap";
            public bool detailAlbedoMapSupport = false;
            public string detailMapPropertyToFind = "_DetailAlbedoMap";
            public string detailMapPropertyToInsert = "_DetailAlbedoMap";
            public bool detailMaskSupport = false;
            public string detailMaskPropertyToFind = "_DetailMask";
            public string detailMaskPropertyToInsert = "_DetailMask";
            public bool pinkNormalMapsFix = true;
        }
        [Serializable]
        public class OnlyAnima2dMeshes
        {
            public AtlasSize atlasResolution = AtlasSize.Pixels512x512;
        }

        //Important private variables from Script (Filled after a merge been done)
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public GameObject[] resultMergeOriginalGameObjects = null;
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public GameObject resultMergeGameObject = null;
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public string resultMergeTextStats = "";
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public List<LogOfMerge> resultMergeLogs = new List<LogOfMerge>();
        ///<summary>[WARNING] Do not change the value of this variable. This is a variable used for internal tool operations.</summary> 
        [HideInInspector]
        public List<string> resultMergeAssetsSaved = new List<string>();

        //Variables of Script (Merge)
        [HideInInspector]
        public MergeMethod mergeMethod;
        [HideInInspector]
        public OneMeshPerMaterialParams oneMeshPerMaterialParams = new OneMeshPerMaterialParams();
        [HideInInspector]
        public JustMaterialColorsParams justMaterialColorsParams = new JustMaterialColorsParams();
        [HideInInspector]
        public AllInOneParams allInOneParams = new AllInOneParams();
        [HideInInspector]
        public OnlyAnima2dMeshes onlyAnima2dMeshes = new OnlyAnima2dMeshes();
        [HideInInspector]
        public BlendShapesSupport blendShapesSupport = BlendShapesSupport.Disabled;
        [HideInInspector]
        public float blendShapesMultiplier = 1.0f;
        [HideInInspector]
        public RootBoneToUse rootBoneToUse = RootBoneToUse.Automatic;
        [HideInInspector]
        public Transform manualRootBoneToUse = null;
        [HideInInspector]
        public bool autoManagePosition = true;
        [HideInInspector]
        public bool compatibilityMode = true;
        [HideInInspector]
        public bool combineInactives = false;
        [HideInInspector]
        public CombineOnStart combineOnStart = CombineOnStart.Disabled;
        [HideInInspector]
        public bool convertCombinedMeshToStaticOnStart = false;
        [HideInInspector]
        public bool legacyAnimationSupport = false;
        [HideInInspector]
        public string nameOfThisMerge = "Combined Meshes";

        //Variables of Script (GameObject to ignore)
        [HideInInspector]
        public List<GameObject> gameObjectsToIgnore = new List<GameObject>();

        //Variables of script (Combine in editor)
        [HideInInspector]
        public bool saveDataInAssets = true;
        [HideInInspector]
        public bool savePrefabOfMerge = false;
        [HideInInspector]
        public string nameOfPrefabOfMerge = "";

        //Variables of script (Debugging)
        [HideInInspector]
        public bool launchConsoleLogs = true;
        [HideInInspector]
        public bool highlightUvVertices = false;

        //Variables of script (Merge Events)
        public UnityEvent onCombineMeshs;
        public UnityEvent onUndoCombineMeshs;

#if UNITY_EDITOR
        //Public variables of Interface
        private bool gizmosOfThisComponentIsDisabled = false;
        [HideInInspector]
        [SerializeField]
        private bool clearUndoHistory = false;
        [HideInInspector]
        [SerializeField]
        private bool showMergeEventsOptions = false;

        //Variables of interface in Editor
        [HideInInspector]
        [SerializeField]
        private int currentTab = 0;
        [HideInInspector]
        [SerializeField]
        private int currentDebuggingTab = 0;
        [HideInInspector]
        [SerializeField]
        private Vector2 logsOfMergeScrollpos = Vector2.zero;
        [HideInInspector]
        [SerializeField]
        private int lastQuantityOfLogs = 0;
        [HideInInspector]
        [SerializeField]
        private bool dontShowWarnsAboutTexturesResolution = false;

        //The UI of this component
        #region INTERFACE_CODE
        [UnityEditor.CustomEditor(typeof(SkinnedMeshCombiner))]
        public class CustomInspector : UnityEditor.Editor
        {
            //Private variables of Editor Only
            private float timeToDisableClearUndoHistory = 0;
            private Vector2 gameObjectsToIgnoreScrollpos = Vector2.zero;
            private Vector2 debuggingScrollpos = Vector2.zero;
            private List<GameObject> debuggingGameObjects = new List<GameObject>();
            private List<Material> debuggingMaterials = new List<Material>();
            private List<Texture2D> debuggingTextures = new List<Texture2D>();

            public override void OnInspectorGUI()
            {
                //Start the undo event support, draw default inspector and monitor of changes
                SkinnedMeshCombiner script = (SkinnedMeshCombiner)target;
                EditorGUI.BeginChangeCheck();
                Undo.RecordObject(target, "Undo Event");
                script.gizmosOfThisComponentIsDisabled = MTAssetsEditorUi.DisableGizmosInSceneView("SkinnedMeshCombiner", script.gizmosOfThisComponentIsDisabled);

                //Clears undo history if desired. After a time, disable the clear undo
                if (script.clearUndoHistory == true)
                {
                    Undo.ClearAll();

                    timeToDisableClearUndoHistory += Time.deltaTime;
                    if (timeToDisableClearUndoHistory > 1f)
                    {
                        script.clearUndoHistory = false;
                        timeToDisableClearUndoHistory = 0;
                    }
                }

                //Warning if animator, or animation not found
                if (script.GetComponent<Animator>() == null && script.legacyAnimationSupport == false)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("The \"Animator\" component could not be found on this object. Please add the \"Animator\" component. The Skinned Mesh Combiner only works on the root of animated objects, next to the \"Animator\" component. Read the documentation for more details.", MessageType.Error);
                    GUILayout.Space(10);
                    script.legacyAnimationSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Legacy Animation Support", ""), script.legacyAnimationSupport);
                    return;
                }
                if (script.GetComponent<Animation>() == null && script.legacyAnimationSupport == true)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("The \"Animation\" component could not be found on this object. Please add the \"Animation\" component. The Skinned Mesh Combiner only works on the root of animated objects, next to the \"Animation\" component. Read the documentation for more details.", MessageType.Error);
                    GUILayout.Space(10);
                    script.legacyAnimationSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Legacy Animation Support", ""), script.legacyAnimationSupport);
                    return;
                }

                //Validate all variables to avoid errors
                script.ValidateAllVariables();

                //Support reminder
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Remember to read the Skinned Mesh Combiner documentation to understand how to use it.\nGet support at: mtassets@windsoft.xyz", MessageType.None);

                GUILayout.Space(10);

                //Show the main resume
                if (script.resultMergeGameObject == null)
                {
                    GUIStyle titulo = new GUIStyle();
                    titulo.fontSize = 16;
                    titulo.normal.textColor = Color.red;
                    titulo.alignment = TextAnchor.MiddleCenter;
                    EditorGUILayout.LabelField("The meshes are not combined", titulo);
                }
                if (script.resultMergeGameObject != null)
                {
                    GUIStyle titulo = new GUIStyle();
                    titulo.fontSize = 16;
                    titulo.normal.textColor = new Color(0, 79.0f / 250.0f, 3.0f / 250.0f);
                    titulo.alignment = TextAnchor.MiddleCenter;
                    EditorGUILayout.LabelField("The meshes are combined!", titulo);
                }
                GUIStyle subTitulo = new GUIStyle();
                subTitulo.fontSize = 10;
                subTitulo.normal.textColor = new Color(69.0f / 250.0f, 69.0f / 250.0f, 69.0f / 250.0f);
                subTitulo.alignment = TextAnchor.MiddleCenter;
                EditorGUILayout.LabelField((CurrentRenderPipeline.haveAnotherSrpPackages == true) ? (CurrentRenderPipeline.packageDetected + " Detected") : "Built-In RP Detected", subTitulo);

                GUILayout.Space(10);

                //Show the toolbar
                script.currentTab = GUILayout.Toolbar(script.currentTab, new string[] { "Merge", "Stats", "Logs of Merge (" + script.resultMergeLogs.Count + ")" });

                GUILayout.Space(10);

                //Draw the content of toolbar selected
                switch (script.currentTab)
                {
                    case 0:
                        Tab_Merge(script);
                        break;
                    case 1:
                        Tab_Stats(script);
                        break;
                    case 2:
                        Tab_LogsOfMerge(script);
                        break;
                }

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

            void Tab_Merge(SkinnedMeshCombiner script)
            {
                //Code of "Merge" tab
                if (script.resultMergeGameObject == null)
                {
                    //Run the Debugging Monitor for interface can knows all resorces of this model, like gameobjects, textures etc...
                    Tab_Merge_RunDebuggingMonitor(script);

                    EditorGUILayout.LabelField("Settings For Combine", EditorStyles.boldLabel);
                    GUILayout.Space(10);

                    //Selection box for "Merge Method"
                    script.mergeMethod = (MergeMethod)EditorGUILayout.EnumPopup(new GUIContent("Combine Method",
                        "Method to which the Skinned Mesh Combiner will use to merge the meshes." +
                        "\n\n\"One Mesh Per Material\" - Combines all meshes that share the same materials in just one mesh. All meshes will continue to use the original materials. It is a fast method to use at runtime." +
                        "\n\n\"All In One\" - This merge method combines all meshes in just one mesh. Even if each mesh uses different materials. The textures and materials will also be merged into just one. It is a fast method, but more suitable for use in the editor. You can configure it to be faster and use it at run time without problems." +
                        "\n\n\"Just Material Colors\" - It only works with the main colors of the materials. This blending method does not work with textures, it's perfect for people who do not use textures, just color their characters. All meshes of the model are merged into one, and all colors of the materials are combined in an atlas color palette. The matched mesh will use the colors of this palette." +
                        "\n\n\"Only Anima2D Meshes\" - Merge mode with exclusive compatibility for the \"Anima2D\" tool from Unity Technologies. This merge method combines all meshes generated from sprites (using the Anima2D tool) in just one mesh." +
                        "\n\nYou can read the documentation to understand all the limitations of metods and how the merge process works in this way to get your bearings."),
                        script.mergeMethod);

                    //Render the options according the merge method selected
                    switch (script.mergeMethod)
                    {
                        case MergeMethod.OneMeshPerMaterial:
                            MergeParms_OneMeshPerMaterialParams(script);
                            break;
                        case MergeMethod.JustMaterialColors:
                            MergeParms_JustMaterialColorsParams(script);
                            break;
                        case MergeMethod.AllInOne:
                            MergeParms_AllInOneParams(script);
                            break;
                        case MergeMethod.OnlyAnima2dMeshes:
                            MergeParms_OnlyAnima2dMeshsParams(script);
                            break;
                    }

                    script.blendShapesSupport = (BlendShapesSupport)EditorGUILayout.EnumPopup(new GUIContent("BlendShapes Support",
                        "Here you can choose how the Skinned Mesh Combiner will process BlendShapes." +
                        "\n\nDisabled - The Skinned Mesh Combiner will skip any meshes that contain Blendshapes. These meshes will not be included or processed in the merge. This option will prevent meshes that have Blendshapes from being merged, but will give you full control over the management of Blendshapes values." +
                        "\n\nEnabled - Skinned Mesh Renderer will not ignore meshes containing Blendshapes on merge, but Blendshapes will stop working after merging." +
                        "\n\nFullSupport - The Skinned Mesh Combiner will combine all meshes, including those with Blendshapes, however, here the Skinned Mesh Combiner will combine the meshes, maintain the Blendshapes values and it will still be possible to manipulate the Blendshapes after merging." +
                        "\n\n** Please note that not all meshes are compatible with the Blendshapes merge process. Because of this, some Blendshapes may not work as expected after merging. It's always good to test. **"),
                        script.blendShapesSupport);
                    if (script.blendShapesSupport == BlendShapesSupport.FullSupport)
                    {
                        //If blendshapes full support is enabled to OnlyAnima2DMeshes, cancel
                        if (script.mergeMethod == MergeMethod.OnlyAnima2dMeshes)
                        {
                            Debug.LogError("Full support for Blendshapes is only available in the \"One Mesh Per Material\", \"All In One\" and \"Just Material Colors\" merge methods.");
                            script.blendShapesSupport = BlendShapesSupport.Enabled;
                        }

                        EditorGUI.indentLevel += 1;
                        if (script.mergeMethod == MergeMethod.OneMeshPerMaterial && script.blendShapesSupport == BlendShapesSupport.FullSupport)
                            EditorGUILayout.HelpBox("Please note that full support for Blendshapes may be incompatible with some meshes when using the \"One Mesh Per Material\" merging method. This is due to the way in which this merging method works, this method needs to \"explode\" the mesh into several pieces according to the use of materials by each vertex and then combine them by rearranging them, which can cause some \"blendshapes are disorganized\" and don't work as expected. The \"All In One\" and \"Just Material Colors\" methods do not have this problem as they only generate 1 mesh in all situations. This incompatibility should not occur with all meshes, but it is always interesting to do a test.", MessageType.Warning);

                        script.blendShapesMultiplier = EditorGUILayout.Slider(new GUIContent("BlendShapes Multiplier",
                            "Sometimes, the Blendshapes of some meshes, in some cases, may not end up reaching the desired final position, even with the Blendshape value at \"100\" in the Skinned Mesh Renderer. In some cases, the Blendshape may appear inverted as well.\n\nYou can increase the value of the \"Blendshapes Multiplier\" to increase the strength that the Blendshapes are modified. If you increase this multiplier to \"2\" for example, the Blendshapes of the mesh resulting from the merge will move 2 times more than they would normally move. You can also change this multiplier to \"-1\" so that the Blendshapes of the mesh resulting from the merge move in the opposite direction.\n\nThe recommended value is \"1\". Only change it, if necessary."),
                            script.blendShapesMultiplier, -100.0f, 100.0f);
                        EditorGUI.indentLevel -= 1;
                    }

                    script.legacyAnimationSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Legacy Animation Support",
                        "By activating this option, Skinned Mesh Combiner will activate support for Unity's legacy animation system (The system that does not use Animator). If you use animations with Animator, it is highly recommended that you keep this option disabled to keep your blends working well."),
                        script.legacyAnimationSupport);

                    script.rootBoneToUse = (RootBoneToUse)EditorGUILayout.EnumPopup(new GUIContent("Root Bone To Use",
                        "Here, you can define which will be the root bone that the mesh resulting from the merging will use. The root bone is the bone that, when moved, will move the entire bone hierarchy as well." +
                        "\n\nAutomatic - Skinned Mesh Combine will use an algorithm to obtain the root bone automatically, from the original meshes." +
                        "\n\nManual - You can even supply a root bone, which is in your bone hierarchy to be used by the mesh resulting from the merge."),
                        script.rootBoneToUse);
                    if (script.rootBoneToUse == RootBoneToUse.Manual)
                    {
                        EditorGUI.indentLevel += 1;
                        //Check if root bone to use is valid
                        if (script.manualRootBoneToUse != null)
                        {
                            SkinnedMeshCombiner combiner = script.manualRootBoneToUse.GetComponentInParent<SkinnedMeshCombiner>();
                            if (combiner == null)
                                EditorGUILayout.HelpBox("It appears that the bone provided is not valid. Please provide a bone that is part of the child bone hierarchy of this Skinned Mesh Combiner component.", MessageType.Warning);
                        }
                        script.manualRootBoneToUse = (Transform)EditorGUILayout.ObjectField(new GUIContent("Custom Root Bone To Use",
                                            "This custom root bone will be used by the mesh resulting from the merge."),
                                            script.manualRootBoneToUse, typeof(Transform), true, GUILayout.Height(16));
                        EditorGUI.indentLevel -= 1;
                    }

                    script.autoManagePosition = (bool)EditorGUILayout.Toggle(new GUIContent("Auto Manage Position",
                        "This option improves the accuracy of Merging Vertices and Blendshapes processing by resetting your character's position in the world before merge and then returning to your character's original position after merge is complete.\n\n** Disable this option if you have problems with your character's unwanted movements/teleports when merging meshes in Runtime. **"),
                        script.autoManagePosition);

                    script.compatibilityMode = (bool)EditorGUILayout.Toggle(new GUIContent("Compatiblity Mode",
                        "If the compatibility mode is enabled, the Skinned Mesh Combiner will calculate the positions (of the array) in a way that it will reach more types of models (such as .blend, .fbx and others). Enabling compatibility mode will ensure that your animations are not deformed on most types of models.\n\nIf any model is displaying deformed animations, after merge, try disabling this option.\n\n**Disabling this option may cause the distortion of your model's animation. Only disable this option if you believe your model will benefit from this."),
                        script.compatibilityMode);

                    script.combineInactives = (bool)EditorGUILayout.Toggle(new GUIContent("Combine Inactives",
                        "If this option is on, the Skinned Mesh Combiner will attempt to combine the disabled meshes too."),
                        script.combineInactives);

                    script.combineOnStart = (CombineOnStart)EditorGUILayout.EnumPopup(new GUIContent("Combine On Start",
                        "Here you can enable or disable automatic meshing of meshes, right at the beginning of the execution of this scene in your game." +
                        "\n\nDisabled - Auto merge will not be performed." +
                        "\n\nOnAwake - Merging will take place in your game's Awake. Awake is executed before all the Start methods in your scene." +
                        "\n\nOnStart - The merging will be done at the Start of your game."),
                        script.combineOnStart);
                    if (script.combineOnStart != CombineOnStart.Disabled)
                    {
                        EditorGUI.indentLevel += 1;
                        script.convertCombinedMeshToStaticOnStart = (bool)EditorGUILayout.Toggle(new GUIContent("Convert To Static Mesh",
                        "Activate this option and when the game starts, the Skinned Mesh Combiner will combine the meshes of this model and then convert the mesh resulting from the merge, into a static mesh."),
                        script.convertCombinedMeshToStaticOnStart);
                        EditorGUI.indentLevel -= 1;
                    }

                    script.nameOfThisMerge = EditorGUILayout.TextField(new GUIContent("Name Of This Merge",
                                        "The name that will be given to the GameObject resulting from the merge."),
                                        script.nameOfThisMerge);

                    //Settings for "Layers Ordening" (Only if the Anima2D merge method is selected)
                    if (script.mergeMethod == MergeMethod.OnlyAnima2dMeshes && debuggingGameObjects.Count > 0)
                    {
                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("Meshes Layers Ordener", EditorStyles.boldLabel);
                        GUILayout.Space(10);

                        if (GUILayout.Button("Set Correct Sprite Meshes Rendering Order"))
                        {
                            System.Reflection.Assembly editorAssembly = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.FullName.StartsWith("Assembly-CSharp-Editor,")); //',' included to ignore  Assembly-CSharp-Editor-FirstPass
                            Type utilityType = editorAssembly.GetTypes().FirstOrDefault(t => t.FullName.Contains("MTAssets.SkinnedMeshCombiner.Editor.SpriteMeshesLayersOrdener"));
                            utilityType.GetMethod("OpenWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(obj: null, parameters: new object[] { debuggingGameObjects[0].transform.parent.transform });
                        }
                    }

                    //Settings for "Meshes To Ignore"
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Ignore During Merge", EditorStyles.boldLabel);
                    GUILayout.Space(10);

                    Texture2D removeItemIcon = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/MT Assets/Skinned Mesh Combiner/Editor/Images/Remove.png", typeof(Texture2D));
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("GameObjects To Ignore During Merge", GUILayout.Width(230));
                    GUILayout.Space(MTAssetsEditorUi.GetInspectorWindowSize().x - 230);
                    EditorGUILayout.LabelField("Size", GUILayout.Width(30));
                    EditorGUILayout.IntField(script.gameObjectsToIgnore.Count, GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();
                    GUILayout.BeginVertical("box");
                    gameObjectsToIgnoreScrollpos = EditorGUILayout.BeginScrollView(gameObjectsToIgnoreScrollpos, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(MTAssetsEditorUi.GetInspectorWindowSize().x), GUILayout.Height(100));
                    if (script.gameObjectsToIgnore.Count == 0)
                        EditorGUILayout.HelpBox("Oops! No GameObject to be ignored has been registered! If you want to subscribe any, click the button below!", MessageType.Info);
                    if (script.gameObjectsToIgnore.Count > 0)
                        for (int i = 0; i < script.gameObjectsToIgnore.Count; i++)
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(removeItemIcon, GUILayout.Width(25), GUILayout.Height(16)))
                                script.gameObjectsToIgnore.RemoveAt(i);
                            script.gameObjectsToIgnore[i] = (GameObject)EditorGUILayout.ObjectField(new GUIContent("GameObject " + i.ToString(), "This GameObject will be ignored during the merge, if it has any mesh, it will not be combined with the other meshes.\n\nClick the button to the left if you want to remove this GameObject from the list."), script.gameObjectsToIgnore[i], typeof(GameObject), true, GUILayout.Height(16));
                            GUILayout.EndHorizontal();
                        }
                    EditorGUILayout.EndScrollView();
                    GUILayout.EndVertical();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Add New Slot"))
                    {
                        script.gameObjectsToIgnore.Add(null);
                        gameObjectsToIgnoreScrollpos.y += 999999;
                    }
                    if (script.gameObjectsToIgnore.Count > 0)
                        if (GUILayout.Button("Remove Empty Slots", GUILayout.Width(Screen.width * 0.48f)))
                            for (int i = script.gameObjectsToIgnore.Count - 1; i >= 0; i--)
                                if (script.gameObjectsToIgnore[i] == null)
                                    script.gameObjectsToIgnore.RemoveAt(i);
                    GUILayout.EndHorizontal();

                    //Settings for "Merge Events"
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Merge Events", EditorStyles.boldLabel);
                    GUILayout.Space(10);
                    script.showMergeEventsOptions = EditorGUILayout.Foldout(script.showMergeEventsOptions, (script.showMergeEventsOptions == true ? "Hide Merge Events Parameters" : "Show Merge Events Parameters"));
                    if (script.showMergeEventsOptions == true)
                    {
                        DrawDefaultInspector();
                    }

                    //Settings for "Debugging"
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Debugging And Resources", EditorStyles.boldLabel);
                    GUILayout.Space(10);

                    if (Application.isPlaying == false)
                    {
                        script.currentDebuggingTab = GUILayout.Toolbar(script.currentDebuggingTab, new string[] { "Meshes (" + debuggingGameObjects.Count + ")", "Materials (" + debuggingMaterials.Count + ")", "Textures (" + debuggingTextures.Count + ")", "Update List" });
                        EditorGUILayout.BeginVertical("box");
                        debuggingScrollpos = EditorGUILayout.BeginScrollView(debuggingScrollpos, GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.Width(MTAssetsEditorUi.GetInspectorWindowSize().x), GUILayout.Height(100));
                        switch (script.currentDebuggingTab)
                        {
                            case 0:
                                Tab_Merge_DebuggingMeshes(script);
                                break;
                            case 1:
                                Tab_Merge_DebuggingMaterials();
                                break;
                            case 2:
                                Tab_Merge_DebuggingTextures_AndVerifyTexturesSizes(false);
                                break;
                            case 3:
                                debuggingGameObjects.Clear();
                                debuggingMaterials.Clear();
                                debuggingTextures.Clear();
                                script.currentDebuggingTab = 0;
                                Debug.Log("The resource debug list has been updated.");
                                break;
                        }
                        EditorGUILayout.EndScrollView();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.HelpBox("If you have recently made changes to your model, such as modifying meshes, textures or materials, click \"Update List\" to have the Skinned Mesh Combiner check for new resources.", MessageType.Info);
                    }
                    if (Application.isPlaying == true)
                    {
                        EditorGUILayout.HelpBox("The debugger is not available while the game is running. Only in editor mode.", MessageType.Info);
                    }

                    if (script.launchConsoleLogs == true)
                    {
                        EditorGUILayout.HelpBox("Tip: The \"Show Logs In Console\" is enabled for this component! Consider disabling this if you no longer need it. Logs generated by debugging can cause problems with memory consumption! You will still continue to receive the logs from the \"Logs of Merge\" tab here in the Inspector.", MessageType.Warning);
                    }
                    script.launchConsoleLogs = (bool)EditorGUILayout.Toggle(new GUIContent("Show Logs In Console",
                        "If debug mode is active, this component will display error messages whenever it encounter any problems during the merge. While you are developing this is fine, but if you are going to release a public version of your game, it is interesting to turn off this option to prevent log messages from taking up game memory."),
                        script.launchConsoleLogs);

                    if (script.mergeMethod == MergeMethod.AllInOne || script.mergeMethod == MergeMethod.JustMaterialColors || script.mergeMethod == MergeMethod.OnlyAnima2dMeshes)
                    {
                        script.highlightUvVertices = (bool)EditorGUILayout.Toggle(new GUIContent("Highlight UV Vertices",
                        "If this option is enabled, after combining the textures in an atlas, the UV map vertices of the combined mesh will be displayed in the atlas by yellow pixels.\n\nKeep in mind that enabling this option will increase processing time when merging meshes."),
                        script.highlightUvVertices);
                    }

                    //Settings for "Combine In Editor"
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("On Merge With Editor", EditorStyles.boldLabel);
                    GUILayout.Space(10);

                    if (Application.isPlaying == false)
                    {
                        script.saveDataInAssets = (bool)EditorGUILayout.Toggle(new GUIContent("Save Data In Assets",
                        "** Only works in the Inspector. **\n\nAfter combining the meshes, save the final mesh data in your project files."),
                        script.saveDataInAssets);

                        script.savePrefabOfMerge = (bool)EditorGUILayout.Toggle(new GUIContent("Save Prefab Of Merge",
                            "** Only works in the Inspector. **\n\nAfter combining the meshes, save a prefab of this GameObject in your project files.\n\nYou must set a name for the prefab to be saved. If a prefab with this same name already exists, it will not be overwritten. So you can update it from this GameObject.\n\nWhen you choose to save a prefab, the data should automatically be saved as well."),
                            script.savePrefabOfMerge);
                        if (script.savePrefabOfMerge == true)
                        {
                            EditorGUI.indentLevel += 1;
                            script.nameOfPrefabOfMerge = EditorGUILayout.TextField(new GUIContent("Prefab Name",
                                        "The name for the prefab to be saved."),
                                        script.nameOfPrefabOfMerge);
                            if (script.nameOfPrefabOfMerge == "")
                            {
                                DateTime now = DateTime.Now;
                                script.nameOfPrefabOfMerge = "prefab_of_merge_(" + script.gameObject.name + ")_" + now.Ticks;
                            }
                            EditorGUI.indentLevel -= 1;
                        }
                    }
                    if (Application.isPlaying == true)
                    {
                        EditorGUILayout.HelpBox("These settings are not available while your game is running. Only in editor mode.", MessageType.Info);
                    }
                }
                if (script.resultMergeGameObject != null)
                {
                    //Merge overview
                    GUIStyle titulo = new GUIStyle();
                    titulo.alignment = TextAnchor.MiddleCenter;
                    titulo.fontStyle = FontStyle.Bold;
                    EditorGUILayout.LabelField("Merge Overview (" + script.nameOfThisMerge + ")", titulo);
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("All skinned meshs associated and affiliated with this GameObject (which have not been marked to be ignored) have been combined. To modify merge parameters, please undo the merge using the button below, modify the desired parameters, then re-merge. Use the tabs above to view data and monitor the merge.", MessageType.Info);
                    GUILayout.Space(10);

                    //Verify if is missing data of meshs merged
                    if (script.isMissingDataOfMergeOrExistsProblemWithMerge() == true && Application.isPlaying == false)
                    {
                        EditorGUILayout.HelpBox("The Skinned Mesh Combiner has detected a problem with this merge. Please try to undo it using the button below, then redo it to fix it. If everything is fine with your combined model, just ignore this message!", MessageType.Error);
                    }

                    //Render the export options if the data is not missing
                    if (script.isMissingDataOfMergeOrExistsProblemWithMerge() == false && Application.isPlaying == false)
                    {
                        Tab_MergeDone_ExportingResources(script);
                    }

                    GUILayout.Space(10);
                }

                //Operate buttons
                if (script.resultMergeGameObject == null)
                {
                    GUILayout.Space(10);

                    //----- Run simple verifications to guarantee the quality of merge and merge of textures, in atlas -----//
                    //Try to found textures with different resolutions
                    script.RunDifferentSizesTexturesChecker(Tab_Merge_DebuggingTextures_AndVerifyTexturesSizes(true));
                    //Try to found textures without Read/Write enabled, in this model
                    script.RunReadWriteCheckerInAllTexturesOfThisModel();
                    //Try to found meshes that is not with zero transform, if fully blendshape support is enabled
                    if (script.blendShapesSupport == BlendShapesSupport.FullSupport)
                        script.RunBlendshapesMeshesWithNonZeroTransformsVerify(debuggingGameObjects);
                    //------- -------- -------//

                    GUILayout.Space(20);

                    if (GUILayout.Button("Combine Meshes!", GUILayout.Height(40)))
                    {
                        switch (script.mergeMethod)
                        {
                            case MergeMethod.OneMeshPerMaterial:
                                script.DoCombineMeshs_OneMeshPerMaterial();
                                break;
                            case MergeMethod.AllInOne:
                                script.DoCombineMeshs_AllInOne();
                                break;
                            case MergeMethod.JustMaterialColors:
                                script.DoCombineMeshs_JustMaterialColors();
                                break;
                            case MergeMethod.OnlyAnima2dMeshes:
                                script.DoCombineMeshs_OnlyAnima2dMeshs();
                                break;
                        }
                    }
                }
                if (script.resultMergeGameObject != null)
                {
                    if (GUILayout.Button("Go To Mesh Result Of Merge", GUILayout.Height(40)))
                    {
                        Selection.objects = new UnityEngine.Object[] { script.resultMergeGameObject };
                    }

                    if (GUILayout.Button("Undo Merge", GUILayout.Height(40)))
                    {
                        script.DoUndoCombineMeshs(true, true, false);
                    }
                }

                GUILayout.Space(10);
            }

            void MergeParms_OneMeshPerMaterialParams(SkinnedMeshCombiner script)
            {
                //Create the classes to storage params, if not exists
                if (script.oneMeshPerMaterialParams == null)
                    script.oneMeshPerMaterialParams = new OneMeshPerMaterialParams();

                //Start of OneMeshPerMaterialParams
                EditorGUI.indentLevel += 1;

                script.oneMeshPerMaterialParams.mergeOnlyEqualRootBones = (bool)EditorGUILayout.Toggle(new GUIContent("Only Eq. Root Bones",
                    "This is a security mechanism to prevent your meshes from being deformed after the merge.\n\nIf this option is enabled, the Skinned Mesh Combiner will ignore meshes that have a different Root Bone, thus preventing the combined mesh from being deformed. For example, Mixamo models often have different Root Bones.\n\nIf this option is disabled, meshes with different Root Bones will be combined. Disabling this option does not guarantee that the end result is the one you want."),
                    script.oneMeshPerMaterialParams.mergeOnlyEqualRootBones);

                EditorGUI.indentLevel -= 1;
            }

            void MergeParms_JustMaterialColorsParams(SkinnedMeshCombiner script)
            {
                //Create the classes to storage params, if not exists
                if (script.justMaterialColorsParams == null)
                    script.justMaterialColorsParams = new JustMaterialColorsParams();

                //Get all properties of material to use
                Dictionary<string, string> propertiesOfMaterialToUse = new Dictionary<string, string>();

                if (script.justMaterialColorsParams.materialToUse != null)
                {
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(script.justMaterialColorsParams.materialToUse.shader); i++)
                    {
                        if (propertiesOfMaterialToUse.ContainsKey(ShaderUtil.GetPropertyName(script.justMaterialColorsParams.materialToUse.shader, i)) == false)
                        {
                            if (ShaderUtil.GetPropertyType(script.justMaterialColorsParams.materialToUse.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                            {
                                propertiesOfMaterialToUse.Add(ShaderUtil.GetPropertyName(script.justMaterialColorsParams.materialToUse.shader, i), ShaderUtil.GetPropertyDescription(script.justMaterialColorsParams.materialToUse.shader, i));
                            }
                        }
                    }
                }

                //Get all properties of all materials founded
                Dictionary<string, string> propertiesOfAllMaterialsFounded = script.GetAllColorPropertiesOfMaterials(debuggingMaterials);

                //Start code of JustMaterialColorsParams
                EditorGUI.indentLevel += 1;

                if (script.justMaterialColorsParams.materialToUse == null)
                {
                    EditorGUILayout.HelpBox("Please add a custom material. This custom material will have its properties copied and will be associated with the merged mesh. The Skinned Mesh Combine can not function without a material.", MessageType.Error);
                }
                script.justMaterialColorsParams.materialToUse = (Material)EditorGUILayout.ObjectField(new GUIContent("Material To Use",
                    "This custom material will have its properties copied and will be associated with the merged mesh."),
                    script.justMaterialColorsParams.materialToUse, typeof(Material), true, GUILayout.Height(16));

                script.justMaterialColorsParams.mergeOnlyEqualsRootBones = (bool)EditorGUILayout.Toggle(new GUIContent("Only Eq. Root Bones",
                    "This is a security mechanism to prevent your meshes from being deformed after the merge.\n\nIf this option is enabled, the Skinned Mesh Combiner will ignore meshes that have a different Root Bone, thus preventing the combined mesh from being deformed. For example, Mixamo models often have different Root Bones.\n\nIf this option is disabled, meshes with different Root Bones will be combined. Disabling this option does not guarantee that the end result is the one you want."),
                    script.justMaterialColorsParams.mergeOnlyEqualsRootBones);

                script.justMaterialColorsParams.useDefaultColorProperty = (bool)EditorGUILayout.Toggle(new GUIContent("Default Color Property",
                   "If this option is disabled, the Skinned Mesh Combiner will try to look up Colors in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the Color in \"Color Settings\". Usually the most used name is \"_Color\" or \"_BaseColor\" (HDRP or URP)."),
                   script.justMaterialColorsParams.useDefaultColorProperty);
                if (script.justMaterialColorsParams.useDefaultColorProperty == false)
                {
                    EditorGUI.indentLevel += 1;
                    script.justMaterialColorsParams.colorPropertyToFind = script.DrawDropDownOfProperties("Find Colors In",
                        "The name of the shader property, which is responsible for storing the Color, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the Color on each material in each mesh. Usually the name used by most shaders is \"_Color\" or \"_BaseColor\" (HDRP or URP), but if any of your shaders have a different name, you can enter it here.",
                        (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "_Color" : "_BaseColor", (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "(Color Property)" : "(Base Color Property)", script.justMaterialColorsParams.colorPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.justMaterialColorsParams.mainTexturePropertyToInsert = script.DrawDropDownOfProperties("Apply Color Atlas In",
                        "The name of the shader property, which will be responsible for storing the Color Atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the Color Atlas in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_MainTex\" or \"_BaseMap\" (HDRP and URP), but if you have defined a custom shader and it has a different property name, you can enter it here.",
                        (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "_MainTex" : "_BaseMap", (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "(Main Texture Property)" : "(Albedo Property)", script.justMaterialColorsParams.mainTexturePropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }
                if (script.justMaterialColorsParams.useDefaultColorProperty == true)
                {
                    EditorGUI.indentLevel += 1;
                    EditorGUILayout.TextField(new GUIContent("Finding Colors In", "This is the default property that Skinned Mesh Combiner will look for your character's material colors. To change this and choose a non-standard property, uncheck the box above.\n\nThe Skinned Mesh Combiner has determined that this is the default property, based on the Scriptable Render Pipeline that is being used now."), script.justMaterialColorsParams.colorPropertyToFind + (((String.IsNullOrEmpty(CurrentRenderPipeline.packageDetected) == true) ? " (Default Property in Built-in SRP)" : " (Default Property in " + CurrentRenderPipeline.packageDetected + ")")));
                    EditorGUILayout.TextField(new GUIContent("Applying Atlas Map In", "This is the default property that Skinned Mesh Combiner will apply the generated color atlas texture to your character. To change this and choose a non-standard property, uncheck the box above.\n\nThe Skinned Mesh Combiner has determined that this is the default property, based on the Scriptable Render Pipeline that is being used now."), script.justMaterialColorsParams.mainTexturePropertyToInsert + (((String.IsNullOrEmpty(CurrentRenderPipeline.packageDetected) == true) ? " (Default Property in Built-in SRP)" : " (Default Property in " + CurrentRenderPipeline.packageDetected + ")")));
                    EditorGUI.indentLevel -= 1;
                }

                EditorGUI.indentLevel -= 1;
            }

            void MergeParms_AllInOneParams(SkinnedMeshCombiner script)
            {
                //Create the classes to storage params, if not exists
                if (script.allInOneParams == null)
                    script.allInOneParams = new AllInOneParams();

                //Get all properties of material to use
                Dictionary<string, string> propertiesOfMaterialToUse = new Dictionary<string, string>();

                if (script.allInOneParams.materialToUse != null)
                {
                    for (int i = 0; i < ShaderUtil.GetPropertyCount(script.allInOneParams.materialToUse.shader); i++)
                    {
                        if (propertiesOfMaterialToUse.ContainsKey(ShaderUtil.GetPropertyName(script.allInOneParams.materialToUse.shader, i)) == false)
                        {
                            if (ShaderUtil.GetPropertyType(script.allInOneParams.materialToUse.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                            {
                                propertiesOfMaterialToUse.Add(ShaderUtil.GetPropertyName(script.allInOneParams.materialToUse.shader, i), ShaderUtil.GetPropertyDescription(script.allInOneParams.materialToUse.shader, i));
                            }
                        }
                    }
                }

                //Get all properties of all materials founded
                Dictionary<string, string> propertiesOfAllMaterialsFounded = script.GetAllTexturePropertiesOfMaterials(debuggingMaterials);

                //Start code of JustMaterialColorsParams
                EditorGUI.indentLevel += 1;

                if (script.allInOneParams.materialToUse == null)
                {
                    EditorGUILayout.HelpBox("Please add a custom material. This custom material will have its properties copied and will be associated with the merged mesh. The Skinned Mesh Combine can not function without a material.", MessageType.Error);
                }
                script.allInOneParams.materialToUse = (Material)EditorGUILayout.ObjectField(new GUIContent("Material To Use",
                    "This custom material will have its properties copied and will be associated with the merged mesh."),
                    script.allInOneParams.materialToUse, typeof(Material), true, GUILayout.Height(16));

                script.allInOneParams.atlasResolution = (AtlasSize)EditorGUILayout.EnumPopup(new GUIContent("Atlas Max Resolution",
                    "The maximum resolution that the generated atlas can have. The higher the texture, the more detail in the model, but the longer the processing time. Larger textures will also consume more video memory."),
                    script.allInOneParams.atlasResolution);

                script.allInOneParams.atlasPadding = (AtlasPadding)EditorGUILayout.EnumPopup(new GUIContent("Atlas Padding",
                    "Here you can select the pixel spacing between each texture packaged in the atlas. If you are having problems with parts of the texture being rendered in unwanted locations on your model, try increasing this field. It is recommended that this field be 0 pixels, but if you want to increase it, keep in mind that this will increase the distance between each texture in the atlas, however, it will reduce the quality of all textures the higher the value selected here."),
                    script.allInOneParams.atlasPadding);

                script.allInOneParams.mipMapEdgesSize = (MipMapEdgesSize)EditorGUILayout.EnumPopup(new GUIContent("Mip Map Edges Size",
                    "Each texture in the atlas must have borders to avoid rendering problems at certain camera angles, and when the atlas is submitted to different levels of detail according to distance (MipMaps). The larger the edges of each texture, the less chance that the textures appear to be in the wrong place depending on the distance or angle of the camera, however, the larger the edges of the textures, the smaller the size of the respective texture and, consequently, the smaller the detail of the textures, forcing you to increase the size of your atlas. In this option, you can select the size in pixels that the edges of the textures will have. Some effects like Height Maps and the like may require a larger border, such as 64 pixels or more.\n\nAlso keep in mind that increasing the size of the edges can increase the copy time for each texture.\n\nTry not to make the edges larger than the textures themselves, as it will cause them to repeat and the quality of each texture will be very low when rendered in your model."),
                    script.allInOneParams.mipMapEdgesSize);

                script.allInOneParams.mergeTiledTextures = (MergeTiledTextures)EditorGUILayout.EnumPopup(new GUIContent("Merge Tiled Textures",
                "The Skinned Mesh Combiner will try to merge meshes that have the UV map larger than the texture (tiled textures) (UV maps larger than the texture, make the textures repeat) using an internal algorithm. When you merge UV maps that are larger than the texture, this mesh may lose some of the texture quality.\n\nSkip All - The Skinned Mesh Combiner will simply ignore all meshes using tiled textures and exclude them from the merge.\n\nLegacy Mode - The Skinned Mesh Combiner will not generate tiled textures in the final atlas of this blend and will do the entire mapping as if it were a normal texture. This will make blending faster and less hassle, however the texture will appear stretched on the model using the tiled texture.\n\nImproved Mode - Skinned Mesh Combiner will use a new and improved algorithm to map the tiled textures. The textures will be tiled inside the atlas and the UV mapping will be done normally, with support for negative UVs and tiling. This can reduce the quality of the tiled texture when it is rendered in the model. The more tiles, the lower the quality of that specific texture."),
                script.allInOneParams.mergeTiledTextures);

                script.allInOneParams.mergeOnlyEqualsRootBones = (bool)EditorGUILayout.Toggle(new GUIContent("Only Eq. Root Bones",
                    "This is a security mechanism to prevent your meshes from being deformed after the merge.\n\nIf this option is enabled, the Skinned Mesh Combiner will ignore meshes that have a different Root Bone, thus preventing the combined mesh from being deformed. For example, Mixamo models often have different Root Bones.\n\nIf this option is disabled, meshes with different Root Bones will be combined. Disabling this option does not guarantee that the end result is the one you want."),
                    script.allInOneParams.mergeOnlyEqualsRootBones);

                script.allInOneParams.useDefaultMainTextureProperty = (bool)EditorGUILayout.Toggle(new GUIContent("Default Main Tex. Prop.",
                    "If this option is disabled, the Skinned Mesh Combiner will try to look up Main Textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the Main Texture in \"Main Texture Settings\". Usually the most used name is \"_MainTex\" or \"_BaseMap\" (HDRP and URP)."),
                    script.allInOneParams.useDefaultMainTextureProperty);
                if (script.allInOneParams.useDefaultMainTextureProperty == false)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.mainTexturePropertyToFind = script.DrawDropDownOfProperties("Find Main Text. In",
                        "The name of the shader property, which is responsible for storing the Main Texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the main texture map on each material in each mesh. Usually the name used by most shaders is \"_MainTex\" or \"_BaseMap\" (HDRP and URP), but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the Main Texture in the mesh material, it will be without Main Texture after merging.",
                        (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "_MainTex" : "_BaseMap", (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "(Main Texture Property)" : "(Albedo Property)", script.allInOneParams.mainTexturePropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.mainTexturePropertyToInsert = script.DrawDropDownOfProperties("Apply Main Text. In",
                        "The name of the shader property, which will be responsible for storing the Main Texture of atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the Main Texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_MainTex\" or \"_BaseMap\" (HDRP and URP), but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the Main Texture in the final material of the mesh merge, it will be without Main Texture after merging.",
                        (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "_MainTex" : "_BaseMap", (CurrentRenderPipeline.haveAnotherSrpPackages == false) ? "(Main Texture Property)" : "(Albedo Property)", script.allInOneParams.mainTexturePropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }
                if (script.allInOneParams.useDefaultMainTextureProperty == true)
                {
                    EditorGUI.indentLevel += 1;
                    EditorGUILayout.TextField(new GUIContent("Finding Textures In", "This is the default property that Skinned Mesh Combiner will look for your character's material Main Textures. To change this and choose a non-standard property, uncheck the box above.\n\nThe Skinned Mesh Combiner has determined that this is the default property, based on the Scriptable Render Pipeline that is being used now."), script.allInOneParams.mainTexturePropertyToFind + (((String.IsNullOrEmpty(CurrentRenderPipeline.packageDetected) == true) ? " (Default Property in Built-in SRP)" : " (Default Property in " + CurrentRenderPipeline.packageDetected + ")")));
                    EditorGUILayout.TextField(new GUIContent("Applying Atlas Map In", "This is the default property that Skinned Mesh Combiner will apply the generated atlas texture to your character. To change this and choose a non-standard property, uncheck the box above.\n\nThe Skinned Mesh Combiner has determined that this is the default property, based on the Scriptable Render Pipeline that is being used now."), script.allInOneParams.mainTexturePropertyToInsert + (((String.IsNullOrEmpty(CurrentRenderPipeline.packageDetected) == true) ? " (Default Property in Built-in SRP)" : " (Default Property in " + CurrentRenderPipeline.packageDetected + ")")));
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.metallicMapSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Metallic Map Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up Mettalic Map textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the metallic texture map in \"Metallic Map Settings\". Usually the most used name is \"_MetallicGlossMap\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.metallicMapSupport);
                if (script.allInOneParams.metallicMapSupport == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.metallicMapPropertyToFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the metallic map texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the metallic texture map on each material in each mesh. Usually the name used by most shaders is \"_MetallicGlossMap\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the metallic map in the mesh material, it will be without metallic map after merging.",
                        "_MetallicGlossMap", "(Metallic Map Property)", script.allInOneParams.metallicMapPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.metallicMapPropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of metallic map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the metallic atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_MetallicGlossMap\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the metallic map in the final material of the mesh merge, it will be without metallic map after merging.",
                        "_MetallicGlossMap", "(Metallic Map Property)", script.allInOneParams.metallicMapPropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.specularMapSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Specu. Map Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up Specular Map textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the specular texture map in \"Specular Map Settings\". Usually the most used name is \"_SpecGlossMap\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.specularMapSupport);
                if (script.allInOneParams.specularMapSupport == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.specularMapPropertyToFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the specular map texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the specular texture map on each material in each mesh. Usually the name used by most shaders is \"_SpecGlossMap\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the specular map in the mesh material, it will be without specular map after merging.",
                        "_SpecGlossMap", "(Specular Map Property)", script.allInOneParams.specularMapPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.specularMapPropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of specular map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the specular atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_SpecGlossMap\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the specular map in the final material of the mesh merge, it will be without specular map after merging.",
                        "_SpecGlossMap", "(Specular Map Property)", script.allInOneParams.specularMapPropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.normalMapSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Normal Map Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up Normal Map textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the normal texture map in \"Normal Map Settings\". Usually the most used name is \"_BumpMap\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.normalMapSupport);
                if (script.allInOneParams.normalMapSupport == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.normalMapPropertyToFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the normal map texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the normal texture map on each material in each mesh. Usually the name used by most shaders is \"_BumpMap\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the normal map in the mesh material, it will be without normal map after merging.",
                        "_BumpMap", "(Normal Map Property)", script.allInOneParams.normalMapPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.normalMapPropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of normal map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the normal atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_BumpMap\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the normal map in the final material of the mesh merge, it will be without normal map after merging.",
                        "_BumpMap", "(Normal Map Property)", script.allInOneParams.normalMapPropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.normalMap2Support = (bool)EditorGUILayout.Toggle(new GUIContent("Norm. Map 2 Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up 2x Normal Map textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the second normal texture map in \"Normal Map 2x Settings\". Usually the most used name is \"_DetailNormalMap\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.normalMap2Support);
                if (script.allInOneParams.normalMap2Support == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.normalMap2PropertyFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the second normal map texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the second normal texture map on each material in each mesh. Usually the name used by most shaders is \"_DetailNormalMap\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the second normal map in the mesh material, it will be without second normal map after merging.",
                        "_DetailNormalMap", "(Normal Map Property)", script.allInOneParams.normalMap2PropertyFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.normalMap2PropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of second normal map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the second normal atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_DetailNormalMap\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the second normal map in the final material of the mesh merge, it will be without second normal map after merging.",
                        "_DetailNormalMap", "(Normal Map Property)", script.allInOneParams.normalMap2PropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.heightMapSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Height Map Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up Height Map textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the height map texture in \"Height Map Settings\". Usually the most used name is \"_ParallaxMap\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.heightMapSupport);
                if (script.allInOneParams.heightMapSupport == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.heightMapPropertyToFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the height map texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the height texture map on each material in each mesh. Usually the name used by most shaders is \"_ParallaxMap\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the height map in the mesh material, it will be without height map after merging.",
                        "_ParallaxMap", "(Height Map Property)", script.allInOneParams.heightMapPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.heightMapPropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of height map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the height atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_ParallaxMap\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the height map in the final material of the mesh merge, it will be without height map after merging.",
                        "_ParallaxMap", "(Height Map Property)", script.allInOneParams.heightMapPropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.occlusionMapSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Occlus. Map Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up Occlusion Map textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the occlusion texture map in \"Occlusion Map Settings\". Usually the most used name is \"_OcclusionMap\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.occlusionMapSupport);
                if (script.allInOneParams.occlusionMapSupport == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.occlusionMapPropertyToFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the occlusion map texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the occlusion texture map on each material in each mesh. Usually the name used by most shaders is \"_OcclusionMap\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the occlusion map in the mesh material, it will be without occlusion map after merging.",
                        "_OcclusionMap", "(Occlusion Map Property)", script.allInOneParams.occlusionMapPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.occlusionMapPropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of occlusion map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the occlusion atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_OcclusionMap\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the occlusion map in the final material of the mesh merge, it will be without occlusion map after merging.",
                        "_OcclusionMap", "(Occlusion Map Property)", script.allInOneParams.occlusionMapPropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.detailAlbedoMapSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Detail Map Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up Detail Albedo Map textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the detail albedo texture map in \"Detail Albedo Map Settings\". Usually the most used name is \"_DetailAlbedoMap\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.detailAlbedoMapSupport);
                if (script.allInOneParams.detailAlbedoMapSupport == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.detailMapPropertyToFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the detail albedo map texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the detail albedo texture map on each material in each mesh. Usually the name used by most shaders is \"_DetailAlbedoMap\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the detail albedo map in the mesh material, it will be without detail albedo map after merging.",
                        "_DetailAlbedoMap", "(Detail Map Property)", script.allInOneParams.detailMapPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.detailMapPropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of detail albedo map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the detail albedo atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_DetailAlbedoMap\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the detail albedo map in the final material of the mesh merge, it will be without detail albedo map after merging.",
                        "_DetailAlbedoMap", "(Detail Map Property)", script.allInOneParams.detailMapPropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                script.allInOneParams.detailMaskSupport = (bool)EditorGUILayout.Toggle(new GUIContent("Detail Mask Support",
                    "If this option is enabled, the Skinned Mesh Combiner will try to look up Detail Mask textures in each material of this model and combine them as well.\n\nYou will also need to provide the name of the property on which the shaders save the detail mask texture map in \"Detail Mask Settings\". Usually the most used name is \"_DetailMask\".\n\nKeep in mind that this function can increase the time taken to process the merge."),
                    script.allInOneParams.detailMaskSupport);
                if (script.allInOneParams.detailMaskSupport == true)
                {
                    EditorGUI.indentLevel += 1;
                    script.allInOneParams.detailMaskPropertyToFind = script.DrawDropDownOfProperties("Find Text. Maps In",
                        "The name of the shader property, which is responsible for storing the detail mask texture, in the material of its meshes. The Skinned Mesh Combiner will use the property here reported to fetch the detail mask texture on each material in each mesh. Usually the name used by most shaders is \"_DetailMask\", but if any of your shaders have a different name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the detail albedo map in the mesh material, it will be without detail mask map after merging.",
                        "_DetailMask", "(Detail Mask Property)", script.allInOneParams.detailMaskPropertyToFind, propertiesOfAllMaterialsFounded);

                    script.allInOneParams.detailMaskPropertyToInsert = script.DrawDropDownOfProperties("Apply Merged Map In",
                        "The name of the shader property, which will be responsible for storing the texture of detail mask map atlas, in the COMBINED mesh material. The Skinned Mesh Combiner will use the property here informed to apply the detail mask atlas map texture in the final material after the merge. Normally the name used by most shaders (including the standard pre-built shader) is \"_DetailMask\", but if you have defined a custom shader and it has a different property name, you can enter it here.\n\nIf the Skinned Mesh Combiner can not find the detail mask map in the final material of the mesh merge, it will be without detail mask map after merging.",
                        "_DetailMask", "(Detail Mask Property)", script.allInOneParams.detailMaskPropertyToInsert, propertiesOfMaterialToUse);
                    EditorGUI.indentLevel -= 1;
                }

                if (script.allInOneParams.normalMapSupport == true || script.allInOneParams.normalMap2Support == true)
                {
                    script.allInOneParams.pinkNormalMapsFix = (bool)EditorGUILayout.Toggle(new GUIContent("Pink Normal Maps Fix",
                    "If this option is active, the Skinned Mesh Combiner will execute an algorithm that will try to prevent the atlases generated from Normal Maps from becoming Pink/Orange, thanks to a different decoding of the colors of the original Normal Maps textures."),
                    script.allInOneParams.pinkNormalMapsFix);
                }

                EditorGUI.indentLevel -= 1;
            }

            void MergeParms_OnlyAnima2dMeshsParams(SkinnedMeshCombiner script)
            {
                //Create the classes to storage params, if not exists
                if (script.onlyAnima2dMeshes == null)
                    script.onlyAnima2dMeshes = new OnlyAnima2dMeshes();

                //Start code of OnlyAnima2dMeshes
                EditorGUI.indentLevel += 1;
#if MTAssets_Anima2D_Available
                script.onlyAnima2dMeshes.atlasResolution = (AtlasSize)EditorGUILayout.EnumPopup(new GUIContent("Atlas Max Resolution",
                "The maximum resolution that the generated atlas can have. The higher the texture, the more detail in the model, but the longer the processing time. Larger textures will also consume more video memory."),
                script.onlyAnima2dMeshes.atlasResolution);
#else
    EditorGUILayout.HelpBox("This merge method works only with meshes generated from Sprites 2D, using the Anima2D tool from Unity Technologies. The Skinned Mesh Combiner has not detected the namespace of this tool anywhere in your project. Please install Anima2D before using this merge method. If you're not going to use Anima2D, consider using other standard merge methods (facing 3D meshes).", MessageType.Error);
#endif
                EditorGUI.indentLevel -= 1;
            }

            void Tab_Merge_RunDebuggingMonitor(SkinnedMeshCombiner script)
            {
                //If the debugging gameobjects list, is major than zero, don't update the resources list
                if (debuggingGameObjects.Count > 0)
                    return;

                //Get all GameObjects to merge
                GameObject[] objects = script.GetAllItemsForCombine(false, false);

                //Reset the list
                debuggingGameObjects.Clear();

                //List all GameObjects
                foreach (GameObject obj in objects)
                {
                    debuggingGameObjects.Add(obj);
                }

                //Reset the list
                debuggingMaterials.Clear();

                //Prepare the variable to store materials
                Dictionary<Material, int> dictionary = new Dictionary<Material, int>();

                //Get all materials
                foreach (GameObject obj in objects)
                {
                    SkinnedMeshRenderer render = obj.GetComponent<SkinnedMeshRenderer>();
                    if (render != null)
                    {
                        foreach (Material mat in render.sharedMaterials)
                        {
                            if (mat == null)
                                continue;

                            if (dictionary.ContainsKey(mat) == false)
                            {
                                dictionary.Add(mat, 0);
                            }
                        }
                    }
                }

                //List all GameObjects
                foreach (var entry in dictionary)
                {
                    debuggingMaterials.Add(entry.Key);
                }


                //Reset the list
                debuggingTextures.Clear();

                //Prepare the variable to store textures
                Dictionary<Texture2D, int> dictionaryTex = new Dictionary<Texture2D, int>();

                //Get all textures
                foreach (Material mat in debuggingMaterials)
                {
                    foreach (var entry in script.ExtractAllTexturesFromMaterialUsingShaderUtils(mat))
                    {
                        if (entry.Value != null)
                        {
                            if (dictionaryTex.ContainsKey(entry.Value) == false)
                            {
                                dictionaryTex.Add(entry.Value, 0);
                            }
                        }

                    }
                }

                if (script.mergeMethod == MergeMethod.OnlyAnima2dMeshes)
                {
#if MTAssets_Anima2D_Available
                    //If the merge method is "Only Anima2D Meshes" get all textures of sprites
                    for (int i = 0; i < objects.Length; i++)
                    {
                        SpriteMeshInstance render = objects[i].GetComponent<SpriteMeshInstance>();
                        Texture2D texture = null;
                        if (render.spriteMesh != null && render.spriteMesh.sprite != null)
                            texture = (Texture2D)render.spriteMesh.sprite.texture;
                        if (texture != null)
                        {
                            if (dictionaryTex.ContainsKey(texture) == false)
                            {
                                dictionaryTex.Add(texture, 0);
                            }
                        }
                    }
#endif
                }

                //List all textures
                foreach (var tex in dictionaryTex)
                {
                    debuggingTextures.Add(tex.Key);
                }
            }

            void Tab_Merge_DebuggingMeshes(SkinnedMeshCombiner script)
            {
                //List all GameObjects
                foreach (GameObject obj in debuggingGameObjects)
                {
                    SkinnedMeshRenderer render = null;

                    GUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(obj.name, EditorStyles.boldLabel);
                    GUILayout.Space(-3);
                    if (script.mergeMethod != MergeMethod.OnlyAnima2dMeshes)
                    {
                        render = obj.GetComponent<SkinnedMeshRenderer>();
                        if (render != null)
                            EditorGUILayout.LabelField((render.sharedMesh != null) ? render.sharedMesh.name + Path.GetExtension(AssetDatabase.GetAssetPath(render.sharedMesh)) + ", " + render.sharedMesh.subMeshCount + " Mesh" + ", " + render.sharedMesh.vertexCount + " Vert" : "Not Found Mesh");
                        if (render == null)
                            EditorGUILayout.LabelField("Empty Skinned Mesh Renderer");
                    }
                    if (script.mergeMethod == MergeMethod.OnlyAnima2dMeshes)
                    {
                        EditorGUILayout.LabelField("Anima2D Mesh");
                    }
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(20);
                    EditorGUILayout.BeginVertical();
                    GUILayout.Space(8);
                    if (GUILayout.Button("Game Object", GUILayout.Height(20)))
                    {
                        EditorGUIUtility.PingObject(obj);
                    }
                    EditorGUILayout.EndVertical();
                    if (render != null)
                    {
                        EditorGUILayout.BeginVertical();
                        GUILayout.Space(8);
                        if (GUILayout.Button("Mesh", GUILayout.Height(20)))
                        {
                            EditorGUIUtility.PingObject(render.sharedMesh);
                        }
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }
            }

            void Tab_Merge_DebuggingMaterials()
            {
                //List all materials
                foreach (Material mat in debuggingMaterials)
                {
                    GUILayout.Space(2);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.LabelField(mat.name, EditorStyles.boldLabel);
                    GUILayout.Space(-3);
                    EditorGUILayout.LabelField(mat.shader.name);
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(20);
                    EditorGUILayout.BeginVertical();
                    GUILayout.Space(8);
                    if (GUILayout.Button("Material", GUILayout.Height(20)))
                    {
                        EditorGUIUtility.PingObject(mat);
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.BeginVertical();
                    GUILayout.Space(8);
                    if (GUILayout.Button("Properties", GUILayout.Height(20)))
                    {
                        StringBuilder str = new StringBuilder();
                        str.Append("Listing all the Properties (variables) found in this Material (shader) and their respective type...\n");

                        Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();

                        for (int i = 0; i < ShaderUtil.GetPropertyCount(mat.shader); i++)
                        {
                            if (dictionary.ContainsKey(ShaderUtil.GetPropertyType(mat.shader, i).ToString()) == false)
                            {
                                dictionary.Add(ShaderUtil.GetPropertyType(mat.shader, i).ToString(), new List<string>() { ShaderUtil.GetPropertyName(mat.shader, i) + " (" + ShaderUtil.GetPropertyDescription(mat.shader, i) + ")" });
                                continue;
                            }
                            if (dictionary.ContainsKey(ShaderUtil.GetPropertyType(mat.shader, i).ToString()) == true)
                            {
                                dictionary[ShaderUtil.GetPropertyType(mat.shader, i).ToString()].Add(ShaderUtil.GetPropertyName(mat.shader, i) + " (" + ShaderUtil.GetPropertyDescription(mat.shader, i) + ")");
                            }
                        }

                        foreach (var entry in dictionary)
                        {
                            str.Append("\nProperties of type \"" + entry.Key + "\"\n\n");

                            foreach (string strr in entry.Value)
                            {
                                str.Append(strr + "\n");
                            }
                        }

                        EditorUtility.DisplayDialog("Showing \"" + mat.name + "\" properties...", str.ToString(), "Close");
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2);
                }
            }

            bool Tab_Merge_DebuggingTextures_AndVerifyTexturesSizes(bool onlyVerifyTheTexturesSize)
            {
                //If not have textures
                if (debuggingTextures.Count == 0)
                    return false;

                //Last resolution found in debuggint textures
                bool existsTexturesWithDifferentSizesIfDebuggingTextures = false;
                Vector2 lastResolutionFound = new Vector2((debuggingTextures[0].width >= 64) ? debuggingTextures[0].width : 64, (debuggingTextures[0].height >= 64) ? debuggingTextures[0].height : 64);

                //List all Textures
                foreach (Texture2D texture in debuggingTextures)
                {
                    if (onlyVerifyTheTexturesSize == false)
                    {
                        GUIStyle estiloIcone = new GUIStyle();
                        estiloIcone.border = new RectOffset(0, 0, 0, 0);
                        estiloIcone.margin = new RectOffset(4, 0, 4, 0);

                        GUILayout.Space(2);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Box(texture, estiloIcone, GUILayout.Width(32), GUILayout.Height(28));
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField(texture.name + Path.GetExtension(AssetDatabase.GetAssetPath(texture)), EditorStyles.boldLabel);
                        GUILayout.Space(-3);
                        TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                        EditorGUILayout.LabelField(textureImporter.textureType.ToString() + ((textureImporter.isReadable == true) ? ", Readable" : ", Unreadable") + ", " + texture.width + "x" + texture.height + ", " + texture.format);
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(20);
                        EditorGUILayout.BeginVertical();
                        GUILayout.Space(8);
                        if (GUILayout.Button("Texture", GUILayout.Height(20)))
                        {
                            EditorGUIUtility.PingObject(texture);
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }

                    //Check if this resolution is different of the last found
                    if (texture.width >= 64 && texture.width != lastResolutionFound.x)
                        existsTexturesWithDifferentSizesIfDebuggingTextures = true;
                    if (texture.width >= 64 && texture.width == lastResolutionFound.x)
                        lastResolutionFound = new Vector2(texture.width, texture.height);
                }

                return existsTexturesWithDifferentSizesIfDebuggingTextures;
            }

            void Tab_MergeDone_ExportingResources(SkinnedMeshCombiner script)
            {
                //If merge method is One Mesh Per Material
                if (script.mergeMethod == MergeMethod.OneMeshPerMaterial)
                {
                    EditorGUILayout.HelpBox("The merging method being used, \"One Mesh Per Material\", does not generate resources that can be exported.", MessageType.Info);
                    return;
                }

                //Create button to export all textures
                if (GUILayout.Button("Export All Atlas", GUILayout.Height(40)))
                {
                    //Open selection of folder
                    string folder = EditorUtility.OpenFolderPanel("Select Folder To Save", "", "");
                    if (String.IsNullOrEmpty(folder) == true)
                        return;

                    //Show progress bar
                    EditorUtility.DisplayProgressBar("A moment", "Exporting Atlas as PNG", 1.0f);

                    //For file of list of assets saved
                    foreach (string assetPath in script.resultMergeAssetsSaved)
                    {
                        //Load type of asset and the asset
                        var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(object));
                        var type = typeof(object);
                        if (asset != null)
                            type = asset.GetType();

                        if (asset == null)
                        {
                            EditorUtility.DisplayDialog("Atlas Not Found", "It was not possible to export the texture, as it was not found in the directory below\n\n" + assetPath, "Continue");
                            continue;
                        }
                        if (asset != null && type != typeof(Texture2D))
                            continue;
                        if (asset != null && type == typeof(Texture2D))
                        {
                            Texture2D texture = asset as Texture2D;
                            byte[] mainTextureBytes = texture.EncodeToPNG();
                            File.WriteAllBytes(folder + "/" + asset.name + ".png", mainTextureBytes);
                        }
                    }

                    //Clear progress bar
                    EditorUtility.ClearProgressBar();

                    //Show warning
                    EditorUtility.DisplayDialog("Done", "Exporting process is finished. All atlas generated by this merge, was exported to path below\n\n" + folder, "Ok");
                }
                if (GUILayout.Button("Export Material", GUILayout.Height(40)))
                {
                    //Open selection of folder
                    string folder = EditorUtility.OpenFolderPanel("Select Folder To Save", "", "");
                    if (String.IsNullOrEmpty(folder) == true)
                        return;

                    //Show progress bar
                    EditorUtility.DisplayProgressBar("A moment", "Exporting Materials", 1.0f);

                    //For file of list of assets saved
                    foreach (string assetPath in script.resultMergeAssetsSaved)
                    {
                        //Load type of asset and the asset
                        var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(object));
                        var type = typeof(object);
                        if (asset != null)
                            type = asset.GetType();

                        if (asset == null)
                        {
                            EditorUtility.DisplayDialog("Materials Not Found", "It was not possible to export the material, as it was not found in the directory below\n\n" + assetPath, "Continue");
                            continue;
                        }
                        if (asset != null && type != typeof(Material))
                            continue;
                        if (asset != null && type == typeof(Material))
                        {
                            File.Copy(assetPath, folder + "/" + asset.name + ".mat");
                        }
                    }

                    //Clear progress bar
                    EditorUtility.ClearProgressBar();

                    //Show warning
                    EditorUtility.DisplayDialog("Done", "Exporting process is finished. All materials generated by this merge, was exported to path below\n\n" + folder, "Ok");
                }
            }

            void Tab_Stats(SkinnedMeshCombiner script)
            {
                //Code of "Stats" tab
                if (Application.isPlaying == false)
                {
                    if (script.resultMergeGameObject == null)
                    {
                        if (CurrentRenderPipeline.haveAnotherSrpPackages == true)
                            EditorGUILayout.HelpBox("The Skinned Mesh Combiner has identified that you are using another Scriptable Render Pipeline package. Apparently you're using " + CurrentRenderPipeline.packageDetected + ".", MessageType.Info);
                        if (CurrentRenderPipeline.haveAnotherSrpPackages == false)
                            EditorGUILayout.HelpBox("The Skinned Mesh Combiner has identified that you are using the Built In Scriptable Render Pipeline.", MessageType.Info);

                        EditorGUILayout.HelpBox("The merge of this model has not yet been made. Please merge to see statistics.", MessageType.Warning);
                    }

                    if (script.resultMergeGameObject != null)
                    {
                        //Merge overview
                        GUIStyle title = new GUIStyle();
                        title.alignment = TextAnchor.MiddleCenter;
                        title.fontStyle = FontStyle.Bold;
                        EditorGUILayout.LabelField("General Statistics", title);
                        GUILayout.Space(10);

                        EditorGUILayout.HelpBox(script.resultMergeTextStats, MessageType.Info);
                        if (CurrentRenderPipeline.haveAnotherSrpPackages == true)
                            EditorGUILayout.HelpBox("The Skinned Mesh Combiner has identified that you are using another Scriptable Render Pipeline package. Apparently you're using " + CurrentRenderPipeline.packageDetected + ".", MessageType.Info);
                        if (CurrentRenderPipeline.haveAnotherSrpPackages == false)
                            EditorGUILayout.HelpBox("The Skinned Mesh Combiner has identified that you are using the Built In Scriptable Render Pipeline.", MessageType.Info);

                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("Generated Resources", title);
                        GUILayout.Space(10);

                        //Prefabs buttom
                        if (script.savePrefabOfMerge == true)
                        {
                            GUILayout.Space(2);
                            EditorGUILayout.BeginHorizontal("box");
                            GUILayout.Box("P", GUILayout.Width(28), GUILayout.Height(28));
                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.LabelField(script.nameOfPrefabOfMerge, EditorStyles.boldLabel);
                            GUILayout.Space(-3);
                            EditorGUILayout.LabelField("Prefab Of This Merge/.prefab");
                            EditorGUILayout.EndVertical();
                            GUILayout.Space(20);
                            EditorGUILayout.BeginVertical();
                            GUILayout.Space(8);
                            if (GUILayout.Button("Prefabs", GUILayout.Height(20)))
                            {
                                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath("Assets/MT Assets/_AssetsData/Assets/Prefabs", typeof(object)));
                            }
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(2);
                        }

                        //GameObject result of merge
                        GUILayout.Space(2);
                        EditorGUILayout.BeginHorizontal("box");
                        GUILayout.Box("G", GUILayout.Width(28), GUILayout.Height(28));
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField(script.resultMergeGameObject.name, EditorStyles.boldLabel);
                        GUILayout.Space(-3);
                        EditorGUILayout.LabelField("Result of Merge/.gameObject");
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(20);
                        EditorGUILayout.BeginVertical();
                        GUILayout.Space(8);
                        if (GUILayout.Button("Result GO", GUILayout.Height(20)))
                        {
                            EditorGUIUtility.PingObject(script.resultMergeGameObject);
                        }
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(2);

                        foreach (string str in script.resultMergeAssetsSaved)
                        {
                            script.DrawItemOfListOfResourcesInStats(str);
                        }
                    }
                }
                if (Application.isPlaying == true)
                {
                    EditorGUILayout.HelpBox("This tab is not available while the game is running. Only in editor mode.", MessageType.Info);
                }
            }

            void Tab_LogsOfMerge(SkinnedMeshCombiner script)
            {
                //Code of "Logs Of Merge" tab
                EditorGUILayout.BeginVertical("box", GUILayout.Width(MTAssetsEditorUi.GetInspectorWindowSize().x), GUILayout.Height(300));
                script.logsOfMergeScrollpos = EditorGUILayout.BeginScrollView(script.logsOfMergeScrollpos);
                for (int i = 0; i < script.resultMergeLogs.Count; i++)
                {
                    if (script.resultMergeLogs[i].logType == LogTypeOf.Assert || script.resultMergeLogs[i].logType == LogTypeOf.Error || script.resultMergeLogs[i].logType == LogTypeOf.Exception)
                    {
                        EditorGUILayout.HelpBox(script.resultMergeLogs[i].content, MessageType.Error);
                    }
                    if (script.resultMergeLogs[i].logType == LogTypeOf.Log)
                    {
                        EditorGUILayout.HelpBox(script.resultMergeLogs[i].content, MessageType.Info);
                    }
                    if (script.resultMergeLogs[i].logType == LogTypeOf.Warning)
                    {
                        EditorGUILayout.HelpBox(script.resultMergeLogs[i].content, MessageType.Warning);
                    }
                }
                if (script.resultMergeLogs.Count == 0)
                {
                    EditorGUILayout.HelpBox("After completing a merge, you can see all the logs that were posted during the merge, here in this tab!", MessageType.Info);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                GUILayout.Space(10);

                EditorGUILayout.HelpBox("This is the logs tab. On this tab, you can see all the logs that this component throws during the merge. This tab only captures released logs while you combine using the Unity Editor. If you combine during the game, you will only be able to see released logs when \"Enable Debug Logs\" is enabled.", MessageType.Info);

                //Set the scroll of logs to end, if has new logs
                if (script.resultMergeLogs.Count != script.lastQuantityOfLogs)
                {
                    script.logsOfMergeScrollpos.y += 99999;
                    script.lastQuantityOfLogs = script.resultMergeLogs.Count;
                }
            }
        }
        #endregion

        #region INTERFACE_CODE_TOOLS_METHODS
        //Tools methods ONLY for editor interface
        private bool isMissingDataOfMergeOrExistsProblemWithMerge()
        {
            //Get the skinned mesh renderer
            SkinnedMeshRenderer renderer = resultMergeGameObject.GetComponent<SkinnedMeshRenderer>();

            //Verify if not exists a skinned mehs renderer
            if (renderer == null)
                return true;

            //Verify if all materials is ok, and textures
            if (renderer != null)
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat == null)
                        return true;
                    if (mergeMethod != MergeMethod.OnlyAnima2dMeshes && mergeMethod != MergeMethod.OneMeshPerMaterial)
                    {
                        if (mat.HasProperty("_BaseMap") == true)
                            if (mat.GetTexture("_BaseMap") == null)
                                if (mat.shader != null)
                                    if (mat.shader.name == "Standard" || mat.shader.name == "Lit" || mat.shader.name == "Unlit")
                                        return true;
                        if (mat.HasProperty("_MainTex") == true)
                            if (mat.GetTexture("_MainTex") == null)
                                if (mat.shader != null)
                                    if (mat.shader.name == "Standard" || mat.shader.name == "Lit" || mat.shader.name == "Unlit")
                                        return true;
                    }
                }

            //Verify if a file is missing
            foreach (string str in resultMergeAssetsSaved)
                if (AssetDatabase.LoadAssetAtPath(str, typeof(object)) == null)
                    return true;

            return false;
        }

        private void RunDifferentSizesTexturesChecker(bool haveTexturesWithDifferentSizes)
        {
            if (haveTexturesWithDifferentSizes == true && dontShowWarnsAboutTexturesResolution == false && mergeMethod == MergeMethod.AllInOne)
            {
                EditorGUILayout.HelpBox("There are textures or maps with different sizes than other textures. This can lead to inconsistencies when all textures are packaged in atlases, which can lead to the wrong positioning of your model's UV in the atlas due to different texture sizes. To correct this, make sure that all textures in your model are the same size.\n\nFor optimal atlas mapping accuracy and correct positioning of your textures and maps, always use square or POT textures. If you use maps like Normal Maps and found that their positioning was incorrect in the mesh after merging, consult the documentation for more details if you have any questions.", MessageType.Warning);
                if (currentDebuggingTab != 2)
                    if (GUILayout.Button("View Resolutions In Textures Tab"))
                        currentDebuggingTab = 2;
                if (GUILayout.Button("Fix All Textures Where Size is Different"))
                    FixAllTexturesWhereResolutionsIsDifferent();
                if (GUILayout.Button("Ignore And Don't Warn To This Mesh Anymore"))
                    if (EditorUtility.DisplayDialog("Are you sure?", "Warnings for resolving texture problems will no longer be displayed for this Skinned Mesh Combiner component. The warning may appear on other components or new Skinned Mesh Combiner components created afterwards, without problems. These warnings are useful to help you avoid merging problems. Are you sure you want to hide any text resolution warnings for this Skinned Mesh Combiner component?", "Yes", "No") == true)
                        dontShowWarnsAboutTexturesResolution = true;
            }
        }

        private void RunReadWriteCheckerInAllTexturesOfThisModel()
        {
            if (mergeMethod != MergeMethod.OneMeshPerMaterial)
            {
                //Get all items to combine
                GameObject[] itemsToCombine = GetAllItemsForCombine(false, false);

                //Prepare the variable of textures without RW
                Dictionary<Texture2D, int> texturesWithouRw = new Dictionary<Texture2D, int>();
                List<Texture2D> texturesWithoutRwList = new List<Texture2D>();

                //Verify each texture for find textures without R/W enabled
                foreach (GameObject obj in itemsToCombine)
                {
                    if (mergeMethod != MergeMethod.OnlyAnima2dMeshes)
                    {
                        for (int i = 0; i < itemsToCombine.Length; i++)
                        {
                            SkinnedMeshRenderer render = obj.GetComponent<SkinnedMeshRenderer>();
                            foreach (Material mat in render.sharedMaterials)
                            {
                                Dictionary<string, Texture2D> dictionary = ExtractAllTexturesFromMaterialUsingShaderUtils(mat);
                                foreach (var key in dictionary)
                                    if (key.Value != null && key.Value.isReadable == false)
                                        if (texturesWithouRw.ContainsKey(key.Value) == false)
                                            texturesWithouRw.Add(key.Value, 0);
                            }
                        }
                    }
                    if (mergeMethod == MergeMethod.OnlyAnima2dMeshes)
                    {
#if MTAssets_Anima2D_Available
                        for (int i = 0; i < itemsToCombine.Length; i++)
                        {
                            SpriteMeshInstance render = obj.GetComponent<SpriteMeshInstance>();
                            Texture2D texture = null;
                            if (render.spriteMesh != null && render.spriteMesh.sprite != null)
                                texture = (Texture2D)render.spriteMesh.sprite.texture;
                            if (texture != null && texture.isReadable == false)
                                if (texturesWithouRw.ContainsKey(texture) == false)
                                    texturesWithouRw.Add(texture, 0);
                        }
#endif
                    }
                }

                //Add the unique textures to list of uniques, and return a array
                foreach (var key in texturesWithouRw.Keys)
                {
                    texturesWithoutRwList.Add(key);
                }

                //Show the warning in interface
                if (texturesWithoutRwList.Count > 0)
                {
                    EditorGUILayout.HelpBox("Textures were identified in this model, which do not have the \"Read/Write\" option enabled in the import settings. Therefore, the Skinned Mesh Combiner cannot analyze or include them in the merge. Use the button below to correct all textures, nothing will be changed in them, rest assured!", MessageType.Warning);
                    if (GUILayout.Button("Fix All Textures Where R/W is Disabled"))
                    {
                        EditorUtility.DisplayProgressBar("Processing", "Enabling Read/Write in all Textures...", 1);

                        foreach (Texture2D tex in texturesWithoutRwList)
                        {
                            TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                            if (textureImporter.isReadable == false)
                            {
                                textureImporter.isReadable = true;
                            }
                            AssetDatabase.ImportAsset(textureImporter.assetPath);
                            AssetDatabase.Refresh();
                        }

                        EditorUtility.ClearProgressBar();
                        EditorUtility.DisplayDialog("Done", "All textures that did not have the \"Read/Write\" option enabled, have been activated!", "Ok");
                    }
                }
            }
        }

        private void FixAllTexturesWhereResolutionsIsDifferent()
        {
            EditorUtility.DisplayProgressBar("A moment", "Processing...", 1);

            //Get all items to combine
            GameObject[] itemsToCombine = GetAllItemsForCombine(false, false);

            //Prepare the variable of unique textures
            Dictionary<Texture2D, int> uniqueTexturesDict = new Dictionary<Texture2D, int>();

            //Verify each texture and insert in dictionary
            foreach (GameObject obj in itemsToCombine)
                for (int i = 0; i < itemsToCombine.Length; i++)
                {
                    SkinnedMeshRenderer render = obj.GetComponent<SkinnedMeshRenderer>();
                    foreach (Material mat in render.sharedMaterials)
                    {
                        Dictionary<string, Texture2D> dictionary = ExtractAllTexturesFromMaterialUsingShaderUtils(mat);
                        foreach (var key in dictionary)
                            if (key.Value != null)
                                if (uniqueTexturesDict.ContainsKey(key.Value) == false)
                                    uniqueTexturesDict.Add(key.Value, 0);
                    }
                }

            //Add the unique textures to list of uniques, and return a array
            List<Texture2D> uniqueTextures = new List<Texture2D>();
            foreach (var key in uniqueTexturesDict.Keys)
                uniqueTextures.Add(key);

            //Get all width and height of all textures
            int[] heights = new int[uniqueTextures.Count];
            int[] widths = new int[uniqueTextures.Count];
            for (int i = 0; i < uniqueTextures.Count; i++)
            {
                heights[i] = uniqueTextures[i].height;
                widths[i] = uniqueTextures[i].width;
            }

            //Verify the menor width and height of all textures
            int minorHeight = Mathf.Min(heights);
            int minorWidth = Mathf.Min(widths);

            //Calculate the final result of minor pixels of all textures
            int minorSize = 0;
            if (minorHeight < minorWidth)
                minorSize = minorHeight;
            if (minorHeight > minorWidth)
                minorSize = minorWidth;
            if (minorHeight == minorWidth)
                minorSize = minorWidth;

            //Show the warning
            EditorUtility.ClearProgressBar();
            if (EditorUtility.DisplayDialog("Continue?", "The Skinned Mesh Combiner will adjust all the import settings for the textures of this model, so that all textures, maps and etc. have the same resolution. You can undo these changes in your textures' import settings later if you want.\n\nThe Skinned Mesh Combiner will leave all your textures at the same resolution as the smallest texture detected, so if the smallest of your textures is 256x256 pixels and the others are 1024x1024, all of your textures will be resized to 256x256 pixels in the import settings. This can avoid several problems with UV mapping and texture placement.\n\nThe minor size found in all textures is " + minorSize.ToString() + "px. Continue?", "Yes", "No") == false)
                return;

            //Resize all textures to minor size of all
            EditorUtility.DisplayProgressBar("Processing", "Resizing all textures to " + minorSize.ToString() + "px...", 1);

            foreach (Texture2D tex in uniqueTextures)
            {
                TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                textureImporter.maxTextureSize = minorSize;
                AssetDatabase.ImportAsset(textureImporter.assetPath);
                AssetDatabase.Refresh();
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Done", "All textures have been resized to " + minorSize.ToString() + "px size in your import settings!", "Ok");
        }

        private void RunBlendshapesMeshesWithNonZeroTransformsVerify(List<GameObject> debuggingGameobjects)
        {
            //Have non zero transform meshes
            bool haveNonZeroTransformMeshes = false;

            //Check all skinned mesh renderers
            foreach (GameObject obj in debuggingGameobjects)
            {
                SkinnedMeshRenderer smr = obj.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                    if (smr.sharedMesh != null)
                        if (smr.sharedMesh.blendShapeCount > 0)
                            if (smr.transform.localPosition != Vector3.zero || smr.transform.localEulerAngles != Vector3.zero || smr.transform.localScale != Vector3.one)
                                haveNonZeroTransformMeshes = true;
            }

            //Notify the identification
            if (haveNonZeroTransformMeshes == true)
            {
                EditorGUILayout.HelpBox("You have enabled full support for Blendshapes. Meshes were found with Blendshapes, which are in position or rotation, different from zero. This can cause the mesh resulting from the merge to move the vertices of the Blendshapes in the wrong direction. Click the button below to correct the problem and avoid problems with the Blendshapes of the mesh resulting from the merging.", MessageType.Warning);
                if (GUILayout.Button("Fix All Transforms of Blendshapes Meshes"))
                    if (EditorUtility.DisplayDialog("Continue?", "This will reset the Transforms of all meshes containing Blendshapes, to the value of zero in the local position and zero in the local rotation, in each mesh. This should not affect the way your characters meshes are rendered. Do you wish to continue?", "Yes", "No") == true)
                    {
                        //Fix all transforms of blendshapes meshes
                        foreach (GameObject obj in debuggingGameobjects)
                        {
                            SkinnedMeshRenderer smr = obj.GetComponent<SkinnedMeshRenderer>();
                            if (smr != null)
                                if (smr.sharedMesh != null)
                                    if (smr.sharedMesh.blendShapeCount > 0)
                                        if (smr.transform.localPosition != Vector3.zero || smr.transform.localEulerAngles != Vector3.zero || smr.transform.localScale != Vector3.one)
                                        {
                                            smr.transform.localPosition = Vector3.zero;
                                            smr.transform.localEulerAngles = Vector3.zero;
                                            smr.transform.localScale = Vector3.one;
                                        }
                        }
                        //Notify
                        Debug.Log("All done. The Transforms of the meshes that contain Blendshapes, have been fixed.");
                    }
            }
        }

        private Dictionary<string, Texture2D> ExtractAllTexturesFromMaterialUsingShaderUtils(Material material)
        {
            //Prepare the variable to store textures
            Dictionary<string, Texture2D> dictionary = new Dictionary<string, Texture2D>();

            //Get all textures from material
            for (int i = 0; i < ShaderUtil.GetPropertyCount(material.shader); i++)
            {
                if (ShaderUtil.GetPropertyType(material.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    Texture texture = material.GetTexture(ShaderUtil.GetPropertyName(material.shader, i));

                    if (dictionary.ContainsKey(ShaderUtil.GetPropertyName(material.shader, i)) == false)
                    {
                        dictionary.Add(ShaderUtil.GetPropertyName(material.shader, i), (Texture2D)texture);
                    }
                }
            }

            //Return the texture from a desired material
            return dictionary;
        }

        private Dictionary<string, string> GetAllTexturePropertiesOfMaterials(List<Material> materials)
        {
            //Get all properties name, and description of all materials informed
            Dictionary<string, string> dictionary = new Dictionary<string, string>();

            foreach (Material mat in materials)
            {
                for (int i = 0; i < ShaderUtil.GetPropertyCount(mat.shader); i++)
                {
                    if (dictionary.ContainsKey(ShaderUtil.GetPropertyName(mat.shader, i)) == false)
                    {
                        if (ShaderUtil.GetPropertyType(mat.shader, i) == ShaderUtil.ShaderPropertyType.TexEnv)
                        {
                            dictionary.Add(ShaderUtil.GetPropertyName(mat.shader, i), ShaderUtil.GetPropertyDescription(mat.shader, i));
                        }
                    }
                }
            }

            return dictionary;
        }

        private Dictionary<string, string> GetAllColorPropertiesOfMaterials(List<Material> materials)
        {
            //Get all properties name, and description of all materials informed
            Dictionary<string, string> dictionary = new Dictionary<string, string>();

            foreach (Material mat in materials)
            {
                for (int i = 0; i < ShaderUtil.GetPropertyCount(mat.shader); i++)
                {
                    if (dictionary.ContainsKey(ShaderUtil.GetPropertyName(mat.shader, i)) == false)
                    {
                        if (ShaderUtil.GetPropertyType(mat.shader, i) == ShaderUtil.ShaderPropertyType.Color)
                        {
                            dictionary.Add(ShaderUtil.GetPropertyName(mat.shader, i), ShaderUtil.GetPropertyDescription(mat.shader, i));
                        }
                    }
                }
            }

            return dictionary;
        }

        private string DrawDropDownOfProperties(string title, string tooltip, string defaultValue, string defaultValueSuffix, string currentSelected, Dictionary<string, string> allPropertiesToShow)
        {
            //Prepare the options formatation to show, and a copy list, that contains only the name of property
            List<string> allOptions = new List<string>();
            List<string> allOptionsFormated = new List<string>();
            allOptions.Add(defaultValue);
            allOptionsFormated.Add(defaultValue + " " + defaultValueSuffix);
            foreach (var entry in allPropertiesToShow)
            {
                if (entry.Key != defaultValue)
                {
                    allOptions.Add(entry.Key);
                    allOptionsFormated.Add(entry.Key + " (" + entry.Value + " Property)");
                }
            }

            //Identify the ID of property name that is using at moment, and show as select at moment, in enum
            int selected = 0;
            for (int i = 0; i < allOptions.Count; i++)
            {
                if (allOptions[i] == currentSelected)
                {
                    selected = i;
                    break;
                }
            }

            //Show a enum with all properties formatted and return the propertie name only, of the selected
            return allOptions[EditorGUILayout.Popup(new GUIContent(title, tooltip), selected, allOptionsFormated.ToArray())];
        }

        private void DrawItemOfListOfResourcesInStats(string filePath)
        {
            //Get the type of asset
            Type assetType = AssetDatabase.GetMainAssetTypeAtPath(filePath);

            //Load the asset
            var asset = AssetDatabase.LoadAssetAtPath(filePath, assetType);

            //Draw the item and represent the desired file
            GUILayout.Space(2);
            EditorGUILayout.BeginHorizontal("box");
            if (assetType == null)
            {
                assetType = typeof(object);
            }
            if (assetType != null)
            {
                if (assetType != typeof(Texture) && assetType != typeof(Texture2D))
                {
                    GUILayout.Box("R", GUILayout.Width(28), GUILayout.Height(28));
                }
                if (assetType == typeof(Texture) || assetType == typeof(Texture2D))
                {
                    GUIStyle estiloIcone = new GUIStyle();
                    estiloIcone.border = new RectOffset(0, 0, 0, 0);
                    estiloIcone.margin = new RectOffset(4, 0, 4, 0);
                    GUILayout.Box((Texture)asset, estiloIcone, GUILayout.Width(28), GUILayout.Height(28));
                }
            }
            EditorGUILayout.BeginVertical();
            if (asset == null)
                EditorGUILayout.LabelField("Resource Not Found", EditorStyles.boldLabel);
            if (asset != null)
                EditorGUILayout.LabelField(asset.name, EditorStyles.boldLabel);
            GUILayout.Space(-3);
            if (assetType == typeof(Mesh))
                EditorGUILayout.LabelField("Mesh/" + Path.GetExtension(filePath));
            if (assetType == typeof(Texture) || assetType == typeof(Texture2D))
                EditorGUILayout.LabelField("Texture/" + Path.GetExtension(filePath));
            if (assetType == typeof(Material))
                EditorGUILayout.LabelField("Material/" + Path.GetExtension(filePath));
            if (assetType == typeof(object))
                EditorGUILayout.LabelField("Unknow/???");
            EditorGUILayout.EndVertical();
            GUILayout.Space(20);
            EditorGUILayout.BeginVertical();
            GUILayout.Space(8);
            if (GUILayout.Button("Resource", GUILayout.Height(20)))
            {
                EditorGUIUtility.PingObject(asset);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }
        #endregion
#endif

        void Awake()
        {
            //Validate all variables on awake
            ValidateAllVariables();

            //If is desired, combine meshes at awake
            if (combineOnStart == CombineOnStart.OnAwake)
            {
                if (Application.isPlaying == true)
                    CombineMeshes();
                if (convertCombinedMeshToStaticOnStart == true && Application.isPlaying == true)
                    ConvertCombinedMeshToStaticMesh();
            }
        }

        void Start()
        {
            //If is desired, combine meshes at start
            if (combineOnStart == CombineOnStart.OnStart)
            {
                if (Application.isPlaying == true)
                    CombineMeshes();
                if (convertCombinedMeshToStaticOnStart == true && Application.isPlaying == true)
                    ConvertCombinedMeshToStaticMesh();
            }
        }

        //Public methods

        public bool isMeshesCombined()
        {
            //Return if the meshes is combined
            if (resultMergeGameObject != null)
            {
                return true;
            }
            return false;
        }

        public void CombineMeshes()
        {
            //Combine meshes
            switch (mergeMethod)
            {
                case MergeMethod.OneMeshPerMaterial:
                    DoCombineMeshs_OneMeshPerMaterial();
                    break;
                case MergeMethod.AllInOne:
                    DoCombineMeshs_AllInOne();
                    break;
                case MergeMethod.JustMaterialColors:
                    DoCombineMeshs_JustMaterialColors();
                    break;
                case MergeMethod.OnlyAnima2dMeshes:
                    DoCombineMeshs_OnlyAnima2dMeshs();
                    break;
            }
        }

        public void UndoCombineMeshes(bool runUnityGc, bool runMonoIl2CppGc)
        {
            //If the mesh result of merge, is converted to static mesh, cancel the process
            if (isCombinedMeshesConvertedToStatic() == true)
            {
                Debug.LogError("Before undoing the meshes merge, please undo the conversion to static mesh by calling the \"UndoConvertCombinedMeshToStaticMesh()\" method.");
                return;
            }

            //Do Undo Combine Meshes
            DoUndoCombineMeshs(runMonoIl2CppGc, runUnityGc, false);
        }

        public SkinnedMeshRenderer GetCombinedMeshSkinnedMeshRenderer()
        {
            //If the meshes is not merged, merge then automatically
            if (isMeshesCombined() == false)
                CombineMeshes();

            //Return all renderer of combined mesh
            return resultMergeGameObject.GetComponent<SkinnedMeshRenderer>();
        }

        public bool isCombinedMeshesConvertedToStatic()
        {
            //If the meshes are not merged
            if (resultMergeGameObject == null)
                return false;

            //Get necessary components
            CombinedMeshesManager meshesManager = resultMergeGameObject.GetComponent<CombinedMeshesManager>();

            //Return the response
            if (meshesManager.resultOfConversionToStaticMesh == null)
                return false;
            if (meshesManager.resultOfConversionToStaticMesh != null)
                return true;
            return false;
        }

        public GameObject ConvertCombinedMeshToStaticMesh()
        {
            //If the meshes are not merged, cancel
            if (resultMergeGameObject == null)
            {
                Debug.LogWarning("Could not convert the combined mesh from \"" + this.gameObject.name + "\" to static. It is necessary that the meshes of this model are merged by the Skinned Mesh Combiner, before converting the combined mesh to a static mesh. Please combine the stitches first.");
                return null;
            }

            //Get necessary components
            SkinnedMeshRenderer meshRender = GetCombinedMeshSkinnedMeshRenderer();
            CombinedMeshesManager meshesManager = meshRender.gameObject.GetComponent<CombinedMeshesManager>();

            //If the mesh already converted to static
            if (meshesManager.resultOfConversionToStaticMesh != null)
            {
                Debug.LogWarning("The combined mesh has already been converted to a static mesh.");
                return meshesManager.resultOfConversionToStaticMesh;
            }

            //If is a Anima2D merge, not execute the conversion
            if (meshesManager.mergeMethodUsed == CombinedMeshesManager.MergeMethod.OnlyAnima2dMeshes)
            {
                Debug.LogWarning("It is not possible to convert a mesh to static, mesh that was combined using the \"Only Anima2D Meshes\" method.");
                return null;
            }

            //Create the new bakes mesh storage
            Mesh bakedMesh = new Mesh();
            bakedMesh.name = "Static Mesh";
            bakedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            //Bake the mesh
            meshRender.BakeMesh(bakedMesh);

            //Create the holder GameObject
            GameObject holderGameObject = new GameObject("Static Mesh");
            holderGameObject.transform.SetParent(meshRender.gameObject.transform);
            meshesManager.resultOfConversionToStaticMesh = holderGameObject;

            //Add the components
            MeshRenderer meshRenderer = holderGameObject.AddComponent<MeshRenderer>();
            MeshFilter meshFilter = holderGameObject.AddComponent<MeshFilter>();

            //Fill the components
            meshFilter.sharedMesh = bakedMesh;
            meshRenderer.sharedMaterials = meshRender.sharedMaterials;

            //Disable the original mesh renderer
            meshRender.enabled = false;

            //Return
            return holderGameObject;
        }

        public void UndoConvertCombinedMeshToStaticMesh()
        {
            //If the meshes are not merged, cancel
            if (resultMergeGameObject == null)
            {
                Debug.LogWarning("It is not possible to undo the conversion to static mesh, because the meshes of the \"" + this.gameObject.name + "\" model have not yet been combined.");
                return;
            }

            //Get necessary components
            SkinnedMeshRenderer meshRender = GetCombinedMeshSkinnedMeshRenderer();
            CombinedMeshesManager meshesManager = meshRender.gameObject.GetComponent<CombinedMeshesManager>();

            //If the mesh is not converted to static
            if (meshesManager.resultOfConversionToStaticMesh == null)
            {
                Debug.LogWarning("It is not possible to undo the conversion to static mesh, as no conversion to static mesh has been done yet.");
                return;
            }

            //Destroy the static mesh
            Destroy(meshesManager.resultOfConversionToStaticMesh);

            //Eanble the original renderer
            meshRender.enabled = true;
        }

        public GameObject GetCombinedMeshConvertedToStatic()
        {
            //Return the result of combined mesh converted to static
            return resultMergeGameObject.GetComponent<CombinedMeshesManager>().resultOfConversionToStaticMesh;
        }

        //Core methods

        private void DoCombineMeshs_OneMeshPerMaterial()
        {
            //Verify if the meshes are already merged
            if (resultMergeGameObject != null)
            {
                LaunchLog("Currently, this character's meshes are already merged. Please, before making a new merge, undo the merge previously done.", LogTypeOf.Warning);
                return;
            }

            //Reset position, rotation and scale and store it (to avoid problems with matrix or blendshapes positioning for example)
            if (autoManagePosition == true)
            {
                thisOriginalPosition = this.gameObject.transform.position;
                thisOriginalRotation = this.gameObject.transform.eulerAngles;
                thisOriginalScale = this.gameObject.transform.localScale;
                this.gameObject.transform.position = Vector3.zero;
                this.gameObject.transform.eulerAngles = Vector3.zero;
                this.gameObject.transform.localScale = Vector3.one;
            }

            //Try to merge. If occurs error, stop merge
            try
            {
                //Clear progress bar
                ShowProgressBar("Merging...", true, 1.0f);

                //Clear Logs Of Merge
                resultMergeLogs.Clear();

                //Validate all variables
                ValidateAllVariables();

                //Change the animator to always animate, and store the original value (if legacy Animation Support is disabled)
                Animator thisAnimator = null;
                AnimatorCullingMode originalCullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (legacyAnimationSupport == false)
                {
                    thisAnimator = GetComponent<Animator>();
                    originalCullingMode = thisAnimator.cullingMode;
                    thisAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                //Change the legacy animation to always animate, and store the original value (if legacy animation support is enabled)
                Animation thisLegacyAnimation = null;
                AnimationCullingType originalLegacyCullingType = AnimationCullingType.AlwaysAnimate;
                if (legacyAnimationSupport == true)
                {
                    thisLegacyAnimation = GetComponent<Animation>();
                    originalLegacyCullingType = thisLegacyAnimation.cullingType;
                    thisLegacyAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
                }

                //Start of Stats
                DateTime timeOfStart = DateTime.Now;
                int verticesCount = 0;
                int mergedMeshes = 0;
                int drawCallReduction = 0;
                int meshesBefore = 0;
                int materialCount = 0;
                int originalUvLenght = 0;

                //Get all GameObjects to merge
                GameObject[] gameObjectsToMerge = GetAllItemsForCombine(false, true);

                //Get all Skinned Mesh Renderers to merge
                SkinnedMeshRenderer[] skinnedMeshesToMerge = GetAllSkinnedMeshsValidatedToCombine(gameObjectsToMerge);

                //Stop the merge if not have meshes to merge
                if (skinnedMeshesToMerge == null || skinnedMeshesToMerge.Length < 1)
                {
                    LaunchLog("The merge has been canceled. There may not be enough meshes to be combined. At least 1 valid mesh is required for the merge process to be possible. Also, there is the possibility that all the meshes found, are invalid or have been ignored during the merge process.", LogTypeOf.Warning);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Verify if exists different root bones
                if (ExistsDifferentRootBones(skinnedMeshesToMerge, true) == true && oneMeshPerMaterialParams.mergeOnlyEqualRootBones == true)
                {
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Count vertices
                verticesCount = CountVerticesInAllMeshes(skinnedMeshesToMerge);

                //------------------------------- START OF MERGE CODE --------------------------------

                //Prepare the storage of submeshes to merge, divided by material
                Dictionary<Material, List<SubMeshToCombine>> submeshesToMerge = new Dictionary<Material, List<SubMeshToCombine>>();

                //Scan and divide each submesh by respective material
                foreach (SkinnedMeshRenderer smr in skinnedMeshesToMerge)
                    for (int i = 0; i < smr.sharedMesh.subMeshCount; i++)
                    {
                        //Store the current sub-mesh or mesh according to your material
                        if (submeshesToMerge.ContainsKey(smr.sharedMaterials[i]) == true)
                            submeshesToMerge[smr.sharedMaterials[i]].Add(new SubMeshToCombine(smr.transform, smr, i));
                        if (submeshesToMerge.ContainsKey(smr.sharedMaterials[i]) == false)
                            submeshesToMerge.Add(smr.sharedMaterials[i], new List<SubMeshToCombine>() { new SubMeshToCombine(smr.transform, smr, i) });
                        materialCount += 1;
                    }

                //List of all submeshes that share same material combined, and your data for each material
                List<SubMeshesCombined> mergedSubmeshesPerMaterial = new List<SubMeshesCombined>();

                //Create a cache skinned mesh renderer to process data for each merged submeshes per material. This render emulate different skinned mesh renderers for each submeshes merged
                GameObject cacheObj = new GameObject("Cache Obj");
                SkinnedMeshRenderer cacheRender = cacheObj.AddComponent<SkinnedMeshRenderer>();

                //Start the merge process
                foreach (var key in submeshesToMerge)
                {
                    //Get all submeshes to combine, for this material
                    List<SubMeshToCombine> submeshesToCombine = key.Value;

                    //Get stats of merged meshes
                    mergedMeshes += submeshesToCombine.Count;
                    drawCallReduction += submeshesToCombine.Count - 1;
                    meshesBefore += 1;

                    //Prepare the storage
                    List<Transform> bonesToMerge = new List<Transform>();
                    List<Matrix4x4> bindPosesToMerge = new List<Matrix4x4>();
                    List<CombineInstance> combinesToMerge = new List<CombineInstance>();

                    //Start the mesh creation
                    foreach (SubMeshToCombine subMesh in submeshesToCombine)
                    {
                        //Add bone to list of bones to merge and set bones bindposes
                        Transform[] currentMeshBones = subMesh.skinnedMeshRenderer.bones;
                        for (int i = 0; i < currentMeshBones.Length; i++)
                        {
                            bonesToMerge.Add(currentMeshBones[i]);
                            if (compatibilityMode == true)
                                bindPosesToMerge.Add(subMesh.skinnedMeshRenderer.sharedMesh.bindposes[i] * subMesh.skinnedMeshRenderer.transform.worldToLocalMatrix);
                            if (compatibilityMode == false)
                                bindPosesToMerge.Add(currentMeshBones[i].worldToLocalMatrix * subMesh.skinnedMeshRenderer.transform.worldToLocalMatrix);
                        }

                        //Configure the Combine Instances for each submesh or mesh
                        CombineInstance combineInstance = new CombineInstance();
                        combineInstance.mesh = subMesh.skinnedMeshRenderer.sharedMesh;
                        combineInstance.subMeshIndex = subMesh.subMeshIndex;
                        combineInstance.transform = subMesh.transform.localToWorldMatrix;
                        combinesToMerge.Add(combineInstance);
                    }

                    //Merge all of this submeshes into one mesh
                    Mesh mergedSubmeshes = new Mesh();
                    if (verticesCount <= MAX_VERTICES_FOR_16BITS_MESH)
                        mergedSubmeshes.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                    if (verticesCount > MAX_VERTICES_FOR_16BITS_MESH)
                        mergedSubmeshes.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mergedSubmeshes.CombineMeshes(combinesToMerge.ToArray(), true, true);

                    //Fill the cache skinned mesh renderer with data, to emulate a new mesh
                    cacheRender.sharedMesh = mergedSubmeshes;
                    cacheRender.sharedMesh.bindposes = bindPosesToMerge.ToArray();
                    cacheRender.bones = bonesToMerge.ToArray();
                    cacheRender.sharedMaterial = key.Key;

                    //Add this submeshes merged, in list, with all your data
                    mergedSubmeshesPerMaterial.Add(new SubMeshesCombined(cacheRender.transform.worldToLocalMatrix, cacheRender.sharedMesh, cacheRender.bones, cacheRender.sharedMesh.bindposes, cacheRender.sharedMaterial));
                }

                //Delete the cache GameObject
                DestroyGameObject(cacheObj);

                //Start the merge of all combined submeshes that share samme material, into one mesh with submeshes
                List<Transform> bonesToMergeFinal = new List<Transform>();
                List<Matrix4x4> bindPosesToMergeFinal = new List<Matrix4x4>();
                List<CombineInstance> combinesToMergeFinal = new List<CombineInstance>();
                List<Material> materialsToMergeFinal = new List<Material>();

                //Collect and sum data of each submesh combined to all lists. Uses the data processed by the cache renderer
                foreach (SubMeshesCombined mesh in mergedSubmeshesPerMaterial)
                {
                    //Insert all combine instances in list to merge in final mesh
                    CombineInstance combineInstanceOfThisSubMesh = new CombineInstance();
                    combineInstanceOfThisSubMesh.mesh = mesh.subMeshesMerged;
                    combineInstanceOfThisSubMesh.subMeshIndex = 0;
                    combineInstanceOfThisSubMesh.transform = mesh.localToWorldMatrix;
                    combinesToMergeFinal.Add(combineInstanceOfThisSubMesh);

                    //Insert all bones in list to merge in final mesh
                    foreach (Transform bone in mesh.bonesToMerge)
                    {
                        bonesToMergeFinal.Add(bone);
                    }

                    //Insert all bind poses in list to merge in final mesh
                    foreach (Matrix4x4 bindPose in mesh.bindPosesToMerge)
                    {
                        bindPosesToMergeFinal.Add(bindPose);
                    }

                    //Insert all materials in list to merge in final mesh
                    materialsToMergeFinal.Add(mesh.thisMaterial);
                }

                //Combine all submeshes into one mesh with submeshes with all materials
                Mesh finalMesh = new Mesh();
                if (verticesCount <= MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                if (verticesCount > MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                finalMesh.name = "Combined Meshes (One Mesh Per Material)";
                finalMesh.CombineMeshes(combinesToMergeFinal.ToArray(), false);

                //Do recalculations where is desired
                finalMesh.RecalculateBounds();

                //Create the holder GameObject
                resultMergeGameObject = new GameObject(nameOfThisMerge);
                resultMergeGameObject.transform.SetParent(this.gameObject.transform);
                SkinnedMeshRenderer smrender = resultMergeGameObject.AddComponent<SkinnedMeshRenderer>();
                smrender.sharedMesh = finalMesh;
                smrender.bones = bonesToMergeFinal.ToArray();
                smrender.sharedMesh.bindposes = bindPosesToMergeFinal.ToArray();
                smrender.sharedMaterials = materialsToMergeFinal.ToArray();
                smrender.rootBone = GetCorrectRootBoneFromAllOriginalSkinnedMeshRenderers(skinnedMeshesToMerge);

                //Process and merge blendshapes of all original skinned mesh renderers, if full support for blendshapes is desired
                if (blendShapesSupport == BlendShapesSupport.FullSupport)
                    MergeAndGetAllBlendShapeDataOfSkinnedMeshRenderers(skinnedMeshesToMerge, finalMesh, smrender);

                //------------------------------- END OF MERGE CODE --------------------------------

                //Add the combined meshes manager to holder GameObject
                CombinedMeshesManager meshesManager = resultMergeGameObject.AddComponent<CombinedMeshesManager>();

                //Fill te combined meshes manager with data of anima2d meshes combined
                meshesManager.rootGameObject = this.gameObject;
                meshesManager.mergeMethodUsed = CombinedMeshesManager.MergeMethod.OneMeshPerMaterial;
                meshesManager.sourceMeshes = skinnedMeshesToMerge;

                //Save the original GameObjects
                resultMergeOriginalGameObjects = gameObjectsToMerge;

                //Disable all original GameObjects that are merged
                foreach (SkinnedMeshRenderer originalRender in skinnedMeshesToMerge)
                {
                    originalRender.gameObject.SetActive(false);
                }

                //Restore original culling mod (if support to legacy animation support is disabled)
                if (legacyAnimationSupport == false)
                    thisAnimator.cullingMode = originalCullingMode;
                //Restore original culling mod (if support to legacy animation support is enabled)
                if (legacyAnimationSupport == true)
                    thisLegacyAnimation.cullingType = originalLegacyCullingType;

                //End of Stats
                DateTime timeOfEnd = DateTime.Now;

                //Save data as asset
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Meshes", smrender.sharedMesh, this.gameObject.name, "asset");

                //Save prefab if is desired
                if (savePrefabOfMerge == true)
                    SaveMergeAsPrefab("Prefabs", nameOfPrefabOfMerge, this.gameObject);

                //Generate the Stats text
                GenerateStatsTextAndFillIt(nameOfThisMerge, "One Mesh Per Material", verticesCount, timeOfStart, timeOfEnd, mergedMeshes, meshesBefore, drawCallReduction, materialCount, originalUvLenght, 0);

                //Clear progress bar
                ShowProgressBar("", false, 1.0f);

                //Show alert e log
                if (Application.isPlaying == false)
                    Debug.Log("The merging of the meshes was completed successfully!");
                LaunchLog("The merging of the meshes was completed successfully! The name of this merge is \"" + nameOfThisMerge + "\"!", LogTypeOf.Log);

                //Call event of done merge
                if (Application.isPlaying == true && onCombineMeshs != null)
                    onCombineMeshs.Invoke();

                //Change to Logs Of Merge tab
                ShowLogsOfMergeTabAndClearUndoHistory();
            }
            //If occurs a error on merge, catch it
            catch (Exception exception)
            {
                StopMergeByErrorWhileMerging(exception);
            }

            //Restore original position, rotation and scale
            if (autoManagePosition == true)
            {
                this.gameObject.transform.position = thisOriginalPosition;
                this.gameObject.transform.eulerAngles = thisOriginalRotation;
                this.gameObject.transform.localScale = thisOriginalScale;
            }
        }

        private void DoCombineMeshs_JustMaterialColors()
        {
            //Verify if the meshes are already merged
            if (resultMergeGameObject != null)
            {
                LaunchLog("Currently, this character's meshes are already merged. Please, before making a new merge, undo the merge previously done.", LogTypeOf.Warning);
                return;
            }

            //Reset position, rotation and scale and store it (to avoid problems with matrix or blendshapes positioning for example)
            if (autoManagePosition == true)
            {
                thisOriginalPosition = this.gameObject.transform.position;
                thisOriginalRotation = this.gameObject.transform.eulerAngles;
                thisOriginalScale = this.gameObject.transform.localScale;
                this.gameObject.transform.position = Vector3.zero;
                this.gameObject.transform.eulerAngles = Vector3.zero;
                this.gameObject.transform.localScale = Vector3.one;
            }

            //Try to merge. If occurs error, stop merge
            try
            {
                //Clear progress bar
                ShowProgressBar("Merging...", true, 1.0f);

                //Clear Logs Of Merge
                resultMergeLogs.Clear();

                //Validate all variables
                ValidateAllVariables();

                //Change the animator to always animate, and store the original value (if legacy Animation Support is disabled)
                Animator thisAnimator = null;
                AnimatorCullingMode originalCullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (legacyAnimationSupport == false)
                {
                    thisAnimator = GetComponent<Animator>();
                    originalCullingMode = thisAnimator.cullingMode;
                    thisAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                //Change the legacy animation to always animate, and store the original value (if legacy animation support is enabled)
                Animation thisLegacyAnimation = null;
                AnimationCullingType originalLegacyCullingType = AnimationCullingType.AlwaysAnimate;
                if (legacyAnimationSupport == true)
                {
                    thisLegacyAnimation = GetComponent<Animation>();
                    originalLegacyCullingType = thisLegacyAnimation.cullingType;
                    thisLegacyAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
                }

                //Start of Stats
                DateTime timeOfStart = DateTime.Now;
                int verticesCount = 0;
                int mergedMeshes = 0;
                int drawCallReduction = 0;
                int meshesBefore = 0;
                int materialCount = 0;
                int originalUvLenght = 0;

                //Get all GameObjects to merge
                GameObject[] gameObjectsToMerge = GetAllItemsForCombine(false, true);

                //Get all Skinned Mesh Renderers to merge
                SkinnedMeshRenderer[] skinnedMeshesToMerge = GetAllSkinnedMeshsValidatedToCombine(gameObjectsToMerge);

                //Verify if is provided a material to use
                if (justMaterialColorsParams.materialToUse == null)
                {
                    LaunchLog("You must provide a material to be used by the mesh resulting from the merge. This material will be a copy of the material you provide, and it will hold the merge atlas for example. Please provide a material for use, so that merge is possible.", LogTypeOf.Error);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Stop the merge if not have meshes to merge
                if (skinnedMeshesToMerge == null || skinnedMeshesToMerge.Length < 1)
                {
                    LaunchLog("The merge has been canceled. There may not be enough meshes to be combined. At least 1 valid mesh is required for the merge process to be possible. Also, there is the possibility that all the meshes found, are invalid or have been ignored during the merge process.", LogTypeOf.Warning);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Verify if exists different root bones
                if (ExistsDifferentRootBones(skinnedMeshesToMerge, true) == true && justMaterialColorsParams.mergeOnlyEqualsRootBones == true)
                {
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Count vertices
                verticesCount = CountVerticesInAllMeshes(skinnedMeshesToMerge);

                //------------------------------- START OF MERGE CODE --------------------------------

                //Prepare the storage
                List<CombineInstance> combinesToMerge = new List<CombineInstance>();
                List<Transform> bonesToMerge = new List<Transform>();
                List<Matrix4x4> bindPosesToMerge = new List<Matrix4x4>();
                List<UvDataAndColorOfThisSubmesh> uvDatasToMerge = new List<UvDataAndColorOfThisSubmesh>();

                //Obtains the data of each merge
                int totalVerticesVerifiedAtHere = 0;
                foreach (SkinnedMeshRenderer meshRender in skinnedMeshesToMerge)
                {
                    //Get the data of each submesh of this mesh
                    for (int i = 0; i < meshRender.sharedMesh.subMeshCount; i++)
                    {
                        //Show progress bar
                        ShowProgressBar("Reading Mesh...", true, 1.0f);

                        //Add bone to list of bones to merge and set bones bindposes
                        Transform[] currentMeshBones = meshRender.bones;
                        for (int x = 0; x < currentMeshBones.Length; x++)
                        {
                            bonesToMerge.Add(currentMeshBones[x]);
                            if (compatibilityMode == true)
                                bindPosesToMerge.Add(meshRender.sharedMesh.bindposes[x] * meshRender.transform.worldToLocalMatrix);
                            if (compatibilityMode == false)
                                bindPosesToMerge.Add(currentMeshBones[x].worldToLocalMatrix * meshRender.transform.worldToLocalMatrix);
                        }

                        //Configure the Combine Instances for each submesh or mesh
                        CombineInstance combineInstance = new CombineInstance();
                        combineInstance.mesh = meshRender.sharedMesh;
                        combineInstance.subMeshIndex = i;
                        combineInstance.transform = meshRender.transform.localToWorldMatrix;
                        combinesToMerge.Add(combineInstance);

                        //Get UV vertices count for this submesh
                        int uvMapSizeOfThisSubMesh = 0;
#if UNITY_2019_3_OR_NEWER
                        //(for Unity 2019.3 or newer)
                        uvMapSizeOfThisSubMesh = combineInstance.mesh.GetSubMesh(combineInstance.subMeshIndex).vertexCount;
#endif
#if !UNITY_2019_3_OR_NEWER
                        //(for Unity 2019.2 or older)
                        uvMapSizeOfThisSubMesh = combineInstance.mesh.SMCGetSubmesh(combineInstance.subMeshIndex).vertexCount;
#endif

                        //Capture and create a storage for all UV data of this submesh
                        UvDataAndColorOfThisSubmesh uvDataOfThisSubmesh = new UvDataAndColorOfThisSubmesh();
                        uvDataOfThisSubmesh.startOfUvVerticesIndex = totalVerticesVerifiedAtHere;
                        uvDataOfThisSubmesh.originalUvVertices = new Vector2[uvMapSizeOfThisSubMesh];
                        uvDataOfThisSubmesh.textureColor = GetTextureFilledWithColorOfMaterial(meshRender.sharedMaterials[i], justMaterialColorsParams.colorPropertyToFind, 64, 64);
                        uvDatasToMerge.Add(uvDataOfThisSubmesh);

                        //Increment stats
                        mergedMeshes += 1;
                        meshesBefore = 1;
                        drawCallReduction = uvDatasToMerge.Count - 1;
                        materialCount += 1;
                        originalUvLenght += uvMapSizeOfThisSubMesh;

                        //Add the total vertices verified
                        totalVerticesVerifiedAtHere += uvMapSizeOfThisSubMesh;
                    }
                }

                //Show progress bar
                ShowProgressBar("Merging...", true, 1.0f);

                //Combine all submeshes into one mesh with submeshes with all materials
                Mesh finalMesh = new Mesh();
                if (verticesCount <= MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                if (verticesCount > MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                finalMesh.name = "Combined Meshes (Just Material Colors)";
                finalMesh.CombineMeshes(combinesToMerge.ToArray(), true, true);

                //Do recalculations where is desired
                finalMesh.RecalculateBounds();

                //Create the holder GameObject
                resultMergeGameObject = new GameObject(nameOfThisMerge);
                resultMergeGameObject.transform.SetParent(this.gameObject.transform);
                SkinnedMeshRenderer smrender = resultMergeGameObject.AddComponent<SkinnedMeshRenderer>();
                smrender.sharedMesh = finalMesh;
                smrender.bones = bonesToMerge.ToArray();
                smrender.sharedMesh.bindposes = bindPosesToMerge.ToArray();
                smrender.rootBone = GetCorrectRootBoneFromAllOriginalSkinnedMeshRenderers(skinnedMeshesToMerge);
                smrender.sharedMaterials = new Material[] { GetValidatedCopyOfMaterial(justMaterialColorsParams.materialToUse, true, true) };
                smrender.sharedMaterials[0].name = "Combined Materials (Just Material Colors)";

                //Process and merge blendshapes of all original skinned mesh renderers, if full support for blendshapes is desired
                if (blendShapesSupport == BlendShapesSupport.FullSupport)
                    MergeAndGetAllBlendShapeDataOfSkinnedMeshRenderers(skinnedMeshesToMerge, finalMesh, smrender);

                //Create all atlas using all collected colors
                ColorAtlasData atlasGenerated = CreateColorAtlas(uvDatasToMerge.ToArray(), 256, 0, true);

                //Show progress bar
                ShowProgressBar("Creating New UV Map...", true, 1.0f);

                //If the UV map of this mesh is inexistent
                if (smrender.sharedMesh.uv.Length == 0)
                {
                    LaunchLog("It was not possible to create a UV map for the combined mesh. Originally, this character's meshes do not have a UV mapping. Create a UV mapping for this character or try using a blending method that does not work with UV mapping, such as One Mesh Per Material.", LogTypeOf.Error);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }
                //If the UV map of this mesh is inexistent
                if (smrender.sharedMesh.uv.Length != smrender.sharedMesh.vertexCount)
                {
                    LaunchLog("It was not possible to create a new UV map for the combianda mesh. The amount of vertices in the original UV map is different from the amount of vertices that the original meshes have, that is, not all vertices of this character are in the UV map. Make sure that all the vertices of this character are on the UV map or try using a blending method that does not work with UV mapping, such as One Mesh Per Material.", LogTypeOf.Error);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Process each submesh UV data and create a new entire UV map for combined mesh
                Vector2[] newUvMapForCombinedMesh = new Vector2[smrender.sharedMesh.uv.Length];
                foreach (UvDataAndColorOfThisSubmesh thisUvData in uvDatasToMerge)
                {
                    //Change all vertex of UV to positive, where vertex position is major than 1 or minor than 0, because the entire UV will resized to fit in your respective texture in atlas
                    for (int i = 0; i < thisUvData.originalUvVertices.Length; i++)
                    {
                        if (thisUvData.originalUvVertices[i].x < 0)
                            thisUvData.originalUvVertices[i].x = thisUvData.originalUvVertices[i].x * -1;
                        if (thisUvData.originalUvVertices[i].y < 0)
                            thisUvData.originalUvVertices[i].y = thisUvData.originalUvVertices[i].y * -1;
                    }

                    //Calculates the highest point of the UV map of each mesh, for know how to reduces to fit in texture atlas, checks which is the largest coordinate found in the list of UV vertices, in X or Y and stores it
                    Vector2 highestVertexCoordinatesForThisSubmesh = Vector2.zero;
                    for (int i = 0; i < thisUvData.originalUvVertices.Length; i++)
                        highestVertexCoordinatesForThisSubmesh = new Vector2(Mathf.Max(thisUvData.originalUvVertices[i].x, highestVertexCoordinatesForThisSubmesh.x), Mathf.Max(thisUvData.originalUvVertices[i].y, highestVertexCoordinatesForThisSubmesh.y));

                    //Calculate the percentage that the edge of textures uses, to center the UV vertices in center of each color
                    Vector2 percentEdgeUsageOfCurrentTexture = new Vector2(0.8f, 0.8f);

                    //Get index of this texture (color) submesh in atlas rects
                    int colorIndexInAtlas = atlasGenerated.GetRectIndexOfThatMainTexture(thisUvData.textureColor);

                    //Verify each vertex of UV map, for respective UV map of this mesh
                    for (int i = 0; i < thisUvData.originalUvVertices.Length; i++)
                    {
                        //Create the vertex
                        Vector2 thisVertex = Vector2.zero;

                        //If the UV map of this mesh is not larger than the texture
                        if (highestVertexCoordinatesForThisSubmesh.x <= 1)
                            thisVertex.x = Mathf.Lerp(atlasGenerated.atlasRects[colorIndexInAtlas].xMin, atlasGenerated.atlasRects[colorIndexInAtlas].xMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.x, 1 - percentEdgeUsageOfCurrentTexture.x, thisUvData.originalUvVertices[i].x));
                        if (highestVertexCoordinatesForThisSubmesh.y <= 1)
                            thisVertex.y = Mathf.Lerp(atlasGenerated.atlasRects[colorIndexInAtlas].yMin, atlasGenerated.atlasRects[colorIndexInAtlas].yMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.y, 1 - percentEdgeUsageOfCurrentTexture.y, thisUvData.originalUvVertices[i].y));

                        //If the UV map is larger than the texture
                        if (highestVertexCoordinatesForThisSubmesh.x > 1)
                            thisVertex.x = Mathf.Lerp(atlasGenerated.atlasRects[colorIndexInAtlas].xMin, atlasGenerated.atlasRects[colorIndexInAtlas].xMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.x, 1 - percentEdgeUsageOfCurrentTexture.x, thisUvData.originalUvVertices[i].x / highestVertexCoordinatesForThisSubmesh.x));
                        if (highestVertexCoordinatesForThisSubmesh.y > 1)
                            thisVertex.y = Mathf.Lerp(atlasGenerated.atlasRects[colorIndexInAtlas].yMin, atlasGenerated.atlasRects[colorIndexInAtlas].yMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.y, 1 - percentEdgeUsageOfCurrentTexture.y, thisUvData.originalUvVertices[i].y / highestVertexCoordinatesForThisSubmesh.y));

                        //Add the created vertex to list of new UV map
                        newUvMapForCombinedMesh[i + thisUvData.startOfUvVerticesIndex] = thisVertex;
                    }
                }

                //Show progress bar
                ShowProgressBar("Finishing...", true, 1.0f);

                //Apply the new UV map merged using modification of all UV vertex of each submesh, apply all atlas too
                smrender.sharedMesh.uv = newUvMapForCombinedMesh;
                ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], justMaterialColorsParams.mainTexturePropertyToInsert, atlasGenerated.colorAtlas);

                //If is desired to hightlight UV vertices
                if (highlightUvVertices == true)
                {
                    for (int i = 0; i < smrender.sharedMesh.uv.Length; i++)
                        atlasGenerated.colorAtlas.SetPixel((int)(atlasGenerated.colorAtlas.width * smrender.sharedMesh.uv[i].x), (int)(atlasGenerated.colorAtlas.height * smrender.sharedMesh.uv[i].y), Color.yellow);
                    atlasGenerated.colorAtlas.Apply();
                }

                //------------------------------- END OF MERGE CODE --------------------------------

                //Add the combined meshes manager to holder GameObject
                CombinedMeshesManager meshesManager = resultMergeGameObject.AddComponent<CombinedMeshesManager>();

                //Fill te combined meshes manager with data of anima2d meshes combined
                meshesManager.rootGameObject = this.gameObject;
                meshesManager.mergeMethodUsed = CombinedMeshesManager.MergeMethod.JustMaterialColors;
                meshesManager.sourceMeshes = skinnedMeshesToMerge;

                //Save the original GameObjects
                resultMergeOriginalGameObjects = gameObjectsToMerge;

                //Disable all original GameObjects that are merged
                foreach (SkinnedMeshRenderer originalRender in skinnedMeshesToMerge)
                {
                    originalRender.gameObject.SetActive(false);
                }

                //Restore original culling mod (if support to legacy animation support is disabled)
                if (legacyAnimationSupport == false)
                    thisAnimator.cullingMode = originalCullingMode;
                //Restore original culling mod (if support to legacy animation support is enabled)
                if (legacyAnimationSupport == true)
                    thisLegacyAnimation.cullingType = originalLegacyCullingType;

                //End of Stats
                DateTime timeOfEnd = DateTime.Now;

                //Save data as asset
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Meshes", smrender.sharedMesh, this.gameObject.name, "asset");
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.colorAtlas, this.gameObject.name + " (ColorAtlas)", "asset");
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Materials", smrender.sharedMaterials[0], this.gameObject.name, "mat");

                //Save prefab if is desired
                if (savePrefabOfMerge == true)
                    SaveMergeAsPrefab("Prefabs", nameOfPrefabOfMerge, this.gameObject);

                //Generate the Stats text
                GenerateStatsTextAndFillIt(nameOfThisMerge, "Just Material Colors", verticesCount, timeOfStart, timeOfEnd, mergedMeshes, meshesBefore, drawCallReduction, materialCount, originalUvLenght, newUvMapForCombinedMesh.Length);

                //Clear progress bar
                ShowProgressBar("", false, 1.0f);

                //Show alert e log
                if (Application.isPlaying == false)
                    Debug.Log("The merging of the meshes was completed successfully!");
                LaunchLog("The merging of the meshes was completed successfully! The name of this merge is \"" + nameOfThisMerge + "\"!", LogTypeOf.Log);

                //Call event of done merge
                if (Application.isPlaying == true && onCombineMeshs != null)
                    onCombineMeshs.Invoke();

                //Change to Logs Of Merge tab
                ShowLogsOfMergeTabAndClearUndoHistory();
            }
            //If occurs a error on merge, catch it
            catch (Exception exception)
            {
                StopMergeByErrorWhileMerging(exception);
            }

            //Restore original position, rotation and scale
            if (autoManagePosition == true)
            {
                this.gameObject.transform.position = thisOriginalPosition;
                this.gameObject.transform.eulerAngles = thisOriginalRotation;
                this.gameObject.transform.localScale = thisOriginalScale;
            }
        }

        private void DoCombineMeshs_AllInOne()
        {
            //Verify if the meshes are already merged
            if (resultMergeGameObject != null)
            {
                LaunchLog("Currently, this character's meshes are already merged. Please, before making a new merge, undo the merge previously done.", LogTypeOf.Warning);
                return;
            }

            //Reset position, rotation and scale and store it (to avoid problems with matrix or blendshapes positioning for example)
            if (autoManagePosition == true)
            {
                thisOriginalPosition = this.gameObject.transform.position;
                thisOriginalRotation = this.gameObject.transform.eulerAngles;
                thisOriginalScale = this.gameObject.transform.localScale;
                this.gameObject.transform.position = Vector3.zero;
                this.gameObject.transform.eulerAngles = Vector3.zero;
                this.gameObject.transform.localScale = Vector3.one;
            }

            //Try to merge. If occurs error, stop merge
            try
            {
                //Show progress bar
                ShowProgressBar("Merging...", true, 1.0f);

                //Clear Logs Of Merge
                resultMergeLogs.Clear();

                //Validate all variables
                ValidateAllVariables();

                //Change the animator to always animate, and store the original value (if legacy Animation Support is disabled)
                Animator thisAnimator = null;
                AnimatorCullingMode originalCullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (legacyAnimationSupport == false)
                {
                    thisAnimator = GetComponent<Animator>();
                    originalCullingMode = thisAnimator.cullingMode;
                    thisAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                //Change the legacy animation to always animate, and store the original value (if legacy animation support is enabled)
                Animation thisLegacyAnimation = null;
                AnimationCullingType originalLegacyCullingType = AnimationCullingType.AlwaysAnimate;
                if (legacyAnimationSupport == true)
                {
                    thisLegacyAnimation = GetComponent<Animation>();
                    originalLegacyCullingType = thisLegacyAnimation.cullingType;
                    thisLegacyAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
                }

                //Start of Stats
                DateTime timeOfStart = DateTime.Now;
                int verticesCount = 0;
                int mergedMeshes = 0;
                int drawCallReduction = 0;
                int meshesBefore = 0;
                int materialCount = 0;
                int originalUvLenght = 0;

                //Get all GameObjects to merge
                GameObject[] gameObjectsToMerge = GetAllItemsForCombine(false, true);

                //Get all Skinned Mesh Renderers to merge
                SkinnedMeshRenderer[] skinnedMeshesToMerge = GetAllSkinnedMeshsValidatedToCombine(gameObjectsToMerge);

                //Verify if is provided a material to use
                if (allInOneParams.materialToUse == null)
                {
                    LaunchLog("You must provide a material to be used by the mesh resulting from the merge. This material will be a copy of the material you provide, and it will hold the merge atlas for example. Please provide a material for use, so that merge is possible.", LogTypeOf.Error);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Stop the merge if not have meshes to merge
                if (skinnedMeshesToMerge == null || skinnedMeshesToMerge.Length < 1)
                {
                    LaunchLog("The merge has been canceled. There may not be enough meshes to be combined. At least 1 valid mesh is required for the merge process to be possible. Also, there is the possibility that all the meshes found, are invalid or have been ignored during the merge process.", LogTypeOf.Warning);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Verify if exists different root bones
                if (ExistsDifferentRootBones(skinnedMeshesToMerge, true) == true && allInOneParams.mergeOnlyEqualsRootBones == true)
                {
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Count vertices
                verticesCount = CountVerticesInAllMeshes(skinnedMeshesToMerge);

                //------------------------------- START OF MERGE CODE --------------------------------

                //Prepare the storage
                List<CombineInstance> combinesToMerge = new List<CombineInstance>();
                List<Transform> bonesToMerge = new List<Transform>();
                List<Matrix4x4> bindPosesToMerge = new List<Matrix4x4>();
                List<TexturesSubMeshes> texturesAndSubMeshes = new List<TexturesSubMeshes>();

                //Prepare the progress bar to read mesh progress (It is used only in editor to show on progress bar)
                int totalSubMeshsInAllSkinnedMeshes = 0;
                foreach (SkinnedMeshRenderer meshRenderer in skinnedMeshesToMerge)
                    totalSubMeshsInAllSkinnedMeshes += meshRenderer.sharedMesh.subMeshCount;
                int totalSkinnedMeshesVerifiedAtHere = 0;

                //Obtains the data of each merge
                int totalVerticesVerifiedAtHere = 0;
                foreach (SkinnedMeshRenderer meshRender in skinnedMeshesToMerge)
                {
                    //Get the data of merge for each submesh of this mesh
                    for (int i = 0; i < meshRender.sharedMesh.subMeshCount; i++)
                    {
                        //Show progress bar
                        float progressOfThisMeshRead = ((float)totalSkinnedMeshesVerifiedAtHere) / ((float)totalSubMeshsInAllSkinnedMeshes + 1);
                        ShowProgressBar("Reading Mesh...", true, progressOfThisMeshRead);

                        //Add bone to list of bones to merge and set bones bindposes
                        Transform[] currentMeshBones = meshRender.bones;
                        for (int x = 0; x < currentMeshBones.Length; x++)
                        {
                            bonesToMerge.Add(currentMeshBones[x]);
                            if (compatibilityMode == true)
                                bindPosesToMerge.Add(meshRender.sharedMesh.bindposes[x] * meshRender.transform.worldToLocalMatrix);
                            if (compatibilityMode == false)
                                bindPosesToMerge.Add(currentMeshBones[x].worldToLocalMatrix * meshRender.transform.worldToLocalMatrix);
                        }

                        //Configure the Combine Instances for each submesh or mesh
                        CombineInstance combineInstance = new CombineInstance();
                        combineInstance.mesh = meshRender.sharedMesh;
                        combineInstance.subMeshIndex = i;
                        combineInstance.transform = meshRender.transform.localToWorldMatrix;
                        combinesToMerge.Add(combineInstance);

                        //Get the entire UV map of this submesh
                        Vector2[] uvMapOfThisSubMesh = combineInstance.mesh.SMCGetSubmesh(i).uv;

                        //Check if UV of this mesh uses a tiled texture (first, get the bounds values of UV of this mesh)
                        TexturesSubMeshes.UvBounds boundDataOfUv = GetBoundValuesOfSubMeshUv(uvMapOfThisSubMesh);
                        //If merge of tiled meshs is legacy, force all textures to be a normal textures, to rest of merge run as normal textures
                        if (allInOneParams.mergeTiledTextures == MergeTiledTextures.LegacyMode)
                        {
                            boundDataOfUv.majorX = 1.0f;
                            boundDataOfUv.majorY = 1.0f;
                            boundDataOfUv.minorX = 0.0f;
                            boundDataOfUv.minorY = 0.0f;
                        }
                        boundDataOfUv.RoundBoundsValuesAndCalculateSpaceNeededToTiling(); //<- This is necessary to avoid calcs problemns with float precision of Unity

                        //If UV of this mesh, use a tiled texture, create another item to storage the data for only this submesh
                        if (isTiledTexture(boundDataOfUv) == true)
                        {
                            //Create another texture and respective submeshes to store it
                            TexturesSubMeshes thisTextureAndSubMesh = new TexturesSubMeshes();

                            //Calculate and get original resolution of main texture of this material
                            Texture2D mainTextureOfThisMaterial = (Texture2D)meshRender.sharedMaterials[i].GetTexture(allInOneParams.mainTexturePropertyToFind);
                            Vector2Int mainTextureSize = Vector2Int.zero;
                            Vector2Int mainTextureSizeWithEdges = Vector2Int.zero;
                            if (mainTextureOfThisMaterial == null)
                                mainTextureSize = new Vector2Int(64, 64);
                            if (mainTextureOfThisMaterial != null)
                                mainTextureSize = new Vector2Int(mainTextureOfThisMaterial.width, mainTextureOfThisMaterial.height);
                            mainTextureSizeWithEdges = new Vector2Int(mainTextureSize.x + (GetEdgesSizeForTextures() * 2), mainTextureSize.y + (GetEdgesSizeForTextures() * 2));

                            //Fill this class
                            thisTextureAndSubMesh.material = meshRender.sharedMaterials[i];
                            thisTextureAndSubMesh.isTiledTexture = true;
                            thisTextureAndSubMesh.mainTextureResolution = mainTextureSize;
                            thisTextureAndSubMesh.mainTextureResolutionWithEdges = mainTextureSizeWithEdges;
                            thisTextureAndSubMesh.mainTexture = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.mainTexturePropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MainTexture, true, progressOfThisMeshRead);
                            if (allInOneParams.metallicMapSupport == true)
                                thisTextureAndSubMesh.metallicMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.metallicMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MetallicMap, true, progressOfThisMeshRead);
                            if (allInOneParams.specularMapSupport == true)
                                thisTextureAndSubMesh.specularMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.specularMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.SpecularMap, true, progressOfThisMeshRead);
                            if (allInOneParams.normalMapSupport == true)
                                thisTextureAndSubMesh.normalMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.normalMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                            if (allInOneParams.normalMap2Support == true)
                                thisTextureAndSubMesh.normalMap2 = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.normalMap2PropertyFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                            if (allInOneParams.heightMapSupport == true)
                                thisTextureAndSubMesh.heightMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.heightMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.HeightMap, true, progressOfThisMeshRead);
                            if (allInOneParams.occlusionMapSupport == true)
                                thisTextureAndSubMesh.occlusionMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.occlusionMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.OcclusionMap, true, progressOfThisMeshRead);
                            if (allInOneParams.detailAlbedoMapSupport == true)
                                thisTextureAndSubMesh.detailMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.detailMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMap, true, progressOfThisMeshRead);
                            if (allInOneParams.detailMaskSupport == true)
                                thisTextureAndSubMesh.detailMask = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.detailMaskPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMask, true, progressOfThisMeshRead);

                            //Create this mesh data. get all UV values from this submesh
                            TexturesSubMeshes.UserSubMeshes userSubMesh = new TexturesSubMeshes.UserSubMeshes();
                            userSubMesh.uvBoundsOfThisSubMesh = boundDataOfUv;
                            userSubMesh.startOfUvVerticesInIndex = totalVerticesVerifiedAtHere;
                            userSubMesh.originalUvVertices = new Vector2[uvMapOfThisSubMesh.Length];
                            for (int v = 0; v < userSubMesh.originalUvVertices.Length; v++)
                                userSubMesh.originalUvVertices[v] = uvMapOfThisSubMesh[v];
                            thisTextureAndSubMesh.userSubMeshes.Add(userSubMesh);

                            //Save the created class
                            texturesAndSubMeshes.Add(thisTextureAndSubMesh);
                        }

                        //If UV of this mesh, use a normal texture
                        if (isTiledTexture(boundDataOfUv) == false)
                        {
                            //Try to find a texture and respective submeshes that already is created that is using this texture
                            TexturesSubMeshes textureOfThisSubMesh = GetTheTextureSubMeshesOfMaterial(meshRender.sharedMaterials[i], texturesAndSubMeshes);

                            //If not found
                            if (textureOfThisSubMesh == null)
                            {
                                //Create another texture and respective submeshes to store it
                                TexturesSubMeshes thisTextureAndSubMesh = new TexturesSubMeshes();

                                //Calculate and get original resolution of main texture of this material
                                Texture2D mainTextureOfThisMaterial = (Texture2D)meshRender.sharedMaterials[i].GetTexture(allInOneParams.mainTexturePropertyToFind);
                                Vector2Int mainTextureSize = Vector2Int.zero;
                                Vector2Int mainTextureSizeWithEdges = Vector2Int.zero;
                                if (mainTextureOfThisMaterial == null)
                                    mainTextureSize = new Vector2Int(64, 64);
                                if (mainTextureOfThisMaterial != null)
                                    mainTextureSize = new Vector2Int(mainTextureOfThisMaterial.width, mainTextureOfThisMaterial.height);
                                mainTextureSizeWithEdges = new Vector2Int(mainTextureSize.x + (GetEdgesSizeForTextures() * 2), mainTextureSize.y + (GetEdgesSizeForTextures() * 2));

                                //Fill this class
                                thisTextureAndSubMesh.material = meshRender.sharedMaterials[i];
                                thisTextureAndSubMesh.isTiledTexture = false;
                                thisTextureAndSubMesh.mainTextureResolution = mainTextureSize;
                                thisTextureAndSubMesh.mainTextureResolutionWithEdges = mainTextureSizeWithEdges;
                                thisTextureAndSubMesh.mainTexture = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.mainTexturePropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MainTexture, true, progressOfThisMeshRead);
                                if (allInOneParams.metallicMapSupport == true)
                                    thisTextureAndSubMesh.metallicMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.metallicMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.MetallicMap, true, progressOfThisMeshRead);
                                if (allInOneParams.specularMapSupport == true)
                                    thisTextureAndSubMesh.specularMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.specularMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.SpecularMap, true, progressOfThisMeshRead);
                                if (allInOneParams.normalMapSupport == true)
                                    thisTextureAndSubMesh.normalMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.normalMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                                if (allInOneParams.normalMap2Support == true)
                                    thisTextureAndSubMesh.normalMap2 = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.normalMap2PropertyFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.NormalMap, true, progressOfThisMeshRead);
                                if (allInOneParams.heightMapSupport == true)
                                    thisTextureAndSubMesh.heightMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.heightMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.HeightMap, true, progressOfThisMeshRead);
                                if (allInOneParams.occlusionMapSupport == true)
                                    thisTextureAndSubMesh.occlusionMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.occlusionMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.OcclusionMap, true, progressOfThisMeshRead);
                                if (allInOneParams.detailAlbedoMapSupport == true)
                                    thisTextureAndSubMesh.detailMap = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.detailMapPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMap, true, progressOfThisMeshRead);
                                if (allInOneParams.detailMaskSupport == true)
                                    thisTextureAndSubMesh.detailMask = GetValidatedCopyOfTexture(thisTextureAndSubMesh.material, allInOneParams.detailMaskPropertyToFind, thisTextureAndSubMesh.mainTextureResolutionWithEdges.x, thisTextureAndSubMesh.mainTextureResolutionWithEdges.y, boundDataOfUv, TextureType.DetailMask, true, progressOfThisMeshRead);

                                //Create this mesh data. get all UV values from this submesh
                                TexturesSubMeshes.UserSubMeshes userSubMesh = new TexturesSubMeshes.UserSubMeshes();
                                userSubMesh.uvBoundsOfThisSubMesh = boundDataOfUv;
                                userSubMesh.startOfUvVerticesInIndex = totalVerticesVerifiedAtHere;
                                userSubMesh.originalUvVertices = new Vector2[uvMapOfThisSubMesh.Length];
                                for (int v = 0; v < userSubMesh.originalUvVertices.Length; v++)
                                    userSubMesh.originalUvVertices[v] = uvMapOfThisSubMesh[v];
                                thisTextureAndSubMesh.userSubMeshes.Add(userSubMesh);

                                //Save the created class
                                texturesAndSubMeshes.Add(thisTextureAndSubMesh);
                            }

                            //If found
                            if (textureOfThisSubMesh != null)
                            {
                                //Create this mesh data and add to textures that already exists. get all UV values from this submesh
                                TexturesSubMeshes.UserSubMeshes userSubMesh = new TexturesSubMeshes.UserSubMeshes();
                                userSubMesh.uvBoundsOfThisSubMesh = boundDataOfUv;
                                userSubMesh.startOfUvVerticesInIndex = totalVerticesVerifiedAtHere;
                                userSubMesh.originalUvVertices = new Vector2[uvMapOfThisSubMesh.Length];
                                for (int v = 0; v < userSubMesh.originalUvVertices.Length; v++)
                                    userSubMesh.originalUvVertices[v] = uvMapOfThisSubMesh[v];
                                textureOfThisSubMesh.userSubMeshes.Add(userSubMesh);
                            }
                        }

                        //Increment stats
                        mergedMeshes += 1;
                        meshesBefore = 1;
                        drawCallReduction += 1;
                        materialCount = texturesAndSubMeshes.Count;
                        originalUvLenght += uvMapOfThisSubMesh.Length;

                        //Add the total vertices verified
                        totalVerticesVerifiedAtHere += uvMapOfThisSubMesh.Length;

                        //Update the value of progress bar of readed meshes
                        totalSkinnedMeshesVerifiedAtHere += 1;
                    }
                }

                //Show progress bar
                ShowProgressBar("Merging...", true, 1.0f);

                //Combine all submeshes into one mesh with submeshes with all materials
                Mesh finalMesh = new Mesh();
                if (verticesCount <= MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                if (verticesCount > MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                finalMesh.name = "Combined Meshes (All In One)";
                finalMesh.CombineMeshes(combinesToMerge.ToArray(), true, true);

                //Do recalculations where is desired
                finalMesh.RecalculateBounds();

                //Create the holder GameObject
                resultMergeGameObject = new GameObject(nameOfThisMerge);
                resultMergeGameObject.transform.SetParent(this.gameObject.transform);
                SkinnedMeshRenderer smrender = resultMergeGameObject.AddComponent<SkinnedMeshRenderer>();
                smrender.sharedMesh = finalMesh;
                smrender.bones = bonesToMerge.ToArray();
                smrender.sharedMesh.bindposes = bindPosesToMerge.ToArray();
                smrender.rootBone = GetCorrectRootBoneFromAllOriginalSkinnedMeshRenderers(skinnedMeshesToMerge);
                smrender.sharedMaterials = new Material[] { GetValidatedCopyOfMaterial(allInOneParams.materialToUse, true, true) };
                smrender.sharedMaterials[0].name = "Combined Materials (All In One)";

                //Process and merge blendshapes of all original skinned mesh renderers, if full support for blendshapes is desired
                if (blendShapesSupport == BlendShapesSupport.FullSupport)
                    MergeAndGetAllBlendShapeDataOfSkinnedMeshRenderers(skinnedMeshesToMerge, finalMesh, smrender);

                //Create all atlas using all collected textures
                AtlasData atlasGenerated = CreateAllAtlas(texturesAndSubMeshes, GetAtlasMaxResolution(), GetAtlasPadding(), true);

                //Show progress bar
                ShowProgressBar("Creating New UV Map...", true, 1.0f);

                //If the UV map of this mesh is inexistent
                if (smrender.sharedMesh.uv.Length == 0)
                {
                    LaunchLog("It was not possible to create a UV map for the combined mesh. Originally, this character's meshes do not have a UV mapping. Create a UV mapping for this character or try using a blending method that does not work with UV mapping, such as One Mesh Per Material.", LogTypeOf.Error);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Process each submesh UV data and create a new entire UV map for combined mesh
                Vector2[] newUvMapForCombinedMesh = new Vector2[smrender.sharedMesh.uv.Length];
                foreach (TexturesSubMeshes thisTexture in texturesAndSubMeshes)
                {
                    //Convert all vertices of all submeshes of this texture, to positive, if is a tiled texture
                    if (thisTexture.isTiledTexture == true)
                        thisTexture.ConvertAllSubMeshsVerticesToPositive();

                    //Process each submesh registered as user of this texture
                    foreach (TexturesSubMeshes.UserSubMeshes submesh in thisTexture.userSubMeshes)
                    {
                        //If this is a normal texture, not is a tiled texture (merge with the basic UV mapping algorthm)
                        if (thisTexture.isTiledTexture == false)
                        {
                            //Change all vertex of UV to positive, where vertex position is major than 1 or minor than 0, because the entire UV will resized to fit in your respective texture in atlas
                            for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                            {
                                if (submesh.originalUvVertices[i].x < 0)
                                    submesh.originalUvVertices[i].x = submesh.originalUvVertices[i].x * -1;
                                if (submesh.originalUvVertices[i].y < 0)
                                    submesh.originalUvVertices[i].y = submesh.originalUvVertices[i].y * -1;
                            }

                            //Calculates the highest point of the UV map of each mesh, for know how to reduces to fit in texture atlas, checks which is the largest coordinate found in the list of UV vertices, in X or Y and stores it
                            Vector2 highestVertexCoordinatesForThisSubmesh = Vector2.zero;
                            for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                                highestVertexCoordinatesForThisSubmesh = new Vector2(Mathf.Max(submesh.originalUvVertices[i].x, highestVertexCoordinatesForThisSubmesh.x), Mathf.Max(submesh.originalUvVertices[i].y, highestVertexCoordinatesForThisSubmesh.y));

                            //Calculate the percentage that the edge of this texture uses, calculates the size of the uv for each texture, to ignore the edges
                            Vector2 percentEdgeUsageOfCurrentTexture = thisTexture.GetEdgesPercentUsageOfThisTextures();

                            //Get index of this main texture submesh in atlas rects
                            int mainTextureIndexInAtlas = atlasGenerated.GetRectIndexOfThatMainTexture(thisTexture.mainTexture);

                            //Process all uv vertices of this submesh
                            for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                            {
                                //Create the vertice
                                Vector2 thisVertex = Vector2.zero;

                                //If the UV map of this mesh is not larger than the texture
                                if (highestVertexCoordinatesForThisSubmesh.x <= 1)
                                    thisVertex.x = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.x, 1 - percentEdgeUsageOfCurrentTexture.x, submesh.originalUvVertices[i].x));
                                if (highestVertexCoordinatesForThisSubmesh.y <= 1)
                                    thisVertex.y = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.y, 1 - percentEdgeUsageOfCurrentTexture.y, submesh.originalUvVertices[i].y));

                                //If the UV map is larger than the texture
                                if (highestVertexCoordinatesForThisSubmesh.x > 1)
                                    thisVertex.x = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.x, 1 - percentEdgeUsageOfCurrentTexture.x, submesh.originalUvVertices[i].x / highestVertexCoordinatesForThisSubmesh.x));
                                if (highestVertexCoordinatesForThisSubmesh.y > 1)
                                    thisVertex.y = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.y, 1 - percentEdgeUsageOfCurrentTexture.y, submesh.originalUvVertices[i].y / highestVertexCoordinatesForThisSubmesh.y));

                                //Save this vertice edited in uv map of combined mesh
                                newUvMapForCombinedMesh[i + submesh.startOfUvVerticesInIndex] = thisVertex;
                            }
                        }

                        //If this is a tiled texture, not is a normal texture
                        if (thisTexture.isTiledTexture == true)
                        {
                            //Calculates the highest point of the UV map of each mesh, for know how to reduces to fit in texture atlas, checks which is the largest coordinate found in the list of UV vertices, in X or Y and stores it
                            Vector2 highestVertexCoordinatesForThisSubmesh = Vector2.zero;
                            for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                                highestVertexCoordinatesForThisSubmesh = new Vector2(Mathf.Max(submesh.originalUvVertices[i].x, highestVertexCoordinatesForThisSubmesh.x), Mathf.Max(submesh.originalUvVertices[i].y, highestVertexCoordinatesForThisSubmesh.y));

                            //Calculate the percentage that the edge of this texture uses, calculates the size of the uv for each texture, to ignore the edges
                            Vector2 percentEdgeUsageOfCurrentTexture = thisTexture.GetEdgesPercentUsageOfThisTextures();

                            //Get index of this main texture submesh in atlas rects
                            int mainTextureIndexInAtlas = atlasGenerated.GetRectIndexOfThatMainTexture(thisTexture.mainTexture);

                            //Process all uv vertices of this submesh
                            for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                            {
                                //Create the vertice
                                Vector2 thisVertex = Vector2.zero;

                                //If the UV map is larger than the texture
                                thisVertex.x = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].xMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.x, 1 - percentEdgeUsageOfCurrentTexture.x, submesh.originalUvVertices[i].x / highestVertexCoordinatesForThisSubmesh.x));
                                thisVertex.y = Mathf.Lerp(atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMin, atlasGenerated.atlasRects[mainTextureIndexInAtlas].yMax, Mathf.Lerp(percentEdgeUsageOfCurrentTexture.y, 1 - percentEdgeUsageOfCurrentTexture.y, submesh.originalUvVertices[i].y / highestVertexCoordinatesForThisSubmesh.y));

                                //Save this vertice edited in uv map of combined mesh
                                newUvMapForCombinedMesh[i + submesh.startOfUvVerticesInIndex] = thisVertex;
                            }
                        }
                    }
                }

                //Show progress bar
                ShowProgressBar("Finishing and Saving Assets...", true, 1.0f);

                //Apply the new UV map merged using modification of all UV vertex of each submesh
                smrender.sharedMesh.uv = newUvMapForCombinedMesh;

                //Apply all atlas too
                ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.mainTexturePropertyToInsert, atlasGenerated.mainTextureAtlas);
                if (allInOneParams.metallicMapSupport == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.metallicMapPropertyToInsert, atlasGenerated.metallicMapAtlas);
                if (allInOneParams.specularMapSupport == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.specularMapPropertyToInsert, atlasGenerated.specularMapAtlas);
                if (allInOneParams.normalMapSupport == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.normalMapPropertyToInsert, atlasGenerated.normalMapAtlas);
                if (allInOneParams.normalMap2Support == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.normalMap2PropertyToInsert, atlasGenerated.normalMap2Atlas);
                if (allInOneParams.heightMapSupport == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.heightMapPropertyToInsert, atlasGenerated.heightMapAtlas);
                if (allInOneParams.occlusionMapSupport == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.occlusionMapPropertyToInsert, atlasGenerated.occlusionMapAtlas);
                if (allInOneParams.detailAlbedoMapSupport == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.detailMapPropertyToInsert, atlasGenerated.detailMapAtlas);
                if (allInOneParams.detailMaskSupport == true)
                    ApplyAtlasInPropertyOfMaterial(smrender.sharedMaterials[0], allInOneParams.detailMaskPropertyToInsert, atlasGenerated.detailMaskAtlas);

                //If is desired to hightlight UV vertices
                if (highlightUvVertices == true)
                {
                    for (int i = 0; i < smrender.sharedMesh.uv.Length; i++)
                        atlasGenerated.mainTextureAtlas.SetPixel((int)(atlasGenerated.mainTextureAtlas.width * smrender.sharedMesh.uv[i].x), (int)(atlasGenerated.mainTextureAtlas.height * smrender.sharedMesh.uv[i].y), Color.yellow);
                    atlasGenerated.mainTextureAtlas.Apply();
                }

                //------------------------------- END OF MERGE CODE --------------------------------

                //Add the combined meshes manager to holder GameObject
                CombinedMeshesManager meshesManager = resultMergeGameObject.AddComponent<CombinedMeshesManager>();

                //Fill te combined meshes manager with data of anima2d meshes combined
                meshesManager.rootGameObject = this.gameObject;
                meshesManager.mergeMethodUsed = CombinedMeshesManager.MergeMethod.AllInOne;
                meshesManager.sourceMeshes = skinnedMeshesToMerge;

                //Save the original GameObjects
                resultMergeOriginalGameObjects = gameObjectsToMerge;

                //Disable all original GameObjects that are merged
                foreach (SkinnedMeshRenderer originalRender in skinnedMeshesToMerge)
                {
                    originalRender.gameObject.SetActive(false);
                }

                //Restore original culling mod (if support to legacy animation support is disabled)
                if (legacyAnimationSupport == false)
                    thisAnimator.cullingMode = originalCullingMode;
                //Restore original culling mod (if support to legacy animation support is enabled)
                if (legacyAnimationSupport == true)
                    thisLegacyAnimation.cullingType = originalLegacyCullingType;

                //End of Stats
                DateTime timeOfEnd = DateTime.Now;

                //Save data as asset
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Meshes", smrender.sharedMesh, this.gameObject.name, "asset");
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.mainTextureAtlas, this.gameObject.name + " (MainTexture)", "asset");
                if (saveDataInAssets == true && allInOneParams.metallicMapSupport == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.metallicMapAtlas, this.gameObject.name + " (MetallicMap)", "asset");
                if (saveDataInAssets == true && allInOneParams.specularMapSupport == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.specularMapAtlas, this.gameObject.name + " (SpecularMap)", "asset");
                if (saveDataInAssets == true && allInOneParams.normalMapSupport == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.normalMapAtlas, this.gameObject.name + " (NormalMap)", "asset");
                if (saveDataInAssets == true && allInOneParams.normalMap2Support == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.normalMap2Atlas, this.gameObject.name + " (NormalMap2x)", "asset");
                if (saveDataInAssets == true && allInOneParams.heightMapSupport == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.heightMapAtlas, this.gameObject.name + " (HeightMap)", "asset");
                if (saveDataInAssets == true && allInOneParams.occlusionMapSupport == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.occlusionMapAtlas, this.gameObject.name + " (OcclusionMap)", "asset");
                if (saveDataInAssets == true && allInOneParams.detailAlbedoMapSupport == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.detailMapAtlas, this.gameObject.name + " (DetailMap)", "asset");
                if (saveDataInAssets == true && allInOneParams.detailMaskSupport == true)
                    SaveAssetAsFile("Atlases", atlasGenerated.detailMaskAtlas, this.gameObject.name + " (DetailMask)", "asset");
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Materials", smrender.sharedMaterials[0], this.gameObject.name, "mat");

                //Save prefab if is desired
                if (savePrefabOfMerge == true)
                    SaveMergeAsPrefab("Prefabs", nameOfPrefabOfMerge, this.gameObject);

                //Generate the Stats text
                GenerateStatsTextAndFillIt(nameOfThisMerge, "All In One", verticesCount, timeOfStart, timeOfEnd, mergedMeshes, meshesBefore, drawCallReduction, materialCount, originalUvLenght, smrender.sharedMesh.uv.Length);

                //Clear progress bar
                ShowProgressBar("", false, 1.0f);

                //Show alert e log
                if (Application.isPlaying == false)
                    Debug.Log("The merging of the meshes was completed successfully!");
                LaunchLog("The merging of the meshes was completed successfully! The name of this merge is \"" + nameOfThisMerge + "\"!", LogTypeOf.Log);

                //Call event of done merge
                if (Application.isPlaying == true && onCombineMeshs != null)
                    onCombineMeshs.Invoke();

                //Change to Logs Of Merge tab
                ShowLogsOfMergeTabAndClearUndoHistory();
            }
            //If occurs a error on merge, catch it
            catch (Exception exception)
            {
                StopMergeByErrorWhileMerging(exception);
            }

            //Restore original position, rotation and scale
            if (autoManagePosition == true)
            {
                this.gameObject.transform.position = thisOriginalPosition;
                this.gameObject.transform.eulerAngles = thisOriginalRotation;
                this.gameObject.transform.localScale = thisOriginalScale;
            }
        }

        private void DoCombineMeshs_OnlyAnima2dMeshs()
        {
#if MTAssets_Anima2D_Available
            //Verify if the meshes are already merged
            if (resultMergeGameObject != null)
            {
                LaunchLog("Currently, this character's meshes are already merged. Please, before making a new merge, undo the merge previously done.", LogTypeOf.Warning);
                return;
            }

            //Reset position, rotation and scale and store it (to avoid problems with matrix or blendshapes positioning for example)
            if (autoManagePosition == true)
            {
                thisOriginalPosition = this.gameObject.transform.position;
                thisOriginalRotation = this.gameObject.transform.eulerAngles;
                thisOriginalScale = this.gameObject.transform.localScale;
                this.gameObject.transform.position = Vector3.zero;
                this.gameObject.transform.eulerAngles = Vector3.zero;
                this.gameObject.transform.localScale = Vector3.one;
            }

            //Try to merge. If occurs error, stop merge
            try
            {
                //Clear progress bar
                ShowProgressBar("Merging...", true, 1.0f);

                //Clear Logs Of Merge
                resultMergeLogs.Clear();

                //Validate all variables
                ValidateAllVariables();

                //Change the animator to always animate, and store the original value (if legacy Animation Support is disabled)
                Animator thisAnimator = null;
                AnimatorCullingMode originalCullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (legacyAnimationSupport == false)
                {
                    thisAnimator = GetComponent<Animator>();
                    originalCullingMode = thisAnimator.cullingMode;
                    thisAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                //Change the legacy animation to always animate, and store the original value (if legacy animation support is enabled)
                Animation thisLegacyAnimation = null;
                AnimationCullingType originalLegacyCullingType = AnimationCullingType.AlwaysAnimate;
                if (legacyAnimationSupport == true)
                {
                    thisLegacyAnimation = GetComponent<Animation>();
                    originalLegacyCullingType = thisLegacyAnimation.cullingType;
                    thisLegacyAnimation.cullingType = AnimationCullingType.AlwaysAnimate;
                }

                //Start of Stats
                DateTime timeOfStart = DateTime.Now;
                int verticesCount = 0;
                int mergedMeshes = 0;
                int drawCallReduction = 0;
                int meshesBefore = 0;
                int materialCount = 0;
                int originalUvLenght = 0;

                //Get all GameObjects to merge
                GameObject[] gameObjectsToMerge = GetAllItemsForCombine(false, true);

                //Get all Skinned Mesh Renderers to merge
                SkinnedMeshRenderer[] skinnedMeshesToMerge = GetAllSkinnedMeshsValidatedToCombine(gameObjectsToMerge);

                //Stop the merge if not have meshes to merge
                if (skinnedMeshesToMerge == null || skinnedMeshesToMerge.Length < 1)
                {
                    LaunchLog("The merge has been canceled. There may not be enough meshes to be combined. At least 1 valid mesh is required for the merge process to be possible. Also, there is the possibility that all the meshes found, are invalid or have been ignored during the merge process.", LogTypeOf.Warning);
                    StopMergeByErrorWhileMerging(null);
                    return;
                }

                //Count vertices
                verticesCount = CountVerticesInAllMeshes(skinnedMeshesToMerge);

                //------------------------------- START OF MERGE CODE --------------------------------

                //Meshes and texture list to be combined
                List<SpriteMeshInstance> spriteMeshInstancesToCombine = new List<SpriteMeshInstance>();
                List<Texture2D> spriteTexturesToCombine = new List<Texture2D>();
                List<int> meshesVertexIndex = new List<int>();

                //Gets the valid meshes that are to be merged
                foreach (SkinnedMeshRenderer meshRender in skinnedMeshesToMerge)
                {
                    //Collect data
                    SpriteMeshInstance thisSpriteMeshInstance = meshRender.GetComponent<SpriteMeshInstance>();
                    spriteMeshInstancesToCombine.Add(thisSpriteMeshInstance);
                    spriteTexturesToCombine.Add(thisSpriteMeshInstance.spriteMesh.sprite.texture);
                    meshesVertexIndex.Add(thisSpriteMeshInstance.spriteMesh.sharedMesh.vertexCount);
                }

                //Collect this current position of model
                Vector3 position = transform.position;
                Quaternion rotation = transform.rotation;
                Vector3 scale = transform.localScale;

                //Reset the current position of model
                transform.position = Vector3.zero;
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;

                //Prepare the storage
                List<Transform> bones = new List<Transform>();
                List<BoneWeight> boneWeights = new List<BoneWeight>();
                List<Matrix4x4> bindposes = new List<Matrix4x4>();
                List<CombineInstance> combineInstances = new List<CombineInstance>();
                int numSubmeshes = 0;

                //Get count of submeshes
                for (int i = 0; i < spriteMeshInstancesToCombine.Count; i++)
                {
                    SpriteMeshInstance spriteMesh = spriteMeshInstancesToCombine[i];

                    if (spriteMesh.cachedSkinnedRenderer)
                    {
                        numSubmeshes += spriteMesh.mesh.subMeshCount;
                    }
                }

                //Start the merge of all meshes
                int[] meshIndex = new int[numSubmeshes];
                int boneOffset = 0;
                for (int i = 0; i < spriteMeshInstancesToCombine.Count; ++i)
                {
                    SpriteMeshInstance spriteMesh = spriteMeshInstancesToCombine[i];

                    if (spriteMesh.cachedSkinnedRenderer)
                    {
                        SkinnedMeshRenderer skinnedMeshRenderer = spriteMesh.cachedSkinnedRenderer;

                        BoneWeight[] meshBoneweight = spriteMesh.sharedMesh.boneWeights;

                        // May want to modify this if the renderer shares bones as unnecessary bones will get added.
                        for (int j = 0; j < meshBoneweight.Length; ++j)
                        {
                            BoneWeight bw = meshBoneweight[j];
                            BoneWeight bWeight = bw;
                            bWeight.boneIndex0 += boneOffset;
                            bWeight.boneIndex1 += boneOffset;
                            bWeight.boneIndex2 += boneOffset;
                            bWeight.boneIndex3 += boneOffset;
                            boneWeights.Add(bWeight);
                        }

                        boneOffset += spriteMesh.bones.Count;

                        Transform[] meshBones = skinnedMeshRenderer.bones;
                        for (int j = 0; j < meshBones.Length; j++)
                        {
                            Transform bone = meshBones[j];
                            bones.Add(bone);
                        }

                        CombineInstance combineInstance = new CombineInstance();
                        Mesh mesh = new Mesh();
                        skinnedMeshRenderer.BakeMesh(mesh);
                        mesh.uv = spriteMesh.spriteMesh.sprite.uv;
                        combineInstance.mesh = mesh;
                        meshIndex[i] = combineInstance.mesh.vertexCount;
                        combineInstance.transform = skinnedMeshRenderer.localToWorldMatrix;
                        combineInstances.Add(combineInstance);
                    }
                }

                //Set the bind poses
                for (int b = 0; b < bones.Count; b++)
                {
                    if (compatibilityMode == true)
                        bindposes.Add(bones[b].worldToLocalMatrix * transform.worldToLocalMatrix);
                    if (compatibilityMode == false)
                        bindposes.Add(bones[b].worldToLocalMatrix * transform.worldToLocalMatrix);
                }

                //Show progress bar
                ShowProgressBar("Merging...", true, 1.0f);

                //Combine all submeshes into one mesh with submeshes with all materials
                Mesh finalMesh = new Mesh();
                if (verticesCount <= MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
                if (verticesCount > MAX_VERTICES_FOR_16BITS_MESH)
                    finalMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                finalMesh.name = "Combined Meshes (Only Anima2D Meshes)";
                finalMesh.CombineMeshes(combineInstances.ToArray(), true, true);

                //Do recalculations where is desired
                finalMesh.RecalculateBounds();

                //Create the holder GameObject
                resultMergeGameObject = new GameObject(nameOfThisMerge);
                resultMergeGameObject.transform.SetParent(this.gameObject.transform);
                SkinnedMeshRenderer smrender = resultMergeGameObject.AddComponent<SkinnedMeshRenderer>();
                smrender.sharedMesh = finalMesh;
                smrender.bones = bones.ToArray();
                smrender.sharedMesh.boneWeights = boneWeights.ToArray();
                smrender.sharedMesh.bindposes = bindposes.ToArray();
                smrender.rootBone = GetCorrectRootBoneFromAllOriginalSkinnedMeshRenderers(skinnedMeshesToMerge);
                smrender.sharedMaterials = new Material[] { GetValidatedCopyOfMaterial(spriteMeshInstancesToCombine[0].sharedMaterials[0], true, true) };
                smrender.sharedMaterials[0].name = "Combined Materials (Only Anima2D Meshes)";

                //Generate the atlas
                Texture2D atlasTexure = new Texture2D(16, 16);
                Rect[] atlasRects = atlasTexure.PackTextures(spriteTexturesToCombine.ToArray(), 0, GetAtlasMaxResolutionAnima2D());

                //Start the modification of UV of merged mesh, to receive atlas sprite
                //Prepare the UVs array
                Vector2[] originalCombinedUVs = smrender.sharedMesh.uv;
                Vector2[] newCombinedUVs = new Vector2[originalCombinedUVs.Length];
                //Resizes the uvs to the atlas so that it ignores the edges of each texture
                int currentMeshOffset = meshesVertexIndex[0];
                int currentMeshUV = 0;
                for (int i = 0; i < originalCombinedUVs.Length; i++)
                {
                    //Verifies which mesh this vertex belongs to
                    if (i >= currentMeshOffset)
                    {
                        //Verify if the current mesh UV is in the end, before add more
                        if (currentMeshUV < meshesVertexIndex.Count - 1)
                            currentMeshUV += 1;
                        currentMeshOffset += meshesVertexIndex[currentMeshUV];
                    }

                    //If the UV is not larger than the texture
                    newCombinedUVs[i].x = Mathf.Lerp(atlasRects[currentMeshUV].xMin, atlasRects[currentMeshUV].xMax, originalCombinedUVs[i].x);
                    newCombinedUVs[i].y = Mathf.Lerp(atlasRects[currentMeshUV].yMin, atlasRects[currentMeshUV].yMax, originalCombinedUVs[i].y);
                }
                //Apply the new UV map
                smrender.sharedMesh.uv = newCombinedUVs;

                //Return the original positions to new mesh
                transform.position = position;
                transform.rotation = rotation;
                transform.localScale = scale;

                //Show UV vertices in all atlas
                if (highlightUvVertices == true)
                {
                    for (int i = 0; i < smrender.sharedMesh.uv.Length; i++)
                        atlasTexure.SetPixel((int)(atlasTexure.width * smrender.sharedMesh.uv[i].x), (int)(atlasTexure.height * smrender.sharedMesh.uv[i].y), Color.yellow);
                    atlasTexure.Apply();
                }

                //------------------------------- END OF MERGE CODE --------------------------------

                //Add the combined meshes manager to holder GameObject
                CombinedMeshesManager meshesManager = resultMergeGameObject.AddComponent<CombinedMeshesManager>();

                //Fill te combined meshes manager with data of anima2d meshes combined
                meshesManager.cachedSkinnedRenderer = smrender;
                meshesManager.materialPropertyBlock = new MaterialPropertyBlock();
                meshesManager.atlasForRenderInChar = atlasTexure;
                meshesManager.atlasForRenderInChar.name = this.gameObject.name + " (MainTexture)";
                meshesManager.rootGameObject = this.gameObject;
                meshesManager.mergeMethodUsed = CombinedMeshesManager.MergeMethod.OnlyAnima2dMeshes;
                meshesManager.sourceMeshes = skinnedMeshesToMerge;

                //Save the original GameObjects
                resultMergeOriginalGameObjects = gameObjectsToMerge;

                //Disable all original GameObjects that are merged
                foreach (SkinnedMeshRenderer originalRender in skinnedMeshesToMerge)
                {
                    originalRender.gameObject.SetActive(false);
                }

                //Restore original culling mod (if support to legacy animation support is disabled)
                if (legacyAnimationSupport == false)
                    thisAnimator.cullingMode = originalCullingMode;
                //Restore original culling mod (if support to legacy animation support is enabled)
                if (legacyAnimationSupport == true)
                    thisLegacyAnimation.cullingType = originalLegacyCullingType;

                //End of Stats
                DateTime timeOfEnd = DateTime.Now;

                //Save data as asset
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Meshes", smrender.sharedMesh, this.gameObject.name, "asset");
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Atlases", atlasTexure, this.gameObject.name + " (SpriteAtlas)", "asset");
                if (saveDataInAssets == true)
                    SaveAssetAsFile("Materials", smrender.sharedMaterials[0], this.gameObject.name, "mat");

                //Save prefab if is desired
                if (savePrefabOfMerge == true)
                    SaveMergeAsPrefab("Prefabs", nameOfPrefabOfMerge, this.gameObject);

                //Generate the Stats text
                GenerateStatsTextAndFillIt(nameOfThisMerge, "Only Anima2D Meshes", verticesCount, timeOfStart, timeOfEnd, mergedMeshes, meshesBefore, drawCallReduction, materialCount, originalUvLenght, smrender.sharedMesh.uv.Length);

                //Clear progress bar
                ShowProgressBar("", false, 1.0f);

                //Show alert e log
                if (Application.isPlaying == false)
                    Debug.Log("The merging of the meshes was completed successfully!");
                LaunchLog("The merging of the meshes was completed successfully! The name of this merge is \"" + nameOfThisMerge + "\"!", LogTypeOf.Log);

                //Call event of done merge
                if (Application.isPlaying == true && onCombineMeshs != null)
                    onCombineMeshs.Invoke();

                //Change to Logs Of Merge tab
                ShowLogsOfMergeTabAndClearUndoHistory();
            }
            //If occurs a error on merge, catch it
            catch (Exception exception)
            {
                StopMergeByErrorWhileMerging(exception);
            }

            //Restore original position, rotation and scale
            if (autoManagePosition == true)
            {
                this.gameObject.transform.position = thisOriginalPosition;
                this.gameObject.transform.eulerAngles = thisOriginalRotation;
                this.gameObject.transform.localScale = thisOriginalScale;
            }
#endif
        }

        private void DoUndoCombineMeshs(bool runMonoIl2CppGc, bool runUnityGc, bool errorOcurred)
        {
            //Verify if the meshes are already merged
            if (resultMergeGameObject == null)
            {
                LaunchLog("Currently the character's meshes are not merged. To undo a merge, perform one first.", LogTypeOf.Warning);
                return;
            }

            //Show progress bar
            if (errorOcurred == false)
                ShowProgressBar("Undoing...", true, 1.0f);

            //Re-active all original GameObjects
            if (resultMergeOriginalGameObjects != null)
            {
                foreach (GameObject obj in resultMergeOriginalGameObjects)
                {
                    if (obj != null)
                        obj.SetActive(true);
                }
            }

            //Delete all assets
#if UNITY_EDITOR
            foreach (string str in resultMergeAssetsSaved)
            {
                //If this is a prefab, not delet assets
                if (savePrefabOfMerge == true)
                    break;

                if (AssetDatabase.LoadAssetAtPath(str, typeof(object)) != null)
                    AssetDatabase.DeleteAsset(str);
            }
#endif

            //Destroy all result merge variables
            DestroyGameObject(resultMergeGameObject);
            resultMergeOriginalGameObjects = null;
            resultMergeTextStats = "";
            resultMergeAssetsSaved.Clear();

            //Clear result merge logs, if a error is not ocurred
            if (errorOcurred == false)
                resultMergeLogs.Clear();

            //Run the GC if is activated
            if (runMonoIl2CppGc == true)
            {
                System.GC.Collect();
            }
            if (runUnityGc == true)
            {
                Resources.UnloadUnusedAssets();
            }

            //Clear progress bar
            if (errorOcurred == false)
                ShowProgressBar("", false, 1.0f);

            //Show alert
            if (errorOcurred == false)
                if (Application.isPlaying == false)
                    Debug.Log("The last merge performed on this character was undone.");

            //Call event of undo merge
            if (Application.isPlaying == true && onUndoCombineMeshs != null)
                onUndoCombineMeshs.Invoke();
        }

        #region CLASSES_OF_CORE_METHODS
        //Can be used in all methods

        private class BlendShapeData
        {
            public string blendShapeFrameName = "";
            public int blendShapeFrameIndex = -1;
            public float blendShapeCurrentValue = 0.0f;
            public List<Vector3> startDeltaVertices = new List<Vector3>();
            public List<Vector3> startDeltaNormals = new List<Vector3>();
            public List<Vector3> startDeltaTangents = new List<Vector3>();
            public List<Vector3> finalDeltaVertices = new List<Vector3>();
            public List<Vector3> finalDeltaNormals = new List<Vector3>();
            public List<Vector3> finalDeltaTangents = new List<Vector3>();
            public string blendShapeNameOnCombinedMesh = "";
        }

        //Used in One Mesh Per Material

        private class SubMeshToCombine
        {
            //Class that stores a mesh of skinned mesh renderer and respective submesh index, to combine
            public Transform transform;
            public SkinnedMeshRenderer skinnedMeshRenderer;
            public int subMeshIndex;

            public SubMeshToCombine(Transform transform, SkinnedMeshRenderer skinnedMeshRenderer, int subMeshIndex)
            {
                this.transform = transform;
                this.skinnedMeshRenderer = skinnedMeshRenderer;
                this.subMeshIndex = subMeshIndex;
            }
        }

        private class SubMeshesCombined
        {
            //Class that stores various submeshes, merged, with their respective material and data
            public Matrix4x4 localToWorldMatrix;
            public Mesh subMeshesMerged;
            public Transform[] bonesToMerge;
            public Matrix4x4[] bindPosesToMerge;
            public Material thisMaterial;

            public SubMeshesCombined(Matrix4x4 localToWorldMatrix, Mesh subMeshesMerged, Transform[] bonesToMerge, Matrix4x4[] bindPosesToMerge, Material thisMaterial)
            {
                //Store the data
                this.localToWorldMatrix = localToWorldMatrix;
                this.subMeshesMerged = subMeshesMerged;
                this.bonesToMerge = bonesToMerge;
                this.bindPosesToMerge = bindPosesToMerge;
                this.thisMaterial = thisMaterial;
            }
        }

        //Used in All In One

        private class TexturesSubMeshes
        {
            public class UvBounds
            {
                //This class stores a data of size of a submesh uv, data like major value of x and y, etc
                public float majorX = 0;
                public float majorY = 0;
                public float minorX = 0;
                public float minorY = 0;
                public float spaceMinorX = 0;
                public float spaceMajorX = 0;
                public float spaceMinorY = 0;
                public float spaceMajorY = 0;
                public float edgesUseX = 0.0f;
                public float edgesUseY = 0.0f;

                public float Round(float value, int places)
                {
                    return float.Parse(value.ToString("F" + places.ToString()), CultureInfo.InvariantCulture);
                }

                public void RoundBoundsValuesAndCalculateSpaceNeededToTiling()
                {
                    //Round all values
                    majorX = Round(majorX, 4);
                    majorY = Round(majorY, 4);
                    minorX = Round(minorX, 4);
                    minorY = Round(minorY, 4);

                    //Calculate aditional space to left of texture
                    if (minorX >= 0.0f)
                        spaceMinorX = 0.0f;
                    if (minorX < 0.0f)
                        spaceMinorX = minorX * -1.0f;

                    //Calculate aditional space to down of texture
                    if (minorY >= 0.0f)
                        spaceMinorY = 0.0f;
                    if (minorY < 0.0f)
                        spaceMinorY = minorY * -1.0f;

                    //Calculate aditional space to up of texture
                    if (majorY >= 1.0f)
                        spaceMajorY = majorY - 1.0f;

                    //Calculate aditional space to right of texture
                    if (majorX >= 1.0f)
                        spaceMajorX = majorX - 1.0f;
                }
            }

            public class UserSubMeshes
            {
                //This class stores data of a submesh that uses this texture
                public UvBounds uvBoundsOfThisSubMesh = new UvBounds();
                public int startOfUvVerticesInIndex = 0;
                public Vector2[] originalUvVertices = null;
            }

            //This class stores textures and all submeshes data that uses this texture. If is tilled texture, this is repeated and used only by one submesh
            public Material material;
            public Texture2D mainTexture;
            public Texture2D metallicMap;
            public Texture2D specularMap;
            public Texture2D normalMap;
            public Texture2D normalMap2;
            public Texture2D heightMap;
            public Texture2D occlusionMap;
            public Texture2D detailMap;
            public Texture2D detailMask;
            public bool isTiledTexture = false;
            public Vector2Int mainTextureResolution;
            public Vector2Int mainTextureResolutionWithEdges;
            public List<UserSubMeshes> userSubMeshes = new List<UserSubMeshes>();

            //Return the edges percent usage, getting from 0 submesh of this texture
            public Vector2 GetEdgesPercentUsageOfThisTextures()
            {
                return new Vector2(userSubMeshes[0].uvBoundsOfThisSubMesh.edgesUseX, userSubMeshes[0].uvBoundsOfThisSubMesh.edgesUseY);
            }

            //Convert all vertices of all submeshes to positive values
            public void ConvertAllSubMeshsVerticesToPositive()
            {
                //Convert all vertices for each submesh
                foreach (UserSubMeshes submesh in userSubMeshes)
                {
                    //Calculate all minor values of vertices of this submehs
                    float[] xAxis = new float[submesh.originalUvVertices.Length];
                    float[] yAxis = new float[submesh.originalUvVertices.Length];
                    for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                    {
                        xAxis[i] = submesh.originalUvVertices[i].x;
                        yAxis[i] = submesh.originalUvVertices[i].y;
                    }
                    Vector2 minorValues = new Vector2(Mathf.Min(xAxis), Mathf.Min(yAxis));

                    //Modify all values of all vertices to positive
                    for (int i = 0; i < submesh.originalUvVertices.Length; i++)
                    {
                        //Get original value
                        Vector2 originalValue = submesh.originalUvVertices[i];

                        //Create the modifyied value
                        Vector2 newValue = Vector2.zero;

                        //Modify the value
                        if (originalValue.x >= 0.0f)
                            newValue.x = originalValue.x + ((minorValues.x < 0.0f) ? (minorValues.x * -1) : 0);
                        if (originalValue.y >= 0.0f)
                            newValue.y = originalValue.y + ((minorValues.y < 0.0f) ? (minorValues.y * -1) : 0);

                        //Convert all negative values to positive, and invert the values, to invert negative texture maping to positive
                        if (originalValue.x < 0.0f)
                            newValue.x = (minorValues.x * -1) - (originalValue.x * -1);
                        if (originalValue.y < 0.0f)
                            newValue.y = (minorValues.y * -1) - (originalValue.y * -1);

                        //Apply the new value
                        submesh.originalUvVertices[i] = newValue;
                    }
                }
            }
        }

        private class AtlasData
        {
            //This class store a atlas data
            public Texture2D mainTextureAtlas = new Texture2D(16, 16);
            public Texture2D metallicMapAtlas = new Texture2D(16, 16);
            public Texture2D specularMapAtlas = new Texture2D(16, 16);
            public Texture2D normalMapAtlas = new Texture2D(16, 16);
            public Texture2D normalMap2Atlas = new Texture2D(16, 16);
            public Texture2D heightMapAtlas = new Texture2D(16, 16);
            public Texture2D occlusionMapAtlas = new Texture2D(16, 16);
            public Texture2D detailMapAtlas = new Texture2D(16, 16);
            public Texture2D detailMaskAtlas = new Texture2D(16, 16);
            public Rect[] atlasRects = new Rect[0];
            public Texture2D[] originalMainTexturesUsedAndOrdenedAccordingToAtlasRect = new Texture2D[0];

            //Return the respective id of rect that the informed texture is posicioned
            public int GetRectIndexOfThatMainTexture(Texture2D texture)
            {
                //Prepare the storage
                int index = -1;

                foreach (Texture2D tex in originalMainTexturesUsedAndOrdenedAccordingToAtlasRect)
                {
                    //Increase de index in onee
                    index += 1;

                    //If the texture informed is equal to original texture used, break this loop and return the respective index
                    if (tex == texture)
                        break;
                }

                //Return the data
                return index;
            }
        }

        private enum TextureType
        {
            //This enum stores type of texture
            MainTexture,
            MetallicMap,
            SpecularMap,
            NormalMap,
            HeightMap,
            OcclusionMap,
            DetailMap,
            DetailMask
        }

        private class ColorData
        {
            //This class stores a color and your respective name
            public string colorName;
            public Color color;

            public ColorData(string colorName, Color color)
            {
                this.colorName = colorName;
                this.color = color;
            }
        }

        //Used in Just Material Colors

        private class UvDataAndColorOfThisSubmesh
        {
            //This class stores all UV data of a submesh
            public Texture2D textureColor;
            public int startOfUvVerticesIndex;
            public Vector2[] originalUvVertices;
        }

        private class ColorAtlasData
        {
            //This class store a atlas data
            public Texture2D colorAtlas = new Texture2D(16, 16);
            public Rect[] atlasRects = new Rect[0];
            public Texture2D[] originalTexturesUsedAndOrdenedAccordingToAtlasRect = new Texture2D[0];

            //Return the respective id of rect that the informed texture is posicioned
            public int GetRectIndexOfThatMainTexture(Texture2D texture)
            {
                //Prepare the storage
                int index = -1;

                foreach (Texture2D tex in originalTexturesUsedAndOrdenedAccordingToAtlasRect)
                {
                    //Increase de index in onee
                    index += 1;

                    //If the texture informed is equal to original texture used, break this loop and return the respective index
                    if (tex == texture)
                        break;
                }

                //Return the data
                return index;
            }
        }
        #endregion

        #region TOOLS_METHODS_FOR_CORE_METHODS
        //API Methods For Interface Editor And Core Methods
        private void LaunchLog(string content, LogTypeOf logType)
        {
            //Launch log in the log of merge and console of unity, if is desired
            if (launchConsoleLogs == true && Application.isPlaying == true)
            {
                if (logType == LogTypeOf.Assert || logType == LogTypeOf.Error || logType == LogTypeOf.Exception)
                {
                    Debug.LogError(content);
                }
                if (logType == LogTypeOf.Log)
                {
                    Debug.Log(content);
                }
                if (logType == LogTypeOf.Warning)
                {
                    Debug.LogWarning(content);
                }
            }

#if UNITY_EDITOR
            DateTime dateTime = new DateTime();
            dateTime = DateTime.Now;
            string month = (dateTime.Month >= 10) ? dateTime.Month.ToString() : "0" + dateTime.Month.ToString();
            string day = (dateTime.Day >= 10) ? dateTime.Day.ToString() : "0" + dateTime.Day.ToString();
            string hour = (dateTime.Hour >= 10) ? dateTime.Hour.ToString() : "0" + dateTime.Hour.ToString();
            string minute = (dateTime.Minute >= 10) ? dateTime.Minute.ToString() : "0" + dateTime.Minute.ToString();

            resultMergeLogs.Add(new LogOfMerge(content + "\n\n" + month + "/" + day + "/" + dateTime.Year + " " + hour + ":" + minute, logType));
#endif
        }

        private void ValidateAllVariables()
        {
            //Validate all variables to avoid problems with merge

            //On merge with editor
            if (savePrefabOfMerge == true)
                saveDataInAssets = true;

            //Additional effects
            if (allInOneParams.specularMapSupport == true && allInOneParams.metallicMapSupport == true)
            {
                allInOneParams.metallicMapSupport = false;
                allInOneParams.specularMapSupport = false;
            }

            //If blendshapes multiplier is equal to zero, reset to one
            if (blendShapesMultiplier == 0)
                blendShapesMultiplier = 1.0f;

            //If the merge name is empty, set as default
            if (String.IsNullOrEmpty(nameOfThisMerge) == true)
                nameOfThisMerge = "Combined Meshes";

            //If have another scriptable render pipeline
            if (CurrentRenderPipeline.haveAnotherSrpPackages == true && allInOneParams.useDefaultMainTextureProperty == true)
            {
                if (CurrentRenderPipeline.packageDetected == "HDRP")   //<- Set default for HDRP/Lit
                {
                    allInOneParams.mainTexturePropertyToFind = "_MainTex";
                    allInOneParams.mainTexturePropertyToInsert = "_BaseColorMap";
                }
                if (CurrentRenderPipeline.packageDetected == "URP")    //<- Set default for URP/Lit
                {
                    allInOneParams.mainTexturePropertyToFind = "_MainTex";
                    allInOneParams.mainTexturePropertyToInsert = "_BaseMap";
                }
            }
            if (CurrentRenderPipeline.haveAnotherSrpPackages == true && justMaterialColorsParams.useDefaultColorProperty == true)
            {
                if (CurrentRenderPipeline.packageDetected == "HDRP")   //<- Set default for HDRP/Lit
                {
                    justMaterialColorsParams.colorPropertyToFind = "_BaseColor";
                    justMaterialColorsParams.mainTexturePropertyToInsert = "_BaseColorMap";
                }
                if (CurrentRenderPipeline.packageDetected == "URP")   //<- Set default for URP/Lit
                {
                    justMaterialColorsParams.colorPropertyToFind = "_BaseColor";
                    justMaterialColorsParams.mainTexturePropertyToInsert = "_BaseMap";
                }
            }

            //If not have another scriptable render pipeline
            if (CurrentRenderPipeline.haveAnotherSrpPackages == false && allInOneParams.useDefaultMainTextureProperty == true)
            {
                allInOneParams.mainTexturePropertyToFind = "_MainTex";
                allInOneParams.mainTexturePropertyToInsert = "_MainTex";
            }
            if (CurrentRenderPipeline.haveAnotherSrpPackages == false && justMaterialColorsParams.useDefaultColorProperty == true)
            {
                justMaterialColorsParams.colorPropertyToFind = "_Color";
                justMaterialColorsParams.mainTexturePropertyToInsert = "_MainTex";
            }
        }

        private GameObject[] GetAllItemsForCombine(bool includeItemsRegisteredToBeIgnored, bool launchLogs)
        {
            //Prepare the variable
            List<GameObject> itemsForCombineStart = new List<GameObject>();

            //Get all items for combine
            if (mergeMethod != MergeMethod.OnlyAnima2dMeshes)
            {
                SkinnedMeshRenderer[] renderers = this.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(combineInactives);
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    //Skip renderers that contains Combined Meshes Manager
                    if (renderer.gameObject.GetComponent<CombinedMeshesManager>() == null)
                        itemsForCombineStart.Add(renderer.gameObject);
                }
            }
            if (mergeMethod == MergeMethod.OnlyAnima2dMeshes)
            {
#if MTAssets_Anima2D_Available
                SpriteMeshInstance[] renderers = this.gameObject.GetComponentsInChildren<SpriteMeshInstance>(combineInactives);
                foreach (SpriteMeshInstance renderer in renderers)
                {
                    itemsForCombineStart.Add(renderer.gameObject);
                }
#endif
            }

            //Return all itens, if is desired
            if (includeItemsRegisteredToBeIgnored == true)
            {
                return itemsForCombineStart.ToArray();
            }

            //Create the final list of items to combine
            List<GameObject> itemsForCombineFinal = new List<GameObject>();

            //Remove all GameObjects registered to be ignored
            for (int i = 0; i < itemsForCombineStart.Count; i++)
            {
                bool isToIgnore = false;
                foreach (GameObject obj in gameObjectsToIgnore)
                {
                    if (obj == itemsForCombineStart[i])
                    {
                        isToIgnore = true;
                    }
                }
                if (isToIgnore == true)
                {
                    if (launchLogs == true)
                        LaunchLog("GameObject \"" + itemsForCombineStart[i].name + "\" was registered to be ignored during the merge, so it was not included in the merge processing.", LogTypeOf.Warning);
                    continue;
                }
                itemsForCombineFinal.Add(itemsForCombineStart[i]);
            }

            return itemsForCombineFinal.ToArray();
        }

        private void StopMergeByErrorWhileMerging(Exception exception)
        {
            //If occurred a exception
            if (exception != null)
            {
                //Launch log error
                LaunchLog("An error occurred while performing this merge. Read on for more details.\n\n" + exception.Message + "\n\n" + exception.StackTrace, LogTypeOf.Error);

                //Clear progress bar
                ShowProgressBar("", false, 1.0f);

                //Change to Logs Of Merge tab
                ShowLogsOfMergeTabAndClearUndoHistory();

                //Show alert
                Debug.LogError("An error occurred during this merge. Check the log or console for more details.");

                //Undo all changes made by this merge
                DoUndoCombineMeshs(true, true, true);
            }

            //If not occurred a exception
            if (exception == null)
            {
                //Clear progress bar
                ShowProgressBar("", false, 1.0f);

                //Show alert
                Debug.LogError("An error occurred during this merge. Check the log or console for more details.");

                //Change to Logs Of Merge tab
                ShowLogsOfMergeTabAndClearUndoHistory();

                //Undo all changes made by this merge
                DoUndoCombineMeshs(true, true, true);
            }
        }

        //API Methods only for Interface Editor

        private void ShowLogsOfMergeTabAndClearUndoHistory()
        {
#if UNITY_EDITOR
            currentTab = 2;
            clearUndoHistory = true;
#endif
        }

        private void ShowProgressBar(string message, bool show, float progress)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == true)
                return;

            if (show == true)
            {
                EditorUtility.DisplayProgressBar("A moment", message, progress);
            }
            if (show == false)
            {
                EditorUtility.ClearProgressBar();
            }
#endif
        }

        private void SaveAssetAsFile(string folderNameToSave, UnityEngine.Object assetToSave, string fileName, string fileExtension)
        {
#if UNITY_EDITOR
            //If is playing, cancel save
            if (Application.isPlaying == true)
            {
                return;
            }

            //Create the directory in project
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets"))
                AssetDatabase.CreateFolder("Assets", "MT Assets");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData/" + folderNameToSave))
                AssetDatabase.CreateFolder("Assets/MT Assets/_AssetsData", folderNameToSave);

            //Get current date
            DateTime dateNow = DateTime.Now;
            string dateNowStr = dateNow.Year.ToString() + dateNow.Month.ToString() + dateNow.Day.ToString() + dateNow.Hour.ToString() + dateNow.Minute.ToString() + dateNow.Second.ToString() + dateNow.Millisecond.ToString();

            //Save the asset
            string fileDirectory = "Assets/MT Assets/_AssetsData/" + folderNameToSave + "/" + fileName + " (" + dateNowStr + ")." + fileExtension;
            AssetDatabase.CreateAsset(assetToSave, fileDirectory);
            resultMergeAssetsSaved.Add(fileDirectory);

            //Save all data and reload
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }

        private void SaveMergeAsPrefab(string folderNameToSave, string prefabName, GameObject targetGo)
        {
#if UNITY_EDITOR
            //f is playing, cancel save
            if (Application.isPlaying == true)
            {
                return;
            }

            //Create the directory in project
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets"))
                AssetDatabase.CreateFolder("Assets", "MT Assets");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData/" + folderNameToSave))
                AssetDatabase.CreateFolder("Assets/MT Assets/_AssetsData", folderNameToSave);

            //Save the asset
            string fileDirectory = "Assets/MT Assets/_AssetsData/" + folderNameToSave + "/" + prefabName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath(fileDirectory, typeof(GameObject)) != null)
            {
                LaunchLog("Prefab \"" + name + "\" already exists in your project files. Therefore, a new file was not created.", LogTypeOf.Warning);
            }
            if (AssetDatabase.LoadAssetAtPath(fileDirectory, typeof(GameObject)) == null)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(targetGo, fileDirectory, InteractionMode.UserAction);
                LaunchLog("The prefab \"" + name + "\" was created in your project files! The prefab generated with the desired name, can be found at \"Assets/MT Assets/_AssetsData/Prefabs\"", LogTypeOf.Log);
            }
#endif
        }

        private void GenerateStatsTextAndFillIt(string nameOfThisMerge, string mergeMethod, int processedVertices, DateTime mergeStart, DateTime mergeEnd, int meshCountBefore, int meshCountAfter, int drawCallReduction, int materialCount, int originalUvLenght, int modifyiedUvLenght)
        {
#if UNITY_EDITOR
            //Get the time time of merge
            TimeSpan timeOfMerge = new TimeSpan(mergeEnd.Ticks - mergeStart.Ticks);
            float optimizationRate = (1 - ((float)meshCountAfter / (float)meshCountBefore)) * (float)100;

            //Create the stats text
            StringBuilder strBuild = new StringBuilder();
            strBuild.Append("All Skinned Meshes childrens of this model are combined. Statistics are available below.\n");
            strBuild.Append("\nMerge Method: " + mergeMethod);
            strBuild.Append("\nName Of This Merge: " + nameOfThisMerge);
            strBuild.Append("\nProcessed Vertices: " + processedVertices.ToString() + " Vertices");
            strBuild.Append("\nProcessing Time: " + timeOfMerge.TotalSeconds.ToString("F1") + " Seconds");
            strBuild.Append("\nMaterials Count: " + materialCount.ToString());
            strBuild.Append("\nMesh Count Before: " + meshCountBefore.ToString());
            strBuild.Append("\nMesh Count After: " + meshCountAfter.ToString());
            strBuild.Append("\nOriginal UV Vertices: " + originalUvLenght.ToString());
            strBuild.Append("\nModifyied UV Vertices: " + modifyiedUvLenght.ToString());
            strBuild.Append("\nDraw Call Reduction ≥ " + drawCallReduction.ToString());
            strBuild.Append("\nOptimization Rate: " + optimizationRate.ToString("F1") + "%");
            strBuild.Append("\n\nStatistics are only generated when combined in Unity Editor.");

            //Fill stats text
            resultMergeTextStats = strBuild.ToString();
#endif
        }

        //Tools Methods for all Core Methods

        private SkinnedMeshRenderer[] GetAllSkinnedMeshsValidatedToCombine(GameObject[] gameObjectsToCombine)
        {
            //Prepare the storage
            List<SkinnedMeshRenderer> meshRenderers = new List<SkinnedMeshRenderer>();

            //Get skinned mesh renderers in all GameObjects to combine
            foreach (GameObject obj in gameObjectsToCombine)
            {
                //Get the Skinned Mesh of this GameObject
                SkinnedMeshRenderer meshRender = obj.GetComponent<SkinnedMeshRenderer>();

                if (meshRender != null)
                {
                    //Verify if msh renderer is disabled
                    if (meshRender.enabled == false)
                    {
                        LaunchLog("The mesh present in GameObject " + meshRender.gameObject.name + " was ignored during the merge process. Reason: " +
                            "The Skinned Mesh Renderer is disabled",
                            LogTypeOf.Log);
                        continue;
                    }

                    //Verify if the sharedmesh is null
                    if (meshRender.sharedMesh == null)
                    {
                        LaunchLog("The mesh present in GameObject " + meshRender.gameObject.name + " was ignored during the merge process. Reason: " +
                            "The mesh is null.",
                            LogTypeOf.Log);
                        continue;
                    }

                    //Verify if exists blendshapes
                    if (meshRender.sharedMesh.blendShapeCount > 0 && blendShapesSupport == BlendShapesSupport.Disabled)
                    {
                        LaunchLog("The mesh present in GameObject " + meshRender.gameObject.name + " was ignored during the merge process. Reason: " +
                                                    "The mesh contains Blendshapes, and the \"Ignore Blendshapes\" option is enabled.",
                                                    LogTypeOf.Log);
                        continue;
                    }

                    //Verify if shared materials is null
                    if (meshRender.sharedMaterials == null)
                    {
                        LaunchLog("The mesh present in GameObject " + meshRender.gameObject.name + " was ignored during the merge process. Reason: " +
                            "This mesh has no materials, and materials list is null.",
                            LogTypeOf.Log);
                        continue;
                    }

                    //Verify if not have materials
                    if (meshRender.sharedMaterials.Length == 0)
                    {
                        LaunchLog("The mesh present in GameObject " + meshRender.gameObject.name + " was ignored during the merge process. Reason: " +
                            "This mesh has no materials.",
                            LogTypeOf.Log);
                        continue;
                    }

                    //Verify if quantity of shared materials is different of submeshes
                    if (meshRender.sharedMaterials.Length != meshRender.sharedMesh.subMeshCount)
                    {
                        LaunchLog("The mesh present in GameObject " + meshRender.gameObject.name + " was ignored during the merge process. Reason: " +
                            "The amount of materials in this mesh does not match the number of sub-meshes.",
                            LogTypeOf.Log);
                        continue;
                    }

                    //Verify if exists null materials in this mesh
                    bool foundNullMaterials = false;
                    foreach (Material mat in meshRender.sharedMaterials)
                    {
                        if (mat == null)
                            foundNullMaterials = true;
                    }
                    if (foundNullMaterials == true)
                    {
                        LaunchLog("The mesh present in GameObject " + meshRender.gameObject.name + " was ignored during the merge process. Reason: " +
                            "Null materials were found in this mesh.",
                            LogTypeOf.Log);
                        continue;
                    }

                    //If the method of merge is "All In One" and "Merge All UV Sizes" is disabled, remove the mesh if the UV is greater than 1 or minor than 0
                    if (mergeMethod == MergeMethod.AllInOne && allInOneParams.mergeTiledTextures == MergeTiledTextures.SkipAll)
                    {
                        bool haveUvVerticesMajorThanOne = false;
                        foreach (Vector2 vertex in meshRender.sharedMesh.uv)
                        {
                            //Check if vertex is major than 1
                            if (vertex.x > 1.0f || vertex.y > 1.0f)
                            {
                                haveUvVerticesMajorThanOne = true;
                            }
                            //Check if vertex is major than 0
                            if (vertex.x < 0.0f || vertex.y < 0.0f)
                            {
                                haveUvVerticesMajorThanOne = true;
                            }
                        }
                        if (haveUvVerticesMajorThanOne == true)
                        {
                            LaunchLog("The mesh present in \"" + meshRender.transform.name + "\" has a larger UV map than the texture (tiled texture). If the \"Merge Tiled Texture\" option is disabled, this mesh was ignored during the merge process. Keep in mind that if this mesh uses a higher UV than its texture (tiled texture), the texture will have to be adapted to fit in an atlas, and this can end up untwisting the way the texture is rendered in this mesh.", LogTypeOf.Log);
                            continue;
                        }
                    }

                    //Add to list of valid Skinned Meshs, if can add
                    meshRenderers.Add(meshRender);
                }
            }

            //Return all Skinned Meshes
            return meshRenderers.ToArray();
        }

        private bool ExistsDifferentRootBones(SkinnedMeshRenderer[] skinnedMeshRenderers, bool launchLogs)
        {
            //Prepare the storage
            Transform lastRootBone = skinnedMeshRenderers[0].rootBone;

            //Verify in each skinned mesh renderer, if exists different root bones
            foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers)
            {
                if (lastRootBone != smr.rootBone)
                {
                    if (launchLogs == true)
                        LaunchLog("Different root bones were found in your character's meshes. Combining meshes with different root bones can cause IK animation problems for example, but in general, the merge works without problems. If you activate the \"Only Equal Root Bones\" option, the Skinned Mesh Combiner will not combine meshes with different root bones in your character.", LogTypeOf.Log);
                    return true;
                }
            }

            return false;
        }

        private int CountVerticesInAllMeshes(SkinnedMeshRenderer[] skinnedMeshRenderers)
        {
            //Return count of vertices
            int verticesCount = 0;

            //Count all
            foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers)
            {
                verticesCount += smr.sharedMesh.vertexCount;
            }

            return verticesCount;
        }

        private void DestroyGameObject(GameObject gameObject)
        {
#if UNITY_EDITOR
            DestroyImmediate(gameObject);
#endif
#if !UNITY_EDITOR
            Destroy(gameObject);
#endif
        }

        private Material GetValidatedCopyOfMaterial(Material targetMaterial, bool copyPropertiesOfTargetMaterial, bool clearAllTextures)
        {
            //Return a copy of target material
            Material material = new Material(targetMaterial.shader);

            //Copy all propertyies, if is desired
            if (copyPropertiesOfTargetMaterial == true)
                material.CopyPropertiesFromMaterial(targetMaterial);

            //Clear all textures, is is desired
            if (clearAllTextures == true)
            {
                if (material.HasProperty("_MainTex") == true)
                    material.SetTexture("_MainTex", null);

                if (material.HasProperty("_BaseMap") == true)
                    material.SetTexture("_BaseMap", null);

                if (material.HasProperty("_MetallicGlossMap") == true)
                    material.SetTexture("_MetallicGlossMap", null);

                if (material.HasProperty("_SpecGlossMap") == true)
                    material.SetTexture("_SpecGlossMap", null);

                if (material.HasProperty("_BumpMap") == true)
                    material.SetTexture("_BumpMap", null);

                if (material.HasProperty("_DetailNormalMap") == true)
                    material.SetTexture("_DetailNormalMap", null);

                if (material.HasProperty("_ParallaxMap") == true)
                    material.SetTexture("_ParallaxMap", null);

                if (material.HasProperty("_OcclusionMap") == true)
                    material.SetTexture("_OcclusionMap", null);

                if (material.HasProperty("_DetailMapSupport") == true)
                    material.SetTexture("_DetailMapSupport", null);

                if (material.HasProperty("_DetailMask") == true)
                    material.SetTexture("_DetailMask", null);

                if (material.HasProperty("_Color") == true)
                    material.SetColor("_Color", Color.white);

                if (material.HasProperty("_BaseColor") == true)
                    material.SetColor("_BaseColor", Color.white);
            }

            return material;
        }

        private void MergeAndGetAllBlendShapeDataOfSkinnedMeshRenderers(SkinnedMeshRenderer[] skinnedMeshesToMerge, Mesh finalMesh, SkinnedMeshRenderer finalSkinnedMeshRenderer)
        {
            //Prepare the list of blendshapes processed
            List<BlendShapeData> allBlendShapeData = new List<BlendShapeData>();
            //Prepare the list of already added blendshapes name, to avoid duplicates
            Dictionary<string, int> alreadyAddedBlendshapesNames = new Dictionary<string, int>();

            //Verify each skinned mesh renderer and get info about all blendshapes of all meshes
            int totalVerticesVerifiedAtHereForBlendShapes = 0;
            foreach (SkinnedMeshRenderer combine in skinnedMeshesToMerge)
            {
                //Get all blendshapes names of this mesh
                string[] blendShapes = new string[combine.sharedMesh.blendShapeCount];
                for (int i = 0; i < combine.sharedMesh.blendShapeCount; i++)
                    blendShapes[i] = combine.sharedMesh.GetBlendShapeName(i);

                //Read all blendshapes data of this mesh
                for (int i = 0; i < blendShapes.Length; i++)
                {
                    //Get the current blendshape data
                    BlendShapeData blendShapeData = new BlendShapeData();
                    blendShapeData.blendShapeFrameName = blendShapes[i];
                    blendShapeData.blendShapeFrameIndex = combine.sharedMesh.GetBlendShapeIndex(blendShapes[i]);
                    blendShapeData.blendShapeCurrentValue = combine.GetBlendShapeWeight(blendShapeData.blendShapeFrameIndex);

                    //Get the vertices vector array of this mesh
                    Vector3[] originalDeltaVertices = new Vector3[combine.sharedMesh.vertexCount];
                    Vector3[] originalDeltaNormals = new Vector3[combine.sharedMesh.vertexCount];
                    Vector3[] originalDeltaTangents = new Vector3[combine.sharedMesh.vertexCount];

                    //Get the vertices vector array of final mesh
                    Vector3[] finalDeltaVertices = new Vector3[finalMesh.vertexCount];
                    Vector3[] finalDeltaNormals = new Vector3[finalMesh.vertexCount];
                    Vector3[] finalDeltaTangents = new Vector3[finalMesh.vertexCount];

                    //Fill the blendshape start vertices
                    blendShapeData.startDeltaVertices.AddRange(finalDeltaVertices);
                    blendShapeData.startDeltaNormals.AddRange(finalDeltaNormals);
                    blendShapeData.startDeltaTangents.AddRange(finalDeltaTangents);

                    //Fill the blendshape final vertices
                    blendShapeData.finalDeltaVertices.AddRange(finalDeltaVertices);
                    blendShapeData.finalDeltaNormals.AddRange(finalDeltaNormals);
                    blendShapeData.finalDeltaTangents.AddRange(finalDeltaTangents);

                    //If this mesh have data for this blendshape, get them. otherwise, just ignores and continues with zero values
                    if (combine.sharedMesh.GetBlendShapeIndex(blendShapes[i]) != -1)
                        combine.sharedMesh.GetBlendShapeFrameVertices(blendShapeData.blendShapeFrameIndex, combine.sharedMesh.GetBlendShapeFrameCount(blendShapeData.blendShapeFrameIndex) - 1, originalDeltaVertices, originalDeltaNormals, originalDeltaTangents);
                    if (combine.sharedMesh.GetBlendShapeIndex(blendShapes[i]) == -1)
                        LaunchLog("Mesh data could not be found in Blendshape \"" + blendShapes[i] + "\". This Blendshape may not work on the mesh resulting from the merge.", LogTypeOf.Warning);

                    //Fill the final blendshape vertices, where vertices that this blendshape will modify only, get from original vertices, normals and tangents
                    //Vertices
                    for (int x = 0; x < originalDeltaVertices.Length; x++)
                        blendShapeData.finalDeltaVertices[x + totalVerticesVerifiedAtHereForBlendShapes] = originalDeltaVertices[x] * blendShapesMultiplier;
                    //Normals
                    for (int x = 0; x < originalDeltaNormals.Length; x++)
                        blendShapeData.finalDeltaNormals[x + totalVerticesVerifiedAtHereForBlendShapes] = originalDeltaNormals[x] * blendShapesMultiplier;
                    //Tangents
                    for (int x = 0; x < originalDeltaTangents.Length; x++)
                        blendShapeData.finalDeltaTangents[x + totalVerticesVerifiedAtHereForBlendShapes] = originalDeltaTangents[x] * blendShapesMultiplier;

                    //Add this blendshape to merge
                    allBlendShapeData.Add(blendShapeData);
                }

                //Set vertices verified at here, after process all blendshapes for this mesh
                totalVerticesVerifiedAtHereForBlendShapes += combine.sharedMesh.vertexCount;
            }

            //Finally add all processed blendshapes of all meshes, into the final skinned mesh renderer
            foreach (BlendShapeData blendShape in allBlendShapeData)
            {
                //Prepare the blendshape name
                StringBuilder blendShapeName = new StringBuilder();
                blendShapeName.Append(blendShape.blendShapeFrameName);
                if (alreadyAddedBlendshapesNames.ContainsKey(blendShape.blendShapeFrameName) == true)
                {
                    blendShapeName.Append(" (");
                    blendShapeName.Append(alreadyAddedBlendshapesNames[blendShape.blendShapeFrameName]);
                    blendShapeName.Append(")");
                    LaunchLog("The Blendshape with the name of \"" + blendShape.blendShapeFrameName + "\" was found in more than one mesh. This would generate duplicates of the same Blendshape in the mesh resulting from the merge, so Blendshape \"" + blendShape.blendShapeFrameName + "\" (duplicate) received a duplicate counter (for example \"" + blendShape.blendShapeFrameName + " (0)\"). This will keep all Blendshapes working.", LogTypeOf.Warning);
                }

                //Add the start frame and final frame of current blendshape
                finalMesh.AddBlendShapeFrame(blendShapeName.ToString(), 0.0f, blendShape.startDeltaVertices.ToArray(), blendShape.startDeltaNormals.ToArray(), blendShape.startDeltaTangents.ToArray());
                finalMesh.AddBlendShapeFrame(blendShapeName.ToString(), 100.0f, blendShape.finalDeltaVertices.ToArray(), blendShape.finalDeltaNormals.ToArray(), blendShape.finalDeltaTangents.ToArray());

                //Save the name of this new blendshape, on the combined mesh, to sync later
                blendShape.blendShapeNameOnCombinedMesh = blendShapeName.ToString();

                //Add information that already added this blendshape name
                if (alreadyAddedBlendshapesNames.ContainsKey(blendShape.blendShapeFrameName) == true)
                    alreadyAddedBlendshapesNames[blendShape.blendShapeFrameName] += 1;
                if (alreadyAddedBlendshapesNames.ContainsKey(blendShape.blendShapeFrameName) == false)
                    alreadyAddedBlendshapesNames.Add(blendShape.blendShapeFrameName, 0);
            }

            //Now sync values of original blendshapes to merged blendshapes
            if (blendShapesSupport == BlendShapesSupport.FullSupport)
                foreach (BlendShapeData blendShape in allBlendShapeData)
                    if (blendShape.blendShapeCurrentValue > 0.0f)
                        finalSkinnedMeshRenderer.SetBlendShapeWeight(finalMesh.GetBlendShapeIndex(blendShape.blendShapeNameOnCombinedMesh), blendShape.blendShapeCurrentValue);
        }

        private Transform GetCorrectRootBoneFromAllOriginalSkinnedMeshRenderers(SkinnedMeshRenderer[] skinnedMeshesToMerge)
        {
            //If root bone to use is manual
            if (rootBoneToUse == RootBoneToUse.Manual)
                return manualRootBoneToUse;
            //If root bone to  use is automatic
            if (rootBoneToUse == RootBoneToUse.Automatic)
            {
                //Root bone to use
                Transform rootBoneToUse = null;

                //Create the dictionary of most used root bones
                Dictionary<Transform, int> rootBones = new Dictionary<Transform, int>();

                //Fill the dictionary
                foreach (SkinnedMeshRenderer render in skinnedMeshesToMerge)
                    if (render != null)
                        if (render.rootBone != null)
                        {
                            if (rootBones.ContainsKey(render.rootBone) == true)
                                rootBones[render.rootBone] += 1;
                            if (rootBones.ContainsKey(render.rootBone) == false)
                                rootBones.Add(render.rootBone, 1);
                        }

                //Verify the most used root bone, set the most used root bone, to be returned
                int lastBoneUsesTime = 0;
                foreach (var key in rootBones)
                    if (key.Value > lastBoneUsesTime)
                    {
                        rootBoneToUse = key.Key;
                        lastBoneUsesTime = key.Value;
                    }

                //Return the root bone to use
                return rootBoneToUse;
            }
            return null;
        }

        //Tools methos for All In One merge method

        private ColorData GetDefaultAndNeutralColorForThisTexture(TextureType textureType)
        {
            //Return the neutral color for texture type
            switch (textureType)
            {
                case TextureType.MainTexture:
                    return new ColorData("RED", Color.red);
                case TextureType.MetallicMap:
                    return new ColorData("BLACK", Color.black);
                case TextureType.SpecularMap:
                    return new ColorData("BLACK", Color.black);
                case TextureType.NormalMap:
                    return new ColorData("PURPLE", new Color(128.0f / 255.0f, 128.0f / 255.0f, 255.0f / 255.0f, 255.0f / 255.0f));
                case TextureType.HeightMap:
                    return new ColorData("BLACK", Color.black);
                case TextureType.OcclusionMap:
                    return new ColorData("WHITE", Color.white);
                case TextureType.DetailMap:
                    return new ColorData("GRAY", Color.gray);
                case TextureType.DetailMask:
                    return new ColorData("WHITE", Color.white);
            }
            return new ColorData("RED", Color.red);
        }

        private int UvBoundToPixels(float uvSize, int textureSize)
        {
            return (int)(uvSize * (float)textureSize);
        }

        private float[] UvBoundSplitted(float uvSize)
        {
            //Convert to positive
            if (uvSize < 0.0f)
                uvSize = uvSize * -1.0f;
            //Result
            float[] result = new float[2];
            //Split
            string[] str = uvSize.ToString().Split(',');
            //Get result
            result[0] = float.Parse(str[0], CultureInfo.InvariantCulture);
            result[1] = 0.0f;
            if (str.Length > 1)
                result[1] = float.Parse("0," + str[1], CultureInfo.InvariantCulture);
            return result;
        }

        private Texture2D GetValidatedCopyOfTexture(Material materialToFindTexture, string propertyToFindTexture, int widthOfCorrespondentMainTexture, int heightOfCorrespondentMainTexture, TexturesSubMeshes.UvBounds boundsUvValues, TextureType textureType, bool showProgress, float progress)
        {
            //Show progress
            if (showProgress == true)
                ShowProgressBar("Copying " + propertyToFindTexture, true, progress);

            //-------------------------------------------- Create a refereference to target texture
            //Try to get the texture of material
            Texture2D targetTexture = null;
            materialToFindTexture.EnableKeyword(propertyToFindTexture);

            //If found the property of texture
            if (materialToFindTexture.HasProperty(propertyToFindTexture) == true && materialToFindTexture.GetTexture(propertyToFindTexture) != null)
                targetTexture = (Texture2D)materialToFindTexture.GetTexture(propertyToFindTexture);

            //If not found the property of texture
            if (materialToFindTexture.HasProperty(propertyToFindTexture) == false || materialToFindTexture.GetTexture(propertyToFindTexture) == null)
            {
                //Get the default and neutral color for this texture
                ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);
                //Launch log
                LaunchLog("It was not possible to find the texture stored in property \"" + propertyToFindTexture + "\" of material \"" + materialToFindTexture.name + "\", so this Texture/Map was replaced by a " + defaultColor.colorName + " texture. This can affect how the texture or effect maps (such as Normal Maps, etc.) are displayed in the combined model. This can result in some small differences in the combined mesh when compared to the separate original meshes.", LogTypeOf.Warning);
                //Create a fake texture blank
                targetTexture = new Texture2D(widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture);
                //Create blank pixels
                Color[] colors = new Color[widthOfCorrespondentMainTexture * heightOfCorrespondentMainTexture];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = defaultColor.color;
                //Apply all pixels in void texture
                targetTexture.SetPixels(0, 0, widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture, colors, 0);
            }

            //-------------------------------------------- Start the creation of copyied texture
            //Prepare the storage for this texture that will be copyied
            Texture2D thisTexture = null;

            //If the texture is readable
            try
            {
                //-------------------------------------------- Calculate the size of copyied texture
                //Get desired edges size for each texture of atlas
                int edgesSize = GetEdgesSizeForTextures();

                //Calculate a preview of the total and final size of texture...
                int texWidth = 0;
                int texHeight = 0;
                int maxSizeOfTextures = 16384;
                bool overcameTheLimitationOf16k = false;
                //If is a normal texture
                if (isTiledTexture(boundsUvValues) == false)
                {
                    texWidth = edgesSize + targetTexture.width + edgesSize;
                    texHeight = edgesSize + targetTexture.height + edgesSize;
                }
                //If is a tiled texture
                if (isTiledTexture(boundsUvValues) == true)
                {
                    texWidth = edgesSize + UvBoundToPixels(boundsUvValues.spaceMinorX, targetTexture.width) + targetTexture.width + UvBoundToPixels(boundsUvValues.spaceMajorX, targetTexture.width) + edgesSize;
                    texHeight = edgesSize + UvBoundToPixels(boundsUvValues.spaceMinorY, targetTexture.height) + targetTexture.height + UvBoundToPixels(boundsUvValues.spaceMajorY, targetTexture.height) + edgesSize;
                }
                //Verify if the size of texture, as overcamed the limitation of 16384 pixels of Unity
                if (texWidth >= maxSizeOfTextures || texHeight >= maxSizeOfTextures)
                    overcameTheLimitationOf16k = true;
                //If overcamed the limitation of texture sizes of unity, create a texture with the size of target texture
                if (overcameTheLimitationOf16k == true)
                {
                    //Get the default and neutral color for this texture
                    ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);
                    if (String.IsNullOrEmpty(targetTexture.name) == false)
                        LaunchLog("It was not possible to process the \"" + targetTexture.name + "\" texture, as its size during processing was greater than Unity's " + maxSizeOfTextures.ToString() + " pixel limitation. This may have happened because its texture is larger than " + maxSizeOfTextures.ToString() + " pixels, or because the tiling of any of its meshes is too extensive. Try to use a texture smaller than " + maxSizeOfTextures.ToString() + " pixels and if that persists, skip any meshes that land a very large tile. During processing this texture reached the size of " + texWidth.ToString() + "x" + texHeight.ToString() + " pixels. This texture has been replaced by a simple texture of color " + defaultColor.colorName + ".", LogTypeOf.Warning);
                    texWidth = targetTexture.width;
                    texHeight = targetTexture.height;
                }
                //Create the texture with size calculated above
                thisTexture = new Texture2D(texWidth, texHeight, TextureFormat.ARGB32, false, false);

                //-------------------------------------------- Copy all original pixels from target texture reference
                //Copy all pixels of the target texture
                Color32[] targetTexturePixels = targetTexture.GetPixels32(0);
                //If pink normal maps fix is enabled. If this is a normal map, try to get colors using different decoding (if have a compression format that uses different channels to store colors)
                if (allInOneParams.pinkNormalMapsFix == true && textureType == TextureType.NormalMap && targetTexture.format == TextureFormat.DXT5)
                    for (int i = 0; i < targetTexturePixels.Length; i++)
                    {
                        Color c = targetTexturePixels[i];
                        c.r = c.a * 2 - 1;  //red<-alpha (x<-w)
                        c.g = c.g * 2 - 1; //green is always the same (y)
                        Vector2 xy = new Vector2(c.r, c.g); //this is the xy vector
                        c.b = Mathf.Sqrt(1 - Mathf.Clamp01(Vector2.Dot(xy, xy))); //recalculate the blue channel (z)
                        targetTexturePixels[i] = new Color(c.r * 0.5f + 0.5f, c.g * 0.5f + 0.5f, c.b * 0.5f + 0.5f); //back to 0-1 range
                    }

                //-------------------------------------------- Create a simple texture if the size of this copy texture has exceeded the limitation
                //Apply the copyied pixels to this texture, if is a texture that overcamed the limitation of pixels
                if (overcameTheLimitationOf16k == true)
                {
                    //Get the default color of this type of texture
                    ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);
                    //Create blank pixels
                    Color[] colors = new Color[targetTexture.width * targetTexture.height];
                    for (int i = 0; i < colors.Length; i++)
                        colors[i] = defaultColor.color;
                    //Apply all pixels in void texture
                    thisTexture.SetPixels(0, 0, targetTexture.width, targetTexture.height, colors, 0);
                }
                //-------------------------------------------- Create a copy of target texture, if this copy of texture is a normal texture without tiling
                //Apply the copyied pixels to this texture if is normal texture
                if (isTiledTexture(boundsUvValues) == false && overcameTheLimitationOf16k == false)
                    thisTexture.SetPixels32(edgesSize, edgesSize, targetTexture.width, targetTexture.height, targetTexturePixels, 0);
                //-------------------------------------------- Create a copy of target texture with support to tiling, if this copy texture not exceed the limitation size of unity
                //Apply the copyied pixels to this texture if is a tiled texture, start the simulated texture tiles
                if (isTiledTexture(boundsUvValues) == true && overcameTheLimitationOf16k == false)
                {
                    //Show progress custom for tilling
                    if (showProgress == true)
                        ShowProgressBar("Copying and Generating Tiles for " + propertyToFindTexture, true, progress);

                    //Prepare the vars
                    Color[] tempColorBlock = null;

                    //Add the left border
                    tempColorBlock = targetTexture.GetPixels(
                        targetTexture.width - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), 0,
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), targetTexture.height, 0);
                    for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1; i++)
                        thisTexture.SetPixels(
                            edgesSize, edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorY)[1], targetTexture.height) + (i * targetTexture.height),
                            UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorX)[1], targetTexture.width), targetTexture.height, tempColorBlock, 0);

                    //Fill the texture with repeated original textures
                    tempColorBlock = targetTexture.GetPixels(0, 0, targetTexture.width, targetTexture.height, 0);
                    for (int x = 0; x < UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1; x++)
                        for (int y = 0; y < UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1; y++)
                            thisTexture.SetPixels(
                                edgesSize + (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.spaceMinorX)[1]) + (x * targetTexture.width),
                                edgesSize + (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.spaceMinorY)[1]) + (y * targetTexture.height),
                                targetTexture.width, targetTexture.height, tempColorBlock, 0);

                    //Add the right border
                    tempColorBlock = targetTexture.GetPixels(0, 0, (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), targetTexture.height, 0);
                    for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1; i++)
                        thisTexture.SetPixels(
                            edgesSize + (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.spaceMinorX)[1]) + (((int)UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + (int)UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1) * targetTexture.width),
                            edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorY)[1], targetTexture.height) + (i * targetTexture.height),
                            UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMajorX)[1], targetTexture.width), targetTexture.height, tempColorBlock, 0);

                    //Add the bottom border
                    tempColorBlock = targetTexture.GetPixels(
                        0, targetTexture.height - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                        targetTexture.width, (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]), 0);
                    for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1; i++)
                        thisTexture.SetPixels(
                            edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorX)[1], targetTexture.width) + (i * targetTexture.width), edgesSize,
                            targetTexture.width, UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorY)[1], targetTexture.height), tempColorBlock, 0);

                    //Add the top border
                    tempColorBlock = targetTexture.GetPixels(0, 0, targetTexture.width, (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]), 0);
                    for (int i = 0; i < UvBoundSplitted(boundsUvValues.spaceMinorX)[0] + UvBoundSplitted(boundsUvValues.spaceMajorX)[0] + 1; i++)
                        thisTexture.SetPixels(
                            edgesSize + UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMinorX)[1], targetTexture.width) + (i * targetTexture.width),
                            edgesSize + (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.spaceMinorY)[1]) + (((int)UvBoundSplitted(boundsUvValues.spaceMinorY)[0] + (int)UvBoundSplitted(boundsUvValues.spaceMajorY)[0] + 1) * targetTexture.height),
                            targetTexture.width, UvBoundToPixels(UvBoundSplitted(boundsUvValues.spaceMajorY)[1], targetTexture.height), tempColorBlock, 0);

                    //Add the bottom left corner
                    tempColorBlock = targetTexture.GetPixels(
                        targetTexture.width - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                        targetTexture.height - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                        (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                        0);
                    thisTexture.SetPixels(edgesSize, edgesSize, (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]), tempColorBlock, 0);

                    //Add the bottom right corner
                    tempColorBlock = targetTexture.GetPixels(
                        0,
                        targetTexture.height - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]),
                        (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]),
                        0);
                    thisTexture.SetPixels(
                        thisTexture.width - edgesSize - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), edgesSize,
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.minorY)[1]), tempColorBlock, 0);

                    //Add the top left corner
                    tempColorBlock = targetTexture.GetPixels(
                        targetTexture.width - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                        0,
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]),
                        (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                        0);
                    thisTexture.SetPixels(
                        edgesSize, thisTexture.height - edgesSize - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.minorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]), tempColorBlock, 0);

                    //Add the top right corner
                    tempColorBlock = targetTexture.GetPixels(
                        0,
                        0,
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]),
                        (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                        0);
                    thisTexture.SetPixels(
                        thisTexture.width - edgesSize - (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), thisTexture.height - edgesSize - (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]),
                        (int)((float)targetTexture.width * UvBoundSplitted(boundsUvValues.majorX)[1]), (int)((float)targetTexture.height * UvBoundSplitted(boundsUvValues.majorY)[1]), tempColorBlock, 0);
                }

                //-------------------------------------------- Create the edges of copy texture, to support mip maps
                //If the edges size is minor than target texture size, uses the "SetPixels and GetPixels" to guarantee a faster copy
                if (edgesSize <= targetTexture.width && edgesSize <= targetTexture.height && overcameTheLimitationOf16k == false)
                {
                    //Prepare the var
                    Color[] copyiedPixels = null;

                    //Copy right border to left of current texture
                    copyiedPixels = thisTexture.GetPixels(thisTexture.width - edgesSize - edgesSize, 0, edgesSize, thisTexture.height, 0);
                    thisTexture.SetPixels(0, 0, edgesSize, thisTexture.height, copyiedPixels, 0);

                    //Copy left(original) border to right of current texture
                    copyiedPixels = thisTexture.GetPixels(edgesSize, 0, edgesSize, thisTexture.height, 0);
                    thisTexture.SetPixels(thisTexture.width - edgesSize, 0, edgesSize, thisTexture.height, copyiedPixels, 0);

                    //Copy bottom (original) border to top of current texture
                    copyiedPixels = thisTexture.GetPixels(0, edgesSize, thisTexture.width, edgesSize, 0);
                    thisTexture.SetPixels(0, thisTexture.height - edgesSize, thisTexture.width, edgesSize, copyiedPixels, 0);

                    //Copy top (original) border to bottom of current texture
                    copyiedPixels = thisTexture.GetPixels(0, thisTexture.height - edgesSize - edgesSize, thisTexture.width, edgesSize, 0);
                    thisTexture.SetPixels(0, 0, thisTexture.width, edgesSize, copyiedPixels, 0);
                }

                //If the edges size is major than target texture size, uses the "SetPixel and GetPixel" to repeat copy of pixels in target texture
                if (edgesSize > targetTexture.width || edgesSize > targetTexture.height && overcameTheLimitationOf16k == false)
                {
                    //Show the warning
                    LaunchLog("You have selected a texture border size (" + edgesSize + "px), where the border size is larger than this texture (\"" + targetTexture.name + "\" " + targetTexture.width + "x" + targetTexture.height + "px) size itself, causing this texture to repeat in the atlas. This increased the merging time due to the need for a new algorithm for creating the borders. It is recommended that the size of the edges of the textures in the atlas, does not exceed the size of the textures themselves.", LogTypeOf.Warning);

                    //Copy right (original) border to left of current texture
                    for (int x = 0; x < edgesSize; x++)
                        for (int y = 0; y < thisTexture.height; y++)
                            thisTexture.SetPixel(x, y, targetTexture.GetPixel((targetTexture.width - edgesSize - edgesSize) + x, y));

                    //Copy left(original) border to right of current texture
                    for (int x = thisTexture.width - edgesSize; x < thisTexture.width; x++)
                        for (int y = 0; y < thisTexture.height; y++)
                            thisTexture.SetPixel(x, y, targetTexture.GetPixel(targetTexture.width - x, y));

                    //Copy bottom (original) border to top of current texture
                    for (int x = 0; x < thisTexture.width; x++)
                        for (int y = 0; y < edgesSize; y++)
                            thisTexture.SetPixel(x, y, targetTexture.GetPixel(x, (targetTexture.width - edgesSize) + y));

                    //Copy top (original) border to bottom of current texture
                    for (int x = 0; x < thisTexture.width; x++)
                        for (int y = thisTexture.height - edgesSize; y < thisTexture.height; y++)
                            thisTexture.SetPixel(x, y, targetTexture.GetPixel(x, edgesSize - (targetTexture.height - y)));
                }
            }
            //If the texture is not readable
            catch (UnityException e)
            {
                if (e.Message.StartsWith("Texture '" + targetTexture.name + "' is not readable"))
                {
                    //Get the default and neutral color for this texture
                    ColorData defaultColor = GetDefaultAndNeutralColorForThisTexture(textureType);

                    //Create the texture
                    thisTexture = new Texture2D(widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture, TextureFormat.ARGB32, false, false);

                    //Create blank pixels
                    Color[] colors = new Color[widthOfCorrespondentMainTexture * heightOfCorrespondentMainTexture];
                    for (int i = 0; i < colors.Length; i++)
                        colors[i] = defaultColor.color;

                    //Apply all pixels in void texture
                    thisTexture.SetPixels(0, 0, widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture, colors, 0);

                    //Launch logs
                    LaunchLog("It was not possible to combine texture \"" + targetTexture.name + "\" within an atlas, as it is not marked as \"Readable\" in the import settings (\"Read/Write Enabled\"). The texture has been replaced with a " + defaultColor.colorName + " one.", LogTypeOf.Error);
                }
            }

            //-------------------------------------------- Calculate the use of edges of this texture, in percent
            //Only calculate if is the main texture, because main texture is more important and is the base texture for all uv mapping and calcs
            if (textureType == TextureType.MainTexture)
            {
                boundsUvValues.edgesUseX = (float)GetEdgesSizeForTextures() / (float)thisTexture.width;
                boundsUvValues.edgesUseY = (float)GetEdgesSizeForTextures() / (float)thisTexture.height;
            }

            //-------------------------------------------- Finally, resize the copy texture to mantain size equal to targe texture with edges
            //If this texture have the size differente of correspondent main texture size, resize it to be equal to main texture 
            if (thisTexture.width != widthOfCorrespondentMainTexture || thisTexture.height != heightOfCorrespondentMainTexture)
                SMCTextureResizer.Bilinear(thisTexture, widthOfCorrespondentMainTexture, heightOfCorrespondentMainTexture);

            //Return the texture 
            return thisTexture;
        }

        private int GetAtlasMaxResolution()
        {
            //If is All In One
            if (mergeMethod == MergeMethod.AllInOne)
            {
                switch (allInOneParams.atlasResolution)
                {
                    case AtlasSize.Pixels32x32:
                        return 32;
                    case AtlasSize.Pixels64x64:
                        return 64;
                    case AtlasSize.Pixels128x128:
                        return 128;
                    case AtlasSize.Pixels256x256:
                        return 256;
                    case AtlasSize.Pixels512x512:
                        return 512;
                    case AtlasSize.Pixels1024x1024:
                        return 1024;
                    case AtlasSize.Pixels2048x2048:
                        return 2048;
                    case AtlasSize.Pixels4096x4096:
                        return 4096;
                    case AtlasSize.Pixels8192x8192:
                        return 8192;
                }
            }

            //Return the max resolution
            return 16;
        }

        private AtlasData CreateAllAtlas(List<TexturesSubMeshes> copyiedTextures, int maxResolution, int paddingBetweenTextures, bool showProgress)
        {
            //Create a atlas
            AtlasData atlasData = new AtlasData();
            List<Texture2D> texturesToUse = new List<Texture2D>();

            //Create the base atlas with main textures
            if (showProgress == true)
                ShowProgressBar("Creating Base Atlas...", true, 1.0f);
            texturesToUse.Clear();
            foreach (TexturesSubMeshes item in copyiedTextures)
                texturesToUse.Add(item.mainTexture);
            atlasData.originalMainTexturesUsedAndOrdenedAccordingToAtlasRect = texturesToUse.ToArray();
            atlasData.atlasRects = atlasData.mainTextureAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);

            //Create the metallic atlas if is desired
            if (allInOneParams.metallicMapSupport == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Metallic Map Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.metallicMap);
                atlasData.metallicMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Create the specullar atlas if is desired
            if (allInOneParams.specularMapSupport == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Specular Map Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.specularMap);
                atlasData.specularMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Create the normal atlas if is desired
            if (allInOneParams.normalMapSupport == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Normal Map Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.normalMap);
                atlasData.normalMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Create the normal 2 atlas if is desired
            if (allInOneParams.normalMap2Support == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Normal Map 2 Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.normalMap2);
                atlasData.normalMap2Atlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Create the height atlas if is desired
            if (allInOneParams.heightMapSupport == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Height Map Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.heightMap);
                atlasData.heightMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Create the occlusion atlas if is desired
            if (allInOneParams.occlusionMapSupport == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Occlusion Map Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.occlusionMap);
                atlasData.occlusionMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Create the detail atlas if is desired
            if (allInOneParams.detailAlbedoMapSupport == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Detail Map Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.detailMap);
                atlasData.detailMapAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Create the detail mask if is desired
            if (allInOneParams.detailMaskSupport == true)
            {
                if (showProgress == true)
                    ShowProgressBar("Creating Detail Mask Atlas...", true, 1.0f);
                texturesToUse.Clear();
                foreach (TexturesSubMeshes item in copyiedTextures)
                    texturesToUse.Add(item.detailMask);
                atlasData.detailMaskAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);
            }

            //Return the object
            return atlasData;
        }

        private void ApplyAtlasInPropertyOfMaterial(Material targetMaterial, string propertyToInsertTexture, Texture2D atlasTexture)
        {
            //If found the property
            if (targetMaterial.HasProperty(propertyToInsertTexture) == true)
            {
                //Try to enable this different keyword
                if (targetMaterial.IsKeywordEnabled(propertyToInsertTexture) == false)
                    targetMaterial.EnableKeyword(propertyToInsertTexture);

                //Apply the texture
                targetMaterial.SetTexture(propertyToInsertTexture, atlasTexture);

                //Try to enable this different keyword
                if (targetMaterial.IsKeywordEnabled(propertyToInsertTexture) == false)
                    targetMaterial.EnableKeyword(propertyToInsertTexture);

                //Forces enable all keyword, where is necessary
                if (propertyToInsertTexture == "_MetallicGlossMap" && targetMaterial.IsKeywordEnabled("_METALLICGLOSSMAP") == false && allInOneParams.metallicMapSupport == true)
                    targetMaterial.EnableKeyword("_METALLICGLOSSMAP");

                if (propertyToInsertTexture == "_SpecGlossMap" && targetMaterial.IsKeywordEnabled("_SPECGLOSSMAP") == false && allInOneParams.specularMapSupport == true)
                    targetMaterial.EnableKeyword("_SPECGLOSSMAP");

                if (propertyToInsertTexture == "_BumpMap" && targetMaterial.IsKeywordEnabled("_NORMALMAP") == false && allInOneParams.normalMapSupport == true)
                    targetMaterial.EnableKeyword("_NORMALMAP");

                if (propertyToInsertTexture == "_ParallaxMap" && targetMaterial.IsKeywordEnabled("_PARALLAXMAP") == false && allInOneParams.heightMapSupport == true)
                    targetMaterial.EnableKeyword("_PARALLAXMAP");

                if (propertyToInsertTexture == "_OcclusionMap" && targetMaterial.IsKeywordEnabled("_OcclusionMap") == false && allInOneParams.occlusionMapSupport == true)
                    targetMaterial.EnableKeyword("_OcclusionMap");

                if (propertyToInsertTexture == "_DetailAlbedoMap" && targetMaterial.IsKeywordEnabled("_DETAIL_MULX2") == false && allInOneParams.detailAlbedoMapSupport == true)
                    targetMaterial.EnableKeyword("_DETAIL_MULX2");

                if (propertyToInsertTexture == "_DetailNormalMap" && targetMaterial.IsKeywordEnabled("_DETAIL_MULX2") == false && allInOneParams.normalMap2Support == true)
                    targetMaterial.EnableKeyword("_DETAIL_MULX2");
            }
            //If not found the property
            if (targetMaterial.HasProperty(propertyToInsertTexture) == false)
                LaunchLog("It was not possible to find and apply the atlas on property \"" + propertyToInsertTexture + "\" of the material to use (\"" + targetMaterial.name + "\"). Therefore, no atlas was applied to this property.", LogTypeOf.Error);
        }

        private int GetEdgesSizeForTextures()
        {
            //If is All In One
            if (mergeMethod == MergeMethod.AllInOne)
            {
                switch (allInOneParams.mipMapEdgesSize)
                {
                    case MipMapEdgesSize.Pixels0x0:
                        return 0;
                    case MipMapEdgesSize.Pixels16x16:
                        return 16;
                    case MipMapEdgesSize.Pixels32x32:
                        return 32;
                    case MipMapEdgesSize.Pixels64x64:
                        return 64;
                    case MipMapEdgesSize.Pixels128x128:
                        return 128;
                    case MipMapEdgesSize.Pixels256x256:
                        return 256;
                    case MipMapEdgesSize.Pixels512x512:
                        return 512;
                    case MipMapEdgesSize.Pixels1024x1024:
                        return 1024;
                }
            }

            //Return the max resolution
            return 2;
        }

        private int GetAtlasPadding()
        {
            //If is All In One
            if (mergeMethod == MergeMethod.AllInOne)
            {
                switch (allInOneParams.atlasPadding)
                {
                    case AtlasPadding.Pixels0x0:
                        return 0;
                    case AtlasPadding.Pixels2x2:
                        return 2;
                    case AtlasPadding.Pixels4x4:
                        return 4;
                    case AtlasPadding.Pixels8x8:
                        return 8;
                    case AtlasPadding.Pixels16x16:
                        return 16;
                }
            }

            //Return the max resolution
            return 0;
        }

        private TexturesSubMeshes.UvBounds GetBoundValuesOfSubMeshUv(Vector2[] subMeshUv)
        {
            //Create the data size
            TexturesSubMeshes.UvBounds uvBounds = new TexturesSubMeshes.UvBounds();

            //Prepare the arrays
            float[] xAxis = new float[subMeshUv.Length];
            float[] yAxis = new float[subMeshUv.Length];

            //Fill all
            for (int i = 0; i < subMeshUv.Length; i++)
            {
                xAxis[i] = subMeshUv[i].x;
                yAxis[i] = subMeshUv[i].y;
            }

            //Return the data size
            uvBounds.majorX = Mathf.Max(xAxis);
            uvBounds.majorY = Mathf.Max(yAxis);
            uvBounds.minorX = Mathf.Min(xAxis);
            uvBounds.minorY = Mathf.Min(yAxis);
            return uvBounds;
        }

        private TexturesSubMeshes GetTheTextureSubMeshesOfMaterial(Material material, List<TexturesSubMeshes> listOfTexturesAndSubMeshes)
        {
            //Run a loop to return the texture and respective submeshes that use this material
            foreach (TexturesSubMeshes item in listOfTexturesAndSubMeshes)
                if (item.material == material && item.isTiledTexture == false)
                    return item;

            //If not found a item with this material, return null
            return null;
        }

        private bool isTiledTexture(TexturesSubMeshes.UvBounds bounds)
        {
            //Return if the bounds is major than one
            if (bounds.minorX < 0 || bounds.minorY < 0 || bounds.majorX > 1 || bounds.majorY > 1)
                return true;
            if (bounds.minorX >= 0 && bounds.minorY >= 0 && bounds.majorX <= 1 && bounds.majorY <= 1)
                return false;
            return false;
        }

        //Tools methods for Just Material Colors

        private Texture2D GetTextureFilledWithColorOfMaterial(Material targetMaterial, string colorPropertyToFind, int width, int height)
        {
            //Prepares the new texture, and color to fill the texture
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false, false);
            Color colorToFillTexture = Color.white;

            //If found the property of color
            if (targetMaterial.HasProperty(colorPropertyToFind) == true)
                colorToFillTexture = targetMaterial.GetColor(colorPropertyToFind);

            //If not found the property of color
            if (targetMaterial.HasProperty(colorPropertyToFind) == false)
            {
                //Launch log
                LaunchLog("It was not possible to find the color stored in property \"" + colorPropertyToFind + "\" of material \"" + targetMaterial.name + "\", so this Color was replaced by a GRAY texture.", LogTypeOf.Warning);

                //Set the fake color
                colorToFillTexture = Color.gray;
            }

            //Create all pixels
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = colorToFillTexture;

            //Fill the texture
            texture.SetPixels(0, 0, width, height, pixels, 0);

            //Return the texture
            return texture;
        }

        private ColorAtlasData CreateColorAtlas(UvDataAndColorOfThisSubmesh[] uvDatasAndColors, int maxResolution, int paddingBetweenTextures, bool showProgress)
        {
            //Create a atlas
            ColorAtlasData atlasData = new ColorAtlasData();
            List<Texture2D> texturesToUse = new List<Texture2D>();

            //Create the base atlas with main textures
            if (showProgress == true)
                ShowProgressBar("Creating Colors Atlas...", true, 1.0f);
            texturesToUse.Clear();
            foreach (UvDataAndColorOfThisSubmesh item in uvDatasAndColors)
                texturesToUse.Add(item.textureColor);
            atlasData.originalTexturesUsedAndOrdenedAccordingToAtlasRect = texturesToUse.ToArray();
            atlasData.atlasRects = atlasData.colorAtlas.PackTextures(texturesToUse.ToArray(), paddingBetweenTextures, maxResolution);

            //Return the object
            return atlasData;
        }

        //Tools methods for OnlyAnima2dMeshes

        private int GetAtlasMaxResolutionAnima2D()
        {
            //If is All In One
            if (mergeMethod == MergeMethod.OnlyAnima2dMeshes)
            {
                switch (onlyAnima2dMeshes.atlasResolution)
                {
                    case AtlasSize.Pixels32x32:
                        return 32;
                    case AtlasSize.Pixels64x64:
                        return 64;
                    case AtlasSize.Pixels128x128:
                        return 128;
                    case AtlasSize.Pixels256x256:
                        return 256;
                    case AtlasSize.Pixels512x512:
                        return 512;
                    case AtlasSize.Pixels1024x1024:
                        return 1024;
                    case AtlasSize.Pixels2048x2048:
                        return 2048;
                    case AtlasSize.Pixels4096x4096:
                        return 4096;
                    case AtlasSize.Pixels8192x8192:
                        return 8192;
                }
            }

            //Return the max resolution
            return 16;
        }
        #endregion
    }

    #region MESH_CLASS_EXTENSION
    public static class SMCMeshClassExtension
    {
        /*
         * This is an extension class, which adds extra functions to the Mesh class. For example, counting vertices for each submesh.
         */

        public class Vertices
        {
            List<Vector3> verts = null;
            List<Vector2> uv1 = null;
            List<Vector2> uv2 = null;
            List<Vector2> uv3 = null;
            List<Vector2> uv4 = null;
            List<Vector3> normals = null;
            List<Vector4> tangents = null;
            List<Color32> colors = null;
            List<BoneWeight> boneWeights = null;

            public Vertices()
            {
                verts = new List<Vector3>();
            }

            public Vertices(Mesh aMesh)
            {
                verts = CreateList(aMesh.vertices);
                uv1 = CreateList(aMesh.uv);
                uv2 = CreateList(aMesh.uv2);
                uv3 = CreateList(aMesh.uv3);
                uv4 = CreateList(aMesh.uv4);
                normals = CreateList(aMesh.normals);
                tangents = CreateList(aMesh.tangents);
                colors = CreateList(aMesh.colors32);
                boneWeights = CreateList(aMesh.boneWeights);
            }

            private List<T> CreateList<T>(T[] aSource)
            {
                if (aSource == null || aSource.Length == 0)
                    return null;
                return new List<T>(aSource);
            }

            private void Copy<T>(ref List<T> aDest, List<T> aSource, int aIndex)
            {
                if (aSource == null)
                    return;
                if (aDest == null)
                    aDest = new List<T>();
                aDest.Add(aSource[aIndex]);
            }

            public int Add(Vertices aOther, int aIndex)
            {
                int i = verts.Count;
                Copy(ref verts, aOther.verts, aIndex);
                Copy(ref uv1, aOther.uv1, aIndex);
                Copy(ref uv2, aOther.uv2, aIndex);
                Copy(ref uv3, aOther.uv3, aIndex);
                Copy(ref uv4, aOther.uv4, aIndex);
                Copy(ref normals, aOther.normals, aIndex);
                Copy(ref tangents, aOther.tangents, aIndex);
                Copy(ref colors, aOther.colors, aIndex);
                Copy(ref boneWeights, aOther.boneWeights, aIndex);
                return i;
            }

            public void AssignTo(Mesh aTarget)
            {
                //Removes the limitation of 65k vertices, in case Unity supports.
                if (verts.Count > 65535)
                    aTarget.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                aTarget.SetVertices(verts);
                if (uv1 != null) aTarget.SetUVs(0, uv1);
                if (uv2 != null) aTarget.SetUVs(1, uv2);
                if (uv3 != null) aTarget.SetUVs(2, uv3);
                if (uv4 != null) aTarget.SetUVs(3, uv4);
                if (normals != null) aTarget.SetNormals(normals);
                if (tangents != null) aTarget.SetTangents(tangents);
                if (colors != null) aTarget.SetColors(colors);
                if (boneWeights != null) aTarget.boneWeights = boneWeights.ToArray();
            }
        }

        //Return count of vertices for submesh
        public static Mesh SMCGetSubmesh(this Mesh aMesh, int aSubMeshIndex)
        {
            if (aSubMeshIndex < 0 || aSubMeshIndex >= aMesh.subMeshCount)
                return null;
            int[] indices = aMesh.GetTriangles(aSubMeshIndex);
            Vertices source = new Vertices(aMesh);
            Vertices dest = new Vertices();
            Dictionary<int, int> map = new Dictionary<int, int>();
            int[] newIndices = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int o = indices[i];
                int n;
                if (!map.TryGetValue(o, out n))
                {
                    n = dest.Add(source, o);
                    map.Add(o, n);
                }
                newIndices[i] = n;
            }
            Mesh m = new Mesh();
            dest.AssignTo(m);
            m.triangles = newIndices;
            return m;
        }
    }
    #endregion

    #region TEXTURE_RESIZER
    public class SMCTextureResizer
    {
        public class ThreadData
        {
            public int start;
            public int end;
            public ThreadData(int s, int e)
            {
                start = s;
                end = e;
            }
        }

        private static Color[] texColors;
        private static Color[] newColors;
        private static int w;
        private static float ratioX;
        private static float ratioY;
        private static int w2;
        private static int finishCount;
        private static Mutex mutex;

        public static void Point(Texture2D tex, int newWidth, int newHeight)
        {
            ThreadedScale(tex, newWidth, newHeight, false);
        }

        public static void Bilinear(Texture2D tex, int newWidth, int newHeight)
        {
            ThreadedScale(tex, newWidth, newHeight, true);
        }

        private static void ThreadedScale(Texture2D tex, int newWidth, int newHeight, bool useBilinear)
        {
            texColors = tex.GetPixels();
            newColors = new Color[newWidth * newHeight];
            if (useBilinear)
            {
                ratioX = 1.0f / ((float)newWidth / (tex.width - 1));
                ratioY = 1.0f / ((float)newHeight / (tex.height - 1));
            }
            else
            {
                ratioX = ((float)tex.width) / newWidth;
                ratioY = ((float)tex.height) / newHeight;
            }
            w = tex.width;
            w2 = newWidth;
            var cores = Mathf.Min(SystemInfo.processorCount, newHeight);
            var slice = newHeight / cores;

            finishCount = 0;
            if (mutex == null)
            {
                mutex = new Mutex(false);
            }
            if (cores > 1)
            {
                int i = 0;
                ThreadData threadData;
                for (i = 0; i < cores - 1; i++)
                {
                    threadData = new ThreadData(slice * i, slice * (i + 1));
                    ParameterizedThreadStart ts = useBilinear ? new ParameterizedThreadStart(BilinearScale) : new ParameterizedThreadStart(PointScale);
                    Thread thread = new Thread(ts);
                    thread.Start(threadData);
                }
                threadData = new ThreadData(slice * i, newHeight);
                if (useBilinear)
                {
                    BilinearScale(threadData);
                }
                else
                {
                    PointScale(threadData);
                }
                while (finishCount < cores)
                {
                    Thread.Sleep(1);
                }
            }
            else
            {
                ThreadData threadData = new ThreadData(0, newHeight);
                if (useBilinear)
                {
                    BilinearScale(threadData);
                }
                else
                {
                    PointScale(threadData);
                }
            }

            tex.Reinitialize(newWidth, newHeight);
            tex.SetPixels(newColors);
            tex.Apply();

            texColors = null;
            newColors = null;
        }

        public static void BilinearScale(System.Object obj)
        {
            ThreadData threadData = (ThreadData)obj;
            for (var y = threadData.start; y < threadData.end; y++)
            {
                int yFloor = (int)Mathf.Floor(y * ratioY);
                var y1 = yFloor * w;
                var y2 = (yFloor + 1) * w;
                var yw = y * w2;

                for (var x = 0; x < w2; x++)
                {
                    int xFloor = (int)Mathf.Floor(x * ratioX);
                    var xLerp = x * ratioX - xFloor;
                    newColors[yw + x] = ColorLerpUnclamped(ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp), ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp), y * ratioY - yFloor);
                }
            }

            mutex.WaitOne();
            finishCount++;
            mutex.ReleaseMutex();
        }

        public static void PointScale(System.Object obj)
        {
            ThreadData threadData = (ThreadData)obj;
            for (var y = threadData.start; y < threadData.end; y++)
            {
                var thisY = (int)(ratioY * y) * w;
                var yw = y * w2;
                for (var x = 0; x < w2; x++)
                {
                    newColors[yw + x] = texColors[(int)(thisY + ratioX * x)];
                }
            }

            mutex.WaitOne();
            finishCount++;
            mutex.ReleaseMutex();
        }

        private static Color ColorLerpUnclamped(Color c1, Color c2, float value)
        {
            return new Color(c1.r + (c2.r - c1.r) * value, c1.g + (c2.g - c1.g) * value, c1.b + (c2.b - c1.b) * value, c1.a + (c2.a - c1.a) * value);
        }
    }
    #endregion
}