using System;
using System.Globalization;
using System.Linq;
using Assets.Airship.VoxelRenderer;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using UnityEngine.Tilemaps;

[LuauAPI]
public class VoxelBlocks : MonoBehaviour {

    public enum CollisionType : int {
        None = 0,
        Solid,
        Slope,
    }

    public enum ContextStyle : int {
        Block,
        Prefab,
        GreedyMeshingTiles,
        PipeBlocks,
        QuarterBlocks,
        StaticMesh,
    }

    //Greedy meshing 
    public enum TileSizes : int {
        TileSize1x1x1 = 0,
        TileSize2x2x2 = 1,
        TileSize3x3x3 = 2,
        TileSize4x4x4 = 3,
        Max = 4,
    }
    static public int[] allTileSizes = new int[] { 1, 2, 3, 4 };

    public static Dictionary<int, Vector3> meshTileOffsets = new Dictionary<int, Vector3>()
    {
        { (int)TileSizes.TileSize1x1x1,Vector3.zero },
        { (int)TileSizes.TileSize2x2x2,(new Vector3(2,2,2)/2.0f) + new Vector3(-0.5f,-0.5f,-0.5f) },
        { (int)TileSizes.TileSize3x3x3,(new Vector3(3,3,3)/2.0f) + new Vector3(-0.5f,-0.5f,-0.5f) },
        { (int)TileSizes.TileSize4x4x4,(new Vector3(4,4,4)/2.0f) + new Vector3(-0.5f,-0.5f,-0.5f) },
    };
    public static Dictionary<int, Vector3Int> meshTileSizes = new()
    {
        { (int)TileSizes.TileSize1x1x1, new Vector3Int(1,1,1) },
        { (int)TileSizes.TileSize2x2x2, new Vector3Int(2,2,2) },
        { (int)TileSizes.TileSize3x3x3, new Vector3Int(3,3,3) },
        { (int)TileSizes.TileSize4x4x4, new Vector3Int(4,4,4) },
    };

    //Make a string table for these
    public static string[] TileSizeNames = new string[]
    {
        "1x1x1",
        "2x2x2",
        "3x3x3",
        "4x4x4",
    };


    //Context Block replacement (edges and pipes)
    public enum PipeBlockTypes : int {
        A = 0,
        B = 1,
        B1 = 2,
        B2A = 3,
        B2B = 4,
        B3 = 5,
        B4 = 6,
        C = 7,
        D = 8,
        E = 9,
        F = 10,
        G = 11,
        MAX = 12,
    }

    public static string[] ContextBlockNames = new string[]
    {
        "A",
        "B",
        "B1",
        "B2A",
        "B2B",
        "B3",
        "B4",
        "C",
        "D",
        "E",
        "F",
        "G",
    };

    public enum QuarterBlockTypes : int {
        UA = 0,  //Front 1
        UB,      //Front 2
        UC,      //Top
        UD,      //Vertical round edge
        UE,      //Horizontal round edge 1
        UF,      //Horizontal round edge 2
        UG,      //Full corner
        UH,      //Top Internal Corner
        UI,      //Vertical internal corner 1
        UJ,      //Vertical internal corner 2
        UK,      //Tri patch 
        UL,      //Square connectors Vert
        UM,      //Square connectors Horizontal 1   
        UN,      //Square connectors Horizontal 2
        DA,
        DB,
        DC,
        DD,
        DE,
        DF,
        DG,
        DH,
        DI,
        DJ,
        DK,
        DL,
        DM,
        DN,

        MAX = DN + 1,
    }
    public static string[] QuarterBlockNames = new string[]
    {
        "UA",
        "UB",
        "UC",
        "UD",
        "UE",
        "UF",
        "UG",
        "UH",
        "UI",
        "UJ",
        "UK",
        "UL",
        "UM",
        "UN",
        "DA",
        "DB",
        "DC",
        "DD",
        "DE",
        "DF",
        "DG",
        "DH",
        "DI",
        "DJ",
        "DK",
        "DL",
        "DM",
        "DN",
    };

    public static QuarterBlockTypes[] QuarterBlockSubstitutions = new QuarterBlockTypes[]
    {
        QuarterBlockTypes.MAX, //ua
        QuarterBlockTypes.UA,  //UB can be flipped UA
        QuarterBlockTypes.MAX, //uc
        QuarterBlockTypes.MAX, //ud
        QuarterBlockTypes.MAX, //ue
        QuarterBlockTypes.UE,  //UF can be flipped UE
        QuarterBlockTypes.MAX, //ug
        QuarterBlockTypes.MAX, //UH
        QuarterBlockTypes.MAX, //UI
        QuarterBlockTypes.UI,  //UJ can be flipped UI
        QuarterBlockTypes.MAX, //UK
        QuarterBlockTypes.MAX, //UL
        QuarterBlockTypes.MAX, //UM
        QuarterBlockTypes.UM,  //UN
        QuarterBlockTypes.MAX, //UA
        QuarterBlockTypes.DA,  //UB can be flipped UA
        QuarterBlockTypes.MAX, //uc
        QuarterBlockTypes.MAX, //ud
        QuarterBlockTypes.MAX, //ue
        QuarterBlockTypes.DE,  //UF can be flipped UE
        QuarterBlockTypes.MAX, //ug
        QuarterBlockTypes.MAX, //UH
        QuarterBlockTypes.MAX, //UI
        QuarterBlockTypes.DI,  //UJ can be flipped UI
        QuarterBlockTypes.MAX, //UK
        QuarterBlockTypes.MAX, //UL
        QuarterBlockTypes.MAX, //UM
        QuarterBlockTypes.DM,  //UN
    };

