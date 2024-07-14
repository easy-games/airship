//write a scriptable object that acts as a container and editor for VoxelBlockDefines called VoxelBlockDefinion
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VoxelBlockDefines", menuName = "Airship/VoxelBlockDefines", order = 2)]
public class VoxelBlockDefines : ScriptableObject {

    public List<VoxelBlockDefinition> blockDefinitions = new List<VoxelBlockDefinition>();

}