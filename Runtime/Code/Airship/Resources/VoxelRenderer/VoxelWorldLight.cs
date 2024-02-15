using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

using VoxelWorldStuff;

public partial class VoxelWorld : Singleton<VoxelWorld>
{
   
    public void AddChunk(Vector3Int key, Chunk chunk)
    {
        chunks.Add(key, chunk);
        chunk.SetGeometryDirty(true);
    }
    
    public static Color SampleSphericalHarmonics(float3[] shMap, Vector3 unitVector)
    {
        const float c1 = 0.429043f;
        const float c2 = 0.511664f;
        const float c3 = 0.743125f;
        const float c4 = 0.886227f;
        const float c5 = 0.247708f;
        float3 f = (c1 * shMap[8] * (unitVector.x * unitVector.x - unitVector.y * unitVector.y) +
            c3 * shMap[6] * unitVector.z * unitVector.z +
            c4 * shMap[0] -
            c5 * shMap[6] +
            2.0f * c1 * shMap[4] * unitVector.x * unitVector.y +
            2.0f * c1 * shMap[7] * unitVector.x * unitVector.z +
            2.0f * c1 * shMap[5] * unitVector.y * unitVector.z +
            2.0f * c2 * shMap[3] * unitVector.x +
            2.0f * c2 * shMap[1] * unitVector.y +
            2.0f * c2 * shMap[2] * unitVector.z
            );
        return new Color(f.x, f.y, f.z);
    }
}
