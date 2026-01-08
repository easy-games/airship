using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
using Code.Zstd;
using Luau;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public partial class VoxelWorld : MonoBehaviour {
    /// <summary>
    /// If enabled all quarter blocks will be replaced with default cube voxels
    /// </summary>
    public bool useSimplifiedVoxels = false;

    public const bool runThreaded = true; //Turn off if you suspect threading problems
    public const int chunkSize = 16; //fixed size

    public const int
        maxMainThreadMeshMillisecondsPerFrame
            = 10; //Dont spend more than 10ms per frame on uploading meshes to GPU or rebuilding collision

    public const int
        maxMainThreadThreadKickoffMillisecondsPerFrame
            = 4; //Dont spent more than 4ms on the main thread kicking off threads

    /// <summary>
    /// Max MS per frame across both maxMainThreadMeshMillisecondsPerFrame & maxMainThreadThreadKickoffMillisecondsPerFrame
    /// </summary>
    public const int maxMainThreadMillisecondsPerFrame = 10;

    public const bool showDebugBounds = false;

    [NonSerialized]
    internal const int logChunkSize = 4; // Log_2 of chunkSize, update with chunkSize (if it is a power of 2)!

    public bool doVisuals {
        get => RunCore.IsClient()
#if UNITY_EDITOR
               || VoxelWorldEditorConfig.instance.renderVoxelWorldInServerView || !Application.isPlaying
#endif
               ;
    } //Turn on for headless servers

    public Vector3 focusPosition {
        get {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView) {
                    var sceneCamera = sceneView.camera;
                    if (sceneCamera) {
                        return sceneCamera.transform.position;
                    }
                }
            }
#endif
            if (useCameraAsFocusPosition && _focusCameraTransform) {
                return _focusCameraTransform.position;
            }

            return _focusPosition;
        }
        set => _focusPosition = value;
    }

    private Vector3 _focusPosition;

    [Tooltip(
        "If enabled we use the main camera position as the VoxelWorld focus position (prioritizing updates to nearby chunks)")]
    public bool useCameraAsFocusPosition = true;

    private Transform _focusCameraTransform;

    internal Camera focusCamera {
        get {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView) {
                    var sceneCamera = sceneView.camera;
                    if (sceneCamera) {
                        return sceneCamera;
                    }
                }
            }
