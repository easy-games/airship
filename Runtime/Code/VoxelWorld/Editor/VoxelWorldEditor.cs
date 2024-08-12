#if UNITY_EDITOR

using Code.Airship.Resources.VoxelRenderer.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using System;

public class VoxelEditAction {
    public Vector3Int position;
    public ushort oldValue;
    public ushort newValue;
    [NonSerialized]
    public WeakReference<VoxelWorld> world;
    public void Initialize(VoxelWorld world, Vector3Int position, ushort oldValue, ushort newValue) {
        this.position = position;
        this.oldValue = oldValue;
        this.newValue = newValue;
        this.world = new(world);
    }
}

public class VoxelEditMarker : ScriptableObject {
    public VoxelEditAction lastAction;
}
public class VoxelEditManager : Singleton<VoxelEditManager> {

    VoxelEditMarker undoObject;
    public List<VoxelEditAction> edits = new List<VoxelEditAction>();
    public List<VoxelEditAction> redos = new List<VoxelEditAction>();


    public void AddEdit(VoxelWorld world, Vector3Int position, ushort oldValue, ushort newValue, string name) {
        VoxelEditAction edit = new VoxelEditAction();
        edit.Initialize(world, position, oldValue, newValue);
        edits.Add(edit);

        //If we're adding a new edit, clear the redos
        redos.Clear();

        if (undoObject == null) {
             undoObject = ScriptableObject.CreateInstance<VoxelEditMarker>();
        }
        undoObject.lastAction = edit;

        //Save the state of this whole object into the undo system
        Undo.RegisterCompleteObjectUndo(undoObject, name);

        world.WriteVoxelAtInternal(position, newValue);
        world.DirtyNeighborMeshes(position);
 
    }

    //Constructor
    public VoxelEditManager() {
        Undo.undoRedoEvent += UndoRedoEvent;
    }

    public void UndoRedoEvent(in UndoRedoInfo info) {

        if (info.isRedo == false) {
            if (edits.Count > 0) {
                VoxelEditAction edit = edits[edits.Count - 1];
                edits.RemoveAt(edits.Count - 1);

                edit.world.TryGetTarget(out VoxelWorld currentWorld);
                if (currentWorld){
                    currentWorld.WriteVoxelAtInternal(edit.position, edit.oldValue);
                    currentWorld.DirtyNeighborMeshes(edit.position);
                }

                redos.Add(edit);
            }
        }
        else {
            if (redos.Count > 0) {
                VoxelEditAction edit = redos[redos.Count - 1];
                redos.RemoveAt(redos.Count - 1);
                edit.world.TryGetTarget(out VoxelWorld currentWorld);
                if (currentWorld) {
                    currentWorld.WriteVoxelAtInternal(edit.position, edit.newValue);
                     currentWorld.DirtyNeighborMeshes(edit.position);
                }

                edits.Add(edit);
            }
        }
    }
}



[CustomEditor(typeof(VoxelWorld))]
public class VoxelWorldEditor : UnityEditor.Editor {
    private static readonly string DefaultBlockDefinesPath = "Assets/Bundles/@Easy/Survival/Shared/Resources/VoxelWorld/SurvivalBlockDefines.xml";
    GameObject handle = null;
    GameObject raytraceHandle = null;
    
    bool mouseOverViewport = false;
    bool lastEnabled = false;
    public void Load(VoxelWorld world) {

        if (world.voxelWorldFile != null) {
            world.LoadWorldFromSaveFile(world.voxelWorldFile);
        }
    }

  
    /*
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
    }*/

    private WorldSaveFile CreateNewVoxelWorldFile() {
        WorldSaveFile world = ScriptableObject.CreateInstance<WorldSaveFile>();

        string path = EditorUtility.SaveFilePanelInProject(
            "Save WorldSaveFile",
            "WorldSaveFile",
            "asset",
            "Please enter a file name to save the WorldSaveFile to");

        if (path == "") {
            Debug.LogWarning("Invalid path for WorldSaveFile");
            return null;
        }

        AssetDatabase.CreateAsset(world, path);
        AssetDatabase.SaveAssets();

        //EditorUtility.FocusProjectWindow();
        //Selection.activeObject = world;

        Debug.Log("WorldSaveFile created and saved at " + path);

        return world;
    }

