#if UNITY_EDITOR

using Code.Airship.Resources.VoxelRenderer.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using System;
using static VoxelEditAction;
using UnityEditor.SceneManagement;
using VoxelWorldStuff;

public class VoxelEditAction {
    
    public struct EditInfo{
        public Vector3Int position;
        public ushort oldValue;
        public ushort newValue;
        //constructor
        public EditInfo(Vector3Int position, ushort oldValue, ushort newValue){
            this.position = position;
            this.oldValue = oldValue;
            this.newValue = newValue;
        }
    }
    public List<EditInfo> edits = new List<EditInfo>();

    [NonSerialized]
    public WeakReference<VoxelWorld> world;
    public void CreateSingleEdit(VoxelWorld world, Vector3Int position, ushort oldValue, ushort newValue) {
        edits.Add(new EditInfo(position, oldValue, newValue));
        this.world = new(world);
    }

    public void CreateMultiEdit(VoxelWorld world, List<EditInfo> edits) {
        this.edits = edits;
        this.world = new(world);
    }
}

public class VoxelEditMarker : ScriptableObject {
    public VoxelEditAction lastAction;
}
public class VoxelEditManager : Singleton<VoxelEditManager> {
    public Dictionary<string, VoxelPlacementModifier> placementModifiers = new();
    public bool buildModsEnabled = false;
    VoxelEditMarker undoObject;
    public List<VoxelEditAction> edits = new List<VoxelEditAction>();
    public List<VoxelEditAction> redos = new List<VoxelEditAction>();

    private HashSet<Vector3Int> WriteVoxel(VoxelWorld world, Vector3Int position, ushort num) {
        var positionSet = new HashSet<Vector3Int>() { position };
        if (buildModsEnabled) {
            foreach (var (id, modifier) in placementModifiers) {
                modifier.OnPlaceVoxels(world, positionSet);
            }
        }

        foreach (var pos in positionSet) {
            world.WriteVoxelAtInternal(pos, num);
        }
        return positionSet;
    }
    
    public HashSet<Vector3Int> ColorVoxel(VoxelWorld world, Vector3Int position, Color col) {
        var positionSet = new HashSet<Vector3Int>() { position };
        if (buildModsEnabled) {
            foreach (var (id, modifier) in placementModifiers) {
                modifier.OnPlaceVoxels(world, positionSet);
            }
        }

        foreach (var pos in positionSet) {
            world.ColorVoxelAt(pos, col, false);
        }
        return positionSet;
    }
        
    public void AddEdit(VoxelWorld world, Vector3Int position, ushort oldValue, ushort newValue, string name) {
        VoxelEditAction edit = new VoxelEditAction();
        edit.CreateSingleEdit(world, position, oldValue, newValue);
        edits.Add(edit);

        //If we're adding a new edit, clear the redos
        redos.Clear();

        if (undoObject == null) {
            undoObject = ScriptableObject.CreateInstance<VoxelEditMarker>();
        }
        undoObject.lastAction = edit;

        //Save the state of this whole object into the undo system
        Undo.RegisterCompleteObjectUndo(undoObject, name);

        foreach (var affectedPos in WriteVoxel(world, position, newValue)) {
            world.DirtyNeighborMeshes(affectedPos);   
        }

        world.hasUnsavedChanges = true; 
    }

