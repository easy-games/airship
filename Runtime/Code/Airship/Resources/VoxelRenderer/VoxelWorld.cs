using System;

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Unity.Mathematics;
using System.Xml;
using System.Runtime.CompilerServices;
using System.Linq;
using Assets.Luau;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public partial class VoxelWorld : MonoBehaviour
{
#if UNITY_SERVER
    public const bool runThreaded = true;       //Turn off if you suspect threading problems
    public const bool doVisuals = false;         //Turn on for headless servers

#else
    public const bool runThreaded = true;       //Turn off if you suspect threading problems
    public const bool doVisuals = true;         //Turn on for headless servers
#endif

    public const bool showDebugSpheres = false;   //Wont activate if threading is enabled
    public const bool showDebugBounds = false;

    [HideInInspector] public bool debugReloadOnScriptReloadMode = false;   //Used when iterating on Airship Rendering, not for production

    [HideInInspector] public bool radiosityEnabled = false;

    [HideInInspector] public const int chunkSize = 16;            //fixed size
    [HideInInspector] public const int radiositySize = 4;         //fixed size

    [HideInInspector] public float globalSunBrightness = 1.0f;
    [HideInInspector] public float globalSkyBrightness = 1.0f;

    //fog
    [HideInInspector] public float globalFogStart = 40.0f;
    [HideInInspector] public float globalFogEnd = 500.0f;
    [HideInInspector] public Color globalFogColor = Color.white;
    
    public const int lightingConvergedCount = -1;// -1 to turn off
    
    [HideInInspector]  public float globalSkySaturation = 1;
    [HideInInspector] public Color globalSunColor = new Color(1, 1, 0.9f);

    [HideInInspector] public Color globalAmbientLight = new Color(0.2f, 0.2f, 0.2f);
    [HideInInspector] public float globalAmbientBrightness = 1.0f;
    [HideInInspector] public float globalAmbientOcclusion = 0.25f;
    [HideInInspector] public float globalRadiosityScale = 0.25f;
    [HideInInspector] public float globalRadiosityDirectLightAmp = 1.0f;
    [HideInInspector] public bool showRadioistyProbes = false;

    [HideInInspector] public Vector3 focusPosition = new Vector3(40, 77, 37);

    //For sunlight - this has to get recalculated during the mesh update so its kinda expensive - maybe an alternative here?
    public const int numSoftShadowSamples = 8;
    public const float softShadowRadius = 1.9f;
    
    public const float radiosityRunawayClamp = 5f;  //Stops the radiosity from feedback looping and going crazy, but generally shouldn't affect anything

    public const int probeMaxRange = 48;  //How far away a probe can be from a wall to sample it. Techically if we were casting rays this would be infinite, but longer rays cost more cpu time 
    

    public const int maxSamplesPerFrame = 32;//Add this many new samples whenever you update a probe
    public const int maxRadiositySamples = 256;
    public const bool skyCountsAsLightForRadiosity = true;
    
    [SerializeField][HideInInspector] public WorldSaveFile voxelWorldFile = null;
    [SerializeField] public List<TextAsset> blockDefines = new();
    [SerializeField] [HideInInspector] public VoxelWorldNetworker worldNetworker;

    [HideInInspector] private Vector3 _globalSunDirection = new Vector3(-1, -2, 1.5f);
    [HideInInspector] private Vector3 _globalSunDirectionNormalized = new Vector3(-1, -2, 1.5f).normalized;
    [HideInInspector] private Vector3 _negativeGlobalSunDirectionNormalized = -(new Vector3(-1, -2, 1.5f).normalized);
    [HideInInspector] private Dictionary<Vector3Int, RadiosityProbeSample> radiosityProbeSamples = new(new Vector3IntEqualityComparer());

    [HideInInspector] public GameObject chunksFolder;
    [HideInInspector] public GameObject lightsFolder;
    
    public event Action<Chunk> BeforeVoxelChunkUpdated;//Array of chunkIds
    public event Action<Chunk> VoxelChunkUpdated;//Array of chunkIds
    public event Action<VoxelData, Vector3Int> BeforeVoxelPlaced;
    public event Action<object, object, object, object> VoxelPlaced;
    public event Action OnFinishedLoading;
    public event Action OnFinishedReplicatingChunksFromServer;
    [HideInInspector] public bool finishedReplicatingChunksFromServer = false;

    static Vector4[] shAmbientData = new Vector4[9];
    //static Vector4[] shSunData = new Vector4[9];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnStartup()
    {
        Array.Clear(shAmbientData, 0, shAmbientData.Length);
        //Array.Clear(shSunData, 0, shSunData.Length);
    }
        
    [HideInInspector] public Dictionary<int, LightReference> sceneLights = new();

    [HideInInspector] public Dictionary<Vector3Int, Chunk> chunks = new(new Vector3IntEqualityComparer());
    [HideInInspector] public Dictionary<string, Transform> worldPositionEditorIndicators = new();

    //Global cubemap
    [HideInInspector] public Cubemap cubeMap;
    [HideInInspector] public string cubeMapPath;
    [HideInInspector] public float3[] cubeMapSHData = new float3[9];
 
    //Detail meshes (grass etc)
    [NonSerialized][HideInInspector]
    public float lodNearDistance = 40; //near meshes will swap to far meshes at this range
    [NonSerialized][HideInInspector]
    public float lodFarDistance = 150;  //far meshes will fade out entirely at this range
    [NonSerialized][HideInInspector]
    public float lodTransitionSpeed = 1;

    [NonSerialized][HideInInspector]
    public List<GameObject> pointLights = new();
    //For shadow sampling
    Vector3[] samplesX;
    Vector3[] samplesY;
    Vector3[] samplesZ;

    public Vector3[][] radiosityRaySamples;
    Vector3[] sphereSampleVectors;
    [HideInInspector]  int numRadiosityRays = 64;

    //Texture atlas/block definitions    
    [HideInInspector] public VoxelBlocks blocks = new VoxelBlocks();
    [HideInInspector] public int selectedBlockIndex = 1;
    
    [HideInInspector] public bool renderingDisabled = false;

    [HideInInspector] private bool debugGrass = false;

    //Methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockId VoxelDataToBlockId(int block)
    {
        return (byte)(block & 0xFFF);    //Lower 12 bits
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockId VoxelDataToBlockId(VoxelData block)
    {
        return (byte)(block & 0xFFF);    //Lower 12 bits
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool VoxelIsSolid(VoxelData voxel)
    {
        return (voxel & 0x8000) != 0; //15th bit 
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HashCoordinates(int x, int y, int z)
    {
        const int prime1 = 73856093;
        const int prime2 = 19349663;
        const int prime3 = 83492791;
       
        return x * prime1 ^ y * prime2 ^ z * prime3;

    }

    public void InvokeOnFinishedReplicatingChunksFromServer() {
        this.finishedReplicatingChunksFromServer = true;
        this.OnFinishedReplicatingChunksFromServer?.Invoke();
    }

    public VoxelRaycastResult RaycastVoxel(Vector3 pos, Vector3 direction, float maxDistance)
    {
        (bool hit, float distance, Vector3 hitPosition, Vector3 hitNormal) = RaycastVoxel_Internal(pos, direction, maxDistance);
        return new VoxelRaycastResult()
        {
            Hit = hit,
            Distance = distance,
            HitPosition = hitPosition,
            HitNormal = hitNormal,
        };
    }

    public void WriteVoxelAt(Vector3 pos, double num, bool priority)
    {
        Vector3Int posInt = Vector3Int.FloorToInt(pos);
        VoxelData voxel = (VoxelData)num;
        
        //Write the single voxel
        var affectedChunk = WriteSingleVoxelAt(posInt, voxel, priority);
        if (affectedChunk != null) {
            //Send network update
            if (RunCore.IsServer() && worldNetworker != null)
            {
                worldNetworker.TargetWriteVoxelRpc(null, posInt, voxel);
            }
        }
    }

    private Chunk WriteSingleVoxelAt(Vector3Int posInt, VoxelData voxel, bool priority) {
        Chunk affectedChunk = WriteVoxelAtInternal(posInt, voxel);
        if (affectedChunk != null)
        {
            //Adding voxels to history stack for playback
            BeforeVoxelPlaced?.Invoke(voxel, posInt);
            DirtyNeighborMeshes(posInt, priority);
            VoxelPlaced?.Invoke(voxel, posInt.x, posInt.y, posInt.z);
        }
        return affectedChunk;
    }

    public void WriteVoxelGroupAtTS(object blob, bool priority) {
        var data = ((BinaryBlob)blob).GetDictionary();
        Vector3[] positions= new Vector3[data.Count];;
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
        
        if (RunCore.IsServer() && worldNetworker != null)
        {
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
    public List<PointLight> GetChildPointLights() {
        List<PointLight> children = new List<PointLight>();
        if (this.lightsFolder != null)
        {
            foreach (Transform pl in this.lightsFolder.transform) {
                var maybePl = pl.GetComponent<PointLight>();
                if (maybePl != null) {
                    children.Add(maybePl);
                }
            }
        }
        return children;
    }

    [HideFromTS]
    public void AddWorldPosition(WorldSaveFile.WorldPosition worldPosition) {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/gg.easy.airship/Runtime/Prefabs/WorldPosition.prefab");
        var go = Instantiate<GameObject>(prefab, this.transform);
        go.hideFlags = HideFlags.DontSaveInEditor;
        go.name = worldPosition.name;
        go.transform.position = worldPosition.position;
        go.transform.rotation = worldPosition.rotation;
        // this.worldPositionEditorIndicators.Add(worldPosition.name, go.transform);
#endif
    }

    [HideFromTS]
    public PointLight AddPointLight(Color color, Vector3 position, Quaternion rotation, float intensity, float range, bool castShadows, bool highQualityLight) {
        var emptyPointLight = new GameObject("Pointlight", typeof(PointLight));
        emptyPointLight.transform.parent = this.lightsFolder.transform;
        emptyPointLight.name = "Pointlight";
        emptyPointLight.transform.position = position;
        emptyPointLight.transform.rotation = rotation;

        /* Populate pointlight component. */
        var pointLight = emptyPointLight.GetComponent<PointLight>();
        pointLight.color = color;
        pointLight.intensity = intensity;
        pointLight.range = range;
        pointLight.castShadows = castShadows;
        pointLight.highQualityLight = highQualityLight;
        return pointLight;
    }

    [HideFromTS]
    public void InitializeChunksAroundChunk(Vector3Int chunkKey)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if (x == 0 && y == 0 && z == 0)
                    {
                        continue;
                    }

                    Vector3Int key = new Vector3Int(chunkKey.x + x, chunkKey.y + y, chunkKey.z + z);
                    if (!chunks.ContainsKey(key))
                    {
                        Chunk chunk = new Chunk(key);
                        this.chunks.Add(chunk.chunkKey, chunk);
                        chunk.SetWorld(this);
                        chunks[chunkKey] = chunk;
                        InitializeLightingForChunk(chunk);
                    }
                }
            }
        }

    }

    
    /**
     * Returns true if the voxel was written.
     * Will return false if the voxel is 
     */
    [HideFromTS]
    public Chunk WriteVoxelAtInternal(Vector3Int pos, VoxelData num)
    {
        // Debug.Log("Writing voxel pos=" + pos + ", voxel=" + num);
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk chunk);
        if (chunk == null)
        {

            chunk = new Chunk(chunkKey);
            this.chunks.Add(chunkKey, chunk);
            chunk.SetWorld(this);
            chunks[chunkKey] = chunk;

            InitializeLightingForChunk(chunk);
        }

        //Set solid bit?
        num = blocks.AddSolidMaskToVoxelValue(num);

        // Ignore if this changes nothing.
        if (num == chunk.GetVoxelAt(pos))
        {
            return null;
        }

        //Write a new voxel
        chunk.WriteVoxel(pos, num);

        return chunk;
    }
 
    [HideFromTS]
    public VoxelData ReadVoxelAtInternal(Vector3Int pos)
    {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        if (value == null)
        {
            return 0;
        }

        return value.GetVoxelAt(pos);
    }
    
    public VoxelData ReadVoxelAt(Vector3 pos)
    {
        return ReadVoxelAtInternal(Vector3Int.FloorToInt(pos));
    }

    [HideFromTS]
    public void WriteChunkAt(Vector3Int pos, Chunk chunk)
    {
        chunk.SetWorld(this);
        chunks[pos] = chunk;
    }

    [HideFromTS]
    public static Vector3Int WorldPosToRadiosityKey(Vector3Int globalCoordinate)
    {
        int x = globalCoordinate.x >= 0 ? globalCoordinate.x / radiositySize : (globalCoordinate.x + 1) / radiositySize - 1;
        int y = globalCoordinate.y >= 0 ? globalCoordinate.y / radiositySize : (globalCoordinate.y + 1) / radiositySize - 1;
        int z = globalCoordinate.z >= 0 ? globalCoordinate.z / radiositySize : (globalCoordinate.z + 1) / radiositySize - 1;

        return new Vector3Int(x, y, z);
    }

    [HideFromTS]
    public static Vector3Int WorldPosToChunkKey(Vector3Int globalCoordinate)
    {
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
    public static Vector3Int WorldPosToChunkKey(int globalCoordinateX, int globalCoordinateY, int globalCoordinateZ)
    {
        int x = globalCoordinateX >= 0 ? globalCoordinateX / chunkSize : (globalCoordinateX + 1) / chunkSize - 1;
        int y = globalCoordinateY >= 0 ? globalCoordinateY / chunkSize : (globalCoordinateY + 1) / chunkSize - 1;
        int z = globalCoordinateZ >= 0 ? globalCoordinateZ / chunkSize : (globalCoordinateZ + 1) / chunkSize - 1;

        return new Vector3Int(x, y, z);
    }

    [HideFromTS]
    public static Vector3Int WorldPosToChunkKey(Vector3 globalC)
    {
        Vector3Int globalCoordinate = new Vector3Int(Mathf.FloorToInt(globalC.x), Mathf.FloorToInt(globalC.y), Mathf.FloorToInt(globalC.z));
        return WorldPosToChunkKey(globalCoordinate);
    }
    
    [HideFromTS]
    public Chunk GetChunkByVoxel(Vector3Int pos)
    {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        return value;
    }

    [HideFromTS]
    public Chunk GetChunkByVoxel(Vector3 pos)
    {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        return value;
    }

    public Chunk GetChunkByChunkPos(Vector3Int pos)
    {
        chunks.TryGetValue(pos, out Chunk chunk);
        return chunk;
    }

    public (VoxelData, Chunk) GetVoxelAndChunkAt(Vector3Int pos)
    {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk value);
        if (value == null)
        {
            return (0, null);
        }

        return (value.GetVoxelAt(pos), value);
    }

    public VoxelData GetVoxelAt(Vector3 pos)
    {
        Vector3Int posi = FloorInt(pos);
        Vector3Int chunkKey = WorldPosToChunkKey(posi);
        chunks.TryGetValue(chunkKey, out Chunk value);
        if (value == null)
        {
            return 0;
        }

        return value.GetVoxelAt(posi);
    }

    public void DirtyMesh(Vector3Int voxel, bool priority = false)
    {
        Chunk chunk = GetChunkByVoxel(voxel);
        if (chunk != null)
        {
            
            chunk.SetGeometryDirty(true, priority);
            if (priority)
            {
                BeforeVoxelChunkUpdated?.Invoke(chunk);
                chunk.MainthreadForceCollisionRebuild();
                VoxelChunkUpdated?.Invoke(chunk);
            }
        }
        else
        {
            //if it is null, create it
            WriteVoxelAtInternal(voxel, 0);
        }
    }
        
     public void DirtyNeighborMeshes(Vector3Int voxel, bool priority = false)
    {
       
        //DateTime startTime = DateTime.Now;

        DirtyMesh(voxel, priority);
        Vector3Int localPosition = Chunk.WorldPosToLocalPos(voxel);

        if (localPosition.x == 0)
        {
            DirtyMesh(voxel + new Vector3Int(-1, 0, 0), false);
        }
        if (localPosition.y == 0)
        {
            DirtyMesh(voxel + new Vector3Int(0, -1, 0), false);
        }
        if (localPosition.z == 0)
        {
            DirtyMesh(voxel + new Vector3Int(0, 0, -1), false);
        }
        if (localPosition.x == chunkSize - 1)
        {
            DirtyMesh(voxel + new Vector3Int(+1, 0, 0), false);
        }
        if (localPosition.y == chunkSize - 1)
        {
            DirtyMesh(voxel + new Vector3Int(0, +1, 0), false);
        }
        if (localPosition.z == chunkSize - 1)
        {
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
 
    public static void DeleteChildGameObjects(GameObject parent)
    {
        Profiler.BeginSample("DeleteChildGameObjects");
        // Get a list of all the child game objects
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in parent.transform)
        {
            if (child.name == "Chunks" || child.name == "Lights")
            {
                DeleteChildGameObjects(child.gameObject);
                continue;
            }
            children.Add(child.gameObject);
        }

        // Delete all the children
        children.ForEach(child => GameObject.DestroyImmediate(child));
        Profiler.EndSample();
    }

    /**
     * Creates missing child GameObjects and names things properly.
     */
    private void PrepareVoxelWorldGameObject()
    {
        if (transform.Find("Chunks") != null)
        {
            this.chunksFolder = transform.Find("Chunks").gameObject;
        } else
        {
            this.chunksFolder = new GameObject("Chunks");
            this.chunksFolder.transform.parent = this.transform;
            this.chunksFolder.hideFlags = HideFlags.DontSaveInEditor;
        }

        if (transform.Find("Lights") != null)
        {
            this.lightsFolder = transform.Find("Lights").gameObject;
        } else
        {
            this.lightsFolder = new GameObject("Lights");
            this.lightsFolder.transform.parent = this.transform;
            this.lightsFolder.hideFlags = HideFlags.DontSaveInEditor;
        }
    }

    public string[] GetBlockDefinesContents() {
        return this.blockDefines.Select((s) => s.text).ToArray();
    }

    public void GenerateWorld(bool populateTerrain = false)
    {
        this.PrepareVoxelWorldGameObject();

        this.blocks = new VoxelBlocks();
        this.blocks.Load(this.GetBlockDefinesContents());

        chunks.Clear();

        DeleteChildGameObjects(gameObject);

        float scale = 4;
        System.Random rand = new System.Random();

        if (populateTerrain)
        {
            for (int x = -64; x < 64; x++)
            {
                //  for (int z = -127; z < 127; z++)
                for (int z = -64; z < 64; z++)
                {
                    int height = (int)(Mathf.PerlinNoise((float)x / 256.0f * scale, (float)z / 256.0f * scale) * 32.0f);
                    for (int y = 0; y < height; y++)
                    {
                        WriteVoxelAtInternal(new Vector3Int(x, y, z), 1);
                    }
                }
            }
        } else
        {
            WriteVoxelAtInternal(new Vector3Int(0, 0, 0), 1);
        }

        RegenerateAllMeshes();
    }

    public void RegenerateAllMeshes()
    {
        Profiler.BeginSample("RegenerateAllMeshes");
        //ensure we have all the light references in the scene captured
        UpdateLights();
        
        //dirty them all
        foreach (var light in sceneLights)
        {
            light.Value.dirty = true;
        }
        
        //Make all the chunks recapture their lights
        foreach (var chunk in chunks)
        {
            chunk.Value.ForceRemoveAllLightReferences();
        }
        
        foreach (var lightRec in sceneLights)
        {
            lightRec.Value.ForceAddAllLightReferencesToChunks(this);
        }

        //Force a mesh update
        foreach (var chunk in chunks)
        {
            chunk.Value.SetGeometryDirty(true);
        }
        Profiler.EndSample();
    }

    private void OnDestroy()
    {
        foreach (var chunk in chunks)
        {
            chunk.Value.Free();
        }
    }
    

    public Vector3 CalculatePlaneIntersection(Vector3 origin, Vector3 dir, Vector3 planeNormal, Vector3 planePoint)
    {
        float t = Vector3.Dot(planePoint - origin, planeNormal) / Vector3.Dot(dir, planeNormal);
        return origin + dir * t;
    }

    public GameObject SpawnDebugSphere(Vector3 pos, Color col, float radius = 0.1f)
    {

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

    [NonSerialized]
    public bool finishedLoading = false;   //Collision has been fully instantiated for this map
    public void LoadWorldFromSaveFile(WorldSaveFile file)
    {
        Profiler.BeginSample("LoadWorldFromVoxelBinaryFile");

        int startTime = System.Environment.TickCount;

        this.delayUpdate = 1;
        this.finishedLoading = false;

        //Clear to begin with
        DeleteChildGameObjects(gameObject);

        this.worldPositionEditorIndicators.Clear();
        this.pointLights.Clear();

        this.PrepareVoxelWorldGameObject();

        //load the text of textAsset
        this.blocks = new VoxelBlocks();
        this.blocks.Load(this.GetBlockDefinesContents());

        file.LoadIntoVoxelWorld(this);
        
        //Turns grass bushes on
        if (debugGrass == true)
        {
            PlaceGrassOnTopOfGrass();
        }

        LoadCubemapSHData();
        CreateSamples();

        Debug.Log("Regen all meshes");
        RegenerateAllMeshes();

        UpdatePropertiesForAllChunksForRendering();

        Debug.Log("Finished loading voxel binary file. Took " + (System.Environment.TickCount - startTime) + "ms");
        Profiler.EndSample();
    }

    private void LoadCubemapSHData()
    {
        //shared/resources/Skybox/BrightSky/bright_sky_2.jpg
        //shared/resources/skybox/brightsky/bright_sky_2.png
        this.cubeMap = AssetBridge.Instance.LoadAssetInternal<Cubemap>(this.cubeMapPath, false);

        //load an xml file from this.cubeMapPath using AssetBridge, but without the extension
        //then load the data into this.cubeMapSHData
        if (this.cubeMap == null || this.cubeMapPath == "")
        {
            Debug.LogWarning("Failed to load cubemap at path: " + this.cubeMapPath);
            return;
        }

        //modify the path
        string xmlPath = this.cubeMapPath.Substring(0, this.cubeMapPath.Length - 4) + ".xml";

        TextAsset text = AssetBridge.Instance.LoadAssetInternal<TextAsset>(xmlPath, false);
        if (text)
        {
            //The data is 9 coefficients stored like so
            /*
            < SphericalHarmonicCoefficients >
            < Coefficient index = "0" value = "(1.44, 1.91, 2.37, 3.47)" />
            etc
            */
            
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(text.text);

            XmlNodeList nodes = doc.GetElementsByTagName("Coefficient");
            for (int i = 0; i < nodes.Count; i++)
            {
                string[] values = nodes[i].Attributes["value"].Value.Split(',');
                float r = float.Parse(values[0].Substring(1));
                float g = float.Parse(values[1]);
                float b = float.Parse(values[2]);
                this.cubeMapSHData[i] = new float3(r, g, b);
            }
        }
    }


    [HideFromTS]
    public void CreateEmptyWorld() {
        this.PrepareVoxelWorldGameObject();

        this.blocks = new VoxelBlocks();
        this.blocks.Load(this.GetBlockDefinesContents());
        chunks.Clear();
        
        LoadCubemapSHData();
        CreateSamples();
        DeleteChildGameObjects(gameObject);
        RegenerateAllMeshes();
 
        UpdatePropertiesForAllChunksForRendering();
    }

    public void UpdateSceneLights() {
        foreach (var sl in sceneLights) {
            sl.Value.dirty = true;
            sl.Value.Update();
        }
    }

    public void SaveToFile()
    {
#if UNITY_EDITOR
        if (this.voxelWorldFile == null) return;

        this.pointLights.Clear();
        foreach (var pointLight in this.GetChildPointLights())
        {
            this.pointLights.Add(pointLight.gameObject);
        }

        WorldSaveFile saveFile = ScriptableObject.CreateInstance<WorldSaveFile>();
        saveFile.CreateFromVoxelWorld(this);

        //Get path of the asset world.voxelWorldFile
        //string path = AssetDatabase.GetAssetPath(world.voxelWorldFile);
        string path = "Assets/Bundles/Server/Resources/Worlds/" + this.voxelWorldFile.name + ".asset";
        AssetDatabase.CreateAsset(saveFile, path);
        this.voxelWorldFile = saveFile;
        Debug.Log("Saved file " + this.voxelWorldFile.name);
        this.UpdatePropertiesForAllChunksForRendering();
#endif
    }

    public void PlaceGrassOnTopOfGrass()
    {
        //Copy the list of chunks
        List<Chunk> chunksCopy = new List<Chunk>(chunks.Values);

        BlockId grass = blocks.GetBlockIdFromStringId("@Easy/Core:GRASS");
        BlockId grassTop = blocks.GetBlockIdFromStringId("@Easy/Core:FLUFFY_GRASS");

        foreach (var chunk in chunksCopy)
        {
            //get voxels
            VoxelData[] readOnlyVoxel = chunk.readWriteVoxel;
            
            //scan through all voxels, if its grass, spawn a grass tile
            for (int x = 0; x < VoxelWorld.chunkSize; x++)
            {
                for (int y = 0; y < VoxelWorld.chunkSize; y++)
                {
                    for (int z = 0; z < VoxelWorld.chunkSize; z++)
                    {
                       
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
                                WriteVoxelAt(new Vector3Int(x, y+1 , z) + chunk.bottomLeftInt, grassTop, false); //grasstop
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
    public void LoadEmptyWorld(string cubeMapPath)
    {
        DeleteChildGameObjects(gameObject);
        this.PrepareVoxelWorldGameObject();
        this.cubeMapPath = cubeMapPath;

        this.blocks = new VoxelBlocks();
        this.blocks.Load(this.GetBlockDefinesContents());

        LoadCubemapSHData();

        CreateSamples();
        RegenerateAllMeshes();
         
        UpdatePropertiesForAllChunksForRendering();
    }
    
    public void UpdatePropertiesForAllChunksForRendering()
    {
        foreach (var chunkRec in chunks)
        {
            chunkRec.Value.UpdateMaterialPropertiesForChunk();
        }
    }

    private void Awake() {
        this.finishedLoading = false;
        // Load the text of textAsset
        if (Application.isPlaying == false)
        {
            this.blocks = new VoxelBlocks();
            this.blocks.Load(this.GetBlockDefinesContents());
        }
    }

    private void OnEnable()
    {
        //Don't load anything on enable unless in editor mode
        if (Application.isPlaying)
            return;
        
        if (debugReloadOnScriptReloadMode == true)
        {
            DeleteChildGameObjects(gameObject);

            if (voxelWorldFile != null)
            {
                LoadWorldFromSaveFile(voxelWorldFile);
            } else
            {
                GenerateWorld(false);
            }
        }
    }
    
    private void RegenerateMissingChunkGeometry()
    {
        // Sort chunks
        List<Chunk> chunksToSort = new();
        foreach (var chunkPair in chunks) 
        {
            if (chunkPair.Value.NeedsToRunUpdate())
            {
                chunksToSort.Add(chunkPair.Value);
            }
        }

        if (chunksToSort.Count > 0)
        {
            var focusPositionChunkKey = WorldPosToChunkKey(this.focusPosition);
            
            chunksToSort.Sort((x, y) => (x.chunkKey - focusPositionChunkKey).magnitude.CompareTo((y.chunkKey - focusPositionChunkKey).magnitude));

            int updateCounter = 0;
            foreach (var chunk in chunksToSort)
            {
                bool didUpdate = chunk.MainthreadUpdateMesh(this);
                if (didUpdate) 
                {
                    updateCounter++;
                    if (updateCounter >= 2 && RunCore.IsClient() && Application.isPlaying)
                    {
                        break;
                    }
                }
            }
        }

        if (!this.finishedLoading)
        {
            bool hasDirtyChunk = false;
            foreach (var chunkPair in chunks)
            {
                if (chunkPair.Value.IsGeometryDirty()) {
                    hasDirtyChunk = true;
                    break;
                }
            }

            if (!hasDirtyChunk)
            {
                this.finishedLoading = true;
                this.OnFinishedLoading?.Invoke();
            }
        }
    }
    
    public void FullWorldUpdate()
    {
        Camera cam = null;
#if UNITY_EDITOR
        if (SceneView.currentDrawingSceneView != null)
        {
            cam = SceneView.currentDrawingSceneView.camera;
        }
#endif
        if (cam == null)
        {
            cam = GameObject.FindObjectOfType<Camera>();
        }
        foreach (var c in chunks)
        {
            c.Value.currentCamera = cam;
        }

        Profiler.BeginSample("UpdateLights");
        UpdateLights();
        Profiler.EndSample();
        
        Profiler.BeginSample("RegenerateMissingChunkGeometry");
        RegenerateMissingChunkGeometry();
        Profiler.EndSample();
   
        Profiler.BeginSample("UpdatePropertiesForAllChunksForRendering");
        UpdatePropertiesForAllChunksForRendering();
        Profiler.EndSample();

        int count = 0;
        foreach (var c in chunks)
        {
            count += c.Value.updatingRadiosity ? 1 : 0;
        }

    }

    public void OnRenderObject()
    {
        if (Application.isPlaying == false && !renderingDisabled)
        {
            StepWorld();
        }
    }
    public void Update()
    {
        if (Application.isPlaying && !renderingDisabled)
        {
            if (this.delayUpdate > 0)
            {
                this.delayUpdate--;
                return;
            }
            StepWorld();
        }
    }

    private void StepWorld()
    {
        if (radiosityEnabled == true && doVisuals == true)
        {
            int minimumTime = 16;
            int maxProcessing = 8;
            int maxLauchPerFrame = 2;

            if (GetNumBusyChunks() < maxProcessing)
            {

                DateTime now = DateTime.Now;
                List<Chunk> sortedChunks = new List<Chunk>(chunks.Count);

                Vector3 camPos = Vector3.zero;
                
#if UNITY_EDITOR
                if (SceneView.currentDrawingSceneView != null)
                {
                    Camera cam = SceneView.currentDrawingSceneView.camera;
                    camPos = cam.transform.position;
                }
#endif

                DateTime zero = new DateTime(0);
                foreach (var chunkVar in chunks)
                {
                    Chunk chunk = chunkVar.Value;
                    if (chunk.lightingConverged >= lightingConvergedCount && lightingConvergedCount > 0)
                    {
                     //   continue;
                    }
                    DateTime last = chunk.GetTimeOfLastRadiosityUpdate();
                    if ((now - last).Milliseconds < minimumTime)
                    {
                   //     continue;
                    }
                  
                    sortedChunks.Add(chunk);
                    
                }

                foreach (var chunk in sortedChunks)
                {
                    var dist = Vector3.Distance(camPos, chunk.GetKey() * chunkSize + new Vector3Int(chunkSize / 2, chunkSize / 2, chunkSize / 2));
                    //chunk.distanceFromCamera = dist;
                    if (chunk.GetTimeOfLastRadiosityUpdate() == zero)
                    {
                      //  chunk.distanceFromCamera /= 1000;
                        
                    }
                    //chunk.distanceFromCamera += chunk.lightingConverged;
                }
                sortedChunks.Sort((a, b) => a.numUpdates < b.numUpdates ? -1 : 1);

                foreach (var chunk in sortedChunks)
                {
                    if (maxLauchPerFrame <= 0)
                    {
                        break;
                    }
                    chunk.MainThreadAddSamplesToProbes();
                    maxLauchPerFrame--;
                }
            }
        }
        
        FullWorldUpdate();
    }

    public int GetNumBusyChunks()
    {
        int counter = 0;
        foreach (var chunk in chunks)
        {
            if (chunk.Value.Busy())
            {
                counter++;
            }
        }
        return counter;
    }

    public struct Vector3IntEqualityComparer : IEqualityComparer<Vector3Int>
    {
        public bool Equals(Vector3Int a, Vector3Int b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public int GetHashCode(Vector3Int obj)
        {
            unchecked
            {
                int hash = 47;
                hash = hash * 53 + obj.x;
                hash = hash * 53 + obj.y;
                hash = hash * 53 + obj.z;
                return hash;
            }
        }
    }

    public void ReloadTextureAtlas() {
        this.blocks = null;
        this.blocks = new VoxelBlocks();

        //If we're in the editor and we're playing the game
        //we can't reload textures because changes have not been imported yet to unity
        //So to get around this, we load the textures directly from disk
        bool useTexturesDirectlyFromDisk = false;
        if (Application.isPlaying && Application.isEditor == true) {
            useTexturesDirectlyFromDisk = true;
        }
        this.blocks.Load(this.GetBlockDefinesContents(), useTexturesDirectlyFromDisk);

        //refresh the geometry
        foreach (var chunk in chunks)
        {
            chunk.Value.SetGeometryDirty(true, false);
        }
    }
}
