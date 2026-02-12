#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Code.Airship.Resources.VoxelRenderer.Editor {
    public class VoxelBuilderEditorWindow : EditorWindow {
        private Vector2 scrollPos;

        // Enum to represent the different modes
        private enum Mode {
            Add,
            Delete
        }

        private int gridWidth = 4;
        private bool[,] grid;

        // The current mode
        private Mode currentMode;
        public static bool active = true;
        private string blockSearchText = string.Empty;
        private bool sortBlocksAlphabetically = false;
        private readonly List<ushort> filteredBlockIds = new List<ushort>();
        private readonly Dictionary<string, int> blockLabelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        [MenuItem("Airship/Misc/VoxelEditor")]
        private static void Init() {
            ShowWindow();
        }

        public static void ForceRepaint() {
            if (active) {
                GetWindow<VoxelBuilderEditorWindow>().Repaint();
            }
        }

        public static void ShowWindow() {
            // Get existing open window or if none, make a new one:

            if (HasOpenInstances<VoxelBuilderEditorWindow>()) {
                GetWindow<VoxelBuilderEditorWindow>().Close();
            } else {
                var myWindow = GetWindow<VoxelBuilderEditorWindow>();
                myWindow.titleContent = new GUIContent("Voxel Editor");
            }
        }

        public static bool Enabled() {
            return active && HasOpenInstances<VoxelBuilderEditorWindow>();
        }

        private VoxelWorld GetVoxelWorld() {
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


        private void ShowSelectionGui() {
            GUILayout.Label("Select Voxel World", EditorStyles.boldLabel);

            //Shows a list of all the VoxelWorld objects in the scene as clickable buttons
            var voxelWorlds = FindObjectsOfType<VoxelWorld>();

            for (var i = 0; i < voxelWorlds.Length; i++) {
                var selectionZone = voxelWorlds[i].GetComponentInChildren<SelectionZone>();

                if (Selection.activeGameObject == voxelWorlds[i].gameObject || (selectionZone != null &&
                        Selection.activeGameObject == selectionZone.gameObject)) {
                    GUI.backgroundColor = Color.green;
                } else {
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


        private void OnGUI() {
            var sectionStyle = new GUIStyle(EditorStyles.helpBox) {
                padding = new RectOffset(8, 8, 8, 8),
                margin = new RectOffset(8, 8, 6, 6)
            };
            var headerStyle = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = 12
            };

            //Create an active toggle as a button that toggles on and off
            active = GUILayout.Toggle(active, "Voxel Editor Active");

            if (active == false) {
                GUI.enabled = false;
            }

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Scene", headerStyle);
            ShowSelectionGui();
            GUILayout.EndVertical();

            var world = GetVoxelWorld();
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
                    selection.transform.localScale = Vector3.one;
                    selection.voxelWorld = world;
                }

                //Select this
                selection.gameObject.SetActive(true);
                Selection.activeGameObject = selection.gameObject;
            }

            if (VoxelWorldEditorToolBase.buttonActive) {
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

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Controls", headerStyle);
            EditorGUILayout.HelpBox(
                "Left click to add\nShift+click to delete\nCtrl+click for repeat placement\nAlt+click or I to select highlighted block type\nG to rotate highlighted block",
                MessageType.Info);
            GUILayout.EndVertical();

            //active = EditorGUILayout.Toggle("Active", active);

            //gap
            EditorGUILayout.Space();

            //Prefab

            var prefab = world.GetPrefabAt(world.highlightedBlockPos);

            var blockData = world.GetVoxelAt(world.highlightedBlockPos);

            var blockDef = world.voxelBlocks.GetBlock(blockData);

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Highlighted Block", headerStyle);

            var highlightedId = VoxelWorld.GetVoxelDataId(blockData);
            if (highlightedId == 0) {
                GUI.enabled = false;
            }

            var flipBits = VoxelWorld.GetVoxelDataFlippedBits(blockData);

            var def = GUI.backgroundColor;

            GUILayout.BeginHorizontal();
            if (blockDef != null) {
                GUILayout.Label("Type: " + blockDef.definition.blockName);
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();


            GUILayout.Label("Rotation: " + VoxelWorld.flipNames[flipBits]);

            GUI.backgroundColor = def;
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (prefab != null) {
                GUILayout.Label("Prefab: " + prefab.name);
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = highlightedId != 0 && highlightedId < world.voxelBlocks.loadedBlocks.Count;
            if (GUILayout.Button("Use Highlighted Type (I)")) {
                world.selectedBlockIndex = highlightedId;
            }
            GUI.enabled = world.selectedBlockIndex < world.voxelBlocks.loadedBlocks.Count;
            if (GUILayout.Button("Jump To Selected")) {
                var selectedIndex = FindFilteredIndex(world.selectedBlockIndex);
                if (selectedIndex >= 0) {
                    scrollPos.y = selectedIndex * 22f;
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("Block Palette", headerStyle);
            GUILayout.BeginHorizontal();
            blockSearchText = EditorGUILayout.TextField("Search", blockSearchText ?? string.Empty);
            if (GUILayout.Button("Clear", GUILayout.Width(52))) {
                blockSearchText = string.Empty;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            sortBlocksAlphabetically = EditorGUILayout.ToggleLeft("Sort A-Z", sortBlocksAlphabetically, GUILayout.Width(96));
            GUILayout.EndHorizontal();

            RebuildFilteredBlocks(world);
            EditorGUILayout.LabelField($"Showing {filteredBlockIds.Count} / {world.voxelBlocks.loadedBlocks.Count} blocks", EditorStyles.miniLabel);

            scrollPos = GUILayout.BeginScrollView(scrollPos);
            if (world.voxelBlocks.loadedBlocks.Count == 0) {
                GUILayout.Label("If no blocks are visible, re-load the Voxel World.");
            } else if (filteredBlockIds.Count == 0) {
                GUILayout.Label("No blocks match the current filters.");
            }

            var selectedStyle = new GUIStyle(GUI.skin.button);
            selectedStyle.normal.textColor = Color.green;
            selectedStyle.hover.textColor = Color.green;

            var loadedBlocks = world.voxelBlocks.loadedBlocks;
            for (var i = 0; i < filteredBlockIds.Count; i++) {
                var id = filteredBlockIds[i];
                var name = GetUniqueBlockDisplayName(world, id);

                if (id == world.selectedBlockIndex) {
                    GUILayout.Button(name, selectedStyle);
                } else {
                    if (GUILayout.Button(name)) {
                        world.selectedBlockIndex = id;
                    }
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.enabled = true;
        }

        private void onSceneGUIDelegate(SceneView sceneView) { }

        private void OnEnable() {
            autoRepaintOnSceneChange = true;
            SceneView.duringSceneGui += onSceneGUIDelegate;
        }

        private void OnDisable() {
            SceneView.duringSceneGui -= onSceneGUIDelegate;
        }

        private string GetBlockDisplayName(VoxelWorld world, ushort id) {
            var definition = world.voxelBlocks.loadedBlocks[id].definition;
            if (!string.IsNullOrEmpty(definition.name)) {
                return definition.name;
            }

            if (!string.IsNullOrEmpty(definition.blockName)) {
                return definition.blockName;
            }

            return "Air";
        }

        private void RebuildFilteredBlocks(VoxelWorld world) {
            filteredBlockIds.Clear();
            blockLabelCounts.Clear();

            for (ushort id = 0; id < world.voxelBlocks.loadedBlocks.Count; id++) {
                var name = GetBlockDisplayName(world, id);
                if (!string.IsNullOrWhiteSpace(blockSearchText) &&
                    name.IndexOf(blockSearchText, StringComparison.OrdinalIgnoreCase) < 0) {
                    continue;
                }

                filteredBlockIds.Add(id);

                if (blockLabelCounts.TryGetValue(name, out var count)) {
                    blockLabelCounts[name] = count + 1;
                } else {
                    blockLabelCounts[name] = 1;
                }
            }

            if (sortBlocksAlphabetically) {
                filteredBlockIds.Sort((a, b) => string.Compare(
                    GetBlockDisplayName(world, a),
                    GetBlockDisplayName(world, b),
                    StringComparison.OrdinalIgnoreCase));
            }
        }

        private string GetUniqueBlockDisplayName(VoxelWorld world, ushort id) {
            var name = GetBlockDisplayName(world, id);
            if (blockLabelCounts.TryGetValue(name, out var count) && count > 1) {
                return $"{name}  (#{id})";
            }
            return name;
        }

        private int FindFilteredIndex(int id) {
            for (var i = 0; i < filteredBlockIds.Count; i++) {
                if (filteredBlockIds[i] == id) {
                    return i;
                }
            }
            return -1;
        }
    }
}
#endif
