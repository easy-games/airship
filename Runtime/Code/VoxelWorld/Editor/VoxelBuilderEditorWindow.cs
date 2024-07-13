#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Code.Airship.Resources.VoxelRenderer.Editor {
    public class VoxelBuilderEditorWindow : EditorWindow {
        private VoxelWorld voxelWorld;
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
            return HasOpenInstances<VoxelBuilderEditorWindow>();
        }

        VoxelWorld GetVoxelWorld() {
            if (this.voxelWorld) {
                return this.voxelWorld;
            }

            this.voxelWorld = GameObject.FindObjectOfType<VoxelWorld>();
            return this.voxelWorld;
        }

        VoxelBlocks.BlockDefinition GetBlock(byte index) {
            VoxelWorld world = GetVoxelWorld();
            if (world == null) {
                return null;
            }
            return world.blocks.GetBlock(index);
        }

        void OnGUI() {
            VoxelWorld world = GetVoxelWorld();
            if (world == null) {
                return;
            }

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            if (world.blocks.loadedBlocks.Count == 0) {
                GUILayout.Label("If no blocks are visible, re-load the Voxel World.");
            }

            GUIStyle selectedStyle = new GUIStyle(GUI.skin.button);
            selectedStyle.normal.textColor = Color.green;

            foreach (var pair in world.blocks.loadedBlocks) {
                if (pair.Key == world.selectedBlockIndex) {
                    GUILayout.Button(pair.Value.name, selectedStyle);
                } else {
                    if (GUILayout.Button(pair.Value.name)) {
                        world.selectedBlockIndex = pair.Key;
                    }
                }
            }
            GUILayout.EndScrollView();
        }
    }
}
#endif