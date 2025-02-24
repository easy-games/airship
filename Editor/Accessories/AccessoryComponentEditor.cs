using UnityEngine;
using UnityEditor;
using Editor.Accessories;
using System.Linq;
using Code.Player.Accessories;
using Code.Platform.Server;
using Code.Platform.Shared;
using Editor.Auth;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

[CustomEditor(typeof(AccessoryComponent))]
public class AccessoryComponentEditor : UnityEditor.Editor {

    private bool foldout = false; // Variable to handle foldout state

    private void OnEnable() {
        var accessoryComponent = (AccessoryComponent)target;
        this.foldout = accessoryComponent.bodyMask > 0;
    }

    [MenuItem("Airship/Avatar/Generate Avatar Items On Server")]
    public static void CreateAvatarItemsOnServer() {
        CreateAllAvatarItems();
    }

    private static async Task CreateAllAvatarItems(){
#if AIRSHIP_STAGING 
            var staging = true;
#else
            var staging = false;
#endif

        var collection = AssetDatabase.LoadAssetAtPath<AvatarAccessoryCollection>(
            "Assets/AirshipPackages/@Easy/Core/Prefabs/Accessories/AvatarItems/EntireAvatarCollection.asset");
        if (!collection) {
            Debug.LogError("Failed to find collection.");
            return;
        }

        //Find Organization
        var orgResourceId = staging ? "6536df9f3843ac629cf3b8b1" : "6b62d6e3-9d74-449c-aeac-b4feed2012b1";

        var allAcc = new List<AccessoryComponent>();
        var allFace = new List<AccessoryFace>();
        var printStatement = "Are you sure you want to create an new item for all of these items?";
        //Find any missing class ID's
        foreach (var accessory in collection.accessories) {
            var classId = staging ? accessory.serverClassIdStaging : accessory.serverClassId;
            if(string.IsNullOrEmpty(classId)){
                printStatement += "\nACC: "+accessory.name;
                allAcc.Add(accessory);
            }
        }
        foreach (var accessory in collection.faces) {
            var classId = staging ? accessory.serverClassIdStaging : accessory.serverClassId;
            if(string.IsNullOrEmpty(classId)){
                printStatement += "\nFACE: "+accessory.name;
                allFace.Add(accessory);
            }
        }
        var totalItems = allAcc.Count + allFace.Count;
        printStatement += "\nTotal Items: " + totalItems;
        
        if(totalItems > 0){
            if(EditorUtility.DisplayDialog("CREATING NEW SERVER ITEMS", printStatement, "CREATE", "Cancel")){
                foreach(var acc in allAcc){
                    var result = await GenerateServerItem(orgResourceId, acc.name, staging);
                    if(staging){
                        acc.serverClassIdStaging = result;
                    }else{
                        acc.serverClassId = result;
                    }
                    EditorUtility.SetDirty(acc);
                }

                foreach(var face in allAcc){
                    var result = await GenerateServerItem(orgResourceId, face.name, staging);
                    if(staging){
                        face.serverClassIdStaging = result;
                    }else{
                        face.serverClassId = result;
                    }
                    EditorUtility.SetDirty(face);
                }
                AssetDatabase.SaveAssets();
            }
        }else{
            EditorUtility.DisplayDialog("CREATING NEW SERVER ITEMS", "Unable to find any missing accessories on the server.", "Ok");
        }
        
    }

    //Creates an item on the server and returns the class ID
    private static async Task<string> GenerateServerItem(string orgResourceId, string accName, bool staging){
        Debug.Log("Creating item on server " + (staging ? "STAGING" : "PRODUCTION") + ": " + accName);
        
		var res = await AirshipInventoryServiceBackend.CreateGearItem(orgResourceId, new GearCreateRequest(){
            name = accName,
            description = "Avatar Item",
            @default = true,
            imageId = "c0e07e88-09d4-4962-b42d-7794a7ad4cb2",//"f49100a9-8279-4a96-9366-807ee22da848",
        });

        if(res.success && res.data.Length > 0){
            Debug.Log("Parsing json: " + res.data);
            var accData = JsonUtility.FromJson<GearClass>(res.data);
            if(accData != null){
                Debug.Log("Created item: " + accData.name + " id: " + accData.classId);
                return accData.classId;
            }else{
                Debug.LogError("Unable to parse json: " + res.data);
            }
        }else {
            Debug.LogError("Unable to create item: " + accName + " error: " + res.error);
        }
        return "";
    }

    public static void GenerateLODs(AccessoryComponent target) {
        Mesh sourceMesh = null;

        if (target.GetComponentsInChildren<MeshFilter>().Length > 1) {
            Debug.LogError("Generate LODs only works with one MeshFilter.");
            return;
        }
        if (target.GetComponentsInChildren<MeshRenderer>().Length > 1) {
            Debug.LogError("Generate LODs only works with one MeshRenderer.");
            return;
        }

        SkinnedMeshRenderer skinnedMeshRenderer = target.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer) {
            sourceMesh = skinnedMeshRenderer.sharedMesh;
        }
        if (sourceMesh == null) {
            MeshFilter meshFilter = target.GetComponentInChildren<MeshFilter>();
            if (meshFilter) {
                sourceMesh = meshFilter.sharedMesh;
            }
        }
        if (sourceMesh == null) {
            Debug.LogError("Must have a MeshFilter or SkinnedMeshRenderer attached with mesh assigned to generate LODs.");
            return;
        }

