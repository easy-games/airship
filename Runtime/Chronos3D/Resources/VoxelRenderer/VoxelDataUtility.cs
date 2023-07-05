using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoxelWorldStuff;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;

// namespace VoxelWorldStuff
// {
//     //Dynamically allocated strucutre that contains extra information about a voxel
//     public class VoxelData
//     {
//         public Vector3 position;
//         public bool solid;
//         public BlockId blockId;
//         public VoxelBlocks.BlockDefinition blockDefinition;
//     }
//
// }
//
// public partial class VoxelWorld : MonoBehaviour
// {
//        
//     public VoxelWorldStuff.VoxelData GetVoxelData(Vector3 position)
//     {
//         VoxelData voxel = ReadVoxelAt(Vector3Int.FloorToInt(Vector3.positiveInfinity));
//
//         if (voxel == 0)
//         {
//             return null;
//         }
//         VoxelWorldStuff.VoxelData data = new();
//         data.blockId = VoxelWorld.VoxelDataToBlockId(voxel);
//         data.solid = VoxelWorld.VoxelIsSolid(voxel);
//         data.position = position;
//         data.blockDefinition = blocks.GetBlock(data.blockId);
//         
//         return data;
//     }
// }

