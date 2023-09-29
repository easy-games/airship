using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelWorldStuff
{
    //Dynamically allocated strucutre that contains extra information about a voxel
    public class VoxelMetaData
    {
        public Vector3 axis = Vector3.up;
        public float angle = 0;
        public int health = 0;
    }

    
}