    public class LodSet {
        public VoxelMeshCopy lod0;
        public VoxelMeshCopy lod1;
        public VoxelMeshCopy lod2;
    }

    //The runtime version of VoxelBlockDefinition, after everything is loaded in
    public class BlockDefinition {
        /// <summary>
        /// The generated world id for this block
        /// </summary>
        [HideFromTS]
        public BlockId blockId { get; set; }

        /// <summary>
        /// A scoped identifier for the given block eg  @Easy/Default:Wood
        /// </summary>
        /// //Todo: Rename this, maybe blockScopeId?
        public string blockTypeId { get; set; }

        /// <summary>
        ///  The properties of this block, as defined in the block definition file
        /// </summary>
        [HideFromTS]
        public VoxelBlockDefinition definition = null;
 
        //Detail == has a mesh like a bed
        public bool detail = false;
        public bool doOcclusion = true;

        public LodSet mesh = null;

        public LodSet[] meshTiles = new LodSet[4];

        public List<int> meshTileProcessingOrder = new();
        
        public VoxelMeshCopy[] meshContexts = new VoxelMeshCopy[(int)QuarterBlockTypes.MAX];
        
        public Texture2D editorTexture; //Null in release

        public Rect topUvs;
        public Rect bottomUvs;
        public Rect sideUvs;
     

        public Material[] materials = new Material[6];
        public Material meshMaterial;
        //public string meshMaterialName;
        
        [CanBeNull]
        public string[] minecraftConversions;

        public Rect GetUvsForFace(int i) {
            switch (i) {
                default: return sideUvs;
                case 1: return sideUvs;
                case 2: return sideUvs;
                case 3: return sideUvs;
                case 4: return topUvs;
                case 5: return bottomUvs;
            }
        }
    }


    //Configuration
    [SerializeField]
    public int maxResolution = 256;
    [SerializeField]
    public int atlasSize = 4096;
    [SerializeField]
    public bool pointFiltering = false;

    //RunTime data
    [NonSerialized] private BlockId blockIdCounter = 0;

    [NonSerialized][HideInInspector] public Material atlasMaterial;
    [NonSerialized] public TexturePacker atlas = new TexturePacker();
    //[NonSerialized] public Dictionary<string, Material> materials = new();

    [NonSerialized] private Dictionary<int, TexturePacker.TextureSet> temporaryTextures = new();

    [NonSerialized] private Dictionary<string, BlockId> blockIdLookup = new();
    [NonSerialized] public Dictionary<BlockId, BlockDefinition> loadedBlocks = new();

    [NonSerialized] public string rootAssetPath;
    [NonSerialized] public List<string> m_bundlePaths = null;

    [SerializeField] public List<VoxelBlockDefinionList> blockDefinionLists = new();

    public BlockDefinition GetBlock(BlockId index) {
        loadedBlocks.TryGetValue(index, out BlockDefinition value);
        return value;
    }

    [HideFromTS]
    public bool TryGetBlock(BlockId index, out BlockDefinition blockDefinition) {
        var hasBlock = loadedBlocks.TryGetValue(index, out blockDefinition);
        return hasBlock;
    }
    
    public BlockDefinition GetBlockDefinitionByStringId(string blockTypeId) {
        foreach (var block in this.loadedBlocks) {
            if (block.Value.blockTypeId == blockTypeId)
                return block.Value;
        }

        return null;
    }

    public BlockDefinition GetBlockDefinitionFromBlockId(int index) {
        return GetBlock((ushort)index);
    }

    /// <summary>
    /// Perform a lookup of the BlockId from the string id of a block
    /// </summary>
    /// <param name="stringId">The string id of the block</param>
    /// <returns>The block id</returns>
    public BlockId GetBlockIdFromStringId(string stringId) {
        var hasMatchingBlockId = this.blockIdLookup.TryGetValue(stringId, out var blockId);
        if (hasMatchingBlockId) {
            return blockId;
        }
        else {
            Debug.LogWarning($"Block of id '{stringId}' was not defined in this world");
            return 0; // AKA: Air
        }
    }

    /// <summary>
    /// Perform a search for a BlockId by partially matching the name.
    /// This is largely meant as a prototyping tool if you quickly need some grass or stone or something
    /// </summary>
    /// <param name="string">Any string that mostly matches the block name eg: "GRASS"</param>
    /// <returns>The block id</returns>
    public BlockId SearchForBlockIdByString(string stringId) {

        foreach (var block in this.loadedBlocks) {
            if (block.Value.blockTypeId.Contains(stringId, StringComparison.OrdinalIgnoreCase)) {
                return block.Value.blockId;
            }
        }

        Debug.LogWarning($"Block of id '{stringId}' was not defined in this world");
        return 0; // AKA: Air
    }