    public void AddEdits(VoxelWorld world, List<EditInfo> editInfos, string name) {
        VoxelEditAction edit = new VoxelEditAction();
        edit.CreateMultiEdit(world, editInfos);
        edits.Add(edit);

        //If we're adding a new edit, clear the redos
        redos.Clear();

        if (undoObject == null) {
            undoObject = ScriptableObject.CreateInstance<VoxelEditMarker>();
        }
        undoObject.lastAction = edit;

        //Save the state of this whole object into the undo system
        Undo.RegisterCompleteObjectUndo(undoObject, name);

        foreach (EditInfo editInfo in editInfos) {
            var affected = WriteVoxel(world, editInfo.position, editInfo.newValue);
            foreach (var pos in affected) {
                world.DirtyNeighborMeshes(pos);   
            }
        }
        
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
                    foreach (var editInfo in edit.edits) {
                        var affectedPositions = WriteVoxel(currentWorld, editInfo.position, editInfo.oldValue);
                        foreach (var editPos in affectedPositions) {
                            currentWorld.DirtyNeighborMeshes(editPos);   
                        }
                    }
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
                    foreach (var editInfo in edit.edits) {
                        var affectedPositions = WriteVoxel(currentWorld, editInfo.position, editInfo.newValue);
                        foreach (var editPos in affectedPositions) {
                            currentWorld.DirtyNeighborMeshes(editPos);   
                        }
                    }
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
    [NonSerialized]
    GameObject handle = null;
    [NonSerialized]
    GameObject faceHandle = null;
    [NonSerialized]
    GameObject raytraceHandle = null;
    
    [NonSerialized]
    bool mouseOverViewport = false;
    [NonSerialized]
    bool lastEnabled = false;
    [NonSerialized]
    bool leftControlDown = false;
    [NonSerialized]
    bool leftShiftDown = false;
    [NonSerialized]
    bool draggingSelection = false;
    [NonSerialized]
    Vector3Int lastPos;
    [NonSerialized]
    Vector3 lastNormal;
    [NonSerialized]
    Vector3Int lastNormalPos;
    [NonSerialized]
    bool validPosition = false;
    [NonSerialized]
    Vector3 placementRotationVector;
    [NonSerialized]
    VoxelWorld.Flips placementFlip = VoxelWorld.Flips.Flip_0Deg;
    [NonSerialized]
    bool placementVertical = false;

    private VoxelWorldEditor() {
        //Add selection handler
        Selection.selectionChanged += OnSelectionChanged;

        //Add a handler for the gizmo refresh event
        SceneView.duringSceneGui += GizmoRefreshEvent;

        EditorApplication.update += OnEditorUpdate;

        //Save handler
        EditorSceneManager.sceneSaving += OnSavingScene;
    }


    private static List<VoxelPlacementModifier> allPlacementModifiers = new() {
        new RotationPlacementMod(),
        new MirrorPlacementMod(),
    };
        
    public void Load(VoxelWorld world) {

        if (world.voxelWorldFile != null) {
            world.LoadWorldFromSaveFile(world.voxelWorldFile);
        }
    }
    
    [MenuItem("GameObject/Airship/Voxel World", false, 0)]
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
        if(rollbackManager){
            rollbackManager.voxelWorld = voxelWorld;
        }

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
        world.autoLoad = EditorGUILayout.Toggle("Auto Load On Enable", world.autoLoad);
        EditorGUILayout.Space(4);


        if (world.voxelWorldFile != null) {
            if (GUILayout.Button("Load")) {
                if (world.hasUnsavedChanges) {
                    if (EditorUtility.DisplayDialog("Discarding Changes",
                            "Are you sure you want to discard unsaved changes to the Voxel World?", "Discard", "Cancel")) {
                        Load(world);
                    }
                } else {
                    Load(world);
                }
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
            if (GUILayout.Button("Unload")) {
                if (world.hasUnsavedChanges) {
                    if (EditorUtility.DisplayDialog("Discarding Changes",
                            "Are you sure you want to discard unsaved changes to the Voxel World?", "Discard", "Cancel")) {
                        world.DeleteRenderedGameObjects();
                    }
                } else {
                    world.DeleteRenderedGameObjects();
                }
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
        if (GUILayout.Button("Generate Flat World")) {
            world.GenerateWorld();
            world.FillFlatGround();
        }
        if (GUILayout.Button("Generate Empty World")) {
            world.GenerateWorld();
            world.FillSingleBlock();
        }

        AirshipEditorGUI.HorizontalLine();

        EditorGUILayout.LabelField("Editing", EditorStyles.boldLabel);

        //Add a button for "Open editor"
        if (GUILayout.Button("Open Editor")) {
            //open theVoxelWorldEditor
            VoxelBuilderEditorWindow.ShowWindow();
        }

        EditorGUILayout.LabelField("Build Mods", EditorStyles.boldLabel);
        VoxelEditManager.Instance.buildModsEnabled =
            GUILayout.Toggle(VoxelEditManager.Instance.buildModsEnabled, "Enabled");
        // placementModsOpen = EditorGUILayout.Foldout(placementModsOpen, "Build Mods");
        // if (placementModsOpen) {
        GUI.enabled = VoxelEditManager.Instance.buildModsEnabled;
        EditorGUI.indentLevel++;
        var enabledMods = VoxelEditManager.Instance.placementModifiers;
        foreach (var placementMod in allPlacementModifiers) {
            var modName = placementMod.GetName();
            var modEnabledOld = enabledMods.ContainsKey(modName);
            var modEnabledNew = GUILayout.Toggle(modEnabledOld, modName);
            if (modEnabledOld && !modEnabledNew) { // Disabled mod
                enabledMods.Remove(modName);
            } else if (!modEnabledOld && modEnabledNew) { // Enable mod
                enabledMods[modName] = placementMod;
            }

            GUI.enabled = modEnabledNew;
            EditorGUI.indentLevel++;
            placementMod.OnInspectorGUI();
            EditorGUI.indentLevel--;
            GUI.enabled = VoxelEditManager.Instance.buildModsEnabled;
        }
        EditorGUI.indentLevel--;
        GUI.enabled = true;

        EditorGUILayout.Space(10);
        AirshipEditorGUI.HorizontalLine();


        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

        //Add a divider
        GUILayout.Box("", new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.Height(1) });

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);

        // World Networker picker
        world.worldNetworker = (VoxelWorldNetworker)EditorGUILayout.ObjectField("Voxel World Networker", world.worldNetworker, typeof(VoxelWorldNetworker), true);
        
        //Add a field for voxelBlocks
        world.voxelBlocks = (VoxelBlocks)EditorGUILayout.ObjectField("Voxel Blocks", world.voxelBlocks, typeof(VoxelBlocks), true);

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Reload Block Atlas")) {
            world.ReloadTextureAtlas();
        }

        EditorGUILayout.Space(5);
        AirshipEditorGUI.HorizontalLine();
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        if (GUILayout.Button("Debug Emit block"))
        {
            MeshProcessor.ProduceSingleBlock(world.selectedBlockIndex, world);
        }
        
        if (GUI.changed) {
            EditorUtility.SetDirty(world);
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

        VoxelWorld world = (VoxelWorld)target;
        if (world) {
            world.highlightedBlock = 0;
        }
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
            Vector3 rawPos = hitPosition - (normal * 0.5f);
            Vector3 pos = VoxelWorld.FloorInt(rawPos) + new Vector3(0.5f, 0.5f, 0.5f);
            
            lastPos = VoxelWorld.FloorInt(pos);
            lastNormal = normal;
            lastNormalPos = VoxelWorld.FloorInt(pos + (lastNormal * 0.6f));
            validPosition = true;
            
            //We aiming at the top half of a block?
            Vector3 blockTargetPos = hitPosition + (normal * 0.001f);
            if (blockTargetPos.y - Mathf.Floor(blockTargetPos.y) > 0.5) {
                placementVertical = true;
            }
            else {
                placementVertical = false;
            }

            Vector3 viewDir = Vector3.zero;

            if (world.currentCamera != null) {
                viewDir = world.TransformVectorToLocalSpace(world.currentCamera.transform.forward);
            }
            placementRotationVector = VoxelWorld.CardinalVector(new Vector3(viewDir.x,0, viewDir.z).normalized);

             
            if (placementRotationVector.x > 0.01) {
                placementFlip = VoxelWorld.Flips.Flip_180Deg;
            }
            else if (placementRotationVector.x < -0.01) {
                placementFlip = VoxelWorld.Flips.Flip_0Deg;
            }
            else if (placementRotationVector.z < -0.01) {
                placementFlip = VoxelWorld.Flips.Flip_270Deg;
            } else {
                placementFlip = VoxelWorld.Flips.Flip_90Deg;
            }
            if (placementVertical) {
                placementFlip += 4;
            }
 

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
            handle.transform.localRotation = Quaternion.identity;

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

            //Track what we mouse over'd
            world.highlightedBlockPos = lastPos;
            world.highlightedBlock = world.GetVoxelAt(lastPos);
            
        }
        
        if (faceHandle) {
            faceHandle.transform.position = world.TransformPointToWorldSpace(lastPos + new Vector3(0.5f, 0.5f, 0.5f) + lastNormal * 0.51f);
 
            
            Vector3 forward = world.transform.forward;
            Vector3 up = world.transform.up;
            Vector3 normal = world.TransformVectorToWorldSpace(lastNormal);

            if (Mathf.Abs(Vector3.Dot(normal, up)) > 0.7f) {
                faceHandle.transform.rotation = Quaternion.LookRotation(normal, forward);
            }
            else {
                faceHandle.transform.rotation = Quaternion.LookRotation(normal, up);
            }
            
            MeshRenderer ren = faceHandle.GetComponent<MeshRenderer>();
            /*if (leftControlDown == true) {
                ren.sharedMaterial.SetColor("_Color", new Color(0, 1, 0, 0.25f));
            }
            else {
                ren.sharedMaterial.SetColor("_Color", new Color(1, 1, 0, 0.25f));
            }*/
            ren.sharedMaterial.SetColor("_Color", new Color(1, 1, 0, 0.25f));
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
        bool enabled = VoxelBuilderEditorWindow.Enabled() && (VoxelWorldEditorToolBase.buttonActive || VoxelWorldBrushToolBase.buttonActive || VoxelWorldPaintBucketToolBase.buttonActive);
        
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
 
            DoMouseMoveEvent(Event.current.mousePosition,  world);
           
            UpdateHandlePosition(world);
            SceneView.RepaintAll();
        }

        //Leftclick up
        if (VoxelWorldEditorToolBase.buttonActive) {
            if (e.type == EventType.MouseUp && e.button == 0) {

                // Create a ray from the mouse position
                if (validPosition) {
                    if (Event.current.shift) {
                        // Remove voxel
                        Vector3Int voxelPos = lastPos;
                        ushort oldValue =
                            world.GetVoxelAt(voxelPos); // Assuming you have a method to get the voxel value
                             
                        VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

                        voxelEditManager.AddEdit(world, voxelPos, oldValue, 0, "Delete Voxel");

                        if (leftControlDown == false) {
                            //Refresh the gizmo like we just moved the mouse here
                            DoMouseMoveEvent(Event.current.mousePosition, world);
                        }
                        else {
                            //Move the pos against the normal to delete along this vector
                            lastPos -= VoxelWorld.CardinalVector(lastNormal);
                            lastNormalPos -= VoxelWorld.CardinalVector(lastNormal);
                        }
                    } else if (Event.current.alt) {
                        ushort voxel = world.GetVoxelAt(lastPos);
                        world.selectedBlockIndex = VoxelWorld.VoxelDataToBlockId(voxel);;
                    } else {
                        // Add voxel
                        Vector3Int voxelPos = lastNormalPos;
                        ushort oldValue =
                            world.GetVoxelAt(voxelPos); // Assuming you have a method to get the voxel value
                        ushort newValue = (ushort)world.selectedBlockIndex;

                        VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

                        var def = world.voxelBlocks.GetBlock(newValue);

                        if (def.definition.rotatedPlacement) {
                            newValue = (ushort)VoxelWorld.SetVoxelFlippedBits(newValue, (int)placementFlip);
                        } 

                        voxelEditManager.AddEdit(world, voxelPos, oldValue, newValue,
                            "Add Voxel " + def.definition.name);

                         
                        //Move the pos by the normal to continue this "line" of voxels
                        lastPos += VoxelWorld.CardinalVector(lastNormal);
                        lastNormalPos += VoxelWorld.CardinalVector(lastNormal);
                        

                        
                    }
                    
                }

                UpdateHandlePosition(world);

                //Repaint
                SceneView.RepaintAll();
            }
        }

        // Voxel painter
        if (VoxelWorldBrushToolBase.buttonActive) {
            if ((e.type == EventType.MouseDrag || e.type == EventType.MouseDown) && e.button == 0) {
                // Create a ray from the mouse position
                if (validPosition) {
                    var voxelPos = lastPos;
                    var oldColor = world.GetVoxelColorAt(voxelPos);
                    Color newCol;
                    if (e.shift) {
                        newCol = new Color32((byte) (Math.Max(oldColor.r + -5, 0)), oldColor.g, oldColor.b, oldColor.a);
                    } else {
                        var colIncr = 5;
                        if (oldColor.r == 0) colIncr = 1; // If just being lightly painted to mark as receiving color
                        newCol = new Color32((byte) (Math.Min(oldColor.r + colIncr, 255)), oldColor.g, oldColor.b, oldColor.a);
                    }
                    VoxelEditManager.Instance.ColorVoxel(world, voxelPos, newCol);
                }
            }
        }
        
        if (VoxelWorldPaintBucketToolBase.buttonActive) {
            if (e.type == EventType.MouseDown && e.button == 0) {
                // Create a ray from the mouse position
                if (validPosition) {
                    // Add voxel
                    Vector3Int voxelPos = lastPos;
                    ushort oldValue =
                        world.GetVoxelAt(voxelPos); // Assuming you have a method to get the voxel value
                    ushort oldBlockId = VoxelWorld.VoxelDataToBlockId(oldValue);
                    if (oldBlockId > 0) {
                        var edits = new List<EditInfo>();
                        PaintBucket(world, edits, lastPos, oldValue, (ushort) world.selectedBlockIndex, new HashSet<Vector3>());
                        VoxelEditManager.Instance.AddEdits(world, edits, "Paint Bucket");
                    }
                }
            }
        }

        if (Event.current.GetTypeForControl(controlID) == EventType.KeyUp) {
            if (Event.current.keyCode == KeyCode.LeftControl) {
                leftControlDown = false;
            }

            if (Event.current.keyCode == KeyCode.LeftShift) {
                leftShiftDown = false;
            }

            if (Event.current.keyCode == KeyCode.A) {
                //Cycle the bits on the selected block
                if (world.selectedBlockIndex > 0) {

                    ushort oldValue = world.GetVoxelAt(lastPos); // Assuming you have a method to get the voxel value
                    ushort newValue = oldValue;

                    int flipBits = VoxelWorld.GetVoxelFlippedBits(oldValue);
                    flipBits += 1;
                    flipBits %= 8;
                    newValue = (ushort)VoxelWorld.SetVoxelFlippedBits(newValue, flipBits);

                    VoxelEditManager voxelEditManager = VoxelEditManager.Instance;

                    var def = world.voxelBlocks.GetBlock(newValue);

                    //newValue = (ushort)VoxelWorld.SetVoxelFlippedBits(newValue, 0x04  );
                    voxelEditManager.AddEdit(world, lastPos, oldValue, newValue, "Flip Voxel " + def.definition.name);
                }
                //Refresh the view
                UpdateHandlePosition(world);
                //Repaint
                SceneView.RepaintAll();
            }

            if (Event.current.keyCode == KeyCode.M) {
                //Cycle the bits on the selected block
                if (world.selectedBlockIndex > 0) {

                    //not undoable
                    var chunk = world.GetChunkByVoxel(lastPos);
                    if (chunk != null) {
                        var localKey = (ushort)VoxelWorldStuff.Chunk.WorldPosToVoxelIndex(lastPos);
                        
                        chunk.damageMap[localKey] = 1;
                        world.DirtyNeighborMeshes(lastPos, true);
                    }
                }
                //Refresh the view
                UpdateHandlePosition(world);
                //Repaint
                SceneView.RepaintAll();
            }
        }
    }

