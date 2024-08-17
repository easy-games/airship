#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Code.Airship.Resources.VoxelRenderer.Editor {
    public class VoxelBuilderEditorWindow : EditorWindow {
        
        Vector2 scrollPos;

        // Enum to represent the different modes
        enum Mode
        {
            Add,
            Delete,
        }

        int gridWidth = 4;
        bool[,] grid;

        // The current mode
        Mode currentMode;
        public static bool active = true;

        [MenuItem("Airship/Misc/VoxelEditor")]
        static void Init() {
            ShowWindow();
        }

        public static void ShowWindow() {
            // Get existing open window or if none, make a new one:

            if (HasOpenInstances<VoxelBuilderEditorWindow>()) {
                GetWindow<VoxelBuilderEditorWindow>().Close();
            }
            else {
                var myWindow = GetWindow<VoxelBuilderEditorWindow>();
                myWindow.titleContent = new GUIContent("Voxel Editor");
            }
        }
           
        public static bool Enabled() {
            return active && HasOpenInstances<VoxelBuilderEditorWindow>();
        }

        VoxelWorld GetVoxelWorld() {
     
            //See if the currently selected object in the world is a voxelworld
            var selectedObject = Selection.activeGameObject;
            if (selectedObject) {
                var voxelWorld = selectedObject.GetComponent<VoxelWorld>();
                if (voxelWorld) {
                    
                    return voxelWorld;
                }
            }
            return null;
        }
  
 
        void ShowSelectionGui() {

            //Label 
            GUILayout.Label("Select VoxelWorld", EditorStyles.boldLabel);

            //Shows a list of all the VoxelWorld objects in the scene as clickable buttons
            VoxelWorld[] voxelWorlds = GameObject.FindObjectsOfType<VoxelWorld>();
            for (int i = 0; i < voxelWorlds.Length; i++) {

                if (Selection.activeGameObject == voxelWorlds[i].gameObject) {
                    GUI.backgroundColor = Color.green;
                }
                else {
                    GUI.backgroundColor = Color.white;
                }

                if (GUILayout.Button(voxelWorlds[i].name)) {
                    //Select it in studio
                    Selection.activeGameObject = voxelWorlds[i].gameObject;
                }
            }

            if (voxelWorlds.Length == 0) {
                GUILayout.Label("No VoxelWorlds in scene");
            }

            GUI.backgroundColor = Color.white;
        }

        void OnGUI() {
            //Create an active toggle as a button that toggles on and off
            active = GUILayout.Toggle(active, "Voxel Editor Active");

            if (active == false) {
                GUI.enabled = false;
            }

            ShowSelectionGui();
            
            VoxelWorld world = GetVoxelWorld();
            if (world == null || world.voxelBlocks == null) {
                GUI.enabled = true;
                return; 
            }

            //Show a foldable help box
            EditorGUILayout.HelpBox("Left click to add\nShift+click to delete\nCtrl+click for repeat placement", MessageType.Info);
            
            //active = EditorGUILayout.Toggle("Active", active);

            //gap
            EditorGUILayout.Space();

            GUILayout.Label("Blocks", EditorStyles.boldLabel);

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            if (world.voxelBlocks.loadedBlocks.Count == 0) {
                GUILayout.Label("If no blocks are visible, re-load the Voxel World.");
            }

            GUIStyle selectedStyle = new GUIStyle(GUI.skin.button);
            selectedStyle.normal.textColor = Color.green;

            
            foreach (var pair in world.voxelBlocks.loadedBlocks) {
                if (pair.Key == world.selectedBlockIndex) {
                    GUILayout.Button(pair.Value.definition.name, selectedStyle);
                } else {
                    if (GUILayout.Button(pair.Value.definition.name)) {
                        world.selectedBlockIndex = pair.Key;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUI.enabled = true;
        }
    }
}
#endif