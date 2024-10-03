using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Unity.Mathematics;

using System.Runtime.CompilerServices;
using Assets.Luau;

#if UNITY_EDITOR
using ParrelSync;
using UnityEditor;
#endif

[ExecuteInEditMode]
[RequireComponent(typeof(VoxelRollbackManager))]
public partial class VoxelWorld : MonoBehaviour {

    public const bool runThreaded = false;       //Turn off if you suspect threading problems
    [NonSerialized]
    public bool doVisuals = true;         //Turn on for headless servers

    public const int maxActiveThreads = 8;
    public const int maxMainThreadMeshMillisecondsPerFrame = 8;    //Dont spend more than 10ms per frame on uploading meshes to GPU or rebuilding collision
    public const int maxMainThreadThreadKickoffMillisecondsPerFrame = 4; //Dont spent more than 4ms on the main thread kicking off threads

    public const bool showDebugSpheres = false;   //Wont activate if threading is enabled
    public const bool showDebugBounds = false;

    [HideInInspector] public bool debugReloadOnScriptReloadMode = false;   //Used when iterating on Airship Rendering, not for production
    
    [HideInInspector] public const int chunkSize = 16;            //fixed size
 
    [HideInInspector]
    [NonSerialized]
    public Vector3 focusPosition = new Vector3(40, 77, 37);

    [SerializeField] public bool autoLoad = true;
    
    [SerializeField][HideInInspector] public WorldSaveFile voxelWorldFile = null;

    [SerializeField][HideInInspector] private WorldSaveFile domainReloadSaveFile = null;
    
    [SerializeField][HideInInspector] public VoxelWorldNetworker worldNetworker;

    [HideInInspector] public GameObject chunksFolder;
    [HideInInspector] public GameObject lightsFolder;

    public event Action<Chunk> BeforeVoxelChunkUpdated;//Array of chunkIds
    public event Action<Chunk> VoxelChunkUpdated;//Array of chunkIds
    public event Action<VoxelData, Vector3Int> BeforeVoxelPlaced;
    public event Action<object, object, object, object> VoxelPlaced;
    public event Action OnFinishedLoading;
    public event Action OnFinishedReplicatingChunksFromServer;
    [HideInInspector] public bool finishedReplicatingChunksFromServer = false;

    [HideInInspector] public Dictionary<Vector3Int, Chunk> chunks = new(new Vector3IntEqualityComparer());
    //[HideInInspector] public Dictionary<string, Transform> worldPositionEditorIndicators = new();
    //[HideInInspector][NonSerialized] public List<WorldSaveFile.WorldPosition> worldPositions = new();

    //Detail meshes (grass etc)
    [NonSerialized]
    [HideInInspector]
    public float lodNearDistance = 40; //near meshes will swap to far meshes at this range
    [NonSerialized]
    [HideInInspector]
    public float lodFarDistance = 150;  //far meshes will fade out entirely at this range
    [NonSerialized]
    [HideInInspector]
    public float lodTransitionSpeed = 1;

 
    //Texture atlas/block definitions    
    [HideInInspector] public VoxelBlocks voxelBlocks; 
    [HideInInspector] public int selectedBlockIndex = 1;

    // Mirroring
    public Vector3 mirrorAround = Vector3.zero;
    
    [HideInInspector] public bool renderingDisabled = false;

    [HideInInspector] private bool debugGrass = false;

    [SerializeField] public bool hasUnsavedChanges = false;

