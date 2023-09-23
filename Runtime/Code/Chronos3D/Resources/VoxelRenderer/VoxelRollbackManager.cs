using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Timing;
using UnityEngine;
using UnityEngine.Serialization;
using VoxelWorldStuff;

public struct ChunkSnapshot
{
    public Vector3Int ChunkPos;
    public List<Bounds> BoxColliders;
}

public struct VoxelPlaceSnapshot
{
    public ushort voxel;
    public Vector3Int voxelPos;
}

public struct WorldSnapshot
{
    public uint Tick;
    public Dictionary<Vector3Int, ChunkSnapshot> Chunks;
}

public class VoxelRollbackManager : MonoBehaviour
{
    public VoxelWorld voxelWorld;
    private List<WorldSnapshot> _worldSnapshots = new();
    private Dictionary<uint, List<VoxelPlaceSnapshot>> _voxelPlacedSnapshots = new();
    private HashSet<Chunk> _dirtiedChunks = new();
    private uint _currentlyLoadedTick;
    public event Action<ushort, Vector3Int> ReplayPreVoxelCollisionUpdate;
    private bool detectedChanges = false;

    private void OnEnable()
    {
        voxelWorld.BeforeVoxelPlaced += OnPreVoxelCollisionUpdate;
        InstanceFinder.TimeManager.OnPostTick += TimeManager_OnPostTick;
    }

    private void OnDisable()
    {
        voxelWorld.BeforeVoxelPlaced -= OnPreVoxelCollisionUpdate;
        InstanceFinder.TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }

    private void TimeManager_OnPostTick() {
        if (this.detectedChanges) {
            this.detectedChanges = false;
        }
    }

    private void OnPreVoxelCollisionUpdate(ushort voxel, Vector3Int voxelPos)
    {
        if (!_voxelPlacedSnapshots.TryGetValue(InstanceFinder.TimeManager.LocalTick, out var voxelSnaps))
        {
            voxelSnaps = new();
            _voxelPlacedSnapshots.Add(InstanceFinder.TimeManager.LocalTick, voxelSnaps);
        }
        voxelSnaps.Add(new VoxelPlaceSnapshot()
        {
            voxel = voxel,
            voxelPos = voxelPos,
        });
    }

    public void AddChunkSnapshotsNearVoxelPos(uint tick, Vector3Int voxelPos, bool preSnapshot)
    {
        // if (preSnapshot)
        // {
        //     if (_worldSnapshots.Count == 0)
        //     {
        //         tick = 0;
        //     } else
        //     {
        //         var found = false;
        //         for (int i = _worldSnapshots.Count - 1; i >= 0; i--)
        //         {
        //             var worldSnapshot = _worldSnapshots[i];
        //             if (IsWorldSnapshotRelevant(worldSnapshot, voxelPos))
        //             {
        //                 if (tick > worldSnapshot.Tick + 1)
        //                 {
        //                     tick = worldSnapshot.Tick + 1;
        //                     found = true;
        //                     break;
        //                 }
        //             }
        //         }
        //
        //         if (!found)
        //         {
        //             tick = 0;
        //         }
        //         // print("Added pre-snapshot at tick " + tick);
        //     }
        // }
        if (preSnapshot) {
            tick--;
        }
        
        var chunkPos = VoxelWorld.WorldPosToChunkKey(voxelPos);
        AddChunkSnapshot(tick, chunkPos);
        // AddChunkSnapshot(tick, chunkPos + new Vector3Int(1, 0, 0));
        // AddChunkSnapshot(tick, chunkPos + new Vector3Int(-1, 0, 0));
        // AddChunkSnapshot(tick, chunkPos + new Vector3Int(0, 0, 1));
        // AddChunkSnapshot(tick, chunkPos + new Vector3Int(0, 0, -1));
        // AddChunkSnapshot(tick, chunkPos + new Vector3Int(0, 1, 0));
        // AddChunkSnapshot(tick, chunkPos + new Vector3Int(0, -1, 0));
    }

