using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;
using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using Unity.Mathematics;

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Assets.Luau;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using ParrelSync;
using UnityEditor;
#endif

[ExecuteInEditMode]
public partial class VoxelWorld : MonoBehaviour {
    /// <summary>
    /// If enabled all quarter blocks will be replaced with default cube voxels
    /// </summary>
    public bool useSimplifiedVoxels = false;
    public const bool runThreaded = true;       //Turn off if you suspect threading problems
    [NonSerialized]
    public bool doVisuals = true;         //Turn on for headless servers

    public const int maxActiveThreads = 8;
    public const int maxMainThreadMeshMillisecondsPerFrame = 8;    //Dont spend more than 10ms per frame on uploading meshes to GPU or rebuilding collision
    public const int maxMainThreadThreadKickoffMillisecondsPerFrame = 4; //Dont spent more than 4ms on the main thread kicking off threads

    public const bool showDebugSpheres = false;   //Wont activate if threading is enabled
    public const bool showDebugBounds = false;

    [HideInInspector] public bool debugReloadOnScriptReloadMode = false;   //Used when iterating on Airship Rendering, not for production
    
    [HideInInspector] public const int chunkSize = 16;            //fixed size
    [NonSerialized] internal const int logChunkSize = 4; // Log_2 of chunkSize, update with chunkSize (if it is a power of 2)!
    
    [NonSerialized] internal const bool chunkSizeIsPowerOfTwo = (chunkSize & (chunkSize - 1)) == 0;
     
 
    [HideInInspector]
    public Vector3 focusPosition {
        get {
            #if UNITY_EDITOR
            if (!Application.isPlaying) {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView) {
                    var sceneCamera = sceneView.camera;
                    if (sceneCamera) return sceneCamera.transform.position;
                }
            }
            #endif
            if (useCameraAsFocusPosition && _focusCameraTransform) return _focusCameraTransform.position;
            return _focusPosition;
        }
        set => _focusPosition = value;
    }
    private Vector3 _focusPosition;
    [Tooltip("If enabled we use the main camera position as the VoxelWorld focus position (prioritizing updates to nearby chunks)")]
    public bool useCameraAsFocusPosition = true;
    private Transform _focusCameraTransform;

    internal Camera focusCamera {
        get {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView) {
                    var sceneCamera = sceneView.camera;
                    if (sceneCamera) return sceneCamera;
                }
            }