        // Delete existing LODs
        foreach (var mesh in target.meshLods) {
            var path = AssetDatabase.GetAssetPath(mesh);
            AssetDatabase.DeleteAsset(path);
        }
        target.meshLods.Clear();

        void MakeLOD(int lodLevel, float quality) {
            var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
            meshSimplifier.Initialize(sourceMesh);

            meshSimplifier.SimplifyMesh(quality);
            var mesh = meshSimplifier.ToMesh();

            var selfPath = AssetDatabase.GetAssetPath(sourceMesh);
            if (selfPath.StartsWith("Assets/Gear")) {
                // New logic for clothing bundles
                var split = selfPath.Split("/");
                selfPath = selfPath.Replace("/" + split[split.Length - 1], "");

                var lodFolderPath = $"{selfPath}/LOD";
                if (!AssetDatabase.AssetPathExists(lodFolderPath)) {
                    AssetDatabase.CreateFolder(selfPath, "LOD");
                }

                var path = $"{selfPath}/LOD/{sourceMesh.name} (LOD {lodLevel})";
                Debug.Log("path: " + path);
                if (AssetDatabase.AssetPathExists(path + ".asset")) {
                    int i = 1;
                    while (AssetDatabase.AssetPathExists(path + $" ({i}).asset")) {
                        i++;
                    }
                    path += $" ({i})";
                }
                AssetDatabase.CreateAsset(mesh, path + ".asset");
                target.meshLods.Add(mesh);
            } else {
                string path;
                string parentPath;
                string parentParentPath;

                if (selfPath.StartsWith("Assets/AirshipPackages")) {
                    var split = selfPath.Split("/");
                    parentParentPath = $"{split[0]}/{split[1]}/{split[2]}/{split[3]}";
                    parentPath = $"{parentParentPath}/Accessory LOD";
                    path = $"{parentPath}/{sourceMesh.name} (LOD {lodLevel})";
                } else {
                    parentParentPath = "Assets";
                    parentPath = $"Assets/Accessory LOD";
                    path = $"{parentPath}/{sourceMesh.name} (LOD {lodLevel})";
                }

                if (!AssetDatabase.AssetPathExists(parentPath)) {
                    AssetDatabase.CreateFolder(parentParentPath, "Accessory LOD");
                }

                if (AssetDatabase.AssetPathExists(path + ".asset")) {
                    int i = 1;
                    while (AssetDatabase.AssetPathExists(path + $" ({i}).asset")) {
                        i++;
                    }
                    path += $" ({i})";
                }
                AssetDatabase.CreateAsset(mesh, path + ".asset");
                target.meshLods.Add(mesh);
            }
        }

        MakeLOD(1, 0.3f);
        MakeLOD(2, 0.06f);

        if (!target.skinnedToCharacter) {
            var renderer = target.GetComponentInChildren<Renderer>();

            // setup LOD group
            LODGroup lodGroup = renderer.gameObject.GetComponent<LODGroup>();
            if (lodGroup == null) {
                lodGroup = renderer.gameObject.AddComponent<LODGroup>();

                float[] screenSizes = new[] { 0.20f, 0.05f, 0.0029f };
                var newLods = new List<LOD>();
                GameObject lod0 = new GameObject($"LOD 0") {
                    transform = {
                        parent = renderer.transform,
                        localPosition = Vector3.zero,
                        localRotation = Quaternion.identity,
                        localScale = Vector3.one,
                    }
                };
                {
                    // Setup LOD0
                    if (renderer is MeshRenderer meshRenderer) {
                        var meshFilter = meshRenderer.GetComponent<MeshFilter>();
                        var newMeshFilter = lod0.AddComponent<MeshFilter>();
                        newMeshFilter.sharedMesh = meshFilter.sharedMesh;

                        var newRenderer = lod0.AddComponent<MeshRenderer>();
                        newRenderer.sharedMaterials = renderer.sharedMaterials;

                        if (renderer.TryGetComponent<MaterialColorURP>(out var matColor)) {
                            var newMatColor = lod0.AddComponent<MaterialColorURP>();
                            newMatColor.CopyFrom(matColor);
                            DestroyImmediate(matColor);
                        }
                        DestroyImmediate(renderer);
                        DestroyImmediate(meshFilter);

                        LOD lod = new LOD(screenSizes[0], new Renderer[]{ newRenderer });
                        newLods.Add(lod);
                    }
                }

                for (int i = 1; i <= 2; i++) {
                    var newGO = Instantiate(lod0, lod0.transform.parent);
                    newGO.name = $"LOD {i}";

                    var meshFilter = newGO.GetComponentInChildren<MeshFilter>();
                    meshFilter.sharedMesh = target.meshLods[i - 1];

                    LOD lod = new LOD(screenSizes[i], newGO.GetComponentsInChildren<Renderer>());
                    newLods.Add(lod);
                }

                // Destroy old renderers
                foreach (var existingLOD in lodGroup.GetLODs()) {
                    foreach (var r in existingLOD.renderers) {
                        if (r) {
                            DestroyImmediate(r);
                        }
                    }
                }

                lodGroup.SetLODs(newLods.ToArray());
            }
        }