    private bool IsWorldSnapshotRelevant(WorldSnapshot worldSnapshot, Vector3Int voxelPos)
    {
        foreach (ChunkSnapshot chunkSnapshot in worldSnapshot.Chunks.Values)
        {
            if ((VoxelWorld.WorldPosToChunkKey(voxelPos) - chunkSnapshot.ChunkPos).magnitude <= 1)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetWorldSnapshot(uint tick, out WorldSnapshot worldSnapshot)
    {
        for (int i = _worldSnapshots.Count - 1; i >= 0; i--)
        {
            if (_worldSnapshots[i].Tick == tick)
            {
                worldSnapshot = _worldSnapshots[i];
                return true;
            }
        }

        worldSnapshot = default;
        return false;
    }

    public void AddChunkSnapshot(uint tick, Vector3Int chunkPos)
    {
        Chunk chunk = voxelWorld.GetChunkByChunkPos(chunkPos);
        if (chunk == null)
        {
            // Debug.LogError("Failed to find chunk with chunkPos=" + chunkPos);
            return;
        }
        // chunk.MainthreadForceCollisionForVoxel(new Vector3());

        if (!TryGetWorldSnapshot(tick, out var worldSnapshot))
        {
            worldSnapshot = new WorldSnapshot()
            {
                Tick = tick,
                Chunks = new(),
            };
            _worldSnapshots.Add(worldSnapshot);
        }

        List<Bounds> bounds = new(chunk.colliders.Count);
        foreach (var chunkCollider in chunk.colliders)
        {
            if (chunkCollider == null) {
                continue;
            }
            bounds.Add(new Bounds(chunkCollider.bounds.center, chunkCollider.bounds.size));
        }

        worldSnapshot.Chunks.Remove(chunk.chunkKey);
        worldSnapshot.Chunks.Add(chunk.chunkKey, new ChunkSnapshot()
        {
            BoxColliders = bounds,
            ChunkPos = chunk.chunkKey
        });
    }

    public void LoadSnapshot(uint tick, Vector3Int voxelPos)
    {
        WorldSnapshot worldSnapshot = default;
        bool snapshotFound = false;
        for (int i = _worldSnapshots.Count - 1; i >= 0; i--)
        {
            var snap = _worldSnapshots[i];
            if (snap.Tick <= tick)
            {
                if (IsWorldSnapshotRelevant(snap, voxelPos))
                {
                    worldSnapshot = snap;
                    snapshotFound = true;
                    // print("Requested load snapshot tick=" + tick + ", picked=" + worldSnapshot.Tick);
                    break;   
                }
            }
        }

        if (!snapshotFound)
        {
            return;
        }

        if (_voxelPlacedSnapshots.TryGetValue(tick, out var placementSnaps))
        {
            foreach (var placement in placementSnaps)
            {
                ReplayPreVoxelCollisionUpdate?.Invoke(placement.voxel, placement.voxelPos);
            }
        }

        Bounds boundsSnapshot;
        BoxCollider collider;
        int colliderCount = 0;
        foreach (var chunkPairSnapshot in worldSnapshot.Chunks)
        {
            var chunk = voxelWorld.GetChunkByChunkPos(chunkPairSnapshot.Key);
            _dirtiedChunks.Add(chunk);
            int i = 0;
            
            // VoxelWorldCollision.ClearCollision(chunk);
            
            // Update existing colliders 
            for (; i < chunk.colliders.Count; i++)
            {
                collider = chunk.colliders[i];
                
                // Not enough snapshot bounds to fill this collider. disable it.
                if (i >= chunkPairSnapshot.Value.BoxColliders.Count) {
                    collider.enabled = false;
                    continue;
                }
            
                boundsSnapshot = chunkPairSnapshot.Value.BoxColliders[i];
                collider.center = boundsSnapshot.center;
                collider.size = boundsSnapshot.size;
                collider.enabled = true;
                colliderCount++;
            }

            // Create new colliders
            for (; i < chunkPairSnapshot.Value.BoxColliders.Count; i++)
            {
                boundsSnapshot = chunkPairSnapshot.Value.BoxColliders[i];
                collider = chunk.GetGameObject().AddComponent<BoxCollider>();
                collider.size = boundsSnapshot.size;
                collider.center = boundsSnapshot.center;
                collider.hasModifiableContacts = true;
                colliderCount++;
            }
        }

        // if (colliderCount > 0)
        // {
        //     print("Updated " + colliderCount + " colliders for tick=" + tick);
        // }

        _currentlyLoadedTick = worldSnapshot.Tick;
    }

    public void RevertBackToRealTime()
    {
        foreach (Chunk chunk in _dirtiedChunks)
        {
            chunk.MainthreadForceCollisionRebuild();
        }
        _dirtiedChunks.Clear();
        _currentlyLoadedTick = InstanceFinder.TimeManager.LocalTick;
    }

    public void DiscardSnapshotsBehindTick(uint tick)
    {
        List<WorldSnapshot> toRemove = new();
        foreach (var snap in _worldSnapshots)
        {
            if (snap.Tick < tick)
            {
                toRemove.Add((snap));
            }
        }
        
        // keep most recent
        // if (toRemove.Count > 0)
        // {
        //     toRemove.RemoveAt(toRemove.Count - 1);
        // }

        List<uint> removed = new();
        foreach (var toRemoveSnapshot in toRemove)
        {
            _worldSnapshots.Remove(toRemoveSnapshot);
            removed.Add(toRemoveSnapshot.Tick);
        }

        // Placements

        List<uint> placeSnapsTicksToRemove = new();
        foreach (var pair in _voxelPlacedSnapshots)
        {
            if (pair.Key < tick)
            {
                placeSnapsTicksToRemove.Add(pair.Key);
            }
        }

        foreach (var removeTick in placeSnapsTicksToRemove)
        {
            _voxelPlacedSnapshots.Remove(removeTick);
        }

        // string s = "Discarded " + toRemove.Count + " behind tick=" + tick + ". Remaining=" + _voxelPlacedSnapshots.Count;
        // Debug.Log(s);
    }
}