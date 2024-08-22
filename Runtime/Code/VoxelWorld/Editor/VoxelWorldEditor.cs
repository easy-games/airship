#if UNITY_EDITOR

using Code.Airship.Resources.VoxelRenderer.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using System;
using static UnityEditor.PlayerSettings;

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

        world.hasUnsavedChanges = true; 
 
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
                    currentWorld.hasUnsavedChanges = true;
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
                    currentWorld.hasUnsavedChanges = true;
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
    GameObject faceHandle = null;
    GameObject raytraceHandle = null;
    
    bool mouseOverViewport = false;
    bool lastEnabled = false;
    bool leftControlDown = false;
    bool leftShiftDown = false;

    Vector3Int lastPos;
    
    Vector3 lastNormal;
    Vector3Int lastNormalPos;
    bool validPosition = false;
        
    public void Load(VoxelWorld world) {

        if (world.voxelWorldFile != null) {
            world.LoadWorldFromSaveFile(world.voxelWorldFile);
        }
    }

  
    
    [MenuItem("GameObject/Airship/3D Object/VoxelWorld", false, 100)]
    static void CreateAirshipVoxelWorld(MenuCommand menuCommand) {
        var parent = menuCommand.context as GameObject;

        //Find a blockDefine object or create one
        VoxelBlocks voxelBlocks = GameObject.FindAnyObjectByType<VoxelBlocks>();

        if (voxelBlocks == null) {
            voxelBlocks = new GameObject("VoxelBlocks").AddComponent<VoxelBlocks>();
            voxelBlocks.name = "VoxelBlocks";
            voxelBlocks.blockDefinionLists = new();

            //Find this asset if it exists
            var resource = Resources.Load<VoxelBlockDefinionList>("VoxelWorldDefaultBlocks/DefaultVoxelBlockDefinionList");
            voxelBlocks.blockDefinionLists.Add(resource);
        }

        var voxelWorldGo = new GameObject("VoxelWorld");
        var voxelWorld = voxelWorldGo.AddComponent<VoxelWorld>();
        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(DefaultBlockDefinesPath);
        voxelWorld.voxelBlocks = voxelBlocks;
        GameObjectUtility.SetParentAndAlign(voxelWorldGo, parent);

        var rollbackManager = voxelWorldGo.GetComponent<VoxelRollbackManager>();
        rollbackManager.voxelWorld = voxelWorld;

        //var voxelWorldNetworkerGo = new GameObject("VoxelWorldNetworker");
        //voxelWorldGo.AddComponent<NetworkObject>();
        //var voxelWorldNetworker = voxelWorldGo.AddComponent<VoxelWorldNetworker>();
        //voxelWorldNetworker.world = voxelWorld;
        //Debug.Log("voxelWorldNetworker world: " + voxelWorldNetworker.world);
        //GameObjectUtility.SetParentAndAlign(voxelWorldGo, parent);
        //voxelWorld.worldNetworker = voxelWorldNetworker;

        var undoId = Undo.GetCurrentGroup();
        Undo.RegisterCreatedObjectUndo(voxelWorldGo, "Create " + voxelWorldGo.name);
        Undo.CollapseUndoOperations(undoId);

        voxelWorld.GenerateWorld(false);
        voxelWorld.CreateSingleStarterBlock();

        Selection.activeObject = voxelWorldGo;
    } 

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
                
                //world.GenerateWorld();

                WorldSaveFile worldSaveFile = CreateNewVoxelWorldFile();
                if (worldSaveFile) {
                    world.voxelWorldFile = worldSaveFile;
                }

                world.SaveToFile();
            } 
        }
        if (world.hasUnsavedChanges == true) {
            GUI.color = Color.yellow;
            GUILayout.Label("(Unsaved Changes)");
            GUI.color = Color.white;
        }
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
        CleanupHandles();
    }

    private void CleanupHandles() {
        if (this.handle) {
            DestroyImmediate(this.handle);
        }
        if (this.faceHandle) {
            DestroyImmediate(this.faceHandle);
        }

        if (this.raytraceHandle) {
            DestroyImmediate(this.raytraceHandle);
        }
        
        validPosition = false;
    }

    private void DoMouseMoveEvent(Vector2 mousePosition, VoxelWorld world) {
        // Create a ray from the mouse position
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);

        //Transform the ray into localspace of this world
        ray = world.TransformRayToLocalSpace(ray);
        (bool res, float distance, Vector3 hitPosition, Vector3 normal) = world.RaycastVoxel_Internal(ray.origin, ray.direction, 200);//, out Vector3 pos, out Vector3 hitNormal);

        if (res == true) {
      
            //handle.transform.position = pos + new Vector3(0.5f, 0.5f, 0.5f); //;//+  VoxelWorld.FloorInt(pos)+ new Vector3(0.5f,0.5f,0.5f);
            // Vector3 pos = ray.origin + ray.direction * (distance + 0.01f);
            Vector3 pos = hitPosition - (normal * 0.5f);
            pos = VoxelWorld.FloorInt(pos) + new Vector3(0.5f, 0.5f, 0.5f);
            
            lastPos = VoxelWorld.FloorInt(pos);
            lastNormal = normal;
            lastNormalPos = VoxelWorld.FloorInt(pos + (lastNormal * 0.6f));
            validPosition = true;
        }
        else {
            validPosition = false;
        }
    }

    private void UpdateHandlePosition(VoxelWorld world) {
    
        if (validPosition == false) {
            CleanupHandles();
            return;
              
        }

        if (handle == null) {
            handle = new GameObject();
            WireCube wireCube = handle.AddComponent<WireCube>();
            wireCube.color = Color.yellow;
            handle.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);
            handle.transform.parent = world.transform;
            handle.name = "_SelectionHandle";
            handle.hideFlags = HideFlags.HideAndDontSave; 
        }



        if (faceHandle == null) {
            faceHandle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            faceHandle.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);
            faceHandle.transform.parent = world.transform;
            faceHandle.name = "_FaceHandle";
            faceHandle.hideFlags = HideFlags.HideAndDontSave;
            
            MeshRenderer ren = faceHandle.GetComponent<MeshRenderer>();
            ren.sharedMaterial = UnityEngine.Resources.Load<Material>("Selection");
        }
        if (Event.current.shift) { //Delete
            DestroyImmediate(faceHandle);
            faceHandle = null;
                
        }
        

        if (handle) {
            handle.transform.position = world.TransformPointToWorldSpace(lastPos + new Vector3(0.5f,0.5f,0.5f));
            handle.transform.localScale = new Vector3(1.01f, 1.01f, 1.01f);

            WireCube wireCube = handle.GetComponent<WireCube>();
            if (wireCube) {

                wireCube.color = Color.white;
                
                if (Event.current.control) {//lock
                    wireCube.color = Color.green;
                }
                if (Event.current.shift) { //Delete
                    wireCube.color = Color.red;
                }

                wireCube.Update();
            }
        }
        
        if (faceHandle) {
            faceHandle.transform.position = world.TransformPointToWorldSpace(lastPos + new Vector3(0.5f, 0.5f, 0.5f) + lastNormal * 0.51f);
            faceHandle.transform.rotation = Quaternion.LookRotation(lastNormal);

            MeshRenderer ren = faceHandle.GetComponent<MeshRenderer>();
            if (leftControlDown == true) {
                ren.sharedMaterial.SetColor("_Color", new Color(0, 1, 0, 0.25f));
            }
            else {
                ren.sharedMaterial.SetColor("_Color", new Color(1, 1, 0, 0.25f));
            }
        }

    }

    private void GizmoRefreshEvent(SceneView obj) {
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        VoxelWorld world = (VoxelWorld)target;

        if (world == null) {
            return;
        }
        Event e = Event.current;

        //Only allow editing if both the editor window is active and the gizmo toolbar is active
        bool enabled = VoxelBuilderEditorWindow.Enabled() && VoxelWorldEditorTool.buttonActive;
        
        if (enabled != lastEnabled) {
            CleanupHandles();
        }
        lastEnabled = enabled;

        if (enabled == false) {
            return;
        }

        if (e.type == EventType.MouseMove) {
            Rect viewRect = SceneView.currentDrawingSceneView.position;

            if (viewRect.Contains(Event.current.mousePosition)) {
                mouseOverViewport = true;
            }
            else if (mouseOverViewport) {
                mouseOverViewport = false;
                CleanupHandles();

            }

            if (leftControlDown == false) {
                
                DoMouseMoveEvent(Event.current.mousePosition, world);
            }
            UpdateHandlePosition(world);
        }

        //Leftclick up
        if (e.type == EventType.MouseUp && e.button == 0) {
            
            // Create a ray from the mouse position
            if (validPosition) {
                
                if (Event.current.shift) {
                    // Remove voxel
                    Vector3Int voxelPos = lastPos;
                    ushort oldValue = world.GetVoxelAt(voxelPos); // Assuming you have a method to get the voxel value

                    VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

                    voxelEditManager.AddEdit(world, voxelPos, oldValue, 0, "Delete Voxel");

                    if (leftControlDown == false) {
                        //Refresh the gizmo like we just moved the mouse here
                        DoMouseMoveEvent(Event.current.mousePosition, world);
                    } else {
                        //Move the pos against the normal to delete along this vector
                        lastPos -= VoxelWorld.CardinalVector(lastNormal);
                        lastNormalPos -= VoxelWorld.CardinalVector(lastNormal);
                    }
                }
                else {
                    // Add voxel
                    Vector3Int voxelPos = lastNormalPos;
                    ushort oldValue = world.GetVoxelAt(voxelPos); // Assuming you have a method to get the voxel value
                    ushort newValue = (ushort)world.selectedBlockIndex;

                    VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

                    var def = world.voxelBlocks.GetBlock(newValue);
                    voxelEditManager.AddEdit(world, voxelPos, oldValue, newValue, "Add Voxel " + def.definition.name);

                    if (leftControlDown == false) {
                        //Refresh the gizmo like we just moved the mouse here
                        DoMouseMoveEvent(Event.current.mousePosition, world);
                    }
                    else {
                        //Move the pos by the normal to continue this "line" of voxels
                        lastPos += VoxelWorld.CardinalVector(lastNormal);
                        lastNormalPos += VoxelWorld.CardinalVector(lastNormal);
                        
                    }
                }
            }


            UpdateHandlePosition(world);
        }

        if (Event.current.GetTypeForControl(controlID) == EventType.KeyDown) {
            if (Event.current.keyCode == KeyCode.LeftControl) {
                leftControlDown = true;
            }
            if (Event.current.keyCode == KeyCode.LeftShift) {
                leftShiftDown = true;
            }
            //Refresh the view
            UpdateHandlePosition(world);
            //Repaint
            SceneView.RepaintAll();
            
        }
        if (Event.current.GetTypeForControl(controlID) == EventType.KeyUp) {
            if (Event.current.keyCode == KeyCode.LeftControl) {
                leftControlDown = false;
            }
            if (Event.current.keyCode == KeyCode.LeftShift) {
                leftShiftDown = false;
            }
            //Refresh the view
            UpdateHandlePosition(world);
            //Repaint
            SceneView.RepaintAll();
          
        }

    }

    void Awake(){

        //Add selection handler
        Selection.selectionChanged += OnSelectionChanged;

        //Add a handler for the gizmo refresh event
        SceneView.duringSceneGui += GizmoRefreshEvent;
    }

    private void OnDestroy() {
        //Remove selection handler
        Selection.selectionChanged -= OnSelectionChanged;

        //Remove the gizmo refresh event handler
        SceneView.duringSceneGui -= GizmoRefreshEvent;
    }

    void OnSelectionChanged() {
        if (target == null) {
            return;
        }
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

    static GUIContent iconContent = null;
    
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


[EditorTool("Edit Voxel Selection", typeof(VoxelWorld))]
public class VoxelWorldSelectionTool : EditorTool {

    public static bool buttonActive = false;

    static GUIContent iconContent = null;

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
                    image = Resources.Load<Texture>("SelectIcon"),
                    tooltip = "Selection"
                };
            }
            return iconContent;
        }
    }
}
#endif