    public void PaintBucket(VoxelWorld world, List<EditInfo> edits, Vector3 pos, ushort from, ushort target, HashSet<Vector3> visited) {
        visited.Add(pos);

        var voxelAtPos = world.GetVoxelAt(pos);
        // Debug.Log("Voxel at pos: " + VoxelWorld.VoxelDataToBlockId(voxelAtPos) + " where from=" + from);
        if (VoxelWorld.VoxelDataToBlockId(voxelAtPos) != VoxelWorld.VoxelDataToBlockId(from)) return;
            
        edits.Add(new EditInfo(VoxelWorld.FloorInt(pos), from, target));

        for (var axis = 0; axis < 3; axis++) {
            for (var sign = -1; sign <= 1; sign += 2) {
                var newPos = pos;
                newPos[axis] += sign; // Cool Vector3 accessor!
                // Debug.Log("Check out pos: " + newPos);
                
                if (visited.Contains(newPos)) continue;
                
                PaintBucket(world, edits, newPos, from, target, visited);
            }
        }
    }


    private void OnDestroy() {
        //Remove selection handler
        Selection.selectionChanged -= OnSelectionChanged;

        //Remove the gizmo refresh event handler
        SceneView.duringSceneGui -= GizmoRefreshEvent;

        EditorApplication.update -= OnEditorUpdate;

        //Save handler
        EditorSceneManager.sceneSaving -= OnSavingScene;
    }

