#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Code.Airship.Resources.VoxelRenderer.Editor;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(VoxelWorld))]
public class VoxelWorldEditor : UnityEditor.Editor {
    private static readonly string DefaultBlockDefinesPath = "Assets/Bundles/@Easy/Survival/Shared/Resources/VoxelWorld/SurvivalBlockDefines.xml";
    GameObject handle = null;
    GameObject raytraceHandle = null;
 
    public void Load(VoxelWorld world)
    {
        if (world.voxelWorldFile != null)
        {
            world.LoadWorldFromSaveFile(world.voxelWorldFile);
        }
    }

    [MenuItem("GameObject/Airship/VoxelWorld", false, 100)]
    static void CreateAirshipVoxelWorld(MenuCommand menuCommand) {
        var parent = menuCommand.context as GameObject;

        var voxelWorldGo = new GameObject("VoxelWorld");
        var voxelWorld = voxelWorldGo.AddComponent<VoxelWorld>();
        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultBlockDefinesPath);
        voxelWorld.blockDefines.Add(textAsset);
        GameObjectUtility.SetParentAndAlign(voxelWorldGo, parent);

        var rollbackManager = voxelWorldGo.GetComponent<VoxelRollbackManager>();
        rollbackManager.voxelWorld = voxelWorld;

        //var voxelWorldNetworkerGo = new GameObject("VoxelWorldNetworker");
        voxelWorldGo.AddComponent<NetworkObject>();
        var voxelWorldNetworker = voxelWorldGo.AddComponent<VoxelWorldNetworker>();
        voxelWorldNetworker.world = voxelWorld;
        Debug.Log("voxelWorldNetworker world: " + voxelWorldNetworker.world);
        GameObjectUtility.SetParentAndAlign(voxelWorldGo, parent);
        //GameObjectUtility.SetParentAndAlign(voxelWorldNetworkerGo, voxelWorldGo);

        voxelWorldGo.layer = LayerMask.NameToLayer("VoxelWorld");
        //voxelWorldNetworkerGo.layer = LayerMask.NameToLayer("VoxelWorld");

        voxelWorld.worldNetworker = voxelWorldNetworker;

        var undoId = Undo.GetCurrentGroup();
        Undo.RegisterCreatedObjectUndo(voxelWorldGo, "Create " + voxelWorldGo.name);
        Undo.CollapseUndoOperations(undoId);

        // Undo.RegisterCreatedObjectUndo(voxelWorldNetworkerGo, "Create " + voxelWorldNetworkerGo.name);
        // Undo.CollapseUndoOperations(undoId);

