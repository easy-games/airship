using System.Collections.Generic;
using UnityEngine;
public abstract class VoxelPlacementModifier {
    public abstract string GetName();
    public abstract void OnPlaceVoxels(VoxelWorld voxelWorld, HashSet<Vector3Int> positions);

    /** Display custom properties for placement mod */
    public virtual void OnInspectorGUI() {
        
    }
}