    /// <summary>
    /// Get the string id of a block from the voxel block id
    /// </summary>
    /// <param name="blockVoxelId">The voxel block id</param>
    /// <returns>The string id of this voxel block</returns>
    public string GetStringIdFromBlockId(BlockId blockVoxelId) {
        var block = TryGetBlock(blockVoxelId, out var blockDefinition);
        if (block) {
            return blockDefinition.blockTypeId;
        }
        else {
            return null;
        }
    }

    //Destructor
    ~VoxelBlocks() {
        atlas.Dispose();
    }

    private void Clear() {
        blockIdCounter = 0;
        atlasMaterial = null;
        atlas = new TexturePacker();
 
        temporaryTextures = new();

        blockIdLookup = new();
        loadedBlocks = new();
    }

    private LodSet BuildLodSet(VoxelBlockDefinition.MeshSet meshSet) {
        LodSet result = new();

        if (meshSet.mesh_LOD0) {
            result.lod0 = new VoxelMeshCopy(meshSet.mesh_LOD0);
        }
        if (meshSet.mesh_LOD1) {
            result.lod1 = new VoxelMeshCopy(meshSet.mesh_LOD1);
        }
        if (meshSet.mesh_LOD2) {
            result.lod2 = new VoxelMeshCopy(meshSet.mesh_LOD2);
        }

        return result;
    }

    private void ParseGreedyTilingMeshBlock(BlockDefinition block) {
        if (block.definition.contextStyle != ContextStyle.GreedyMeshingTiles) {
            return;
        }

        if (block.definition.meshTile1x1x1.mesh_LOD0 == null) {
            return;
        }
        
        block.meshTiles[(int)TileSizes.TileSize1x1x1] = BuildLodSet(block.definition.meshTile1x1x1);
        block.meshTiles[(int)TileSizes.TileSize2x2x2] = BuildLodSet(block.definition.meshTile2x2x2);
        block.meshTiles[(int)TileSizes.TileSize3x3x3] = BuildLodSet(block.definition.meshTile3x3x3);
        block.meshTiles[(int)TileSizes.TileSize4x4x4] = BuildLodSet(block.definition.meshTile4x4x4);

        for (int i = (int)TileSizes.Max - 1; i > 0; i--) {
            
            if (block.meshTiles[i].lod0 != null && i > 0) {
                block.meshTileProcessingOrder.Add(i);
            }
        }
    }
    private void ParseStaticMeshBlock(BlockDefinition block) {
        if (block.definition.contextStyle != ContextStyle.StaticMesh) {
            return;
        }
        
        if (block.definition.staticMeshLOD0 == null) {
            return;
        }

        block.mesh = new();
        
        block.mesh.lod0 = new VoxelMeshCopy(block.definition.staticMeshLOD0);
        
        if (block.definition.staticMeshLOD1 != null){
            block.mesh.lod1 = new VoxelMeshCopy(block.definition.staticMeshLOD1);
        }
        
        if (block.definition.staticMeshLOD2 != null){
            block.mesh.lod2 = new VoxelMeshCopy(block.definition.staticMeshLOD2);
        }
        
        //Apply the material to this
        if (block.meshMaterial != null) {
            block.mesh.lod0.ApplyMaterial(block.meshMaterial);
            block.mesh.lod1.ApplyMaterial(block.meshMaterial);
            block.mesh.lod2.ApplyMaterial(block.meshMaterial);
        }
                
    }