    [NonSerialized]
    private GameObject lastSelectedGameObject = null;

    private void OnEditorUpdate() {
        
        //Check to see if we selected the selection zone of this voxelWorldEditor
        GameObject selected = Selection.activeGameObject;

        // If selection has changed and the object is inactive or active, detect it
        if (selected != lastSelectedGameObject) {
            if (selected != null && !selected.activeInHierarchy) {
                
                SelectionZone zone = selected.GetComponent<SelectionZone>();
                if (zone) { //&& zone.voxelWorld.gameObject == target
                    //If it does, select the voxel world
                    selected.SetActive(true);
                    ToolManager.SetActiveTool<VoxelWorldSelectionToolBase>();
                    
                }
            }
            
            // Store the current selection to detect changes
            lastSelectedGameObject = selected;
        }
    }

    private void OnSavingScene(UnityEngine.SceneManagement.Scene scene, string path) {
        VoxelWorld world = (VoxelWorld)target;
        if (world == null) {
            return;
        }
        if (world.hasUnsavedChanges == true) {
            Debug.Log("Saving voxels because scene is saving.");
           
            world.SaveToFile();
        }
    }

    void OnSelectionChanged() { 
        if (target == null) {
            return;
        }
        //If we're seleceted
        if (Selection.activeGameObject == ((VoxelWorld)target).gameObject) {
            ToolManager.SetActiveTool<VoxelWorldEditorToolBase>();
            
        }
    }
}