    //Methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockId VoxelDataToBlockId(int block) {
        return (byte)(block & 0xFFF);    //Lower 12 bits
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockId VoxelDataToBlockId(VoxelData block) {
        return (byte)(block & 0xFFF);    //Lower 12 bits
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort VoxelDataToExtraBits(VoxelData block) {
        //mask off everything except the upper 4 bits
        return (byte)(block & 0xF000);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool VoxelIsSolid(VoxelData voxel) {
        return (voxel & 0x8000) != 0; //15th bit 
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HashCoordinates(int x, int y, int z) {
        const int prime1 = 73856093;
        const int prime2 = 19349663;
        const int prime3 = 83492791;

        return x * prime1 ^ y * prime2 ^ z * prime3;

    }

    public VoxelBlocks.CollisionType GetCollisionType(VoxelData voxelData) {
        if (voxelBlocks == null) {
            return VoxelBlocks.CollisionType.None;
        }
        return voxelBlocks.GetCollisionType(VoxelWorld.VoxelDataToBlockId(voxelData));
    }

    public Ray TransformRayToLocalSpace(Ray ray) {
        Matrix4x4 mat = transform.worldToLocalMatrix;
        Vector3 origin = mat.MultiplyPoint(ray.origin);
        Vector3 direction = mat.MultiplyVector(ray.direction);
        return new Ray(origin, direction);
    }
    
    public Vector3 TransformPointToLocalSpace(Vector3 point) {
        return transform.worldToLocalMatrix.MultiplyPoint(point);
    }

    public Vector3 TransformPointToWorldSpace(Vector3 point) {
        return transform.localToWorldMatrix.MultiplyPoint(point);
    }

    public Vector3 TransformVectorToWorldSpace(Vector3 vec) {
        return transform.localToWorldMatrix.MultiplyVector(vec);
    }


    public void InvokeOnFinishedReplicatingChunksFromServer() {
        this.finishedReplicatingChunksFromServer = true;
        this.OnFinishedReplicatingChunksFromServer?.Invoke();
    }

    //This is in localspace, make sure you transform your ray into localspace first
    public VoxelRaycastResult RaycastVoxel(Vector3 pos, Vector3 direction, float maxDistance) {
        (bool hit, float distance, Vector3 hitPosition, Vector3 hitNormal) = RaycastVoxel_Internal(pos, direction, maxDistance);
        return new VoxelRaycastResult() {
            Hit = hit,
            Distance = distance,
            HitPosition = hitPosition,
            HitNormal = hitNormal,
        };
    }

    public void WriteVoxelAt(Vector3 pos, double num, bool priority) {
        Vector3Int posInt = Vector3Int.FloorToInt(pos);
        VoxelData voxel = (VoxelData)num;

        //Write the single voxel
        var affectedChunk = WriteSingleVoxelAt(posInt, voxel, priority);
        if (affectedChunk != null) {
            //Send network update
            if (RunCore.IsServer() && worldNetworker != null) {
                worldNetworker.TargetWriteVoxelRpc(null, posInt, voxel);
            }
        }
    }

    private Chunk WriteSingleVoxelAt(Vector3Int posInt, VoxelData voxel, bool priority) {
        Chunk affectedChunk = WriteVoxelAtInternal(posInt, voxel);
        if (affectedChunk != null) {
            //Adding voxels to history stack for playback
            BeforeVoxelPlaced?.Invoke(voxel, posInt);
            DirtyNeighborMeshes(posInt, priority);
            VoxelPlaced?.Invoke(voxel, posInt.x, posInt.y, posInt.z);
        }
        return affectedChunk;
    }

    public void WriteVoxelGroupAtTS(object blob, bool priority) {
        var data = ((BinaryBlob)blob).GetDictionary();
        Vector3[] positions = new Vector3[data.Count]; ;
        double[] nums = new double[data.Count];

        // Parse binaryblob
        int i = 0;
        foreach (var kvp in data) {
            var values = kvp.Value as Dictionary<object, object>;
            positions[i] = (Vector3)values["pos"];
            nums[i] = Convert.ToDouble((byte)values["blockId"]);
            i++;
        }

        this.WriteVoxelGroupAt(positions, nums, priority);
    }

    public void WriteVoxelGroupAt(Vector3[] positions, double[] nums, bool priority) {
        HashSet<Chunk> affectedChunks = new();
        for (var i = 0; i < positions.Length; i++) {
            var pos = VoxelWorld.FloorInt(positions[i]);
            var num = (VoxelData)nums[i];
            var affectedChunk = WriteSingleVoxelAt(pos, num, false);
            if (affectedChunk != null) {
                affectedChunks.Add(affectedChunk);
            }
        }

        if (affectedChunks.Count > 0 && priority) {
            foreach (var chunk in affectedChunks) {
                BeforeVoxelChunkUpdated?.Invoke(chunk);
                chunk.MainthreadForceCollisionRebuild();
                VoxelChunkUpdated?.Invoke(chunk);
            }
        }

        if (RunCore.IsServer() && worldNetworker != null) {
            worldNetworker.TargetWriteVoxelGroupRpc(null, positions, nums, priority);
        }
    }

    [HideFromTS]
    public List<GameObject> GetChildGameObjects() {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in gameObject.transform) {
            children.Add(child.gameObject);
        }

        return children;
    }

    [HideFromTS]
    public List<Light> GetChildPointLights() {
        List<Light> children = new List<Light>();
        if (this.lightsFolder != null) {
            foreach (Transform pl in this.lightsFolder.transform) {
                var maybePl = pl.GetComponent<Light>();
                if (maybePl != null) {
                    children.Add(maybePl);
                }
            }
        }
        return children;
    }

    /*
    [HideFromTS]
    public void AddWorldPosition(WorldSaveFile.WorldPosition worldPosition) {
#if UNITY_EDITOR
        if (!Application.isPlaying) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/gg.easy.airship/Runtime/Prefabs/WorldPosition.prefab");
            var go = Instantiate<GameObject>(prefab, this.transform);
            var indicator = go.GetComponent<VoxelWorldPositionIndicator>();
            indicator.Init(this);
            go.hideFlags = HideFlags.DontSave;
            go.name = worldPosition.name;
            go.transform.position = worldPosition.position;
            go.transform.rotation = worldPosition.rotation;
        }
#endif
        this.worldPositions.Add(worldPosition);
    }*/

    /*
    [HideFromTS]
    public Light AddPointLight(Color color, Vector3 position, Quaternion rotation, float intensity, float range, bool castShadows) {
        var emptyPointLight = new GameObject("Pointlight", typeof(Light));
        emptyPointLight.transform.parent = this.lightsFolder.transform;
        emptyPointLight.name = "Pointlight";
        emptyPointLight.transform.position = position;
        emptyPointLight.transform.rotation = rotation;

       
        var pointLight = emptyPointLight.GetComponent<Light>();
        pointLight.color = color;
        pointLight.intensity = intensity;
        pointLight.range = range;

        return pointLight;
    }*/

    [HideFromTS]
    public void InitializeChunksAroundChunk(Vector3Int chunkKey) {
        for (int x = -1; x <= 1; x++) {
            for (int y = -1; y <= 1; y++) {
                for (int z = -1; z <= 1; z++) {
                    if (x == 0 && y == 0 && z == 0) {
                        continue;
                    }

                    Vector3Int key = new Vector3Int(chunkKey.x + x, chunkKey.y + y, chunkKey.z + z);
                    if (!chunks.ContainsKey(key)) {
                        Chunk chunk = CreateChunk(key);
                        this.chunks.Add(chunk.chunkKey, chunk);
                        chunk.SetWorld(this);
                        chunks[chunkKey] = chunk;

                    }
                }
            }
        }

    } 

    public static Chunk CreateChunk(Vector3Int key) {
        Chunk chunk = new();
        chunk.chunkKey = key;
        return chunk;
    }

    /**
     * Returns true if the voxel was written.
     * Will return false if the voxel is 
     */
    [HideFromTS]
    public Chunk WriteVoxelAtInternal(Vector3Int pos, VoxelData num) {
        // Debug.Log("Writing voxel pos=" + pos + ", voxel=" + num);
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk chunk);
        if (chunk == null) {

            chunk = CreateChunk(chunkKey);
            this.chunks.Add(chunkKey, chunk);
            chunk.SetWorld(this);
            chunks[chunkKey] = chunk;
        }

        //Set solid bit?
        num = voxelBlocks.AddSolidMaskToVoxelValue(num);

        // Ignore if this changes nothing.
        if (num == chunk.GetVoxelAt(pos)) {
            return null;
        }

        //Write a new voxel
        chunk.WriteVoxel(pos, num);

        return chunk;
    }

    [HideFromTS]
    public VoxelData ReadVoxelAtInternal(Vector3Int pos) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        if (value == null) {
            return 0;
        }

        return value.GetVoxelAt(pos);
    }