#endif
            return _focusCamera;
        }
        set => _focusCamera = value;
    }
    private Camera _focusCamera;

    [SerializeField] public bool autoLoad = true;
    
    [SerializeField][HideInInspector] public WorldSaveFile voxelWorldFile = null;

    //[SerializeField][HideInInspector] private WorldSaveFile domainReloadSaveFile = null;
    
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
    [NonSerialized][HideInInspector] public int selectedBlockIndex = 1;

    //For the editor
    [NonSerialized][HideInInspector] public VoxelData highlightedBlock = 0;
    [NonSerialized][HideInInspector] public Vector3Int highlightedBlockPos = new();

    [NonSerialized]
    [HideInInspector]
    public Camera currentCamera;

    // Mirroring
    public Vector3 mirrorAround = Vector3.zero;

    //Flipped blocks 
    public enum Flips : byte {
        Flip_0Deg = 0,
        Flip_90Deg = 1,
        Flip_180Deg = 2,
        Flip_270Deg = 3,
        Flip_0DegVertical = 4,
        Flip_90DegVertical = 5,
        Flip_180DegVertical = 6,
        Flip_270DegVertical = 7
    }

    public static string[] flipNames = {
        "0 Deg",
        "90 Deg",
        "180 Deg",
        "270 Deg",
        "0 Deg Vertical",
        "90 Deg Vertical",
        "180 Deg Vertical",
        "270 Deg Vertical"
    };

    public static Flips[] allFlips = (Flips[])System.Enum.GetValues(typeof(Flips));

    [HideInInspector] public bool renderingDisabled = false;

    //[HideInInspector] private bool debugGrass = false;
    [NonSerialized] public bool hasUnsavedChanges = false;

    //Methods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockId VoxelDataToBlockId(int block) {
        return (ushort)(block & 0xFFF);    //Lower 12 bits
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlockId VoxelDataToBlockId(VoxelData block) {
        return (ushort)(block & 0xFFF);    //Lower 12 bits
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort VoxelDataToExtraBits(VoxelData block) {
        //mask off everything except the upper 4 bits
        return (ushort)(block & 0xF000);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool VoxelIsSolid(VoxelData voxel) {
        return (voxel & 0x8000) != 0; //15th bit 
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VoxelData SetVoxelSolidBit(VoxelData voxel, bool solid) {
        //Solid bit is bit 15, toggle it on or off
        if (solid) {
            return (ushort)(voxel | 0x8000);
        }
        else {
            return (ushort)(voxel & 0x7FFF);
        }
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetVoxelFlippedBits(VoxelData voxel) {
        //Flipped bits are the 12th,13th and 14th bits
        return (voxel & 0x7000) >> 12;
    }
    
    public static Quaternion FlipBitsToQuaternion(int flipBits) {
        var flipEnum = (Flips) flipBits;
        switch (flipEnum) {
            case Flips.Flip_0Deg:
                return Quaternion.identity;
            case Flips.Flip_90Deg:
                return Quaternion.Euler(0, 90, 0);
            case Flips.Flip_180Deg:
                return Quaternion.Euler(0, 180, 0);
            case Flips.Flip_270Deg:
                return Quaternion.Euler(0, 270, 0);
            case Flips.Flip_0DegVertical:
                return Quaternion.Euler(0, 0, 180);
            case Flips.Flip_90DegVertical:
                return Quaternion.Euler(0, 90, 180);
            case Flips.Flip_180DegVertical:
                return Quaternion.Euler(0, 180, 180);
            case Flips.Flip_270DegVertical:
                return Quaternion.Euler(0, 270, 180);
        }
        return Quaternion.identity;
    }

    /// <summary>
    /// Half blocks are scaled based on their flip bits
    /// </summary>
    public static Vector3 GetScaleFromFlipBits(int flipBits) {
        if (flipBits % 4 == 0) return new Vector3(1, 0.5f, 1);
        if (flipBits % 4 == 1) return new Vector3(0.5f, 1, 1);
        if (flipBits % 4 == 2) return new Vector3(1, 1, 0.5f);
        return Vector3.one;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SetVoxelFlippedBits(int voxel, int flippedBits) {
        // Ensure flippedBits is a 3-bit value (0-7)
        flippedBits &= 0x7;

        // Clear the 12th, 13th, and 14th bits in the original voxel
        voxel &= ~0x7000;

        // Set the 12th, 13th, and 14th bits using the flippedBits
        voxel |= (flippedBits << 12);

        return voxel;
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
    public Vector3 TransformVectorToLocalSpace(Vector3 vec) {
        return transform.worldToLocalMatrix.MultiplyVector(vec);
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
            if (RunCore.IsServer() && worldNetworker != null && worldNetworker.networkWriteVoxels) {
                worldNetworker.TargetWriteVoxelRpc(null, posInt, voxel);
            }
        }
    }
    
    /// <summary>
    /// Method to quickly update the voxel collision at a certain position. Useful for
    /// resimulating changes to the Voxel World in a server auth game. These collisions
    /// will be overriden when the chunk next rebuilds itself (hence why they are called temporary).
    /// </summary>
    /// <param name="pos">Position to write collision to</param>
    /// <param name="num">The block type to write (if the block definition doesn't have collisions this won't do anything).
    /// Use 0 to delete the existing collisions at this position.</param>
    public void WriteTemporaryVoxelCollisionAt(Vector3 pos, ushort num) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        if (!chunks.TryGetValue(chunkKey, out Chunk chunk)) return;

        var addCollision = num > 0;
        
        // Don't write temporary collision if voxel doesn't have collisions
        if (addCollision && GetCollisionType(num) != VoxelBlocks.CollisionType.Solid) return;
        
        chunk.WriteTemporaryCollision(pos, addCollision);
    }

    public void ColorVoxelAt(Vector3 pos, Color color, bool priority) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk chunk);
        if (chunk == null) {
            return;
        }

        var voxelPos = FloorInt(pos);
        if (chunk.GetVoxelAt(voxelPos) == 0) return;
        
        chunk.WriteVoxelColor(voxelPos, color);
        DirtyNeighborMeshes(voxelPos, priority);
    }
    
    public void DamageVoxelAt(Vector3 pos, float damage, bool priority) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk chunk);
        if (chunk == null) {
            return;
        }

        var voxelPos = FloorInt(pos);
        if (chunk.GetVoxelAt(voxelPos) == 0) return;
        
        chunk.WriteVoxelDamage(voxelPos, damage);
        DirtyMesh(voxelPos, priority);
    }

    private Chunk WriteSingleVoxelAt(Vector3Int posInt, VoxelData voxel, bool priority) {
        Chunk affectedChunk = WriteVoxelAtInternal(posInt, voxel);
        DamageVoxelAt(posInt, 0.0f, false);
        if (affectedChunk != null) {
            //Adding voxels to history stack for playback
            BeforeVoxelPlaced?.Invoke(voxel, posInt);
            DirtyNeighborMeshes(posInt, priority);
            VoxelPlaced?.Invoke(voxel, posInt.x, posInt.y, posInt.z);
        }
        return affectedChunk;
    }

    public ushort[] BulkReadVoxels(Vector3[] positions) {
        var result = new ushort[positions.Length];
        for (var i = 0; i < positions.Length; i++) {
            result[i] = ReadVoxelAt(positions[i]);
        }
        return result;
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
     
    public GameObject GetPrefabAt(Vector3 pos) {
        Vector3Int chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out Chunk chunk);
        if (chunk == null) {
            return null;
        }
 
        return chunk.GetPrefabAt(pos);
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

    /// <summary>
    /// Returns a random occupied voxel position in the world.
    /// </summary>
    public Vector3 GetRandomVoxelInWorld() {
        var rand = new System.Random();
        var randomChunk = chunks.ElementAt(rand.Next(0, chunks.Count)).Value;
        return randomChunk.GetRandomOccupiedVoxelPosition();
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
        return WorldPosToChunkKey(globalCoordinate.x, globalCoordinate.y, globalCoordinate.z);
    }

    [HideFromTS]
    public static Vector3Int ChunkKeyToWorldPos(Vector3Int chunkPos) {
        return chunkPos * chunkSize;
    }

    [HideFromTS]
    public static Vector3Int WorldPosToChunkKey(int globalCoordinateX, int globalCoordinateY, int globalCoordinateZ) {
        int x = globalCoordinateX >= 0 ? globalCoordinateX >> logChunkSize : -(-(globalCoordinateX + 1) >> logChunkSize) - 1;
        int y = globalCoordinateY >= 0 ? globalCoordinateY >> logChunkSize : -(-(globalCoordinateY + 1) >> logChunkSize) - 1;
        int z = globalCoordinateZ >= 0 ? globalCoordinateZ >> logChunkSize : -(-(globalCoordinateZ + 1) >> logChunkSize) - 1;

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

    public VoxelData 
        GetVoxelAt(Vector3 pos) {
        Vector3Int posi = FloorInt(pos);
        Vector3Int chunkKey = WorldPosToChunkKey(posi);
        chunks.TryGetValue(chunkKey, out Chunk value);
        if (value == null) {
            return 0;
        } 

        return value.GetVoxelAt(posi);
    }
    
    public Color32 GetVoxelColorAt(Vector3 pos) {
        var posi = FloorInt(pos);
        var chunkKey = WorldPosToChunkKey(posi);
        if (!chunks.TryGetValue(chunkKey, out var value)) {
            return new Color32();
        }
        return value.GetVoxelColorAt(posi);
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
            if (child.name == "Chunks") {
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

    public void FillFlatGround() {
        VoxelData grass = voxelBlocks.SearchForBlockIdByString("GRASS");

        for (int x = -64; x < 64; x++) {
            for (int z = -64; z < 64; z++) {
                WriteVoxelAtInternal(new Vector3Int(x, 0, z), grass);
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

#if UNITY_EDITOR        
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
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
        if (this.voxelBlocks == null) {
            //Error
            Debug.LogError("No voxel blocks defined. Please define some blocks in the inspector.");
            return;
        }
        
        Profiler.BeginSample("LoadWorldFromVoxelBinaryFile");

        float startTime = Time.realtimeSinceStartup;
 
        this.delayUpdate = 1;
        
        //Clear to begin with
        DeleteChildGameObjects(gameObject);
        
        this.PrepareVoxelWorldGameObject();
        this.loadingStatus = LoadingStatus.Loading;

        this.voxelBlocks.Reload();
        
        //load the text of textAsset
        file.LoadIntoVoxelWorld(this);

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

        if (chunks.Count > 0 && hasUnsavedChanges) {
            //Create a temporary asset for saving
            /*this.domainReloadSaveFile = ScriptableObject.CreateInstance<WorldSaveFile>();
            this.domainReloadSaveFile.CreateFromVoxelWorld(this);
            Debug.Log("Temporarily saving Voxel World");*/
            SaveToFile();
        }
#endif        
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
        var mainCam = Camera.main;
        if (mainCam) {
            _focusCameraTransform = mainCam.transform;
            _focusCamera = mainCam;
        }
        doVisuals = RunCore.IsClient() || Application.isEditor;
        PrepareVoxelWorldGameObject();
    }

    public VoxelWorld() {
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    /// <summary>
    /// When VoxelWorld is setup we set the focus camera to Camera.main. This function
    /// is to change that camera at any point.
    /// </summary>
    public void UpdateFocusCamera(Camera focusCamera) {
        if (!RunCore.IsClient()) {
            Debug.LogError("VoxelWorld focus camera is client only.");
            return;
        }
        if (!useCameraAsFocusPosition) {
            Debug.LogWarning("Updated VoxelWorld focus camera won't be used (UseCameraAsFocusPosition is false).");
        }
        _focusCameraTransform = focusCamera.transform;
        _focusCamera = focusCamera;
    }

    private void OnEnable() {

#if UNITY_EDITOR
       /* if (this.domainReloadSaveFile != null) {
            Debug.Log("Reloading " + name + " after doman reload");
            this.LoadWorldFromSaveFile(this.domainReloadSaveFile);
            this.domainReloadSaveFile = null;
            this.hasUnsavedChanges = true;
            return; 
        }*/
          
#endif
        

        if (Application.isPlaying && this.autoLoad) {
            if (this.voxelWorldFile != null) {
                this.LoadWorldFromSaveFile(this.voxelWorldFile);
            }
            return;
        }

        if (!Application.isPlaying) {
            if (this.voxelWorldFile != null) {
                this.LoadWorldFromSaveFile(this.voxelWorldFile);
                return;
            }
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

    /// <summary>
    /// Waits until the chunk containing the passed in position loads. Returns
    /// immediately if the chunk is already loaded.
    /// </summary>
    public async Task WaitForChunkToLoad(Vector3 voxel) {
        var chunk = GetChunkByVoxel(voxel);
        if (chunk == null) return;
        
        await chunk.WaitForLoaded();
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

        Camera relevantFocusCamera = null;
        if (useCameraAsFocusPosition) relevantFocusCamera = focusCamera;
        Vector3 forward = Vector3.zero;
        Vector3 camPos = Vector3.zero;
        if (relevantFocusCamera) {
            var camTransform = relevantFocusCamera.transform;
            forward = camTransform.rotation * Vector3.forward;
            camPos = camTransform.position - forward * (chunkSize >> 1);
        }

        var cos50Deg = 0.6427;
        
        if (maxChunksToUpdateVar > 0 && chunksThatNeedThreadKickoff.Count > 0) {
            var focusPositionChunkKey = WorldPosToChunkKey(this.focusPosition);

            chunksThatNeedThreadKickoff.Sort((a, b) => {
                var aDist = (a.chunkKey - focusPositionChunkKey).magnitude;
                var bDist = (b.chunkKey - focusPositionChunkKey).magnitude;
                // If chunk is beyond 55 degrees of view from camera then treat it as much (250 blocks) further
                // in terms of priority
                if (forward != Vector3.zero) {
                    if (Vector3.Dot(forward, (((a.chunkKey + Vector3.one * 0.5f) * chunkSize) - camPos).normalized) < cos50Deg) aDist += (250f / chunkSize);
                    if (Vector3.Dot(forward, (((b.chunkKey + Vector3.one * 0.5f) * chunkSize) - camPos).normalized) < cos50Deg) bDist += (250f / chunkSize);
                }
                
                return aDist.CompareTo(bDist);
            });

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
            cam = GameObject.FindFirstObjectByType<Camera>();
        }
        foreach (var c in chunks) {
            c.Value.currentCamera = cam;
        }
        currentCamera = cam;

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

#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (state == PlayModeStateChange.ExitingEditMode && chunks.Count > 0 && hasUnsavedChanges) {
            SaveToFile();
        }
    }
#endif



}