//Create the spiffy toolbar addition
public class VoxelWorldEditorToolBase : EditorTool {

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



public class VoxelWorldSelectionToolBase : EditorTool {

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

public class VoxelWorldPaintBucketToolBase : EditorTool {

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
                    image = Resources.Load<Texture>("PaintBucketIcon"),
                    tooltip = "Paint Bucket"
                };
            }
            return iconContent;
        }
    }
}

public class VoxelWorldBrushToolBase : EditorTool {

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
                    image = Resources.Load<Texture>("BrushIcon"),
                    tooltip = "Brush"
                };
            }
            return iconContent; 
        }
    }
}

[EditorTool("Edit Voxel World", typeof(VoxelWorld))]
public class VoxelWorldSelectionToolVW : VoxelWorldSelectionToolBase {
}

[EditorTool("Edit Voxel Selection", typeof(VoxelWorld))]
public class VoxelWorldEditorToolVW : VoxelWorldEditorToolBase {
}

[EditorTool("Paint Bucket", typeof(VoxelWorld))]
public class VoxelWorldPaintBucketToolVW : VoxelWorldPaintBucketToolBase {
}

[EditorTool("Paint Voxels", typeof(VoxelWorld))]
public class VoxelWorldBrushToolVW : VoxelWorldBrushToolBase {
}

//Same again for SelectionZone
[EditorTool("Edit Voxel World", typeof(SelectionZone))]
public class VoxelWorldSelectionToolSZ : VoxelWorldSelectionToolBase {
}

[EditorTool("Edit Voxel Selection", typeof(SelectionZone))]
public class VoxelWorldEditorToolSZ : VoxelWorldEditorToolBase {
}

[EditorTool("Paint Voxels", typeof(SelectionZone))]
public class VoxelWorldBrushToolSZ : VoxelWorldBrushToolBase {
}

#endif