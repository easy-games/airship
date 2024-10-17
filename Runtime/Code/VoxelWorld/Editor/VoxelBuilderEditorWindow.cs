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

            if (selectedObject) {
                var selectionZone = selectedObject.GetComponentInParent<SelectionZone>();
                if (selectionZone && selectionZone.voxelWorld) {
                    return selectionZone.voxelWorld;
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


                SelectionZone selectionZone = voxelWorlds[i].GetComponentInChildren<SelectionZone>();

                if (Selection.activeGameObject == voxelWorlds[i].gameObject || (selectionZone!=null && Selection.activeGameObject == selectionZone.gameObject)) {
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
            SelectionZone selection = null;
            if (world == null || world.voxelBlocks == null) {
                GUI.enabled = true; //cleanup from above
                return; 
            }
            
            //See if we're in the selection mode
            if (VoxelWorldSelectionToolBase.buttonActive == true) {
                //Find or create the SelectionZone for this voxelWorld

                selection = world.GetComponentInChildren<SelectionZone>(true);
                if (selection == null) {
                    selection = new GameObject("SelectionZone").AddComponent<SelectionZone>();
                    selection.hideFlags = HideFlags.HideAndDontSave;
                    selection.transform.parent = world.transform;
                    selection.transform.localPosition = Vector3.zero;
                    selection.voxelWorld = world;
                }
                //Select this
                selection.gameObject.SetActive(true);
                Selection.activeGameObject = selection.gameObject;
                
            }
            if (VoxelWorldEditorToolBase.buttonActive == true) {
            
                //If we're not in selection mode, disable the selection zone
                selection = world.GetComponentInChildren<SelectionZone>();
                                
                if (selection) {
                    //Select the world
                    Selection.activeGameObject = world.gameObject;
                    //disable it
                    selection.gameObject.SetActive(false);
                    
                    //we used to destroy it
                    //DestroyImmediate(selection.gameObject);
                }
            }

            //Show a foldable help box
            EditorGUILayout.HelpBox("Left click to add\nShift+click to delete\nCtrl+click for repeat placement", MessageType.Info);
            
            //active = EditorGUILayout.Toggle("Active", active);

            //gap
            EditorGUILayout.Space();

            //Row of three tiny buttons, X Y Z
            ushort blockData = world.highlightedBlock;
            
            if (blockData > 0) {
                GUILayout.Label("Block flipping");

                int flipBits = VoxelWorld.GetVoxelFlippedBits(blockData);
                
                if (Event.current.keyCode == KeyCode.LeftAlt) {
                    flipBits = (flipBits + 1) % 3;
                }
                ushort newData = (ushort)VoxelWorld.SetVoxelFlippedBits(blockData, flipBits);
                if (newData != blockData) {
                    world.WriteVoxelAt(world.highlightedBlockPos, newData, true);
                }
                

                Color def = GUI.backgroundColor;

                GUILayout.BeginHorizontal();
                if ((flipBits & 1 << 0) > 0) {
                    GUI.backgroundColor = Color.green;
                }
                else {
                    GUI.backgroundColor = def;
                }

                if (GUILayout.Button("X", GUILayout.Width(20))) {
                 
                }

                if ((flipBits & 1 << 0) > 0) {
                    GUI.backgroundColor = Color.green;
                }
                else {
                    GUI.backgroundColor = def;
                }
                if (GUILayout.Button("Y", GUILayout.Width(20))) {

                }

                if ((flipBits & 1 << 0) > 0) {
                    GUI.backgroundColor = Color.green;
                }
                else {
                    GUI.backgroundColor = def;
                }
                if (GUILayout.Button("Z", GUILayout.Width(20))) {

                }

                GUI.backgroundColor = def;
                GUILayout.EndHorizontal();
            }
            
            GUILayout.Label("Blocks", EditorStyles.boldLabel);

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            if (world.voxelBlocks.loadedBlocks.Count == 0) {
                GUILayout.Label("If no blocks are visible, re-load the Voxel World.");
            }

            GUIStyle selectedStyle = new GUIStyle(GUI.skin.button);
            selectedStyle.normal.textColor = Color.green;
            selectedStyle.hover.textColor = Color.green;
            
            foreach (var pair in world.voxelBlocks.loadedBlocks) {

                string name = pair.Value.definition.name;
                if (name == "") {
                    name = "Air";
                }

                if (pair.Key == world.selectedBlockIndex) {
                    GUILayout.Button(name, selectedStyle);
                } else {
                    if (GUILayout.Button(name)) {
                        world.selectedBlockIndex = pair.Key;
                    }
                }
            }
            GUILayout.EndScrollView();
            GUI.enabled = true;
        }

        void OnEnable() {
            base.autoRepaintOnSceneChange = true;
        }
    }
}
#endif