    private void ParseQuarterBlock(BlockDefinition block) {

        if (block.definition.quarterBlockMesh == null || block.definition.contextStyle != ContextStyle.QuarterBlocks) {
            return;
        }

        block.meshMaterial = block.definition.meshMaterial;

        VoxelQuarterBlockMeshDefinition source = block.definition.quarterBlockMesh;

        //Parse the quarterblocks
        for (int i = 0; i < (int)QuarterBlockTypes.MAX; i++) {

            string name = QuarterBlockNames[i];
            GameObject obj = source.GetQuarterBlockMesh(name);
                        
            if (i == 0 && obj == null) {
                break;
            }

            VoxelMeshCopy meshToAdd = null;

            if (obj == null) {
             
                //Can we flip an existing one
                if (QuarterBlockSubstitutions[i] != QuarterBlockTypes.MAX) {
                    VoxelMeshCopy meshSrc = block.meshContexts[ (int)QuarterBlockSubstitutions[i]];
                    if (meshSrc != null && meshSrc.surfaces != null) {
                        VoxelMeshCopy meshCopySub = new VoxelMeshCopy(meshSrc);
                        meshCopySub.FlipHorizontally();
                        meshToAdd = meshCopySub;
                         
                    }
                }

                if (meshToAdd == null) {
                    
                    if (i >= (int)QuarterBlockTypes.DA) {

                        //Can we flip the upwards one?
                        VoxelMeshCopy meshSrc = block.meshContexts[i - (int)QuarterBlockTypes.DA];

                        if (meshSrc != null && meshSrc.surfaces != null) {
                            VoxelMeshCopy meshCopySub = new VoxelMeshCopy(meshSrc);
                            meshCopySub.FlipVertically();
                            meshToAdd =  meshCopySub;
                        
                        }
                    }
                }
             
            }
            else {
                //grab the first mesh out of obj
                meshToAdd = new VoxelMeshCopy(obj);
                 
            }
            
            if (meshToAdd == null) {
                meshToAdd = new VoxelMeshCopy("", false);
            }
          
            block.meshContexts[i] = meshToAdd;
        }

        
        for (int i = 0; i < (int)QuarterBlockTypes.MAX; i++) {
            if (block.meshContexts[i] == null) {
                //Debug.Log("Missing mesh for " + block.blockTypeId + " " + QuarterBlockNames[i]);
            }
        }
    }
    public void Load(bool loadTexturesDirectlyFromDisk = false) {

        //clear everything
        Clear();
                
        //Profiler.BeginSample("VoxelBlocks.Load");
        temporaryTextures.Clear();
        atlas = new TexturePacker();
        
        //Add air
        BlockDefinition airBlock = new BlockDefinition();
        
        airBlock.definition = ScriptableObject.CreateInstance<VoxelBlockDefinition>();
        airBlock.definition.blockName = "Air";
        airBlock.definition.solid = false;
        airBlock.definition.collisionType = CollisionType.None;
        airBlock.blockTypeId = "air";
        airBlock.blockId = blockIdCounter++;

        loadedBlocks.Add(airBlock.blockId, airBlock);
        blockIdLookup.Add("air", airBlock.blockId);

        foreach (VoxelBlockDefinionList voxelDefinitionList in blockDefinionLists) {
            foreach (VoxelBlockDefinition voxelBlockDefinition in voxelDefinitionList.blockDefinitions) {
                var scopedId = $"{voxelDefinitionList.scope}:{voxelBlockDefinition.name}"; // e.g. @Easy/Core:OAK_LOG

                BlockDefinition block = new BlockDefinition();
                block.blockId = blockIdCounter++;
                block.definition = voxelBlockDefinition;
                block.blockTypeId = scopedId;
                

                if (blockIdLookup.ContainsKey(scopedId)) {
                    Debug.LogWarning($"Duplicate Block Id: {scopedId} at index {blockIdCounter}");
                    continue;
                }
                blockIdLookup.Add(scopedId, block.blockId);

                if (block.definition.sideTexture.diffuse != null) {
                    int key = block.definition.sideTexture.diffuse.GetInstanceID();
                    if (temporaryTextures.ContainsKey(key) == false) {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.definition.sideTexture, block.definition.smoothness, block.definition.metallic, block.definition.normalScale, block.definition.emissive, block.definition.brightness);
                    
                    }
                }
                if (block.definition.topTexture.diffuse != null) {
                    int key = block.definition.topTexture.diffuse.GetInstanceID();
                    if (temporaryTextures.ContainsKey(key) == false) {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.definition.topTexture, block.definition.smoothness, block.definition.metallic, block.definition.normalScale, block.definition.emissive, block.definition.brightness);

                    }
                }
                if (block.definition.bottomTexture.diffuse != null) {
                    int key = block.definition.bottomTexture.diffuse.GetInstanceID();
                    if (temporaryTextures.ContainsKey(key) == false) {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.definition.bottomTexture, block.definition.smoothness, block.definition.metallic, block.definition.normalScale, block.definition.emissive, block.definition.brightness);

                    }
                }

                ParseQuarterBlock(block);

                ParseStaticMeshBlock(block);

                ParseGreedyTilingMeshBlock(block);

