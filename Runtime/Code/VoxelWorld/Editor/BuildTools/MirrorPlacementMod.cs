using System.Collections.Generic;
using UnityEngine;

public class MirrorPlacementMod : VoxelPlacementModifier {
    public bool[] mirror = new bool[] { false, false, false }; // Mirror X, Y, Z
    
    public override void OnInspectorGUI() {
        mirror[0] = GUILayout.Toggle(mirror[0], "X");
        mirror[1] = GUILayout.Toggle(mirror[1], "Y");
        mirror[2] = GUILayout.Toggle(mirror[2], "Z");
    }
    
    public override string GetName() {
        return "Flip";
    }

    public override void OnPlaceVoxels(VoxelWorld world, HashSet<Vector3Int> positions) {
        var toAdd = new List<Vector3Int>();
        var mirrorAroundPosition = world.mirrorAround;
        // Check for mirror around each axis 
        for (var i = 0; i < 3; i++) {
            if (!mirror[i]) continue;
            var mirrorAxis = new Vector3(i == 0 ? 1 : 0, i == 1 ? 1 : 0, i == 2 ? 1 : 0);
            
            foreach (var pos in positions) {
                var distOnAxis = pos - mirrorAroundPosition;
                distOnAxis.Scale(mirrorAxis);
                var dest = pos - distOnAxis * 2;

                var placementPosition = Vector3Int.FloorToInt(dest);
                toAdd.Add(placementPosition);
            }
            
            // Register all added positions
            foreach (var addedPos in toAdd) {
                positions.Add(addedPos);
            }
            toAdd.Clear();
        }
    }
}