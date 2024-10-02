using System.Collections.Generic;
using UnityEngine;

public class RotationPlacementMod : VoxelPlacementModifier {
    public override string GetName() {
        return "Rotation Symmetry";
    }

    public override void OnPlaceVoxels(VoxelWorld world, HashSet<Vector3Int> positions, int data) {
        var toAdd = new List<Vector3Int>();
        // For each current placement position rotate it 3 times around center
        foreach (var pos in positions) {
            var mirrorAroundPosition = world.mirrorAround + new Vector3(0.5f, 0.5f, 0.5f);
            for (var i = 1; i < 4; i++) {
                var offset = (Vector3)pos + (Vector3.one / 2) - mirrorAroundPosition;
                var rotated = Quaternion.Euler(0, 90 * i, 0) * offset;
                var dest = mirrorAroundPosition + rotated;

                var placementPosition = Vector3Int.FloorToInt(dest);
                toAdd.Add(placementPosition);
            }
        }

        // Register all added positions
        foreach (var addedPos in toAdd) {
            positions.Add(addedPos);
        }
    }
}