        EditorUtility.SetDirty(target);
    }

    public override void OnInspectorGUI() {
        AccessoryComponent myTarget = (AccessoryComponent)target;
        var serializedObject = new SerializedObject(target);

        //Accessory Slot
        myTarget.accessorySlot = (AccessorySlot)EditorGUILayout.EnumPopup("Slot", myTarget.accessorySlot);

        //Visibility Mode
        myTarget.visibilityMode = (AccessoryComponent.VisibilityMode)EditorGUILayout.EnumPopup("Visibility", myTarget.visibilityMode);

        //Skinned To Character
        myTarget.skinnedToCharacter = EditorGUILayout.Toggle("Skinned", myTarget.skinnedToCharacter);

        if (myTarget.skinnedToCharacter) {
            EditorGUILayout.Space(10);
            EditorGUI.indentLevel++;

            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);
            buttonStyle.margin = new RectOffset(EditorGUI.indentLevel * 15, 0, 0, 0);
            if (GUILayout.Button("Generate LODs", buttonStyle)) {
                GenerateLODs(myTarget);
                this.SaveChanges();
            }

            var property = serializedObject.FindProperty("meshLods");
            serializedObject.Update();
            EditorGUILayout.PropertyField(property, true);
            serializedObject.ApplyModifiedProperties();
            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
        }

        // Add the Open Editor button:
        EditorGUILayout.Space(10);
        if (RunCore.IsClone()) {
            GUILayout.Label("Accessory Editor disabled in clone window.");
            return;
        }
        if (GUILayout.Button("Open Accessory Editor")) {
            var accessory = targets?.First((obj) => obj is AccessoryComponent) as AccessoryComponent;
            if (accessory != null) {
                AccessoryEditorWindow.OpenWithAccessory(accessory);
            }
        }

        // Start a foldout
        EditorGUILayout.Space();
        this.foldout = EditorGUILayout.Foldout(this.foldout, "Hide Body Parts");
        if (this.foldout) {
            EditorGUI.indentLevel++;

            // Show bools for all the hide bits
            int hideBits = myTarget.bodyMask;

            // Display them based on the sort order in BodyMaskInspectorData
            foreach (var maskData in AccessoryComponent.BodyMaskInspectorDatas) {
                if (maskData.bodyMask == AccessoryComponent.BodyMask.NONE) {
                    continue;
                }

                bool isHidden = (hideBits & (int)maskData.bodyMask) != 0;
                bool newIsHidden = EditorGUILayout.Toggle(maskData.name, isHidden);
                if (newIsHidden != isHidden) {
                    if (newIsHidden) {
                        hideBits |= (int)maskData.bodyMask;
                    }
                    else {
                        hideBits &= ~(int)maskData.bodyMask;
                    }
                }
            }

            if (hideBits != myTarget.bodyMask) {
                myTarget.bodyMask = hideBits;
                EditorUtility.SetDirty(myTarget);
            }

            EditorGUI.indentLevel--;
        }

#if AIRSHIP_INTERNAL
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Legacy IDs");
        myTarget.serverClassId = EditorGUILayout.TextField("Class Id", myTarget.serverClassId);
        myTarget.serverClassIdStaging = EditorGUILayout.TextField("Class Id (Staging)", myTarget.serverClassIdStaging);
#endif

        if (GUI.changed){
            EditorUtility.SetDirty(myTarget);
        }
    }

    [MenuItem("Airship/Avatar/Accessory Editor")]
    public static void OpenAccessoryEditor() {
        AccessoryEditorWindow.OpenOrCreateWindow();
    }

    #if AIRSHIP_INTERNAL
    [MenuItem("Airship/Internal/Generate Skinned Accessory LODs")]
    public static void GenerateLODsForAllAccessories() {
        var collection = AssetDatabase.LoadAssetAtPath<AvatarAccessoryCollection>(
            "Assets/AirshipPackages/@Easy/Core/Prefabs/Accessories/AvatarItems/EntireAvatarCollection.asset"
        );

        Debug.Log($"Generating LODs...");
        var st = Stopwatch.StartNew();
        int counter = 0;
        foreach (var accessory in collection.accessories) {
            if (!accessory.skinnedToCharacter) continue;
            GenerateLODs(accessory);
            counter++;
        }
        Debug.Log($"<color=green>Finished generating {counter} LODs in {st.ElapsedMilliseconds} ms!</color>");
    }
    #endif
}