    public VoxelData ReadVoxelAt(Vector3 pos) {
        return ReadVoxelAtInternal(Vector3Int.FloorToInt(pos));
    }

    [HideFromTS]
    public void WriteChunkAt(Vector3Int pos, Chunk chunk) {
        chunk.SetWorld(this);
        chunks[pos] = chunk;
    }
 

    [HideFromTS]
    public static Vector3Int WorldPosToChunkKey(Vector3Int globalCoordinate) {
        int x = globalCoordinate.x >= 0 ? globalCoordinate.x / chunkSize : (globalCoordinate.x + 1) / chunkSize - 1;
        int y = globalCoordinate.y >= 0 ? globalCoordinate.y / chunkSize : (globalCoordinate.y + 1) / chunkSize - 1;
        int z = globalCoordinate.z >= 0 ? globalCoordinate.z / chunkSize : (globalCoordinate.z + 1) / chunkSize - 1;

        return new Vector3Int(x, y, z);
    }

    [HideFromTS]
    public static Vector3Int ChunkKeyToWorldPos(Vector3Int chunkPos) {
        return chunkPos * chunkSize;
    }

    [HideFromTS]
    public static Vector3Int WorldPosToChunkKey(int globalCoordinateX, int globalCoordinateY, int globalCoordinateZ) {
        int x = globalCoordinateX >= 0 ? globalCoordinateX / chunkSize : (globalCoordinateX + 1) / chunkSize - 1;
        int y = globalCoordinateY >= 0 ? globalCoordinateY / chunkSize : (globalCoordinateY + 1) / chunkSize - 1;
        int z = globalCoordinateZ >= 0 ? globalCoordinateZ / chunkSize : (globalCoordinateZ + 1) / chunkSize - 1;

        return new Vector3Int(x, y, z);
    }