        Selection.activeObject = voxelWorldGo;
    }

    public override void OnInspectorGUI() {
        VoxelWorld world = (VoxelWorld)target;

        EditorGUILayout.LabelField("Configure Blocks", EditorStyles.boldLabel);
        {
            var style = EditorStyles.label;
            style.wordWrap = true;
            EditorGUILayout.LabelField("Add additional xml files to expand the list of blocks in the game. For reference, see CoreBlockDefines.xml\nIt is recommended to always include CoreBlockDefines.xml", style);
        }

        EditorGUILayout.Space(4);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("blockDefines"), true);
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space(4);

        //Add big divider
        AirshipEditorGUI.HorizontalLine();
        EditorGUILayout.LabelField("Save Files", EditorStyles.boldLabel);
        {
            var style = EditorStyles.label;
            style.wordWrap = true;
            EditorGUILayout.LabelField("Worlds are saved as files. You can set the save file below and then load it.", style);
        }
        EditorGUILayout.Space(4);

        //Add a file picker for  voxelWorldFile
        world.voxelWorldFile = (WorldSaveFile)EditorGUILayout.ObjectField("Voxel World File", world.voxelWorldFile, typeof(WorldSaveFile), false);

        EditorGUILayout.Space(4);

        if (world.voxelWorldFile != null)
        {
            if (GUILayout.Button("Load"))
            {
                Load(world);
            }

            if (world.chunks.Count > 0)
            {
                if (GUILayout.Button("Save")) {
                    world.SaveToFile();
                }
            }
            else
            {
                //Draw greyed out save button
                GUI.enabled = false;

                if (GUILayout.Button("Save"))
                {


                }
                GUI.enabled = true;
                
            }
        } else {
            if (GUILayout.Button("Create New"))
            {
                world.Unload();

                WorldSaveFile saveFile = CreateInstance<WorldSaveFile>();
                saveFile.CreateFromVoxelWorld(world);

                //Create a file picker to save the file, prepopulate it with the asset path of world.asset
                string path = EditorUtility.SaveFilePanel("Save Voxel World", "Assets/Bundles/Server/Resources/Worlds", "New World", "asset");
                string relativePath = "Assets/" + path.Split("Assets")[1];
                AssetDatabase.CreateAsset(saveFile, relativePath);

                world.voxelWorldFile = saveFile;
                world.UpdatePropertiesForAllChunksForRendering();
                world.LoadWorldFromSaveFile(saveFile);
            }
        }

        EditorGUILayout.Space(5);
        AirshipEditorGUI.HorizontalLine();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("World Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (GUILayout.Button("Generate Full World"))
        {
            world.GenerateWorld(true);
        }
        if (GUILayout.Button("Generate Empty World"))
        {
            world.GenerateWorld(false);
        }

        EditorGUILayout.Space(10);
        AirshipEditorGUI.HorizontalLine();

        //Add a button for "Open editor"
        if (GUILayout.Button("Open Editor")) {
            //open theVoxelWorldEditor
            VoxelBuilderEditorWindow.ShowWindow();

        }

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

        if (GUILayout.Button("Reload Atlas"))
        {
            world.ReloadTextureAtlas();
        }

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

        
        //Add a toggle button for world.isRadiosityEnabled
        //world.radiosityEnabled = EditorGUILayout.Toggle("Radiosity Enabled", world.radiosityEnabled);

        //Add a float slider for globalRadiosityScale
        //world.globalRadiosityScale = EditorGUILayout.Slider("Global RadiosityScale", world.globalRadiosityScale, 0, 3);

        //Add a float slider for globalRadiosityScale
        //world.globalRadiosityDirectLightAmp = EditorGUILayout.Slider("Radiosity Direct Light Amp", world.globalRadiosityDirectLightAmp, 0, 5);
        
        // World Networker picker
        world.worldNetworker = (VoxelWorldNetworker)EditorGUILayout.ObjectField("Voxel World Networker", world.worldNetworker, typeof(VoxelWorldNetworker), true);
        
        world.autoLoad = EditorGUILayout.Toggle("Auto Load", world.autoLoad);

        //if (GUILayout.Button("Emit block"))
        //{
        //    MeshProcessor.ProduceSingleBlock(1, world);
        //}

        AirshipEditorGUI.HorizontalLine();
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        if (GUILayout.Button("Clear Visual Chunks"))
        {
            world.DeleteRenderedGameObjects();
        }
        if (GUILayout.Button("Reload Atlas"))
        {
            world.ReloadTextureAtlas();
        }

        if (GUI.changed) {
 

            // Trigger a repaint
            world.FullWorldUpdate();
        }
    }

    private void OnDisable() {
        if (this.handle) {
            DestroyImmediate(this.handle);
        }


        if (this.raytraceHandle) {
            DestroyImmediate(this.raytraceHandle);
        }
    }

    private void OnSceneGUI() {
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        VoxelWorld world = (VoxelWorld)target;
        Event e = Event.current;

        if (VoxelBuilderEditorWindow.Enabled() == false) {
            return;
        }

        if (e.type == EventType.MouseMove) {
            
            // Create a ray from the mouse position
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            //Transform the ray into localspace of this world
            ray = world.TransformRayToLocalSpace(ray);
            (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);//, out Vector3 pos, out Vector3 hitNormal);

            if (res == true)
            {
                if (handle == null)
                {
                    handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    handle.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);
                    handle.transform.parent = world.transform;
                    MeshRenderer ren = handle.GetComponent<MeshRenderer>();
                    ren.sharedMaterial = UnityEngine.Resources.Load<Material>("Selection");
                }
                //handle.transform.position = pos + new Vector3(0.5f, 0.5f, 0.5f); //;//+  VoxelWorld.FloorInt(pos)+ new Vector3(0.5f,0.5f,0.5f);
                // Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
                Vector3 pos = hitPosition + (normal * 0.1f);
                handle.transform.position = VoxelWorld.FloorInt(pos) + new Vector3(0.5f, 0.5f, 0.5f);
                //Debug.Log("Mouse on cell" + VoxelWorld.FloorInt(pos));

            }
        }

        //Leftclick up
        if (e.type == EventType.MouseUp && e.button == 0) {
            // Create a ray from the mouse position
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            ray = world.TransformRayToLocalSpace(ray);
            (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);
            if (res) {
                Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
                if (Event.current.shift) {
                    //set voxel to 0
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos);
                    world.WriteVoxelAtInternal(voxelPos, 0);
                    world.DirtyNeighborMeshes(voxelPos, true);
                } else {
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos) + VoxelWorld.FloorInt(normal);
                    world.WriteVoxelAtInternal(voxelPos, (byte)world.selectedBlockIndex);
                    world.DirtyNeighborMeshes(voxelPos, true);
                }
            }

        }

        if (Event.current.GetTypeForControl(controlID) == EventType.KeyDown) {
            if (Event.current.keyCode == KeyCode.LeftShift) {
                //shiftDown = true;
            }
        }
        if (Event.current.GetTypeForControl(controlID) == EventType.KeyUp) {
            if (Event.current.keyCode == KeyCode.LeftShift) {
                //shiftDown = false;
            }
        }
    }
}
#endif