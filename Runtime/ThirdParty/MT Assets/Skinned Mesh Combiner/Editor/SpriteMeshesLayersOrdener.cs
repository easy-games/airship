#if MTAssets_Anima2D_Available
using Anima2D;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTAssets.SkinnedMeshCombiner.Editor
{
    public class SpriteMeshesLayersOrdener : EditorWindow
    {
        /*
        This class is responsible for the functioning of the "Sprite Meshes Layers Ordener" component, and all its functions.
        */
        /*
         * The Skinned Mesh Combiner was developed by Marcos Tomaz in 2019.
         * Need help? Contact me (mtassets@windsoft.xyz)
         */

        //Variables of window
        private bool isWindowOnFocus = false;

        //Variables of script
        private static SmLayersOrdenerPreferences layersOrdenerPreferences;
        private bool preferencesLoadedOnInspectorUpdate = false;
        private static Transform parentOfAllSpriteMeshes = null;
#if MTAssets_Anima2D_Available
        private Dictionary<int, List<SpriteMeshInstance>> spriteMeshesByLayers = new Dictionary<int, List<SpriteMeshInstance>>();
#endif

        //Variables of UI
        private Vector2 scrollPosPreferences;

        public static void OpenWindow(Transform parentOfAllSprites)
        {
            //Method to open the Window
            var window = GetWindow<SpriteMeshesLayersOrdener>("Layer Ordener");
            window.minSize = new Vector2(400, 650);
            window.maxSize = new Vector2(400, 650);
            var position = window.position;
            position.center = new Rect(0f, 0f, Screen.currentResolution.width, Screen.currentResolution.height).center;
            window.position = position;
            window.Show();

            //Get parent of all sprites
            parentOfAllSpriteMeshes = parentOfAllSprites;
        }

        //UI Code
        #region INTERFACE_CODE
        void OnEnable()
        {
            //On enable this window, on re-start this window after compilation
            isWindowOnFocus = true;

            //Load the preferences
            LoadThePreferences(this);
        }

        void OnDisable()
        {
            //On disable this window, after compilation, disables the window and enable again
            isWindowOnFocus = false;

            //Save the preferences
            SaveThePreferences(this);
        }

        void OnDestroy()
        {
            //On close this window
            isWindowOnFocus = false;

            //Save the preferences
            SaveThePreferences(this);
        }

        void OnFocus()
        {
            //On focus this window
            isWindowOnFocus = true;
        }

        void OnLostFocus()
        {
            //On lose focus in window
            isWindowOnFocus = false;
        }

        void OnGUI()
        {
            //Start the undo event support, draw default inspector and monitor of changes
            EditorGUI.BeginChangeCheck();

            #region IF_NOT_HAVE_ANIMA2D
            //If don't have Anima2D
#if !MTAssets_Anima2D_Available
            EditorGUILayout.HelpBox("This function is only available if the Anima2D plugin from Unity Technologies is in your project. This plugin's Namespace was not found.", MessageType.Error);
            GUILayout.Space(10);
            if (GUILayout.Button("Ok, Close This!"))
            {
                //Save the preferences
                SaveThePreferences(this);

                //Close the window
                this.Close();
            }
#endif
            #endregion

            #region IF_HAVE_ANIMA2D
            //If have Anima2D
#if MTAssets_Anima2D_Available
            //Start the UI

            //Update the sprite meshes list
            UpdateSpriteMeshesList();

            //Format the title label
            GUIStyle tituloBox = new GUIStyle();
            tituloBox.fontStyle = FontStyle.Bold;
            tituloBox.alignment = TextAnchor.MiddleCenter;

            scrollPosPreferences = EditorGUILayout.BeginScrollView(scrollPosPreferences, GUILayout.Width(400), GUILayout.Height(616));
            //The title
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Sprite Meshes Layers Ordener", tituloBox);
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Here you can determine the rendering order (that is, the layer order) of each mesh of your character. Thus, you will have no problems in the rendering order of each Sprite Mesh, after merging the mesh of your Anima2D character. For everything to work well, it is necessary that all Sprite Mesh Instance of your character, have the variable \"Sorting Layer\" in \"Default\". The higher the layer number, the higher the mesh will be rendered.", MessageType.Info);
            GUILayout.Space(10);

            if (parentOfAllSpriteMeshes == null)
                EditorGUILayout.HelpBox("Okay. First I need to know which GameObject is the parent of all the meshes (Sprite Meshes) of your character. Drag that GameObject parent from your scene, to the variable below.", MessageType.Info);
            parentOfAllSpriteMeshes = (Transform)EditorGUILayout.ObjectField(new GUIContent("Parent Of All Meshes",
                    "GameObject that is the parent of all the meshes (Sprite Meshes) of your character."),
                    parentOfAllSpriteMeshes, typeof(Transform), true, GUILayout.Height(16));

            GUILayout.Space(10);

            //If providade parent of all sprite meshes
            if (parentOfAllSpriteMeshes != null)
            {
                //Order the array of layers
                List<int> arrayOfLayers = spriteMeshesByLayers.Keys.ToList();
                arrayOfLayers.Sort();
                List<int> arrayOfLayersInverse = new List<int>();
                for (int i = arrayOfLayers.Count - 1; i >= 0; i--)
                {
                    arrayOfLayersInverse.Add(arrayOfLayers[i]);
                }

                //Create the new sibling index
                List<SpriteMeshInstance> newSiblingIndex = new List<SpriteMeshInstance>();
                //Fill the new sibling index list
                foreach (int layer in arrayOfLayersInverse)
                {
                    foreach (SpriteMeshInstance spriteMesh in spriteMeshesByLayers[layer])
                    {
                        //Add this
                        newSiblingIndex.Add(spriteMesh);
                    }
                }
                //Set all sibling index
                for (int i = 0; i < newSiblingIndex.Count; i++)
                {
                    newSiblingIndex[i].sortingOrder = newSiblingIndex[i].transform.GetSiblingIndex();
                }

                //Create style of icon
                GUIStyle estiloIcone = new GUIStyle();
                estiloIcone.border = new RectOffset(0, 0, 0, 0);
                estiloIcone.margin = new RectOffset(4, 0, 6, 0);

                //Render each SpriteMesh by layer
                foreach (int layer in arrayOfLayersInverse)
                {
                    //Render this layer name
                    EditorGUILayout.LabelField("Layer " + layer, EditorStyles.boldLabel);

                    //Render all sprite mesh inside this layer
                    foreach (SpriteMeshInstance spriteMesh in spriteMeshesByLayers[layer])
                    {
                        //List this SpriteMesh
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(50);
                        EditorGUILayout.BeginVertical("box");
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(5);
                        if (spriteMesh.spriteMesh != null && spriteMesh.spriteMesh.sprite != null)
                            GUILayout.Box(spriteMesh.spriteMesh.sprite.texture, estiloIcone, GUILayout.Width(24), GUILayout.Height(24));
                        GUILayout.Space(5);
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField(spriteMesh.transform.name, EditorStyles.boldLabel);
                        GUILayout.Space(-3);
                        EditorGUILayout.LabelField("Hierarchy position is " + spriteMesh.transform.GetSiblingIndex() + "/" + (newSiblingIndex.Count - 1));
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        GUILayout.Space(10);
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(150);
                        if (GUILayout.Button("Select"))
                            Selection.objects = new Object[] { spriteMesh.gameObject };
                        if (GUILayout.Button("Ping"))
                            EditorGUIUtility.PingObject(spriteMesh.gameObject);
                        if (GUILayout.Button("/\\"))
                        {
                            if (newSiblingIndex.IndexOf(spriteMesh) > 0)
                                spriteMesh.transform.SetSiblingIndex(spriteMesh.transform.GetSiblingIndex() + 1);
                        }
                        if (GUILayout.Button("\\/"))
                        {
                            if (newSiblingIndex.IndexOf(spriteMesh) < newSiblingIndex.Count - 1)
                                spriteMesh.transform.SetSiblingIndex(spriteMesh.transform.GetSiblingIndex() - 1);
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            EditorGUILayout.EndScrollView();

            //The close button
            GUILayout.Space(8);
            if (GUILayout.Button("Done", GUILayout.Height(25)))
            {
                //Save the preferences
                SaveThePreferences(this);

                //Close the window
                this.Close();
            }
#endif
            #endregion

            //Apply changes on script, case is not playing in editor
            if (GUI.changed == true && Application.isPlaying == false)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            if (EditorGUI.EndChangeCheck() == true)
            {

            }
        }

        void OnInspectorUpdate()
        {
            //On inspector update, on lost focus in this Window
            if (isWindowOnFocus == false)
            {
                //Update this window
                Repaint();
                //Update the scene GUI
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.Repaint();
                }
            }

            //Try to load the preferences on inspector update (if this window is in focus or not, try to load here, because this method runs after OpenWindow() method)
            if (preferencesLoadedOnInspectorUpdate == false)
            {
                if (layersOrdenerPreferences.windowPosition.x != 0 && layersOrdenerPreferences.windowPosition.y != 0)
                {
                    LoadThePreferences(this);
                }
                preferencesLoadedOnInspectorUpdate = true;
            }
        }

        //Sprite founder
        public void UpdateSpriteMeshesList()
        {
            //If parent of sprite meshes is null, cancel update
            if (parentOfAllSpriteMeshes == null)
                return;

#if MTAssets_Anima2D_Available
            //Clear the dictionary
            spriteMeshesByLayers.Clear();

            //Get all sprite meshes
            SpriteMeshInstance[] spriteMeshes = parentOfAllSpriteMeshes.GetComponentsInChildren<SpriteMeshInstance>(false);

            //Fill the dictionary
            foreach (SpriteMeshInstance spriteMesh in spriteMeshes)
            {
                //If this is a sprite stored in parent, ignore this
                if (spriteMesh.transform == parentOfAllSpriteMeshes)
                    continue;

                //If this Sorting Layer is different of "Default", ignore this
                if (spriteMesh.sortingLayerName != "Default")
                    continue;

                //If this sprite mesh is disabled, skip this
                if (spriteMesh.enabled == false)
                    continue;

                if (spriteMeshesByLayers.ContainsKey(spriteMesh.sortingOrder) == false)
                    spriteMeshesByLayers.Add(spriteMesh.sortingOrder, new List<SpriteMeshInstance>());
            }

            //Fill all lists of all layers
            foreach (SpriteMeshInstance spriteMesh in spriteMeshes)
            {
                //If this is a sprite stored in parent, ignore this
                if (spriteMesh.transform == parentOfAllSpriteMeshes)
                    continue;

                //If this Sorting Layer is different of "Default", ignore this
                if (spriteMesh.sortingLayerName != "Default")
                    continue;

                //If this sprite mesh is disabled, skip this
                if (spriteMesh.enabled == false)
                    continue;

                spriteMeshesByLayers[spriteMesh.sortingOrder].Add(spriteMesh);
            }
#endif
        }
        #endregion

        static void LoadThePreferences(SpriteMeshesLayersOrdener instance)
        {
            //Create the default directory, if not exists
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData"))
                AssetDatabase.CreateFolder("Assets/MT Assets", "_AssetsData");
            if (!AssetDatabase.IsValidFolder("Assets/MT Assets/_AssetsData/Preferences"))
                AssetDatabase.CreateFolder("Assets/MT Assets/_AssetsData", "Preferences");

            //Try to load the preferences file
            layersOrdenerPreferences = (SmLayersOrdenerPreferences)AssetDatabase.LoadAssetAtPath("Assets/MT Assets/_AssetsData/Preferences/SpriteMeshesLayersOrdener.asset", typeof(SmLayersOrdenerPreferences));
            //Validate the preference file. if this preference file is of another project, delete then
            if (layersOrdenerPreferences != null)
            {
                if (layersOrdenerPreferences.projectName != Application.productName)
                {
                    AssetDatabase.DeleteAsset("Assets/MT Assets/_AssetsData/Preferences/SpriteMeshesLayersOrdener.asset");
                    layersOrdenerPreferences = null;
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                if (layersOrdenerPreferences != null && layersOrdenerPreferences.projectName == Application.productName)
                {
                    //Set the position of Window 
                    instance.position = layersOrdenerPreferences.windowPosition;
                }
            }
            //If null, create and save a preferences file
            if (layersOrdenerPreferences == null)
            {
                layersOrdenerPreferences = ScriptableObject.CreateInstance<SmLayersOrdenerPreferences>();
                layersOrdenerPreferences.projectName = Application.productName;
                AssetDatabase.CreateAsset(layersOrdenerPreferences, "Assets/MT Assets/_AssetsData/Preferences/SpriteMeshesLayersOrdener.asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        static void SaveThePreferences(SpriteMeshesLayersOrdener instance)
        {
            //Save the preferences in Prefs.asset
            layersOrdenerPreferences.projectName = Application.productName;
            layersOrdenerPreferences.windowPosition = new Rect(instance.position.x, instance.position.y, instance.position.width, instance.position.height);
            EditorUtility.SetDirty(layersOrdenerPreferences);
            AssetDatabase.SaveAssets();
        }
    }
}