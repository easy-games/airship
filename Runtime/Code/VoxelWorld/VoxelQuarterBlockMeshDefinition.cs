using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "VoxelQuarterBlockMeshDefinition", menuName = "Airship/VoxelQuarterBlockMeshDefinition")]
public class VoxelQuarterBlockMeshDefinition : ScriptableObject {
    public GameObject UA;
    public GameObject UB;
    public GameObject UC;
    public GameObject UD;
    public GameObject UE;
    public GameObject UF;
    public GameObject UG;
    public GameObject UH;
    public GameObject UI;
    public GameObject UJ;
    public GameObject UK;
    public GameObject UL;
    public GameObject UM;
    public GameObject UN;
    
    public GameObject DA;
    public GameObject DB;
    public GameObject DC;
    public GameObject DD;
    public GameObject DE;
    public GameObject DF;
    public GameObject DG;
    public GameObject DH;
    public GameObject DI;
    public GameObject DJ;
    public GameObject DK;
    public GameObject DL;
    public GameObject DM;
    public GameObject DN;

    //Accessor to get them by enum
    public GameObject GetQuarterBlockMesh(VoxelBlocks.QuarterBlockTypes block) {
        switch (block) {
            case VoxelBlocks.QuarterBlockTypes.UA:
            return UA;
            case VoxelBlocks.QuarterBlockTypes.UB:
            return UB;
            case VoxelBlocks.QuarterBlockTypes.UC:
            return UC;
            case VoxelBlocks.QuarterBlockTypes.UD:
            return UD;
            case VoxelBlocks.QuarterBlockTypes.UE:
            return UE;
            case VoxelBlocks.QuarterBlockTypes.UF:
            return UF;
            case VoxelBlocks.QuarterBlockTypes.UG:
            return UG;
            case VoxelBlocks.QuarterBlockTypes.UH:
            return UH;
            case VoxelBlocks.QuarterBlockTypes.UI:
            return UI;
            case VoxelBlocks.QuarterBlockTypes.UJ:
            return UJ;
            case VoxelBlocks.QuarterBlockTypes.UK:
            return UK;
            case VoxelBlocks.QuarterBlockTypes.UL:
            return UL;
            case VoxelBlocks.QuarterBlockTypes.UM:
            return UM;
            case VoxelBlocks.QuarterBlockTypes.UN:
            return UN;
            case VoxelBlocks.QuarterBlockTypes.DA:
            return DA;
            case VoxelBlocks.QuarterBlockTypes.DB:
            return DB;
            case VoxelBlocks.QuarterBlockTypes.DC:
            return DC;
            case VoxelBlocks.QuarterBlockTypes.DD:
            return DD;
            case VoxelBlocks.QuarterBlockTypes.DE:
            return DE;
            case VoxelBlocks.QuarterBlockTypes.DF:
            return DF;
            case VoxelBlocks.QuarterBlockTypes.DG:
            return DG;
            case VoxelBlocks.QuarterBlockTypes.DH:
            return DH;
            case VoxelBlocks.QuarterBlockTypes.DI:
            return DI;
            case VoxelBlocks.QuarterBlockTypes.DJ:
            return DJ;
            case VoxelBlocks.QuarterBlockTypes.DK:
            return DK;
            case VoxelBlocks.QuarterBlockTypes.DL:
            return DL;
            case VoxelBlocks.QuarterBlockTypes.DM:
            return DM;
            case VoxelBlocks.QuarterBlockTypes.DN:
            return DN;
            default:
            return null;
        }
    }
       
}