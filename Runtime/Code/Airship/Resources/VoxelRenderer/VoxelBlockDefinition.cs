using UnityEngine;

   
public class VoxelBlockDefinition : ScriptableObject {

    public string blockName = "undefined";
    public string description;
  
    public VoxelBlocks.ContextStyle contextStyle = VoxelBlocks.ContextStyle.None;
    
    public string material { get; set; }    //overwrites all others
    public string topMaterial { get; set; }     //overwrites topTexture
    public string sideMaterial { get; set; }    //overwrites sideTexture
    public string bottomMaterial { get; set; }  //overwrites bottomTexture
    
    public string topTexture { get; set; }
    public string sideTexture { get; set; }
    public string bottomTexture { get; set; }

    public string meshTexture { get; set; }
    public string meshPath { get; set; }
    public string meshPathLod { get; set; }

    public float metallic = 0;
    public float smoothness = 0;
    public float normalScale = 1;
    public float emissive = 0;
    public float brightness = 1;

    public bool solid = true;  //Blocks all rendering behind it eg: stone.  leafs would be false

    public VoxelBlocks.CollisionType collisionType = VoxelBlocks.CollisionType.Solid;

    public bool randomRotation = false; //Object gets flipped on the x or z axis "randomly" (always the same per coordinate)

}