                loadedBlocks.Add(block.blockId, block);
            }
        }
        
        atlasMaterial = Resources.Load<Material>("VoxelWorldURP");
        
        /*foreach (var stringContent in contentsOfBlockDefines) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(stringContent);

            XmlElement xmlBlocks = xmlDoc["Blocks"];

            rootAssetPath = xmlBlocks?["RootAssetPath"]?.InnerText;
            if (rootAssetPath == null) {
                rootAssetPath = "Shared/Resources/VoxelWorld";
            }
            else {
                // Debug.Log("Using RootAssetPath \"" + rootAssetPath + "\"");
            }

            var scope = xmlBlocks?["Scope"];
            if (scope == null) {
                Debug.LogError($"Cannot load BlockDefines in a document due to missing a <Scope/> tag in the root");
                continue;
            }

            XmlNodeList blockList = xmlDoc.GetElementsByTagName("Block");

            Profiler.BeginSample("XmlParsing");
            foreach (XmlNode blockNode in blockList) {
                var id = blockNode["Id"]?.InnerText;
                if (id == null)
                    throw new MissingFieldException($"Missing field 'Id' for '{blockNode.InnerXml}'");

                var scopedId = $"{scope.InnerText}:{blockNode["Id"].InnerText}"; // e.g. @Easy/Core:OAK_LOG

                BlockDefinition block = new BlockDefinition();
                block.blockId = blockIdCounter++;
                block.name = blockNode["Name"].InnerText;
                block.blockTypeId = scopedId;

                if (blockIdLookup.ContainsKey(scopedId)) {
                    Debug.LogWarning($"Duplicate Block Id: {scopedId} at index {blockIdCounter}");
                    continue;
                }
                blockIdLookup.Add(scopedId, block.blockId);

                block.meshTexture = blockNode["MeshTexture"] != null ? blockNode["MeshTexture"].InnerText : "";
                block.topTexture = blockNode["TopTexture"] != null ? blockNode["TopTexture"].InnerText : "";
                block.topMaterial = blockNode["TopMaterial"] != null ? blockNode["TopMaterial"].InnerText : "";
                block.sideMaterial = blockNode["SideMaterial"] != null ? blockNode["SideMaterial"].InnerText : "";
                block.bottomMaterial = blockNode["BottomMaterial"] != null ? blockNode["BottomMaterial"].InnerText : "";
                block.material = blockNode["Material"] != null ? blockNode["Material"].InnerText : "";

                block.bottomTexture = blockNode["BottomTexture"] != null ? blockNode["BottomTexture"].InnerText : "";

                block.sideTexture = blockNode["SideTexture"] != null ? blockNode["SideTexture"].InnerText : "";

                block.metallic = blockNode["Metallic"] != null ? float.Parse(blockNode["Metallic"].InnerText, CultureInfo.InvariantCulture) : 0;
                block.smoothness = blockNode["Smoothness"] != null ? float.Parse(blockNode["Smoothness"].InnerText, CultureInfo.InvariantCulture) : 0;

                block.emissive = blockNode["Emissive"] != null ? float.Parse(blockNode["Emissive"].InnerText, CultureInfo.InvariantCulture) : 0;

                block.brightness = blockNode["Brightness"] != null ? float.Parse(blockNode["Brightness"].InnerText, CultureInfo.InvariantCulture) : 1;

                block.solid = blockNode["Solid"] != null ? bool.Parse(blockNode["Solid"].InnerText) : true;

                block.meshPath = blockNode["Mesh"] != null ? blockNode["Mesh"].InnerText : null;
                block.meshPathLod = blockNode["MeshLod"] != null ? blockNode["MeshLod"].InnerText : null;
                block.normalScale = blockNode["NormalScale"] != null ? float.Parse(blockNode["NormalScale"].InnerText, CultureInfo.InvariantCulture) : 1;

                block.randomRotation = blockNode["RandomRotation"] != null ? bool.Parse(blockNode["RandomRotation"].InnerText) : true;

                block.detail = blockNode["Detail"] != null ? bool.Parse(blockNode["Detail"].InnerText) : true;

                string collisionString = "Solid";
                if (blockNode["Collision"] == null) {
                    if (block.solid == false) {
                        collisionString = "None";
                    }
                }
                else {
                    collisionString = blockNode["Collision"].InnerText;
                }

                //Parse collisionString into the matching enum
                switch (collisionString) {
                    case "Solid":
                    block.collisionType = CollisionType.Solid;
                    break;
                    case "Slope":
                    block.collisionType = CollisionType.Slope;
                    break;
                    case "None":
                    block.collisionType = CollisionType.None;
                    break;
                    default:
                    Debug.LogWarning($"Unknown collision type: {collisionString}");
                    break;
                }

                if (blockNode["Minecraft"] != null) {
                    string text = blockNode["Minecraft"].InnerText;
                    string[] split = text.Split(",");
                    block.minecraftConversions = split;
                }
                else {
                    block.minecraftConversions = null;
                }

                if (blockNode["Prefab"] != null && bool.Parse(blockNode["Prefab"].InnerText)) {
                    block.prefab = true;
                    block.solid = false;
                    block.collisionType = CollisionType.Solid;
                }

                string tileBase = blockNode["TileSet"] != null ? blockNode["TileSet"].InnerText : "";

                if (tileBase != "") {
                    //Do the Greedymeshing Tiles
                    for (int i = 0; i < (int)TileSizes.Max; i++) {
                        string meshPath = $"{rootAssetPath}/Meshes/" + tileBase + TileSizeNames[i];
                        string meshPathLod1 = $"{rootAssetPath}/Meshes/" + tileBase + TileSizeNames[i] + "_1";
                        string meshPathLod2 = $"{rootAssetPath}/Meshes/" + tileBase + TileSizeNames[i] + "_2";

                        VoxelMeshCopy meshCopy = new VoxelMeshCopy(meshPath);
                        if (meshCopy.surfaces == null) {
                            if (i == 0) {
                                //Debug.LogWarning("Could not find tile mesh at " + meshPath);
                                //Dont look for any more if the 1x1 is missing
                                break;
                            }
                        }
                        else {
                            LodSet set = new LodSet();
                            set.lod0 = meshCopy;
                            block.meshTiles.Add(i, set);

                            VoxelMeshCopy meshCopyLod1 = new VoxelMeshCopy(meshPathLod1);
                            if (meshCopyLod1.surfaces != null) {
                                set.lod1 = meshCopyLod1;
                            }

                            VoxelMeshCopy meshCopyLod2 = new VoxelMeshCopy(meshPathLod2);
                            if (meshCopyLod2.surfaces != null) {
                                set.lod2 = meshCopyLod2;
                            }

                            block.contextStyle = ContextStyle.GreedyMeshingTiles;
                        }
                    }


                    //see if its context based
                    for (int i = 0; i < (int)ContextBlockTypes.MAX; i++) {
                        string meshPath = $"{rootAssetPath}/Meshes/" + tileBase + ContextBlockNames[i];

                        VoxelMeshCopy meshCopy = new VoxelMeshCopy(meshPath);
                        if (meshCopy.surfaces == null) {
                            if (i == 0) {
                                //Dont look for any more if the A is missing
                                break;
                            }
                        }
                        else {
                            block.meshContexts.Add(i, meshCopy);
                            block.contextStyle = ContextStyle.ContextBlocks;
                        }
                    }

                    //see if its quarterBlock based
                    for (int i = 0; i < (int)QuarterBlockTypes.MAX; i++) {
                        string meshPath = $"{rootAssetPath}/Meshes/" + tileBase + QuarterBlockNames[i];

                        VoxelMeshCopy meshCopy = new VoxelMeshCopy(meshPath);
                        if (meshCopy.surfaces == null) {
                            if (i == 0) {
                                //Dont look for any more if the A is missing
                                break;
                            }
                            else {
                                //Can we flip an existing one
                                if (i < (int)QuarterBlockTypes.DA && QuarterBlockSubstitutions[i] != QuarterBlockTypes.MAX) {
                                    block.meshContexts.TryGetValue((int)QuarterBlockSubstitutions[i], out VoxelMeshCopy meshSrc);
                                    if (meshSrc != null && meshSrc.surfaces != null) {
                                        VoxelMeshCopy meshCopySub = new VoxelMeshCopy(meshSrc);
                                        meshCopySub.FlipHorizontally();
                                        block.meshContexts.Add(i, meshCopySub);
                                        continue;
                                    }
                                    else {
                                        //Add a blank
                                        block.meshContexts.Add(i, new VoxelMeshCopy("", false));
                                        continue;
                                    }
                                }

                                //Can we flip the upwards one?
                                if (i >= (int)QuarterBlockTypes.DA) {
                                    block.meshContexts.TryGetValue(i - (int)QuarterBlockTypes.DA, out VoxelMeshCopy meshSrc);

                                    if (meshSrc != null && meshSrc.surfaces != null) {
                                        VoxelMeshCopy meshCopySub = new VoxelMeshCopy(meshSrc);
                                        meshCopySub.FlipVertically();
                                        block.meshContexts.Add(i, meshCopySub);
                                        continue;
                                    }
                                    else {
                                        //Add a blank
                                        block.meshContexts.Add(i, new VoxelMeshCopy("", false));
                                        continue;
                                    }

                                }
                                else {

                                    //Add a blank
                                    block.meshContexts.Add(i, new VoxelMeshCopy("", false));
                                }
                            }
                        }
                        else {
                            block.meshContexts.Add(i, meshCopy);
                            block.contextStyle = ContextStyle.QuarterTiles;
                        }
                    }
                    //Overwrite the materials
                    if (block.material != null) {
                        //Force load the material here for this purpose
                        string matName = block.material;

                        if (!string.IsNullOrEmpty(matName)) {
                            Material sourceMat = AssetBridge.Instance.LoadAssetInternal<Material>($"{rootAssetPath}/Materials/" + matName + ".mat", true);
                            if (sourceMat) {
                                foreach (KeyValuePair<int, VoxelMeshCopy> kvp in block.meshContexts) {
                                    if (kvp.Value.surfaces != null) {
                                        foreach (VoxelMeshCopy.Surface surf in kvp.Value.surfaces) {
                                            surf.meshMaterial = sourceMat;
                                            surf.meshMaterialName = block.material;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                //iterate through the Tilesizes backwards
                for (int i = (int)TileSizes.Max - 1; i > 0; i--) {
                    bool found = block.meshTiles.TryGetValue(i, out LodSet val);
                    if (found && i > 0) {
                        block.meshTileProcessingOrder.Add(i);
                    }
                }

                ////Check for duplicate
                //if (blocks.ContainsKey(block.index))
                //{
                //    Debug.LogError("Duplicate block index: " + block.index + " for block: " + block.name + " Existing block name is" + blocks[block.index].name);
                //    continue;
                //}

                blocks.Add(block.blockId, block);

                if (block.meshPath != null) {
                    block.meshPath = rootAssetPath + "/Meshes/" + block.meshPath;
                    block.mesh = new VoxelMeshCopy(block.meshPath);

                    //Texture should be adjacent to the mesh
                    if (block.meshTexture != "") {
                        string pathWithoutFilename = block.meshPath.Substring(0, block.meshPath.LastIndexOf('/'));
                        block.meshTexturePath = Path.Combine(pathWithoutFilename, block.meshTexture);
                        if (temporaryTextures.ContainsKey(block.meshTexturePath) == false) {
                            var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.meshTexturePath, block.smoothness, block.metallic, block.normalScale, block.emissive, block.brightness);
#if UNITY_EDITOR
                            //prefer the mesh texture..
                            if (tex != null) {
                                block.editorTexture = tex.diffuse;
                            }
#endif
                        }
                    }
                }

                if (block.meshPathLod != null) {
                    block.meshPathLod = $"{rootAssetPath}/Meshes/" + block.meshPathLod;
                    block.meshLod = new VoxelMeshCopy(block.meshPathLod);
                }

                if (block.sideTexture != "") {
                    block.sideTexturePath = $"{rootAssetPath}/Textures/" + block.sideTexture;
                    if (temporaryTextures.ContainsKey(block.sideTexturePath) == false) {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.sideTexturePath, block.smoothness, block.metallic, block.normalScale, block.emissive, block.brightness);
#if UNITY_EDITOR
                        //prefer the side texture..
                        if (tex != null) {
                            block.editorTexture = tex.diffuse;
                        }
#endif
                    }
                }

                if (block.topTexture != "") {
                    block.topTexturePath = $"{rootAssetPath}/Textures/" + block.topTexture;
                    if (temporaryTextures.ContainsKey(block.topTexturePath) == false) {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.topTexturePath, block.smoothness, block.metallic, block.normalScale, block.emissive, block.brightness);
#if UNITY_EDITOR
                        if (block.editorTexture == null && tex != null) {
                            block.editorTexture = tex.diffuse;
                        }
#endif
                    }
                }

                if (block.bottomTexture != "") {
                    block.bottomTexturePath = $"{rootAssetPath}/Textures/" + block.bottomTexture;
                    if (temporaryTextures.ContainsKey(block.bottomTexturePath) == false) {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.bottomTexturePath, block.smoothness, block.metallic, block.normalScale, block.emissive, block.brightness);
#if UNITY_EDITOR
                        if (block.editorTexture == null && tex != null) {
                            block.editorTexture = tex.diffuse;
                        }
#endif
                    }
                }

                loadedBlocks[block.blockId] = block;
            }
        } */

        //Profiler.EndSample();
        // Debug.Log("Loaded " + blocks.Count + " blocks");

        //Create atlas
        int numMips = 8;    //We use a restricted number of mipmaps because after that we start spilling into other regions and you get distant shimmers
        int defaultTextureSize = maxResolution;
        int padding = defaultTextureSize / 2;
        atlas.PackTextures(temporaryTextures, padding, atlasSize, atlasSize, numMips, defaultTextureSize);
        temporaryTextures.Clear();

        atlasMaterial.SetTexture("_MainTex", atlas.diffuse);
        atlasMaterial.SetTexture("_SpecialTex", atlas.normals);

        //create the materials
        Profiler.BeginSample("CreateMaterials");
        foreach (var blockRec in loadedBlocks) {
            for (int i = 0; i < 6; i++) {
                blockRec.Value.materials[i] = atlasMaterial;
            }

            Material fullMaterial = blockRec.Value.definition.topTexture.material;
            if (fullMaterial != null) {

                blockRec.Value.materials[0] = fullMaterial;
                blockRec.Value.materials[1] = fullMaterial;
                blockRec.Value.materials[2] = fullMaterial;
                blockRec.Value.materials[3] = fullMaterial;
                blockRec.Value.materials[4] = fullMaterial;
                blockRec.Value.materials[5] = fullMaterial;
            }

            if (blockRec.Value.definition.topTexture.material != null) {
                blockRec.Value.materials[4] = blockRec.Value.definition.topTexture.material;
            }
            
            if (blockRec.Value.definition.sideTexture.material != null) {
                blockRec.Value.materials[0] = blockRec.Value.definition.sideTexture.material;
                blockRec.Value.materials[1] = blockRec.Value.definition.sideTexture.material;
                blockRec.Value.materials[2] = blockRec.Value.definition.sideTexture.material;
                blockRec.Value.materials[3] = blockRec.Value.definition.sideTexture.material;
            }

            if (blockRec.Value.definition.bottomTexture.material != null) {
                blockRec.Value.materials[5] = blockRec.Value.definition.bottomTexture.material;
            }

            /*
            if (blockRec.Value.meshTexture != "") {
                blockRec.Value.meshMaterialName = "atlas";

            }
            else {
                //MeshCopy has already loaded its material
            }*/

        }
        Profiler.EndSample();

        //fullPBR, needs two materials, one for opaque and one for transparencies


        //Set appropriate settings for the atlas  (vertex light will get selected if its part of the voxel system)
        //Set the properties too so they dont come undone on reload
        /*
        atlasMaterial.DisableKeyword("EXPLICIT_MAPS_ON");
        atlasMaterial.SetFloat("EXPLICIT_MAPS", 0);

        atlasMaterial.DisableKeyword("SLIDER_OVERRIDE_ON");
        atlasMaterial.SetFloat("SLIDER_OVERRIDE", 0);

        if (pointFiltering == true) {
            atlasMaterial.EnableKeyword("POINT_FILTER_ON");
            atlasMaterial.SetFloat("POINT_FILTER", 1);
        }
        else {
            atlasMaterial.DisableKeyword("POINT_FILTER_ON");
            atlasMaterial.SetFloat("POINT_FILTER", 0);
        }

        atlasMaterial.EnableKeyword("EXTRA_FEATURES_ON");
        atlasMaterial.SetFloat("EXTRA_FEATURES", 1);
        */
        //materials["atlas"] = atlasMaterial;

        //Finalize uvs etc
        foreach (var blockRec in loadedBlocks) {

            if (blockRec.Value.definition.topTexture.diffuse != null) {
                blockRec.Value.topUvs = atlas.GetUVs(blockRec.Value.definition.topTexture.diffuse);
            }
            else {
                blockRec.Value.topUvs = new Rect(0, 0, 0, 0);
            }

            if (blockRec.Value.definition.sideTexture.diffuse != null) {
                blockRec.Value.sideUvs = atlas.GetUVs(blockRec.Value.definition.sideTexture.diffuse);
            }
            else {
                blockRec.Value.sideUvs = blockRec.Value.topUvs; //Use other Uvs if none available
            }

            if (blockRec.Value.definition.bottomTexture.diffuse != null) {
                blockRec.Value.bottomUvs = atlas.GetUVs(blockRec.Value.definition.bottomTexture.diffuse);
            }
            else {
                blockRec.Value.bottomUvs = blockRec.Value.topUvs; //use top uvs if bottom uvs not available
            }

            /*
            if (blockRec.Value.meshTexturePath != "") {
                blockRec.Value.mesh.AdjustUVs(atlas.GetUVs(blockRec.Value.meshTexturePath));

            }*/
        }
        Profiler.EndSample();
    }

    //Fix a voxel value up with its solid mask bit
    public VoxelData AddSolidMaskToVoxelValue(VoxelData voxelValue) {
        BlockId blockid = VoxelWorld.VoxelDataToBlockId(voxelValue);
        BlockDefinition block = GetBlock(blockid);

        if (block == null) {
            return voxelValue;
        }
        //Set bit 0x8000 based on wether block.solid is true
        if (block.definition.solid) {
            return (VoxelData)(voxelValue | 0x8000);
        }
        else {
            //Return it with that bit masked off
            return (VoxelData)(voxelValue & 0x7FFF);
        }
    }

    static readonly VoxelData BlockBitMask = 0x0FFF;

    public VoxelData UpdateVoxelBlockId(VoxelData voxelValue, BlockId blockId) {
        return (VoxelData)((voxelValue & (~BlockBitMask)) | blockId);
    }

    private string ResolveAssetPath(string path) {
        if (m_bundlePaths == null) {
            string[] gameRootPaths = AssetBridge.Instance.GetAllGameRootPaths();

            string rootPath = Application.dataPath;
            string assetsFolder = "/Assets";
            if (rootPath.EndsWith(assetsFolder)) {
                rootPath = rootPath.Substring(0, rootPath.Length - assetsFolder.Length);
            }

            m_bundlePaths = new() { Path.Combine(rootPath, "AirshipPackages") };

            foreach (string gameRoot in gameRootPaths) {
                m_bundlePaths.Add(Path.Combine(rootPath, "AirshipPackages"));
            }
        }

        //check each one for our path
        foreach (var bundlePath in m_bundlePaths) {
            string checkPath = Path.Combine(bundlePath, path);
            if (File.Exists(checkPath)) {
                return checkPath;
            }
        }

        return path;
    }

    private Texture2D LoadTextureInternal(bool loadTextureDirectlyFromDisk, string path) {
        if (loadTextureDirectlyFromDisk == false) {
            return AssetBridge.Instance.LoadAssetInternal<Texture2D>(path, false);
        }
        else {
            //Do a direct file read of this thing
            Debug.Log("resolving path " + path);
            string newPath = ResolveAssetPath(path);
            Texture2D tex = TextureLoaderUtil.TextureLoader.LoadTexture(newPath);

            if (tex == null) {
                return null;
            }

            //Convert the texture to Linear space
            Texture2D linearTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
            linearTex.SetPixels(tex.GetPixels());
            linearTex.Apply();

            return linearTex;
        }
    }
    private TexturePacker.TextureSet LoadTexture(bool loadTexturesDirectlyFromDisk, VoxelBlockDefinition.TextureSet textures, float smoothness, float metallic, float normalScale, float emissive, float brightness) {
       
        TexturePacker.TextureSet res = new(textures.diffuse, textures.normal, textures.smooth, textures.metallic, textures.emissive, smoothness, metallic, normalScale, emissive, brightness);
        temporaryTextures.Add(textures.diffuse.GetInstanceID(), res);
        return res;
    }

    internal CollisionType GetCollisionType(VoxelData blockId) {
        BlockDefinition block = GetBlock(blockId);
        if (block == null) {
            return CollisionType.None;
        }

        return block.definition.collisionType;
    }

    public void Reload(bool useTexturesDirectlyFromDisk = false) {

        Load(useTexturesDirectlyFromDisk);
        //Todo!
        //this.blocks = new VoxelBlocks();
        //this.blocks.Load(this.GetBlockDefinesContents());
    }

    //When the game doesnt have this block definiton, we want to create a temporary one just so we dont wreck their data just for loading this file
    internal BlockDefinition CreateTemporaryBlockDefinition(string name) {
       
        BlockDefinition blockDef = new BlockDefinition();

        blockDef.definition = ScriptableObject.CreateInstance<VoxelBlockDefinition>();
        blockDef.definition.blockName = name;
        blockDef.definition.solid = true;
        blockDef.definition.collisionType = CollisionType.Solid;
        blockDef.blockTypeId = name;
        blockDef.blockId = blockIdCounter++;

        loadedBlocks.Add(blockDef.blockId, blockDef);
        blockIdLookup.Add(name, blockDef.blockId);

        for (int i = 0; i < 6; i++) {
            blockDef.materials[i] = atlasMaterial;
        }

        return blockDef;
    }
}

#if UNITY_EDITOR
//Create an editor for VoxelBlock
[CustomEditor(typeof(VoxelBlocks))]
public class VoxelBlockEditor : Editor {
    public override void OnInspectorGUI() {
        DrawDefaultInspector();
        
        VoxelBlocks voxelBlocks = (VoxelBlocks)target;
        if (GUILayout.Button("Reload")) {
            voxelBlocks.Reload();
        }
        
    }
}



#endif