    public override void OnInspectorGUI() {
        VoxelWorld world = (VoxelWorld)target;

        //Add a field for voxelBlocks
        world.voxelBlocks = (VoxelBlocks)EditorGUILayout.ObjectField("Voxel Blocks", world.voxelBlocks, typeof(VoxelBlocks), true);

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


        if (world.voxelWorldFile != null) {
            if (GUILayout.Button("Load")) {
                Load(world);
            }

            if (world.chunks.Count > 0) {
                if (GUILayout.Button("Save")) {
                    world.SaveToFile();
                }
            }
            else {
                //Draw greyed out save button
                GUI.enabled = false;

                if (GUILayout.Button("Save")) {


                }
                GUI.enabled = true;

            }
        }
        else {
            if (GUILayout.Button("Create New")) {
                //acts as a clear
                world.GenerateWorld();

                WorldSaveFile worldSaveFile = CreateNewVoxelWorldFile();
                if (worldSaveFile) {
                    world.voxelWorldFile = worldSaveFile;
                }
            }
        }

        EditorGUILayout.Space(5);
        AirshipEditorGUI.HorizontalLine();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("World Creator", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (GUILayout.Button("Generate Full World")) {
            world.GenerateWorld();
            world.FillRandomTerrain();
        }
        if (GUILayout.Button("Generate Empty World")) {
            world.GenerateWorld();
            world.FillSingleBlock();
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

        if (GUILayout.Button("Reload Atlas")) {
            world.ReloadTextureAtlas();
        }

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });


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
        if (GUILayout.Button("Clear Visual Chunks")) {
            world.DeleteRenderedGameObjects();
        }
        if (GUILayout.Button("Reload Atlas")) {
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

        //Only allow editing if both the editor window is active and the gizmo toolbar is active
        bool enabled = VoxelBuilderEditorWindow.Enabled() && VoxelWorldEditorTool.buttonActive;

        if (enabled != lastEnabled) {
            if (handle != null) {
                DestroyImmediate(handle);
            }
        }
        lastEnabled = enabled;

        if (enabled == false) {
            return;
        }

        Rect viewRect = SceneView.currentDrawingSceneView.position;

        if (viewRect.Contains(Event.current.mousePosition)) {
            mouseOverViewport = true;
        }
        else if (mouseOverViewport) {
            mouseOverViewport = false;
            if (handle != null) {
                DestroyImmediate(handle);
            }
        }
        
        if (e.type == EventType.MouseMove) {

            // Create a ray from the mouse position
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);

            //Transform the ray into localspace of this world
            ray = world.TransformRayToLocalSpace(ray);
            (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);//, out Vector3 pos, out Vector3 hitNormal);

            if (res == true) {
                if (handle == null) {
                    handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    handle.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);
                    handle.transform.parent = world.transform;
                    MeshRenderer ren = handle.GetComponent<MeshRenderer>();
                    ren.sharedMaterial = UnityEngine.Resources.Load<Material>("Selection");
                    ren.sharedMaterial.SetColor("_Color", new Color(1, 1, 0, 0.25f));
                }
                //handle.transform.position = pos + new Vector3(0.5f, 0.5f, 0.5f); //;//+  VoxelWorld.FloorInt(pos)+ new Vector3(0.5f,0.5f,0.5f);
                // Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
                Vector3 pos = hitPosition + (normal * 0.1f);
                pos = VoxelWorld.FloorInt(pos) + new Vector3(0.5f, 0.5f, 0.5f);
                handle.transform.position = world.TransformPointToWorldSpace(pos);
                handle.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);

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
                    // Remove voxel
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos);
                    ushort oldValue = world.GetVoxelAt(voxelPos); // Assuming you have a method to get the voxel value

                    VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

                    voxelEditManager.AddEdit(world, voxelPos, oldValue, 0, "Delete Voxel");
                }
                else {
                    // Add voxel
                    Vector3Int voxelPos = VoxelWorld.FloorInt(pos) + VoxelWorld.FloorInt(normal);
                    ushort oldValue = world.GetVoxelAt(voxelPos); // Assuming you have a method to get the voxel value
                    ushort newValue = (ushort)world.selectedBlockIndex;

                    VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

                    var def = world.voxelBlocks.GetBlock(newValue);
                    voxelEditManager.AddEdit(world, voxelPos, oldValue, newValue, "Add Voxel " + def.definition.name);
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

    void Awake(){

        //Add selection handler
        Selection.selectionChanged += OnSelectionChanged;
    }

    void OnSelectionChanged() {
        //If we're seleceted
        if (Selection.activeGameObject == ((VoxelWorld)target).gameObject) {
            ToolManager.SetActiveTool<VoxelWorldEditorTool>();
        }
       
    }
}


//Create the spiffy toolbar addition
[EditorTool("Edit Voxel World", typeof(VoxelWorld))]
public class VoxelWorldEditorTool : EditorTool {

    public static bool buttonActive = false;

    GUIContent iconContent = null;
    
    public override void OnActivated() {
        buttonActive = true;
        
    }
    public override void OnWillBeDeactivated() {
        buttonActive = false;
    }
    public override GUIContent toolbarIcon {
        get { 
            if (iconContent == null) {
                iconContent = new GUIContent() {
                    image = Resources.Load<Texture>("VoxelIcon"),
                    tooltip = "Voxel"
                };
            }
            return iconContent; 
        }
    }
}

#endif