    [HideFromTS]
    public static Vector3Int WorldPosToChunkKey(Vector3 globalC) {
        Vector3Int globalCoordinate = new Vector3Int(Mathf.FloorToInt(globalC.x), Mathf.FloorToInt(globalC.y), Mathf.FloorToInt(globalC.z));
        return WorldPosToChunkKey(globalCoordinate);
    }

    [HideFromTS]
    public Chunk GetChunkByVoxel(Vector3Int pos) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        return value;
    }

    [HideFromTS]
    public Chunk GetChunkByVoxel(Vector3 pos) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        return value;
    }

    public Chunk GetChunkByChunkPos(Vector3Int pos) {
        chunks.TryGetValue(pos, out Chunk chunk);
        return chunk;
    }

    public (VoxelData, Chunk) GetVoxelAndChunkAt(Vector3Int pos) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        if (value == null) {
            return (0, null);
        }

        return (value.GetVoxelAt(pos), value);
    }

    public VoxelData GetVoxelAt(Vector3 pos) {
        Vector3Int posi = FloorInt(pos);
        Vector3Int chunkKey = WorldPosToChunkKey(posi);
        chunks.TryGetValue(chunkKey, out Chunk value);
        if (value == null) {
            return 0;
        }

        return value.GetVoxelAt(posi);
    }

    public void DirtyMesh(Vector3Int voxel, bool priority = false) {
        Chunk chunk = GetChunkByVoxel(voxel);
        if (chunk != null) {

            chunk.SetGeometryDirty(true, priority);
            if (priority) {
                BeforeVoxelChunkUpdated?.Invoke(chunk);
                chunk.MainthreadForceCollisionRebuild();
                VoxelChunkUpdated?.Invoke(chunk);
            }
        }
        else {
            //if it is null, create it
            WriteVoxelAtInternal(voxel, 0);
        }
    }

    public void DirtyNeighborMeshes(Vector3Int voxel, bool priority = false) {

        //DateTime startTime = DateTime.Now;

        DirtyMesh(voxel, priority);
        Vector3Int localPosition = Chunk.WorldPosToLocalPos(voxel);

        if (localPosition.x == 0) {
            DirtyMesh(voxel + new Vector3Int(-1, 0, 0), false);
        }
        if (localPosition.y == 0) {
            DirtyMesh(voxel + new Vector3Int(0, -1, 0), false);
        }
        if (localPosition.z == 0) {
            DirtyMesh(voxel + new Vector3Int(0, 0, -1), false);
        }
        if (localPosition.x == chunkSize - 1) {
            DirtyMesh(voxel + new Vector3Int(+1, 0, 0), false);
        }
        if (localPosition.y == chunkSize - 1) {
            DirtyMesh(voxel + new Vector3Int(0, +1, 0), false);
        }
        if (localPosition.z == chunkSize - 1) {
            DirtyMesh(voxel + new Vector3Int(0, 0, +1), false);
        }
    }

    public void DeleteRenderedGameObjects() {
        if (this.chunksFolder) {
            DeleteChildGameObjects(this.chunksFolder);
        }

        if (this.lightsFolder) {
            DeleteChildGameObjects(this.lightsFolder);
        }
    }

    public static void DeleteChildGameObjects(GameObject parent) {
        Profiler.BeginSample("DeleteChildGameObjects");
        // Get a list of all the child game objects
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in parent.transform) {
            if (child.name == "Chunks" || child.name == "Lights") {
                DeleteChildGameObjects(child.gameObject);
                continue;
            }
            children.Add(child.gameObject);
        }

        // Delete all the children
        //children.ForEach(child => GameObject.DestroyImmediate(child));
        Profiler.EndSample();
    }

    /**
     * Creates missing child GameObjects and names things properly.
     */
    private void PrepareVoxelWorldGameObject() {
        
        this.loadingStatus = LoadingStatus.NotLoading;
        
        if (transform.Find("Chunks") != null) {
            this.chunksFolder = transform.Find("Chunks").gameObject;
        }
        else {
            this.chunksFolder = new GameObject("Chunks");
            this.chunksFolder.transform.parent = this.transform;
        }
        this.chunksFolder.transform.localPosition = Vector3.zero;
        this.chunksFolder.transform.localScale = Vector3.one;
        this.chunksFolder.transform.localRotation = Quaternion.identity;

        this.chunksFolder.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
    }

    public void GenerateWorld(bool populateTerrain = false) {
        this.PrepareVoxelWorldGameObject();
                
        if (!voxelBlocks) {
            Debug.LogError("No voxel blocks defined. Please define some blocks in the inspector.");
            return;
        }
        voxelBlocks.Reload();
        
        //this.blocks.Load(this.GetBlockDefinesContents());

        chunks.Clear();

        DeleteChildGameObjects(gameObject);
        
        RegenerateAllMeshes();

        hasUnsavedChanges = true;
    }

    public void CreateSingleStarterBlock() {

        if (voxelBlocks == null || voxelBlocks.loadedBlocks.Count < 2) {
            Debug.LogError("No voxel blocks defined.");
            return;
        }
        
        foreach (var def in voxelBlocks.loadedBlocks) {
            if (def.Value.definition.solid == true) {
                WriteVoxelAtInternal(new Vector3Int(0, 0, 0), def.Value.blockId);
                return;
            }
        }

    }

    public void FillRandomTerrain() {
        float scale = 4;
        System.Random rand = new System.Random();

       
        VoxelData grass = voxelBlocks.SearchForBlockIdByString("GRASS");
        VoxelData dirt = voxelBlocks.SearchForBlockIdByString("DIRT");

        for (int x = -64; x < 64; x++) {
            //  for (int z = -127; z < 127; z++)
            for (int z = -64; z < 64; z++) {
                int height = (int)(Mathf.PerlinNoise((float)x / 256.0f * scale, (float)z / 256.0f * scale) * 32.0f);
                for (int y = 0; y < height; y++) {
                    WriteVoxelAtInternal(new Vector3Int(x, y, z), dirt);
                }

                WriteVoxelAtInternal(new Vector3Int(x, height, z), grass);

            }
        }
        RegenerateAllMeshes();

        hasUnsavedChanges = true;
    }

    public void FillSingleBlock() {
        
        VoxelData dirt = voxelBlocks.SearchForBlockIdByString("DIRT");

        WriteVoxelAtInternal(new Vector3Int(0, 0, 0), dirt);

        RegenerateAllMeshes();
    }

    public void RegenerateAllMeshes() {
        Profiler.BeginSample("RegenerateAllMeshes");

        //Force a mesh update
        foreach (var chunk in chunks) {
            chunk.Value.SetGeometryDirty(true);
        }
        Profiler.EndSample();
    }

    private void OnDestroy() {
        foreach (var chunk in chunks) {
            chunk.Value.Free();
        }

        if (this.chunksFolder) {
            if (Application.isPlaying) {
                Destroy(this.chunksFolder);
            }
            else {
                DestroyImmediate(this.chunksFolder);
            }
        }

        if (this.lightsFolder) {
            if (Application.isPlaying) {
                Destroy(this.lightsFolder);
            }
            else {
                DestroyImmediate(this.lightsFolder);
            }
        }
    }


    public Vector3 CalculatePlaneIntersection(Vector3 origin, Vector3 dir, Vector3 planeNormal, Vector3 planePoint) {
        float t = Vector3.Dot(planePoint - origin, planeNormal) / Vector3.Dot(dir, planeNormal);
        return origin + dir * t;
    }

    public GameObject SpawnDebugSphere(Vector3 pos, Color col, float radius = 0.1f) {

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = pos;
        sphere.transform.localScale = new Vector3(radius, radius, radius);
        sphere.transform.parent = this.gameObject.transform;

        MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();

        renderer.sharedMaterial = new Material(Resources.Load<Material>("DebugSphere"));
        renderer.sharedMaterial.SetColor("_Color", col);

        return sphere;
    }
    
    private int delayUpdate = 0;    // Don't run the voxelWorld update this frame, because we just loaded

    public enum LoadingStatus {
        NotLoading,
        Loading,
        Loaded
    }
    
    [NonSerialized]
    public LoadingStatus loadingStatus = LoadingStatus.NotLoading;
    
    
    public void LoadWorldFromSaveFile(WorldSaveFile file) {
        Profiler.BeginSample("LoadWorldFromVoxelBinaryFile");

        if (this.voxelBlocks == null) {
            //Error
            Debug.LogError("No voxel blocks defined. Please define some blocks in the inspector.");
            return;
        }

        float startTime = Time.realtimeSinceStartup;
 
        this.delayUpdate = 1;
        
        //Clear to begin with
        DeleteChildGameObjects(gameObject);
        
        this.PrepareVoxelWorldGameObject();
        this.loadingStatus = LoadingStatus.Loading;

        this.voxelBlocks.Reload();
        
        //load the text of textAsset
        file.LoadIntoVoxelWorld(this);

        //Turns grass bushes on
        if (debugGrass == true) {
            PlaceGrassOnTopOfGrass();
        }

        RegenerateAllMeshes();
         
        Debug.Log("Finished loading voxel save file. Took " + (Time.realtimeSinceStartup - startTime) + " seconds.");
        Profiler.EndSample();

        //Clear this
        hasUnsavedChanges = false;
    }
   
    [HideFromTS]
    public void CreateEmptyWorld() {
        
        if (voxelBlocks == null) {
            Debug.LogError("No voxel blocks defined. Please define some blocks in the inspector.");
            return;
        }
        this.PrepareVoxelWorldGameObject();
                      
        chunks.Clear();
 
        DeleteChildGameObjects(gameObject);
        RegenerateAllMeshes();
 
    }


    
    public void SaveToFile() {
#if UNITY_EDITOR
        if (this.voxelWorldFile == null) return;
 
        this.voxelWorldFile.CreateFromVoxelWorld(this);
        //Save the asset
        EditorUtility.SetDirty(this.voxelWorldFile);
        AssetDatabase.SaveAssets();
        
        hasUnsavedChanges = false;
#endif
    } 

    public void SaveToDomainReloadFile() {
#if UNITY_EDITOR

        if (chunks.Count > 0) {
            //Create a temporary asset for saving
            this.domainReloadSaveFile = ScriptableObject.CreateInstance<WorldSaveFile>();
            this.domainReloadSaveFile.CreateFromVoxelWorld(this);
        }
#endif        
    }

    public void PlaceGrassOnTopOfGrass() {
        
        if (voxelBlocks == null) {
            return;
        }
        //Copy the list of chunks
        List<Chunk> chunksCopy = new List<Chunk>(chunks.Values);

        BlockId grass = voxelBlocks.GetBlockIdFromStringId("@Easy/Core:GRASS");
        BlockId grassTop = voxelBlocks.GetBlockIdFromStringId("@Easy/Core:FLUFFY_GRASS");

        foreach (var chunk in chunksCopy) {
            //get voxels
            VoxelData[] readOnlyVoxel = chunk.readWriteVoxel;

            //scan through all voxels, if its grass, spawn a grass tile
            for (int x = 0; x < VoxelWorld.chunkSize; x++) {
                for (int y = 0; y < VoxelWorld.chunkSize; y++) {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++) {

                        int voxelKey = x + y * chunkSize + z * chunkSize * chunkSize;
                        VoxelData vox = readOnlyVoxel[voxelKey];

                        BlockId blockIndex = VoxelWorld.VoxelDataToBlockId(vox);

                        if (blockIndex == grass) //grass
                        {
                            //grab the one above, if its air
                            VoxelData air = ReadVoxelAt(new Vector3Int(x, y + 1, z) + chunk.bottomLeftInt);
                            BlockId blockIndex2 = VoxelWorld.VoxelDataToBlockId(air);

                            if (blockIndex2 == 0) //air
                            {
                                //spawn a grass tile
                                WriteVoxelAt(new Vector3Int(x, y + 1, z) + chunk.bottomLeftInt, grassTop, false); //grasstop
                            }
                        }


                    }
                }
            }
        }
    }

    /**
     * Used in TS on the client.
     * The client will load an empty world and then wait for server to
     * send data over network.
     */
    public void LoadEmptyWorld() {

        if (voxelBlocks == null) {
            Debug.LogError("No voxel blocks defined. Please define some blocks in the inspector.");
            return;
        }

        DeleteChildGameObjects(gameObject);
        this.PrepareVoxelWorldGameObject();

        this.voxelBlocks.Reload();

        RegenerateAllMeshes();
 
    }
 

    private void Awake() {
        doVisuals = true;
#if UNITY_EDITOR        
        if (RunCore.IsServer() == true || ClonesManager.GetArgument() == "server") {
            doVisuals = false;
            Debug.Log("Voxelworld do visuals is false");
        }
#endif
#if UNITY_SERVER
        doVisuals = false;
#endif
        
    }

    public VoxelWorld() {
        #if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif

    } 

    private void OnEnable() {

#if UNITY_EDITOR
        if (this.domainReloadSaveFile != null) {
            Debug.Log("Reloading " + name + " after doman reload");
            this.LoadWorldFromSaveFile(this.domainReloadSaveFile);
            this.domainReloadSaveFile = null;
            this.hasUnsavedChanges = true;
            return;
        }
          
#endif

        if (!Application.isPlaying) {
            if (this.voxelWorldFile != null) {
                this.LoadWorldFromSaveFile(this.voxelWorldFile);
            }
        }

        if (Application.isPlaying && this.autoLoad) {
            if (voxelWorldFile != null) {
                this.LoadWorldFromSaveFile(voxelWorldFile);
            }
            
            return;
        }

        /*
        //Don't load anything on enable unless in editor mode
        if (Application.isPlaying)
            return;

        if (debugReloadOnScriptReloadMode == true) {
            DeleteChildGameObjects(gameObject);

            if (voxelWorldFile != null) {
                LoadWorldFromSaveFile(voxelWorldFile);
            }
            else {
                GenerateWorld(false);
            }
        }*/
    }

    private void RegenerateMissingChunkGeometry() {
        float regenerateMissingChunkGeometryStartTime = Time.realtimeSinceStartup;
        int maxChunksToUpdateVar = maxActiveThreads;

        // Sort chunks
        List<Chunk> chunksThatNeedThreadKickoff = new();
        List<Chunk> chunksThatNeedMeshUpdates = new();
        foreach (var chunkPair in chunks) {
            if (chunkPair.Value.NeedsToCopyMeshToScene()) {
                chunksThatNeedMeshUpdates.Add(chunkPair.Value);
                continue;
            }
            else
            if (chunkPair.Value.NeedsToGenerateMesh()) {
                chunksThatNeedThreadKickoff.Add(chunkPair.Value);
            }

        }

        Profiler.BeginSample("ThreadKickoff");
        //Kickoff threads, sorted by closest to camera
        int currentlyUpdatingChunks = GetNumProcessingMeshChunks();
        maxChunksToUpdateVar = math.max(0, maxChunksToUpdateVar - currentlyUpdatingChunks);
        int updateCounter = 0;

        if (maxChunksToUpdateVar > 0 && chunksThatNeedThreadKickoff.Count > 0) {
            var focusPositionChunkKey = WorldPosToChunkKey(this.focusPosition);

            chunksThatNeedThreadKickoff.Sort((x, y) => (x.chunkKey - focusPositionChunkKey).magnitude.CompareTo((y.chunkKey - focusPositionChunkKey).magnitude));

            float startTime = Time.realtimeSinceStartup;

            foreach (var chunk in chunksThatNeedThreadKickoff) {
                if (maxChunksToUpdateVar <= 0) {
                    break;
                }

                bool didUpdate = chunk.MainthreadUpdateMesh(this);

                if (didUpdate) {
                    updateCounter++;
                    maxChunksToUpdateVar -= 1;

                    int elapsedTime = (int)((Time.realtimeSinceStartup - startTime) * 1000);
                    if (elapsedTime > maxMainThreadThreadKickoffMillisecondsPerFrame) {
                        //Debug.Log("ThreadKickoff Timedout after " + elapsedTime + "ms");
                        break;
                    }
                }
            }
        }
        Profiler.EndSample();

        Profiler.BeginSample("MainthreadUpdateMeshs");
        //Kickoff mainthread mesh copies, sorted by closest to camera
        maxChunksToUpdateVar = math.max(0, maxChunksToUpdateVar - currentlyUpdatingChunks);


        if (chunksThatNeedMeshUpdates.Count > 0) {
           
            float startTime = Time.realtimeSinceStartup;
            var focusPositionChunkKey = WorldPosToChunkKey(this.focusPosition);

            chunksThatNeedMeshUpdates.Sort((x, y) => (x.chunkKey - focusPositionChunkKey).magnitude.CompareTo((y.chunkKey - focusPositionChunkKey).magnitude));

            foreach (var chunk in chunksThatNeedMeshUpdates) {
                chunk.MainthreadUpdateMesh(this);

                int elapsedTime = (int)((Time.realtimeSinceStartup - startTime) * 1000);
                if (elapsedTime > maxMainThreadMeshMillisecondsPerFrame) {
                    //Debug.Log("MainthreadUpdateMeshs Timedout after " + elapsedTime + "ms");
                    break;
                }
            }
        }
        Profiler.EndSample();
        if (updateCounter > 0) {
            //Debug.Log("Updated:" + updateCounter);
        }


        if (this.loadingStatus == LoadingStatus.Loading) {
            
            bool hasDirtyChunk = false;
            foreach (var chunkPair in chunks) {
                if (chunkPair.Value.IsGeometryDirty()) {
                    hasDirtyChunk = true;
                    break;
                }
            }
            //Debug.Log("Awaiting load - chunks remaining:" + hasDirtyChunk);

            if (!hasDirtyChunk) {
                this.loadingStatus = LoadingStatus.Loaded;
                this.OnFinishedLoading?.Invoke();
            }
        }

        float regenerateMissingChunkGeometryEndTime = Time.realtimeSinceStartup;
        float elapsedTimeInMs = (regenerateMissingChunkGeometryEndTime - regenerateMissingChunkGeometryStartTime) * 1000;
        if (elapsedTimeInMs > 17) {
            //Debug.Log("Slow voxelworld frame update:" + elapsedTimeInMs + "ms");
        }
    }

    public void FullWorldUpdate() {
        Camera cam = null;
#if UNITY_EDITOR
        if (SceneView.currentDrawingSceneView != null) {
            cam = SceneView.currentDrawingSceneView.camera;
        }
#endif
        if (cam == null) {
            cam = GameObject.FindObjectOfType<Camera>();
        }
        foreach (var c in chunks) {
            c.Value.currentCamera = cam;
        }

        Profiler.BeginSample("RegenerateMissingChunkGeometry");
        RegenerateMissingChunkGeometry();
        Profiler.EndSample();
    }

    public void OnRenderObject() {
        if (Application.isPlaying == false && !renderingDisabled) {
            StepWorld();
        }
    }
    public void Update() {
        if (Application.isPlaying && !renderingDisabled) {
            if (this.delayUpdate > 0) {
                this.delayUpdate--;
                return;
            }
            StepWorld();
        }

        if (!Application.isPlaying) {

            if (Camera.main) {
                this.focusPosition = Camera.main.transform.position;
            }
#if UNITY_EDITOR
            if (SceneView.lastActiveSceneView != null) {
                this.focusPosition = SceneView.lastActiveSceneView.camera.transform.position;
            }
#endif

        }
    }

    private void StepWorld() {
       
        FullWorldUpdate();
    }

    public int GetNumRadiosityProcessingChunks() {
        int counter = 0;
        foreach (var chunk in chunks) {
            if (chunk.Value.Busy()) {
                counter++;
            }
        }
        return counter;
    }

    public static Vector3Int CardinalVector(Vector3 normal) {
        if (normal.x > 0.5f) {
            return Vector3Int.right;
        }
        if (normal.x < -0.5f) {
            return Vector3Int.left;
        }
        if (normal.y > 0.5f) {
            return Vector3Int.up;
        }
        if (normal.y < -0.5f) {
            return Vector3Int.down;
        }
        if (normal.z > 0.5f) {
            return Vector3Int.forward;
        }
        if (normal.z < -0.5f) {
            return Vector3Int.back;
        }
        return Vector3Int.zero;
    }

    public int GetNumProcessingMeshChunks() {
        int counter = 0;
        foreach (var chunk in chunks) {
            if (chunk.Value.Busy()) {
                counter++;
            }
        }
        return counter;
    }

    public struct Vector3IntEqualityComparer : IEqualityComparer<Vector3Int> {
        public bool Equals(Vector3Int a, Vector3Int b) {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public int GetHashCode(Vector3Int obj) {
            unchecked {
                int hash = 47;
                hash = hash * 53 + obj.x;
                hash = hash * 53 + obj.y;
                hash = hash * 53 + obj.z;
                return hash;
            }
        }
    }

    
    public void ReloadTextureAtlas() {
      
        if (this.voxelBlocks == null) {
            return;
        }

        //If we're in the editor and we're playing the game
        //we can't reload textures because changes have not been imported yet to unity
        //So to get around this, we load the textures directly from disk
        bool useTexturesDirectlyFromDisk = false;
        if (Application.isPlaying && Application.isEditor == true) {
            useTexturesDirectlyFromDisk = true;
        }
        voxelBlocks.Reload(useTexturesDirectlyFromDisk); // this.GetBlockDefinesContents(), useTexturesDirectlyFromDisk);
         
        //refresh the geometry
        foreach (var chunk in chunks) {
            chunk.Value.SetGeometryDirty(true, false);
        } 
    }
    
    public void AddChunk(Vector3Int key, Chunk chunk) {
        chunks.Add(key, chunk);
        chunk.SetGeometryDirty(true);
    }

    //Todo: How do we want to handle having multiple voxelworlds?
    public static VoxelWorld GetFirstInstance() {
        return GameObject.FindAnyObjectByType<VoxelWorld>(); 
    }
    
    private void OnBeforeAssemblyReload() {
        SaveToDomainReloadFile();
    } 

    
}
