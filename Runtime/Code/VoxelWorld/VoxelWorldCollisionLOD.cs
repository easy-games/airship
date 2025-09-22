using System;
using System.Collections.Generic;
using Code.Network.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;

/// <summary>
/// When added to a VoxelWorld GameObject this will cause VoxelWorld collisions to be disabled if not
/// near the focal point of the world.
/// </summary>
[LuauAPI][RequireComponent(typeof(VoxelWorld))]
public class VoxelWorldCollisionLOD : MonoBehaviour {
    public int distance {
        get => _distance;
        set {
            _distance = value;
        }
    }

    /// <summary>
    /// Distance in chunk space, squared
    /// </summary>
    private float ChunkDistanceSqr {
        get => Mathf.Pow(_distance / (float) VoxelWorld.chunkSize, 2);
    }
    [SerializeField][Tooltip("Any chunks within this distance from the VoxelWorld focal point will have collisions")]
    private int _distance = 16;
    
    /// <summary>
    /// The focus position at the time of the most recent chunk LOD update 
    /// </summary>
    private Vector3 _lastChunkUpdatePosition;
    private VoxelWorld _voxelWorld;
    /// <summary>
    /// How far does the focus position need to change for us to recalculate whether nearby chunks should be
    /// collision enabled (squared as a minor optimization)
    /// </summary>
    private double _refreshDistanceSqr;
    /// <summary>
    /// Set of all chunk keys that are currently active (have collisions)
    /// </summary>
    private HashSet<Vector3Int> _activeCollisionChunks = new();

    private void Awake() {
        if (RunCore.IsServer()) {
            enabled = false;
            return;
        }

        _voxelWorld = transform.GetComponent<VoxelWorld>();
        _refreshDistanceSqr = Mathf.Pow(VoxelWorld.chunkSize / 4.0f, 2);
    }

#if !UNITY_SERVER
    private void OnEnable() {
        AirshipSimulationManager.Instance.OnTick += OnTick;
        _voxelWorld.ChunkAdded += OnChunkAdded;
        
        // Disable all chunks and check for an LOD update to enable nearby chunks
        SetAllChunksActive(false);
        CheckForLODUpdate();
    }

    private void OnDisable() {
        AirshipSimulationManager.Instance.OnTick -= OnTick;
        _voxelWorld.ChunkAdded -= OnChunkAdded;
        _activeCollisionChunks.Clear();
        
        // Re-enable all chunk collisions
        SetAllChunksActive(true);
    }
    
    private void SetAllChunksActive(bool active) {
        foreach (var (_, chunk) in _voxelWorld.chunks) {
            chunk.GetCollisionGameObject().SetActive(active);
        }
    }

    private void OnChunkAdded(Chunk chunk) {
        var focusChunkKey = VoxelWorld.WorldPosToChunkKey(_lastChunkUpdatePosition);
        var shouldBeActive = (chunk.chunkKey - focusChunkKey).sqrMagnitude < ChunkDistanceSqr;
        chunk.GetCollisionGameObject().SetActive(shouldBeActive);

        if (shouldBeActive) _activeCollisionChunks.Add(chunk.chunkKey);
    }
    
    private void OnTick(object tick, object time, object isReplay) {
        CheckForLODUpdate();
    }
    
    private void CheckForLODUpdate() {
        var currentPosition = _voxelWorld.focusPosition;
        CheckForLODUpdate(currentPosition);
    }
    
    private void CheckForLODUpdate(Vector3 currentPosition) {
        var focusMovedSignificantly = (currentPosition - _lastChunkUpdatePosition).sqrMagnitude > _refreshDistanceSqr;
        if (_lastChunkUpdatePosition != default && !focusMovedSignificantly) return;
        
        Profiler.BeginSample("VWCollisionLODUpdate");
        var min = currentPosition - (distance + VoxelWorld.chunkSize) * Vector3.one;
        var max = currentPosition + (distance + VoxelWorld.chunkSize) * Vector3.one;

        var currPosChunkSpace = currentPosition / VoxelWorld.chunkSize;
        var lastPosChunkSpace = _lastChunkUpdatePosition / VoxelWorld.chunkSize;
        
        var newEnabledChunks = new HashSet<Vector3Int>();

        // Loop over all chunks within distance of currentPosition
        for (var x = min.x; x < max.x + VoxelWorld.chunkSize; x += VoxelWorld.chunkSize) {
            for (var y = min.y; y < max.y + VoxelWorld.chunkSize; y += VoxelWorld.chunkSize) {
                for (var z = min.z; z < max.z + VoxelWorld.chunkSize; z += VoxelWorld.chunkSize) {
                    var chunkKey = VoxelWorld.WorldPosToChunkKey(new Vector3(x, y, z));
                    var centerOfChunk = chunkKey + Vector3.one / 2;
                    
                    var inLastRange = (centerOfChunk - lastPosChunkSpace).sqrMagnitude <= ChunkDistanceSqr;
                    var inCurrRange = (centerOfChunk - currPosChunkSpace).sqrMagnitude <= ChunkDistanceSqr;
                    // On first run there are no previously enabled chunks
                    if (_lastChunkUpdatePosition == default) inLastRange = false;

                    if (inCurrRange) newEnabledChunks.Add(chunkKey);
                    
                    // Debug.DrawLine(new Vector3(x, y, z), new Vector3(x, y + 4, z), !inCurrRange ? Color.red : (inLastRange == inCurrRange) ? Color.magenta : Color.green, 3);
                    
                    // No change in whether this chunk should have collisions
                    if (inLastRange == inCurrRange) continue;
                    
                    if (_voxelWorld.chunks.TryGetValue(chunkKey, out var chunk)) {
                        chunk.GetCollisionGameObject().SetActive(inCurrRange);
                    }
                }
            }   
        }
        
        // Disable all previously enabled chunks
        foreach (var chunkKey in _activeCollisionChunks) {
            if (newEnabledChunks.Contains(chunkKey)) continue;
            
            if (_voxelWorld.chunks.TryGetValue(chunkKey, out var chunk)) {
                chunk.GetCollisionGameObject().SetActive(false);
            }
        }
        
        _activeCollisionChunks = newEnabledChunks;
        _lastChunkUpdatePosition = currentPosition;
        Profiler.EndSample();
    }
#endif
}