#endif
            return _focusCamera;
        }
        set => _focusCamera = value;
    }

    private Camera _focusCamera;

    [SerializeField] public bool autoLoad = true;

    [SerializeField] [HideInInspector] public WorldSaveFile voxelWorldFile = null;

    [SerializeField] [HideInInspector] public VoxelWorldNetworker worldNetworker;

    [HideInInspector] public GameObject chunksFolder;
    [HideInInspector] public GameObject lightsFolder;

    public event Action<Chunk> BeforeVoxelChunkUpdated; //Array of chunkIds
    public event Action<Chunk> VoxelChunkUpdated; //Array of chunkIds
    public event Action<ushort, Vector3Int> BeforeVoxelPlaced;
    public event Action<object, object, object, object> VoxelPlaced;
    public event Action OnFinishedLoading;
    public event Action OnFinishedReplicatingChunksFromServer;
    [HideInInspector] public bool finishedReplicatingChunksFromServer = false;

    [HideInInspector] public Dictionary<Vector3Int, Chunk> chunks = new(new Vector3IntEqualityComparer());
    //[HideInInspector] public Dictionary<string, Transform> worldPositionEditorIndicators = new();
    //[HideInInspector][NonSerialized] public List<WorldSaveFile.WorldPosition> worldPositions = new();

    // Tracks which chunks are currently being processed for mesh generation.  HashSet will need to be updated
    // if we need to add another code path that sets a chunk as processing or nulls it.
    private HashSet<Vector3Int> processingMeshChunks = new();

    //Detail meshes (grass etc)
    [NonSerialized]
    public float lodNearDistance = 40; //near meshes will swap to far meshes at this range

    [NonSerialized]
    public float lodFarDistance = 150; //far meshes will fade out entirely at this range

    [NonSerialized]
    public float lodTransitionSpeed = 1;

    //Texture atlas/block definitions    
    [HideInInspector]
    public VoxelBlocks voxelBlocks;

    [NonSerialized]
    public int selectedBlockIndex = 1;

    //For the editor
    [NonSerialized]
    public ushort highlightedBlock = 0;

    [NonSerialized]
    public Vector3Int highlightedBlockPos = new();

    [NonSerialized]
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

    public static Flips[] allFlips = (Flips[])Enum.GetValues(typeof(Flips));

    [HideInInspector]
    public bool renderingDisabled = false;

    //[HideInInspector] private bool debugGrass = false;
    [NonSerialized]
    public bool hasUnsavedChanges = false;

    // Methods
    /// <summary>
    /// Get the block id of the voxel. Used to index from BlockDefinition
    /// </summary>
    /// <param name="voxelData"></param>
    /// <returns>Unsigned short that represents the index in the voxel worlds block definitions</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetVoxelDataId(int voxelData) {
        return (ushort)(voxelData & 0xFFF); //Lower 12 bits
    }

    // Methods
    /// <summary>
    /// Get the block id of the voxel. Used to index from BlockDefinition
    /// </summary>
    /// <param name="voxelData"></param>
    /// <returns>Unsigned short that represents the index in the voxel worlds block definitions</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetVoxelDataId(ushort voxelData) {
        return (ushort)(voxelData & 0xFFF); //Lower 12 bits
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetVoxelDataExtraBits(ushort voxelData) {
        //mask off everything except the upper 4 bits
        return (ushort)(voxelData & 0xF000);
    }

    // Methods
    /// <summary>
    /// Check if this voxel is a solid voxel
    /// </summary>
    /// <param name="voxelData"></param>
    /// <returns>true if it takes up space</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsVoxelDataIsSolid(ushort voxelData) {
        return (voxelData & 0x8000) != 0; //15th bit 
    }

    /// <summary>
    /// Takes voxel data and applies the solid data to it
    /// </summary>
    /// <param name="voxelData"></param>
    /// <param name="solid"></param>
    /// <returns>Returns the new voxel data with the set "solid" value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetVoxelDataWithSolidBit(ushort voxelData, bool solid) {
        //Solid bit is bit 15, toggle it on or off
        if (solid) {
            return (ushort)(voxelData | 0x8000);
        } else {
            return (ushort)(voxelData & 0x7FFF);
        }
    }

    /// <summary>
    /// Gets the flipped bits from a byte packed voxelData
    /// </summary>
    /// <param name="voxelData"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetVoxelDataFlippedBits(ushort voxelData) {
        //Flipped bits are the 12th,13th and 14th bits
        return (voxelData & 0x7000) >> 12;
    }

    protected static Quaternion GetRotationFromFlipBits(int flipBits) {
        var flipEnum = (Flips)flipBits;
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

    public static Quaternion GetVoxelDataRotation(ushort voxelData) {
        return GetRotationFromFlipBits(GetVoxelDataFlippedBits(voxelData));
    }

    public static Flips GetVoxelDataFlips(ushort voxelData) {
        return (Flips)GetVoxelDataFlippedBits(voxelData);
    }

    /// <summary>
    /// Half blocks are scaled based on their flip bits
    /// </summary>
    protected static Vector3 GetScaleFromFlipBits(int flipBits) {
        if (flipBits % 4 == 0) {
            return new Vector3(1, 0.5f, 1);
        }

        if (flipBits % 4 == 1) {
            return new Vector3(0.5f, 1, 1);
        }

        if (flipBits % 4 == 2) {
            return new Vector3(1, 1, 0.5f);
        }

        return Vector3.one;
    }
    
    public static Vector3 GetVoxelDataScale(ushort voxelData) {
        return GetScaleFromFlipBits(GetVoxelDataFlippedBits(voxelData));
    }

    /// <summary>
    /// Takes voxel data and applies the flippedBits
    /// </summary>
    /// <param name="voxelData"></param>
    /// <param name="flippedBits"></param>
    /// <returns>Returns the new voxel data with the set "flippedBits" value</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetVoxelDataWithFlippedBits(int voxel, int flippedBits) {
        // Ensure flippedBits is a 3-bit value (0-7)
        flippedBits &= 0x7;

        // Clear the 12th, 13th, and 14th bits in the original voxel
        voxel &= ~0x7000;

        // Set the 12th, 13th, and 14th bits using the flippedBits
        voxel |= flippedBits << 12;

        return voxel;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HashCoordinates(int x, int y, int z) {
        const int prime1 = 73856093;
        const int prime2 = 19349663;
        const int prime3 = 83492791;

        return (x * prime1) ^ (y * prime2) ^ (z * prime3);
    }

    public VoxelBlocks.CollisionType GetCollisionType(ushort voxelData) {
        if (voxelBlocks == null) {
            return VoxelBlocks.CollisionType.None;
        }

        return voxelBlocks.GetCollisionType(GetVoxelDataId(voxelData));
    }

    public Ray TransformRayToLocalSpace(Ray ray) {
        var mat = transform.worldToLocalMatrix;
        var origin = mat.MultiplyPoint(ray.origin);
        var direction = mat.MultiplyVector(ray.direction);
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
        finishedReplicatingChunksFromServer = true;
        OnFinishedReplicatingChunksFromServer?.Invoke();
    }
    
    /// <summary>
    /// Cast a ray and return hit voxel
    /// This is in local space, make sure you transform your ray into local space first
    /// </summary>
    /// <param name="localPos"></param>
    /// <param name="localDirection"></param>
    /// <param name="maxDistance"></param>
    /// <returns></returns>
    public VoxelRaycastResult RaycastVoxel(Vector3 localPos, Vector3 localDirection, float maxDistance) {
        var (hit, distance, hitPosition, hitNormal) = RaycastVoxel_Internal(localPos, localDirection, maxDistance);
        return new VoxelRaycastResult() {
            Hit = hit,
            Distance = distance,
            HitPosition = hitPosition,
            HitNormal = hitNormal
        };
    }

    public void WriteVoxelAt(Vector3 pos, double num, bool priority) {
        var posInt = Vector3Int.FloorToInt(pos);
        var voxel = (ushort)num;

        //Write the single voxel
        var affectedChunk = WriteSingleVoxelAt(posInt, voxel, priority);
        if (affectedChunk != null) {
            //Send network update
            if (RunCore.IsServer() && worldNetworker != null && worldNetworker.networkWriteVoxels) {
                worldNetworker.RpcWriteVoxel(posInt, voxel);
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
        var chunkKey = WorldPosToChunkKey(pos);
        if (!chunks.TryGetValue(chunkKey, out var chunk)) {
            return;
        }

        var addCollision = num > 0;

        // Don't write temporary collision if voxel doesn't have collisions
        if (addCollision && GetCollisionType(num) != VoxelBlocks.CollisionType.Solid) {
            return;
        }

        chunk.WriteTemporaryCollision(pos, addCollision);
    }

    /// <summary>
    /// Assign custom data to a voxel
    /// </summary>
    /// <param name="pos">World Position</param>
    /// <param name="data"></param>
    /// <param name="priority"></param>
    public void WriteVoxelCustomDataAt(Vector3 pos, BinaryBlob data, bool priority) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var chunk);
        if (chunk == null) {
            return;
        }

        var voxelPos = FloorInt(pos);
        if (chunk.GetVoxelDataAt(voxelPos) == 0) {
            return;
        }

        chunk.WriteCustomDataAt(voxelPos, data);
    }

    /// <summary>
    /// Assign a color to a voxel
    /// </summary>
    /// <param name="pos">World Position</param>
    /// <param name="color"></param>
    /// <param name="priority"></param>
    public void WriteVoxelColorAt(Vector3 pos, Color color, bool priority) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var chunk);
        if (chunk == null) {
            return;
        }

        var voxelPos = FloorInt(pos);
        if (chunk.GetVoxelDataAt(voxelPos) == 0) {
            return;
        }

        chunk.WriteVoxelColor(voxelPos, color);
        DirtyNeighborMeshes(voxelPos, false, priority);
    }

    public void DamageVoxelAt(Vector3 pos, float damage, bool priority) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var chunk);
        if (chunk == null) {
            return;
        }

        var voxelPos = FloorInt(pos);
        if (chunk.GetVoxelDataAt(voxelPos) == 0) {
            return;
        }

        chunk.WriteVoxelDamage(voxelPos, damage);
        DirtyMesh(voxelPos, false, priority);
    }

    private Chunk WriteSingleVoxelAt(Vector3Int posInt, ushort voxelData, bool priority) {
        var affectedChunk = WriteVoxelAtInternal(posInt, voxelData, out var collisionUpdated);
        DamageVoxelAt(posInt, 0.0f, false);
        if (affectedChunk != null) {
            //Adding voxels to history stack for playback
            BeforeVoxelPlaced?.Invoke(voxelData, posInt);
            DirtyNeighborMeshes(posInt, collisionUpdated, priority);
            VoxelPlaced?.Invoke(voxelData, posInt.x, posInt.y, posInt.z);
        }

        return affectedChunk;
    }

    /// <summary>
    /// Grab a set of voxel data based on positions
    /// </summary>
    /// <param name="positions"></param>
    /// <returns>Array of Voxel Data</returns>
    public ushort[] BulkReadVoxels(Vector3[] positions) {
        var result = new ushort[positions.Length];
        for (var i = 0; i < positions.Length; i++) {
            result[i] = ReadVoxelAt(positions[i]);
        }

        return result;
    }

    public void WriteVoxelGroupAt(Vector3[] positions, double[] voxelData, bool priority) {
        HashSet<Chunk> affectedChunks = new();
        for (var i = 0; i < positions.Length; i++) {
            var pos = FloorInt(positions[i]);
            var num = (ushort)voxelData[i];
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

        if (RunCore.IsServer() && worldNetworker != null && worldNetworker.networkWriteVoxels) {
            worldNetworker.RpcWriteVoxelGroup(positions, voxelData, priority);
        }
    }

    [HideFromTS]
    public List<GameObject> GetChildGameObjects() {
        var children = new List<GameObject>();
        foreach (Transform child in gameObject.transform) {
            children.Add(child.gameObject);
        }

        return children;
    }

    public GameObject GetPrefabAt(Vector3 pos) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var chunk);
        if (chunk == null) {
            return null;
        }

        return chunk.GetPrefabAt(pos);
    }

    [HideFromTS]
    public void InitializeChunksAroundChunk(Vector3Int chunkKey) {
        for (var x = -1; x <= 1; x++) {
            for (var y = -1; y <= 1; y++) {
                for (var z = -1; z <= 1; z++) {
                    if (x == 0 && y == 0 && z == 0) {
                        continue;
                    }

                    var key = new Vector3Int(chunkKey.x + x, chunkKey.y + y, chunkKey.z + z);
                    if (!chunks.ContainsKey(key)) {
                        var chunk = CreateChunk(key);
                        chunks.Add(chunk.chunkKey, chunk);
                        chunk.SetWorld(this);
                        chunks[chunkKey] = chunk;
                    }
                }
            }
        }
    }

    public static Chunk CreateChunk(Vector3Int key) {
        return new Chunk {
            chunkKey = key
        };
    }

    /**
     * Returns true if the voxel was written.
     * Will return false if the voxel is 
     */
    [HideFromTS]
    public Chunk WriteVoxelAtInternal(Vector3Int pos, ushort num, out bool collisionUpdated) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var chunk);
        if (chunk == null) {
            chunk = CreateChunk(chunkKey);
            chunks.Add(chunkKey, chunk);
            chunk.SetWorld(this);
            chunks[chunkKey] = chunk;
        }

        //Set solid bit?
        num = voxelBlocks.AddSolidMaskToVoxelValue(num);

        // Ignore if this changes nothing.
        var existingVoxel = chunk.GetVoxelDataAt(pos);
        if (num == existingVoxel) {
            collisionUpdated = false;
            return null;
        }

        // Check if we have the same collision type
        collisionUpdated = true;
        if (num != 0 || existingVoxel != 0) {
            collisionUpdated = GetCollisionType(num) != GetCollisionType(existingVoxel);
        }

        //Write a new voxel
        chunk.WriteVoxelAt(pos, num);
        return chunk;
    }

    /// <summary>
    /// Returns a random occupied voxel position in the world.
    /// </summary>
    public Vector3 GetRandomOccupiedVoxelPosition() {
        if (chunks.Count == 0) {
            throw new InvalidOperationException("GetRandomOccupiedVoxelPosition");
        }
        var rand = new System.Random();
        var randomChunk = chunks.ElementAt(rand.Next(0, chunks.Count)).Value;
        return randomChunk.GetRandomOccupiedVoxelPosition();
    }

    [HideFromTS]
    public ushort ReadVoxelAtInternal(Vector3Int pos) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var value);
        if (value == null) {
            return 0;
        }

        return value.GetVoxelDataAt(pos);
    }

    public ushort ReadVoxelAt(Vector3 pos) {
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
        var x = globalCoordinateX >= 0
            ? globalCoordinateX >> logChunkSize
            : -(-(globalCoordinateX + 1) >> logChunkSize) - 1;
        var y = globalCoordinateY >= 0
            ? globalCoordinateY >> logChunkSize
            : -(-(globalCoordinateY + 1) >> logChunkSize) - 1;
        var z = globalCoordinateZ >= 0
            ? globalCoordinateZ >> logChunkSize
            : -(-(globalCoordinateZ + 1) >> logChunkSize) - 1;

        return new Vector3Int(x, y, z);
    }

    [HideFromTS]
    public static Vector3Int WorldPosToChunkKey(Vector3 globalC) {
        var globalCoordinate = new Vector3Int(Mathf.FloorToInt(globalC.x), Mathf.FloorToInt(globalC.y),
            Mathf.FloorToInt(globalC.z));
        return WorldPosToChunkKey(globalCoordinate);
    }

    [HideFromTS]
    public Chunk GetChunkByVoxel(Vector3Int pos) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var value);
        return value;
    }

    [HideFromTS]
    public Chunk GetChunkByVoxel(Vector3 pos) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var value);
        return value;
    }

    public Chunk GetChunkByChunkPos(Vector3Int pos) {
        chunks.TryGetValue(pos, out var chunk);
        return chunk;
    }

    public (ushort, Chunk) GetVoxelAndChunkAt(Vector3Int pos) {
        var chunkKey = WorldPosToChunkKey(pos);
        chunks.TryGetValue(chunkKey, out var value);
        if (value == null) {
            return (0, null);
        }

        return (value.GetVoxelDataAt(pos), value);
    }

    public ushort GetVoxelAt(Vector3 pos) {
        var posI = FloorInt(pos);
        var chunkKey = WorldPosToChunkKey(posI);
        chunks.TryGetValue(chunkKey, out var value);
        if (value == null) {
            return 0;
        }

        return value.GetVoxelDataAt(posI);
    }

    public int GetVoxelIdAt(Vector3 pos) {
        var posI = FloorInt(pos);
        var chunkKey = WorldPosToChunkKey(posI);
        if (!chunks.TryGetValue(chunkKey, out var value)) {
            return -1;
        }

        return GetVoxelDataId(value.GetVoxelDataAt(posI));
    }

    public VoxelBlocks.BlockDefinition GetVoxelBlockType(Vector3 pos) {
        var index = GetVoxelIdAt(pos);
        if (index >= 0) {
            return voxelBlocks.GetBlock((ushort)index);
        }

        return null;
    }
    
    public BinaryBlob GetVoxelCustomDataAt(Vector3 pos) {
        var posi = FloorInt(pos);
        var chunkKey = WorldPosToChunkKey(posi);
        if (!chunks.TryGetValue(chunkKey, out var value)) {
            return null;
        }

        return value.GetCustomDataAt(posi);
    }
    
    public Color32 GetVoxelColorAt(Vector3 pos) {
        var posi = FloorInt(pos);
        var chunkKey = WorldPosToChunkKey(posi);
        if (!chunks.TryGetValue(chunkKey, out var value)) {
            return new Color32();
        }

        return value.GetVoxelColorAt(posi);
    }

    [HideFromTS]
    public uint GetVoxelColorUIntAt(Vector3 pos) {
        var posi = FloorInt(pos);
        var chunkKey = WorldPosToChunkKey(posi);
        if (!chunks.TryGetValue(chunkKey, out var value)) {
            return 0;
        }

        return value.GetVoxelColorUIntAt(posi);
    }

    [HideFromTS]
    public static uint Color32ToUInt(Color32 col) {
        var res = (uint)col.r << 24;
        res |= (uint)col.g << 16;
        res |= (uint)col.b << 8;
        res |= (uint)col.a;
        return res;
    }

    [HideFromTS]
    public static Color32 UIntToColor32(uint col) {
        var r = (byte)((col & 0xFF000000) >> 24);
        var g = (byte)((col & 0x00FF0000) >> 16);
        var b = (byte)((col & 0x0000FF00) >> 8);
        var a = (byte)(col & 0x000000FF);
        return new Color32(r, g, b, a);
    }

    public void DirtyMesh(Vector3Int voxel, bool dirtyCollisions, bool priority = false) {
        var chunk = GetChunkByVoxel(voxel);
        if (chunk != null) {
            chunk.SetGeometryDirty(true, priority);
            if (dirtyCollisions) {
                chunk.SetCollisionDirty(true);
            }

            if (priority && dirtyCollisions) {
                BeforeVoxelChunkUpdated?.Invoke(chunk);
                chunk.MainthreadForceCollisionRebuild();
                VoxelChunkUpdated?.Invoke(chunk);
            }
        } else {
            //if it is null, create it
            WriteVoxelAtInternal(voxel, 0, out var collisionUpdated);
        }
    }

    public void DirtyNeighborMeshes(Vector3Int voxel, bool dirtyCollision, bool priority = false) {
        DirtyMesh(voxel, dirtyCollision, priority);
        var localPosition = Chunk.WorldPosToLocalPos(voxel);

        if (localPosition.x == 0) {
            DirtyMesh(voxel + new Vector3Int(-1, 0, 0), false, false);
        }

        if (localPosition.y == 0) {
            DirtyMesh(voxel + new Vector3Int(0, -1, 0), false, false);
        }

        if (localPosition.z == 0) {
            DirtyMesh(voxel + new Vector3Int(0, 0, -1), false, false);
        }

        if (localPosition.x == chunkSize - 1) {
            DirtyMesh(voxel + new Vector3Int(+1, 0, 0), false, false);
        }

        if (localPosition.y == chunkSize - 1) {
            DirtyMesh(voxel + new Vector3Int(0, +1, 0), false, false);
        }

        if (localPosition.z == chunkSize - 1) {
            DirtyMesh(voxel + new Vector3Int(0, 0, +1), false, false);
        }
    }

    public void DeleteRenderedGameObjects() {
        if (chunksFolder) {
            DeleteChildGameObjects(chunksFolder);
        }

        if (lightsFolder) {
            DeleteChildGameObjects(lightsFolder);
        }
    }

    public static void DeleteChildGameObjects(GameObject parent) {
        Profiler.BeginSample("DeleteChildGameObjects");
        // Get a list of all the child game objects
        var children = new List<GameObject>();
        foreach (Transform child in parent.transform) {
            if (child.name == "Chunks") {
                DeleteChildGameObjects(child.gameObject);
                continue;
            }

            children.Add(child.gameObject);
        }

        // Delete all the children
        children.ForEach(child => DestroyImmediate(child));
        Profiler.EndSample();
    }

    /**
     * Creates missing child GameObjects and names things properly.
     */
    private void PrepareVoxelWorldGameObject() {
        loadingStatus = LoadingStatus.NotLoading;

        if (transform.Find("Chunks") != null) {
            chunksFolder = transform.Find("Chunks").gameObject;
        } else {
            chunksFolder = new GameObject("Chunks");
            chunksFolder.transform.parent = transform;
        }

        chunksFolder.transform.localPosition = Vector3.zero;
        chunksFolder.transform.localScale = Vector3.one;
        chunksFolder.transform.localRotation = Quaternion.identity;

        chunksFolder.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
    }

    public void GenerateWorld(bool populateTerrain = false) {
        PrepareVoxelWorldGameObject();

        if (!voxelBlocks) {
            Debug.LogError("No voxel blocks defined. Please define some blocks in the inspector.");
            return;
        }

        voxelBlocks.Reload(useSimplifiedVoxels);

        //this.blocks.Load(this.GetBlockDefinesContents());

        chunks.Clear();
        ClearProcessingMeshChunks();

        DeleteChildGameObjects(gameObject);

        RegenerateAllMeshes();

        hasUnsavedChanges = true;
    }

    public void CreateSingleStarterVoxel() {
        if (voxelBlocks == null || voxelBlocks.loadedBlocks.Count < 2) {
            Debug.LogError("No voxel blocks defined.");
            return;
        }

        foreach (var def in voxelBlocks.loadedBlocks) {
            if (def.definition.solid == true) {
                WriteVoxelAtInternal(new Vector3Int(0, 0, 0), def.blockId, out _);
                return;
            }
        }
    }

    public void FillRandomTerrain() {
        float scale = 4;
        var rand = new System.Random();

        var grass = voxelBlocks.SearchForBlockIdByString("GRASS");
        var dirt = voxelBlocks.SearchForBlockIdByString("DIRT");

        for (var x = -64; x < 64; x++) {
            //  for (int z = -127; z < 127; z++)
            for (var z = -64; z < 64; z++) {
                var height = (int)(Mathf.PerlinNoise((float)x / 256.0f * scale, (float)z / 256.0f * scale) * 32.0f);
                for (var y = 0; y < height; y++) {
                    WriteVoxelAtInternal(new Vector3Int(x, y, z), dirt, out _);
                }

                WriteVoxelAtInternal(new Vector3Int(x, height, z), grass, out _);
            }
        }

        RegenerateAllMeshes();

        hasUnsavedChanges = true;
    }

    public void FillFlatGround() {
        var grass = voxelBlocks.SearchForBlockIdByString("GRASS");

        for (var x = -64; x < 64; x++) {
            for (var z = -64; z < 64; z++) {
                WriteVoxelAtInternal(new Vector3Int(x, 0, z), grass, out _);
            }
        }

        RegenerateAllMeshes();

        hasUnsavedChanges = true;
    }

    public void FillSingleBlock() {
        var dirt = voxelBlocks.SearchForBlockIdByString("DIRT");

        WriteVoxelAtInternal(new Vector3Int(0, 0, 0), dirt, out _);

        RegenerateAllMeshes();
    }

    public void RegenerateAllMeshes() {
        Profiler.BeginSample("RegenerateAllMeshes");

        loadingStatus = LoadingStatus.Loading;

        // Force a mesh update
        foreach (var (_, chunk) in chunks) {
            chunk.SetGeometryDirty(true);
            chunk.SetCollisionDirty(true);
        }

        Profiler.EndSample();
    }

    private void OnDestroy() {
#if UNITY_EDITOR
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        foreach (var chunk in chunks) {
            chunk.Value.DestroyAllMeshes();
        }

        if (chunksFolder) {
            if (Application.isPlaying) {
                Destroy(chunksFolder);
            } else {
                DestroyImmediate(chunksFolder);
            }
        }

        if (lightsFolder) {
            if (Application.isPlaying) {
                Destroy(lightsFolder);
            } else {
                DestroyImmediate(lightsFolder);
            }
        }
    }

    public Vector3 CalculatePlaneIntersection(Vector3 origin, Vector3 dir, Vector3 planeNormal, Vector3 planePoint) {
        var t = Vector3.Dot(planePoint - origin, planeNormal) / Vector3.Dot(dir, planeNormal);
        return origin + dir * t;
    }

    public GameObject SpawnDebugSphere(Vector3 pos, Color col, float radius = 0.1f) {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = pos;
        sphere.transform.localScale = new Vector3(radius, radius, radius);
        sphere.transform.parent = gameObject.transform;

        var renderer = sphere.GetComponent<MeshRenderer>();

        renderer.sharedMaterial = new Material(Resources.Load<Material>("DebugSphere"));
        renderer.sharedMaterial.SetColor("_Color", col);

        return sphere;
    }

    private int delayUpdate = 0; // Don't run the voxelWorld update this frame, because we just loaded

    public enum LoadingStatus {
        NotLoading,
        Loading,
        Loaded
    }

    [NonSerialized]
    public LoadingStatus loadingStatus = LoadingStatus.NotLoading;

    public void LoadWorldFromSaveFile(WorldSaveFile file) {
        if (voxelBlocks == null) {
            //Error
            Debug.LogError("No voxel blocks defined. Please define some blocks in the inspector.");
            return;
        }

        Profiler.BeginSample("LoadWorldFromVoxelBinaryFile");

        var startTime = Time.realtimeSinceStartup;

        delayUpdate = 1;

        //Clear to begin with
        DeleteChildGameObjects(gameObject);

        PrepareVoxelWorldGameObject();
        loadingStatus = LoadingStatus.Loading;
        ClearProcessingMeshChunks();

        voxelBlocks.Reload(useSimplifiedVoxels);

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

        PrepareVoxelWorldGameObject();

        chunks.Clear();
        ClearProcessingMeshChunks();

        DeleteChildGameObjects(gameObject);
        RegenerateAllMeshes();
    }

    public void SaveToFile() {
#if UNITY_EDITOR
        if (voxelWorldFile == null) {
            return;
        }

        voxelWorldFile.CreateFromVoxelWorld(this);
        //Save the asset
        EditorUtility.SetDirty(voxelWorldFile);
        AssetDatabase.SaveAssets();

        hasUnsavedChanges = false;
#endif
    }

    public void SaveToDomainReloadFile() {
#if UNITY_EDITOR
        if (chunks.Count > 0 && hasUnsavedChanges) {
            // Create a temporary asset for saving
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
        PrepareVoxelWorldGameObject();
        ClearProcessingMeshChunks();

        voxelBlocks.Reload(useSimplifiedVoxels);

        RegenerateAllMeshes();
    }

    private void Awake() {
        var mainCam = Camera.main;
        if (mainCam) {
            _focusCameraTransform = mainCam.transform;
            _focusCamera = mainCam;
        }

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


        if (Application.isPlaying && autoLoad) {
            if (voxelWorldFile != null) {
                LoadWorldFromSaveFile(voxelWorldFile);
            }

            return;
        }

        if (!Application.isPlaying) {
            if (voxelWorldFile != null) {
                LoadWorldFromSaveFile(voxelWorldFile);
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
        if (chunk == null) {
            return;
        }

        await chunk.WaitForLoaded();
    }

    private void RegenerateMissingChunkGeometry() {
        var regenerateMissingChunkGeometryStartTime = Time.realtimeSinceStartup;
        // This can be high, we're mainly throttling on max time variables
        var maxChunksToUpdateVar = Mathf.Max(1, SystemInfo.processorCount - 2);

        // Sort chunks
        List<Chunk> chunksThatNeedThreadKickoff = new();
        List<Chunk> chunksThatNeedMeshUpdates = new();
        foreach (var chunkPair in chunks) {
            if (chunkPair.Value.NeedsToCopyMeshToScene()) {
                chunksThatNeedMeshUpdates.Add(chunkPair.Value);
                continue;
            } else if (chunkPair.Value.NeedsToGenerateMesh()) {
                chunksThatNeedThreadKickoff.Add(chunkPair.Value);
            }
        }
        
        Profiler.BeginSample("BuildAndFinalizeChunkMeshes");
        // Kickoff mainthread mesh copies, sorted by closest to camera
        if (chunksThatNeedMeshUpdates.Count > 0) {
            var startTime = Time.realtimeSinceStartup;
            var focusPositionChunkKey = WorldPosToChunkKey(focusPosition);

            if (RunCore.IsClient()) {
                chunksThatNeedMeshUpdates.Sort((x, y) =>
                    (x.chunkKey - focusPositionChunkKey).magnitude.CompareTo((y.chunkKey - focusPositionChunkKey)
                        .magnitude));
            }

            foreach (var chunk in chunksThatNeedMeshUpdates) {
                chunk.MainthreadUpdateMesh(this);
                
                // Make sure total time spent regenerating chunk geometry is okay
                var totalElapsedTime = (int)((Time.realtimeSinceStartup - regenerateMissingChunkGeometryStartTime) * 1000);
                if (totalElapsedTime > maxMainThreadMillisecondsPerFrame) {
                    break;
                }

                var elapsedTime = (int)((Time.realtimeSinceStartup - startTime) * 1000);
                if (elapsedTime > maxMainThreadMeshMillisecondsPerFrame) {
                    break;
                }
            }
        }
        Profiler.EndSample();
        
        Profiler.BeginSample("ChunkThreadKickoff");
        var currentlyUpdatingChunks = GetNumProcessingMeshChunks();
        maxChunksToUpdateVar = math.max(0, maxChunksToUpdateVar - currentlyUpdatingChunks);
        
        var chunkKickoffStartTime = Time.realtimeSinceStartup;
        // Kickoff new chunk threads until we've run out of time (or logical processors)
        foreach (var _ in StartChunkUpdateThread(chunksThatNeedThreadKickoff, maxChunksToUpdateVar)) {
            // Make sure chunk kickoff time is okay
            var elapsedTime = (int)((Time.realtimeSinceStartup - chunkKickoffStartTime) * 1000);
            if (elapsedTime > maxMainThreadThreadKickoffMillisecondsPerFrame) {
                break;
            }
            
            // Make sure total time spent regenerating chunk geometry is okay
            var totalElapsedTime = (int)((Time.realtimeSinceStartup - regenerateMissingChunkGeometryStartTime) * 1000);
            if (totalElapsedTime > maxMainThreadMillisecondsPerFrame) {
                break;
            }
        }
        Profiler.EndSample();


        if (loadingStatus == LoadingStatus.Loading) {
            var hasDirtyChunk = false;
            foreach (var chunkPair in chunks) {
                if (chunkPair.Value.IsGeometryDirty()) {
                    hasDirtyChunk = true;
                    break;
                }
            }
            //Debug.Log("Awaiting load - chunks remaining:" + hasDirtyChunk);

            if (!hasDirtyChunk) {
                loadingStatus = LoadingStatus.Loaded;
                OnFinishedLoading?.Invoke();
                
                // TODO this might not be the best location to be static batching
                StaticBatchingUtility.Combine(chunksFolder);
            }
        }

        var regenerateMissingChunkGeometryEndTime = Time.realtimeSinceStartup;
        var elapsedTimeInMs = (regenerateMissingChunkGeometryEndTime - regenerateMissingChunkGeometryStartTime) * 1000;
        if (elapsedTimeInMs > 17) {
            //Debug.Log("Slow voxelworld frame update:" + elapsedTimeInMs + "ms");
        }
    }

    private IEnumerable<int> StartChunkUpdateThread(List<Chunk> chunksThatNeedThreadKickoff, int maxChunksToUpdate) {
        Camera relevantFocusCamera = null;
        if (useCameraAsFocusPosition) {
            relevantFocusCamera = focusCamera;
        }
        
        var forward = Vector3.zero;
        var camPos = Vector3.zero;
        if (relevantFocusCamera) {
            var camTransform = relevantFocusCamera.transform;
            forward = camTransform.rotation * Vector3.forward;
            camPos = camTransform.position - forward * (chunkSize >> 1);
        }
        
        if (maxChunksToUpdate > 0 && chunksThatNeedThreadKickoff.Count > 0) {
            var focusPositionChunkKey = WorldPosToChunkKey(focusPosition);

            Profiler.BeginSample("Sort");
            var numChunksToKickOff = Mathf.Min(maxChunksToUpdate, chunksThatNeedThreadKickoff.Count);
            var chunksToKickOffNow = new Chunk[numChunksToKickOff];
            for (var i = 0; i < numChunksToKickOff; i++) {
                chunksToKickOffNow[i] = chunksThatNeedThreadKickoff[i];
            }

            // Loop over all chunks and keep replacing with best available chunk
            // This is random and definitely not a true sort function but should be good enough & fast
            // (this is only useful on client where focal point matters)
            if (RunCore.IsClient() && chunksThatNeedThreadKickoff.Count > numChunksToKickOff) {
                var replaceIndex = 0;
                var compareAgainstOrder =
                    GetChunkRenderOrder(chunksToKickOffNow[replaceIndex], camPos, forward, focusPositionChunkKey);
                for (var i = numChunksToKickOff; i < chunksThatNeedThreadKickoff.Count; i++) {
                    var chunk = chunksThatNeedThreadKickoff[i];
                    var chunkOrder = GetChunkRenderOrder(chunk, camPos, forward, focusPositionChunkKey);
                    // If this chunk is earlier in order replace and continue
                    if (chunkOrder < compareAgainstOrder) {
                        chunksToKickOffNow[replaceIndex] = chunk;
                        compareAgainstOrder = chunkOrder;
                        replaceIndex = (replaceIndex + 1) % maxChunksToUpdate;
                    }
                }
            }

            Profiler.EndSample();

            var updatedChunks = 0;
            foreach (var chunk in chunksToKickOffNow) {
                var didUpdate = chunk.MainthreadUpdateMesh(this);

                if (didUpdate) {
                    updatedChunks++;
                    yield return updatedChunks;
                }
            }
        }
    }

    private const double Cos65Deg = 0.422;

    /// <summary>
    /// Returns the render order for a chunk with lower values representing highest priority chunks
    /// </summary>
    private float GetChunkRenderOrder(Chunk chunk, Vector3 camPos, Vector3 forward, Vector3 focusPositionChunkKey) {
        var chunkKey = chunk.chunkKey;
        var dist = (chunkKey - focusPositionChunkKey).magnitude;
        // If chunk is beyond 55 degrees of view from camera then treat it as much (250 blocks) further
        // in terms of priority
        if (forward != Vector3.zero) {
            if (Vector3.Dot(forward, ((chunkKey + Vector3.one * 0.5f) * chunkSize - camPos).normalized) < Cos65Deg) {
                dist += 250f / chunkSize;
            }
        }
        
        // Super prioritize priority updates
        // if (chunk.GetPriorityUpdate()) dist -= 1000;

        return dist;
    }

    public void FullWorldUpdate() {
        Camera cam = null;
#if UNITY_EDITOR
        if (SceneView.currentDrawingSceneView != null) {
            cam = SceneView.currentDrawingSceneView.camera;
        }
#endif
        if (cam == null) {
            cam = FindFirstObjectByType<Camera>();
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
            if (delayUpdate > 0) {
                delayUpdate--;
                return;
            }

            StepWorld();
        }
    }

    private void StepWorld() {
        FullWorldUpdate();
    }

    public int GetNumRadiosityProcessingChunks() {
        var counter = 0;
        foreach (var chunk in chunks) {
            if (chunk.Value.IsBusy()) {
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

    internal void MarkChunkAsProcessing(Vector3Int chunkKey) {
        processingMeshChunks.Add(chunkKey);
    }

    internal void RemoveChunkFromProcessing(Vector3Int chunkKey) {
        processingMeshChunks.Remove(chunkKey);
    }

    internal void ClearProcessingMeshChunks() {
        processingMeshChunks.Clear();
    }

    public int GetNumProcessingMeshChunks() {
        return processingMeshChunks.Count;
    }

    public struct Vector3IntEqualityComparer : IEqualityComparer<Vector3Int> {
        public bool Equals(Vector3Int a, Vector3Int b) {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public int GetHashCode(Vector3Int obj) {
            unchecked {
                var hash = 47;
                hash = hash * 53 + obj.x;
                hash = hash * 53 + obj.y;
                hash = hash * 53 + obj.z;
                return hash;
            }
        }
    }

    public void ReloadTextureAtlas() {
        if (voxelBlocks == null) {
            return;
        }

        // If we're in the editor and we're playing the game
        // we can't reload textures because changes have not been imported yet to unity
        // So to get around this, we load the textures directly from disk
        var useTexturesDirectlyFromDisk = Application.isPlaying && Application.isEditor;
        voxelBlocks.Reload(useSimplifiedVoxels, useTexturesDirectlyFromDisk);

        // refresh the geometry
        foreach (var (_, chunk) in chunks) {
            chunk.SetGeometryDirty(true, false);
        }
    }

    public void AddChunk(Vector3Int key, Chunk chunk) {
        chunks.Add(key, chunk);
        chunk.SetGeometryDirty(true);
        chunk.SetCollisionDirty(true);
    }

    public string EncodeToString() {
       return Convert.ToBase64String(Zstd.CompressData(ToBuffer().Data, Zstd.DefaultCompressionLevel));
    }

    public void DecodeFromString(string stringData) {
        FromBuffer(Zstd.DecompressData(Convert.FromBase64String(stringData)));
    }

    public LuauBuffer ToBuffer() {
        var saveFile = ScriptableObject.CreateInstance<WorldSaveFile>();
        saveFile.CreateFromVoxelWorld(this);

        using var memStream = new MemoryStream();
        using var writer = new BinaryWriter(memStream);

        // Serialize the BlockIdToScopeNames list:
        saveFile.SerializeBlockIdToScopeNames(writer);

        // Get serialized data from above:
        var serialized = memStream.GetBuffer();
        var blockIdToScopeNamesSerialized = new ReadOnlySpan<byte>(serialized, 0, (int)memStream.Length);

        // Combine BlockIdToScopeNames and chunksCompressed:
        var allData = new byte[blockIdToScopeNamesSerialized.Length + saveFile.chunksCompressed.Length + sizeof(int)];
        using var memStreamFinal = new MemoryStream(allData);
        using var writerFinal = new BinaryWriter(memStreamFinal);
        writerFinal.Write(blockIdToScopeNamesSerialized);
        writerFinal.Write(saveFile.chunksCompressed.Length);
        writerFinal.Write(saveFile.chunksCompressed);

        return allData;
    }

    public void FromBuffer(LuauBuffer buffer) {
        var saveFile = ScriptableObject.CreateInstance<WorldSaveFile>();

        using var memStream = new MemoryStream(buffer);
        using var reader = new BinaryReader(memStream);

        saveFile.DeserializeBlockIdToScopeNames(reader);

        var chunksCompressedLen = reader.ReadInt32();
        var chunksCompressed = reader.ReadBytes(chunksCompressedLen);
        saveFile.chunksCompressed = chunksCompressed;
        saveFile.chunksCompressedV2 = true;

        saveFile.LoadIntoVoxelWorld(this);
    }

    public static VoxelWorld GetFirstInstance() {
        return FindAnyObjectByType<VoxelWorld>();
    }

    public static VoxelWorld[] GetAllInstances(FindObjectsInactive findObjectsInactive) {
        return FindObjectsByType<VoxelWorld>(findObjectsInactive, FindObjectsSortMode.None);
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