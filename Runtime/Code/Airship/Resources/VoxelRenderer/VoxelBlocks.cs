using Assets.Airship.VoxelRenderer;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using System;
using System.Linq;
using System.Globalization;

[LuauAPI]
public class VoxelBlocks
{
    //configuration
    public int maxResolution = 256;
    public int atlasSize = 4096;
    public bool pointFiltering = false;


    public enum CollisionType :int
    {
        None = 0,
        Solid,
        Slope,
    }

    public enum ContextStyle : int
    {
        None,
        GreedyMeshingTiles,
        ContextBlocks,
        QuarterTiles,
    }


    //Greedy meshing 
    public enum TileSizes : int
    {
        TileSize1x1x1=0,
        TileSize2x2x2=1,
        TileSize3x3x3=2,
        TileSize4x4x4=3,
        Max = 4,
    }
    
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
    public enum ContextBlockTypes : int
    {
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


    public enum QuarterBlockTypes : int
    {
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

        MAX = DN+1,
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


    public class LodSet
    {
        public VoxelMeshCopy lod0;
        public VoxelMeshCopy lod1;
        public VoxelMeshCopy lod2;
    }

    public class BlockDefinition
    {
        /// <summary>
        /// The generated world id for this block
        /// </summary>
        [HideFromTS]
        public BlockId blockId { get; set; }

        /// <summary>
        /// A scoped identifier for the given block
        /// </summary>
        public string blockTypeId { get; set;  }

        public string name { get; set; }

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

        public bool prefab = false;

        public float metallic = 0;
        public float roughness = 1;
        public float normalScale = 1;
        public float emissive = 0;
        public float brightness = 1;
                
        public bool solid = true; //Solid means "does the block completely occlude 1x1x1, nothing to do with collision
        public VoxelBlocks.CollisionType collisionType = CollisionType.Solid; 
        
        public bool randomRotation = false;

        public VoxelMeshCopy mesh = null;
        public VoxelMeshCopy meshLod = null;
        
        public Dictionary<int, LodSet> meshTiles = new();
        
        public List<int> meshTileProcessingOrder = new();

        
        public ContextStyle contextStyle = ContextStyle.None;
        public Dictionary<int, VoxelMeshCopy> meshContexts = new();
        

        public bool detail = false;

        public string meshTexturePath = "";
        public string topTexturePath = "";
        public string sideTexturePath = "";
        public string bottomTexturePath = "";

        public Texture2D editorTexture; //Null in release

        public Rect topUvs;
        public Rect bottomUvs;
        public Rect sideUvs;
        public bool doOcclusion = true;

        public string[] materials = new string[6];
        public string meshMaterialName;

        public Color[] averageColor = new Color[3];//x y z

        [CanBeNull]
        public string[] minecraftConversions;
 
        public Rect GetUvsForFace(int i)
        {
            switch (i)
            {
                default: return sideUvs;
                case 1: return sideUvs;
                case 2: return sideUvs;
                case 3: return sideUvs;
                case 4: return topUvs;
                case 5: return bottomUvs;
            }
        }
    }

    /// <summary>
    /// The block counter - this is an internal id repesentation in the voxel blocks
    /// </summary>
    private BlockId blockIdCounter = 0;

    public TexturePacker atlas = new TexturePacker();
    public Dictionary<string, Material> materials = new();

    private Dictionary<string, TexturePacker.TextureSet> temporaryTextures = new();
    
    
    private Dictionary<string, BlockId> blockIdLookup = new();
    public Dictionary<BlockId, BlockDefinition> loadedBlocks = new();

    public string rootAssetPath;
    public List<string> m_bundlePaths = null;
    public BlockDefinition GetBlock(BlockId index)
    {
        loadedBlocks.TryGetValue(index, out BlockDefinition value);
        return value;
    }

    [HideFromTS]
    public bool TryGetBlock(BlockId index, out BlockDefinition blockDefinition) {
        var hasBlock = loadedBlocks.TryGetValue(index, out blockDefinition);
        return hasBlock;
    }

    public BlockDefinition GetBlockDefinitionByStringId(string blockTypeId)
    {
        foreach (var block in this.loadedBlocks)
        {
            if (block.Value.blockTypeId == blockTypeId)
                return block.Value;
        }

        return this.loadedBlocks[0];
    }

    public BlockDefinition GetBlockDefinitionFromIndex(int index) {
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
    ~VoxelBlocks()
    {
        atlas.Dispose();

        //destroy all the materials
        foreach (KeyValuePair<string, Material> pair in materials)
        {
            UnityEngine.Object.Destroy(pair.Value);
        }
    }
    
    public void Load(string[] contentsOfBlockDefines, bool loadTexturesDirectlyFromDisk = false)
    {
        Profiler.BeginSample("VoxelBlocks.Load");
        temporaryTextures.Clear();
        atlas = new TexturePacker();
        
        //Add air
        BlockDefinition airBlock = new BlockDefinition();
        airBlock.solid = false;
        airBlock.collisionType = CollisionType.None;
        airBlock.blockTypeId = "air";
        airBlock.blockId = blockIdCounter++;
        loadedBlocks.Add(airBlock.blockId, airBlock);
        blockIdLookup.Add("air", airBlock.blockId);

        Dictionary<BlockId, BlockDefinition> blocks = new();
        
        foreach (var stringContent in contentsOfBlockDefines) {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(stringContent);

            XmlElement xmlBlocks = xmlDoc["Blocks"];

            rootAssetPath = xmlBlocks?["RootAssetPath"]?.InnerText;
            if (rootAssetPath == null)
            {
                rootAssetPath = "Shared/Resources/VoxelWorld";
            } else
            {
                Debug.Log("Using RootAssetPath \"" + rootAssetPath + "\"");
            }

            var scope = xmlBlocks?["Scope"];
            if (scope == null)
            {
                Debug.LogError($"Cannot load BlockDefines in a document due to missing a <Scope/> tag in the root");
                continue;
            }

            XmlNodeList blockList = xmlDoc.GetElementsByTagName("Block");

            Profiler.BeginSample("XmlParsing");
            foreach (XmlNode blockNode in blockList)
            {
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
                block.roughness = blockNode["Roughness"] != null ? float.Parse(blockNode["Roughness"].InnerText, CultureInfo.InvariantCulture) : 1;
                block.emissive = blockNode["Emissive"] != null ? float.Parse(blockNode["Emissive"].InnerText, CultureInfo.InvariantCulture) : 0;

                block.brightness = blockNode["Brightness"] != null ? float.Parse(blockNode["Brightness"].InnerText, CultureInfo.InvariantCulture) : 1;

                block.solid = blockNode["Solid"] != null ? bool.Parse(blockNode["Solid"].InnerText) : true;
                
                block.meshPath = blockNode["Mesh"] != null ? blockNode["Mesh"].InnerText : null;
                block.meshPathLod = blockNode["MeshLod"] != null ? blockNode["MeshLod"].InnerText : null;
                block.normalScale = blockNode["NormalScale"] != null ? float.Parse(blockNode["NormalScale"].InnerText, CultureInfo.InvariantCulture) : 1;

                block.randomRotation = blockNode["RandomRotation"] != null ? bool.Parse(blockNode["RandomRotation"].InnerText) : true;

                block.detail = blockNode["Detail"] != null ? bool.Parse(blockNode["Detail"].InnerText) : true;

                string collisionString = "Solid";
                if (blockNode["Collision"] == null)
                {
                    if (block.solid == false)
                    {
                        collisionString = "None";
                    }
                }
                else
                {
                    collisionString = blockNode["Collision"].InnerText;
                }

                //Parse collisionString into the matching enum
                switch (collisionString)
                {
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
                
                if (blockNode["Minecraft"] != null)
                {
                    string text = blockNode["Minecraft"].InnerText;
                    string[] split = text.Split(",");
                    block.minecraftConversions = split;
                } else
                {
                    block.minecraftConversions = null;
                }

                if (blockNode["Prefab"] != null && bool.Parse(blockNode["Prefab"].InnerText))
                {
                    block.prefab = true;
                    block.solid = false;
                    block.collisionType = CollisionType.Solid;
                }
 
                string tileBase = blockNode["TileSet"] != null ? blockNode["TileSet"].InnerText : "";

                if (tileBase != "")
                {
                    //Do the Greedymeshing Tiles
                    for (int i = 0; i < (int)TileSizes.Max; i++)
                    {
                        string meshPath = $"{rootAssetPath}/Meshes/" + tileBase + TileSizeNames[i];
                        string meshPathLod1 = $"{rootAssetPath}/Meshes/" + tileBase + TileSizeNames[i] + "_1";
                        string meshPathLod2 = $"{rootAssetPath}/Meshes/" + tileBase + TileSizeNames[i] + "_2";

                        VoxelMeshCopy meshCopy = new VoxelMeshCopy(meshPath);
                        if (meshCopy.triangles == null)
                        {
                            if (i == 0)
                            {
                                Debug.LogWarning("Could not find tile mesh at " + meshPath);
                                //Dont look for any more if the 1x1 is missing
                                break;
                            }
                        }
                        else
                        {
                            LodSet set = new LodSet();
                            set.lod0 = meshCopy;
                            block.meshTiles.Add(i, set);

                            VoxelMeshCopy meshCopyLod1 = new VoxelMeshCopy(meshPathLod1);
                            if (meshCopyLod1.triangles != null)
                            {
                                set.lod1 = meshCopyLod1;
                            }

                            VoxelMeshCopy meshCopyLod2 = new VoxelMeshCopy(meshPathLod2);
                            if (meshCopyLod2.triangles != null)
                            {
                                set.lod2 = meshCopyLod2;
                            }

                            block.contextStyle = ContextStyle.GreedyMeshingTiles;
                        }
                    }


                    //see if its context based
                    for (int i = 0; i < (int)ContextBlockTypes.MAX; i++)
                    {
                        string meshPath = $"{rootAssetPath}/Meshes/" + tileBase + ContextBlockNames[i];

                        VoxelMeshCopy meshCopy = new VoxelMeshCopy(meshPath);
                        if (meshCopy.triangles == null)
                        {
                            if (i == 0) 
                            {
                                //Dont look for any more if the A is missing
                                break;
                            }
                        }
                        else
                        {
                            block.meshContexts.Add(i, meshCopy);
                            block.contextStyle = ContextStyle.ContextBlocks;
                        }
                    }

                    //see if its quarterBlock based
                    for (int i = 0; i < (int)QuarterBlockTypes.MAX; i++)
                    {
                        string meshPath = $"{rootAssetPath}/Meshes/" + tileBase + QuarterBlockNames[i];

                        VoxelMeshCopy meshCopy = new VoxelMeshCopy(meshPath);
                        if (meshCopy.triangles == null)
                        {
                            if (i == 0)
                            {
                                //Dont look for any more if the A is missing
                                break;
                            }
                            else
                            {
                                //Can we flip the upwards one?
                                if (i >= (int)QuarterBlockTypes.DA)
                                {
                                    string upwardsName = QuarterBlockNames[i  - (int)QuarterBlockTypes.DA];
                                    string downMeshPath = $"{rootAssetPath}/Meshes/" + tileBase + upwardsName;
                                    VoxelMeshCopy downMeshCopy = new VoxelMeshCopy(downMeshPath);

                                    if (downMeshCopy.triangles != null)
                                    {
                                        downMeshCopy.FlipVertically();
                                        block.meshContexts.Add(i, downMeshCopy);
                                    }
                                    else
                                    {
                                        //Add a blank
                                        block.meshContexts.Add(i, new VoxelMeshCopy("", false));
                                    }

                                }
                                else
                                {

                                    //Add a blank
                                    block.meshContexts.Add(i, new VoxelMeshCopy("",false));
                                }
                            }
                        }
                        else
                        {
                            block.meshContexts.Add(i, meshCopy);
                            block.contextStyle = ContextStyle.QuarterTiles;
                        }
                    }
                }

                //iterate through the Tilesizes backwards
                for (int i = (int)TileSizes.Max-1;i>0;i--)
                {
                    bool found = block.meshTiles.TryGetValue(i, out LodSet val);
                    if (found && i > 0)
                    {
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

                if (block.meshPath != null)
                {
                    block.meshPath = rootAssetPath + "/Meshes/" + block.meshPath;
                    block.mesh = new VoxelMeshCopy(block.meshPath);

                    //Texture should be adjacent to the mesh
                    if (block.meshTexture != "")
                    {
                        string pathWithoutFilename = block.meshPath.Substring(0, block.meshPath.LastIndexOf('/'));
                        block.meshTexturePath = Path.Combine(pathWithoutFilename, block.meshTexture);
                        if (temporaryTextures.ContainsKey(block.meshTexturePath) == false)
                        {
                            var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.meshTexturePath, block.roughness, block.metallic, block.normalScale, block.emissive, block.brightness);
        #if UNITY_EDITOR
                            //prefer the mesh texture..
                            block.editorTexture = tex.diffuse;
        #endif
                        }
                    }
                }

                if (block.meshPathLod != null)
                {
                    block.meshPathLod = $"{rootAssetPath}/Meshes/" + block.meshPathLod;
                    block.meshLod = new VoxelMeshCopy(block.meshPathLod);
                }

                if (block.sideTexture != "")
                {
                    block.sideTexturePath = $"{rootAssetPath}/Textures/" + block.sideTexture;
                    if (temporaryTextures.ContainsKey(block.sideTexturePath) == false)
                    {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.sideTexturePath, block.roughness, block.metallic, block.normalScale, block.emissive, block.brightness);
    #if UNITY_EDITOR
                        //prefer the side texture..
                        block.editorTexture = tex.diffuse;
    #endif
                    }
                }

                if (block.topTexture != "")
                {
                    block.topTexturePath = $"{rootAssetPath}/Textures/" + block.topTexture;
                    if (temporaryTextures.ContainsKey(block.topTexturePath) == false)
                    {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.topTexturePath, block.roughness, block.metallic, block.normalScale, block.emissive, block.brightness);
    #if UNITY_EDITOR
                        if (block.editorTexture == null)
                        {
                            block.editorTexture = tex.diffuse;
                        }
    #endif
                    }
                }

                if (block.bottomTexture != "")
                {
                    block.bottomTexturePath = $"{rootAssetPath}/Textures/" + block.bottomTexture;
                    if (temporaryTextures.ContainsKey(block.bottomTexturePath) == false)
                    {
                        var tex = LoadTexture(loadTexturesDirectlyFromDisk, block.bottomTexturePath, block.roughness, block.metallic, block.normalScale, block.emissive, block.brightness);
    #if UNITY_EDITOR
                        if (block.editorTexture == null)
                        {
                            block.editorTexture = tex.diffuse;
                        }
    #endif
                    }
                }

                loadedBlocks[block.blockId] = block;
            }
        }

        Profiler.EndSample();
        Debug.Log("Loaded " + blocks.Count + " blocks");

        //Create atlas
        int numMips = 8;    //We use a restricted number of mipmaps because after that we start spilling into other regions and you get distant shimmers
        int defaultTextureSize = maxResolution;
        int padding = defaultTextureSize / 2;
        atlas.PackTextures(temporaryTextures, padding, atlasSize, atlasSize, numMips, defaultTextureSize);
        temporaryTextures.Clear();

        //create the materials
        Profiler.BeginSample("CreateMaterials");
        foreach (var blockRec in loadedBlocks)
        {
            for (int i = 0; i < 6; i++)
            {
                blockRec.Value.materials[i] = "atlas";
            }
           
            if (blockRec.Value.material != "")
            {
                string matName = blockRec.Value.material;
              
                if (matName != null)
                {
                    materials.TryGetValue(matName, out Material sourceMat);
                    if (sourceMat == null)
                    {
                        sourceMat = AssetBridge.Instance.LoadAssetInternal<Material>($"{rootAssetPath}/Materials/" + matName + ".mat", true);
                        //See if sourceMat.EXPLICIT_MAPS_ON is false
                        if (sourceMat.IsKeywordEnabled("EXPLICIT_MAPS_ON") == false)
                        {
                           sourceMat.SetTexture("_MainTex", atlas.diffuse);
                           sourceMat.SetTexture("_NormalTex", atlas.normals);
                        }
                        
                        materials[matName] = sourceMat;
                    }
                    if (sourceMat != null)
                    {
                        blockRec.Value.materials[0] = matName;
                        blockRec.Value.materials[1] = matName;
                        blockRec.Value.materials[2] = matName;
                        blockRec.Value.materials[3] = matName;
                        blockRec.Value.materials[4] = matName;
                        blockRec.Value.materials[5] = matName;
                    }

                    if (sourceMat != null && blockRec.Value.mesh != null)
                    {
                        blockRec.Value.mesh.meshMaterial = sourceMat;
                        blockRec.Value.mesh.meshMaterialName = sourceMat.name;
                    }
                    if (sourceMat != null && blockRec.Value.meshLod != null)
                    {
                        blockRec.Value.meshLod.meshMaterial = sourceMat;
                        blockRec.Value.meshLod.meshMaterialName = sourceMat.name;
                    }
                }
            }

            if (blockRec.Value.topMaterial != "")
            {
                string matName = blockRec.Value.topMaterial;

                if (matName != null)
                {
                    materials.TryGetValue(matName, out Material sourceMat);
                    if (sourceMat == null)
                    {
                        sourceMat = AssetBridge.Instance.LoadAssetInternal<Material>($"{rootAssetPath}/Materials/" + matName + ".mat", true);
                        //See if sourceMat.EXPLICIT_MAPS is false
                        if (sourceMat.IsKeywordEnabled("EXPLICIT_MAPS_ON") == false)
                        {
                           sourceMat.SetTexture("_MainTex", atlas.diffuse);
                           sourceMat.SetTexture("_NormalTex", atlas.normals);
                        }
                        materials[matName] = sourceMat;
                    }
                    if (sourceMat != null)
                    {
                        //The top mat
                        blockRec.Value.materials[4] = matName;
                    }
                }
            }

            if (blockRec.Value.sideMaterial != "")
            {
                string matName = blockRec.Value.sideMaterial;

                if (matName != null)
                {
                    materials.TryGetValue(matName, out Material sourceMat);
                    if (sourceMat == null)
                    {
                        sourceMat = AssetBridge.Instance.LoadAssetInternal<Material>($"{rootAssetPath}/Materials/" + matName + ".mat", true);
                        //See if sourceMat.EXPLICIT_MAPS is false
                        if (sourceMat.IsKeywordEnabled("EXPLICIT_MAPS_ON") == false)
                        {
                            sourceMat.SetTexture("_MainTex", atlas.diffuse);
                            sourceMat.SetTexture("_NormalTex", atlas.normals);
                        }
                        materials[matName] = sourceMat;
                    }
                    if (sourceMat != null)
                    {
                        //All the sides
                        blockRec.Value.materials[0] = matName;
                        blockRec.Value.materials[1] = matName;
                        blockRec.Value.materials[2] = matName;
                        blockRec.Value.materials[3] = matName;
                    }
                }
            }

            if (blockRec.Value.bottomMaterial != "")
            {
                string matName = blockRec.Value.bottomMaterial;

                if (matName != null)
                {
                    materials.TryGetValue(matName, out Material sourceMat);
                    if (sourceMat == null)
                    {
                        sourceMat = AssetBridge.Instance.LoadAssetInternal<Material>($"{rootAssetPath}/Materials/" + matName + ".mat", true);
                        //See if sourceMat.EXPLICIT_MAPS is false
                        if (sourceMat.IsKeywordEnabled("EXPLICIT_MAPS_ON") == false)
                        {
                            sourceMat.SetTexture("_MainTex", atlas.diffuse);
                            sourceMat.SetTexture("_NormalTex", atlas.normals);
                        }
                        materials[matName] = sourceMat;
                    }
                    if (sourceMat != null)
                    {
                        //Bottom Material
                        blockRec.Value.materials[5] = matName;
                    }
                }
            }


            if (blockRec.Value.meshTexture != "")
            {
                blockRec.Value.meshMaterialName = "atlas";
 
            }
            else
            {
                //MeshCopy has already loaded its material
            }

        }
        Profiler.EndSample();

        //fullPBR, needs two materials, one for opaque and one for transparencies
        Material atlasMaterial;
        atlasMaterial = new Material(Shader.Find("Airship/WorldShaderPBR"));
        atlasMaterial.SetTexture("_MainTex", atlas.diffuse);
        atlasMaterial.SetTexture("_NormalTex", atlas.normals);

        //Set appropriate settings for the atlas  (vertex light will get selected if its part of the voxel system)
        //Set the properties too so they dont come undone on reload
        atlasMaterial.DisableKeyword("EXPLICIT_MAPS_ON");
        atlasMaterial.SetFloat("EXPLICIT_MAPS", 0);

        atlasMaterial.DisableKeyword("SLIDER_OVERRIDE_ON");
        atlasMaterial.SetFloat("SLIDER_OVERRIDE", 0);
        
        if (pointFiltering == true)
        {
            atlasMaterial.EnableKeyword("POINT_FILTER_ON");
            atlasMaterial.SetFloat("POINT_FILTER", 1);
        }
        else
        {
            atlasMaterial.DisableKeyword("POINT_FILTER_ON");
            atlasMaterial.SetFloat("POINT_FILTER", 0);
        }

        atlasMaterial.EnableKeyword("EMISSIVE_ON");
        atlasMaterial.SetFloat("EMISSIVE", 1);
        
        materials["atlas"] = atlasMaterial;

        //Finalize uvs etc
        foreach (var blockRec in loadedBlocks)
        {
            if (blockRec.Value.sideTexturePath != "")
            {
                blockRec.Value.sideUvs = atlas.GetUVs(blockRec.Value.sideTexturePath);
                blockRec.Value.averageColor[0] = atlas.GetColor(blockRec.Value.sideTexturePath);
                blockRec.Value.averageColor[1] = blockRec.Value.averageColor[0];
                blockRec.Value.averageColor[2] = blockRec.Value.averageColor[0];
            }
            else
            {
                blockRec.Value.sideUvs = new Rect(0, 0, 0, 0);
            }

            if (blockRec.Value.topTexturePath != "")
            {
                blockRec.Value.topUvs = atlas.GetUVs(blockRec.Value.topTexturePath);
                blockRec.Value.averageColor[1] = atlas.GetColor(blockRec.Value.topTexturePath);
                //Debug.Log("TopColor: " + blockRec.Value.name + " Color: " + blockRec.Value.averageColor[1]);
            }
            else
            {
                blockRec.Value.topUvs = blockRec.Value.sideUvs;
            }

            if (blockRec.Value.bottomTexturePath != "")
            {
                blockRec.Value.bottomUvs = atlas.GetUVs(blockRec.Value.bottomTexturePath);
            }
            else
            {
                blockRec.Value.bottomUvs = blockRec.Value.topUvs;
            }

            if (blockRec.Value.meshTexturePath != "")
            {
                blockRec.Value.mesh.AdjustUVs(atlas.GetUVs(blockRec.Value.meshTexturePath));

            }
        }
        Profiler.EndSample();
    }

    //Fix a voxel value up with its solid mask bit
    public VoxelData AddSolidMaskToVoxelValue(VoxelData voxelValue)
    {
        BlockId blockid = VoxelWorld.VoxelDataToBlockId(voxelValue);
        BlockDefinition block = GetBlock(blockid);

        if (block == null)
        {
            return voxelValue;
        }
        //Set bit 0x8000 based on wether block.solid is true
        if (block.solid)
        {
            return (VoxelData)(voxelValue | 0x8000);
        }
        else
        {
            //Return it with that bit masked off
            return (VoxelData)(voxelValue & 0x7FFF);
        }
    }

    static readonly VoxelData BlockBitMask = 0x0FFF;
    
    public VoxelData UpdateVoxelBlockId(VoxelData voxelValue, BlockId blockId)
    {
        return (VoxelData)((voxelValue & (~BlockBitMask)) | blockId);
    }

    private string ResolveAssetPath(string path)
    {
        if (m_bundlePaths == null)
        {
            string[] gameRootPaths = AssetBridge.Instance.GetAllGameRootPaths();
            
            string rootPath = Application.dataPath;
            string assetsFolder = "/Assets";
            if (rootPath.EndsWith(assetsFolder))
            {
                rootPath = rootPath.Substring(0, rootPath.Length - assetsFolder.Length);
            }

            m_bundlePaths = new() {Path.Combine(rootPath, "Bundles")};
            
            foreach (string gameRoot in gameRootPaths)
            {
                m_bundlePaths.Add(Path.Combine(rootPath, "Bundles"));
            }
        }
       
        //check each one for our path
        foreach (var bundlePath in m_bundlePaths)
        {
            string checkPath = Path.Combine(bundlePath, path);
            if (File.Exists(checkPath)) 
            {
                return checkPath;
            }
        }

        return path;
    }

    private Texture2D LoadTextureInternal(bool loadTextureDirectlyFromDisk, string path)
    {
        if (loadTextureDirectlyFromDisk == false)
        {
            return AssetBridge.Instance.LoadAssetInternal<Texture2D>(path, false);
        }
        else
        {
            //Do a direct file read of this thing
            Debug.Log("resolving path " + path);
            string newPath = ResolveAssetPath(path);
            Texture2D tex = TextureLoaderUtil.TextureLoader.LoadTexture(newPath);

            if (tex == null)
            {
                return null;
            }

            //Convert the texture to Linear space
            Texture2D linearTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
            linearTex.SetPixels(tex.GetPixels());
            linearTex.Apply();

            return linearTex;
        }
    }
    private TexturePacker.TextureSet LoadTexture(bool loadTexturesDirectlyFromDisk, string path, float roughness, float metallic, float normalScale, float emissive, float brightness)
    {
        Texture2D texture = LoadTextureInternal(loadTexturesDirectlyFromDisk, path + ".png");
        Texture2D texture_n = LoadTextureInternal(loadTexturesDirectlyFromDisk, path + "_n.png");
        Texture2D texture_r = LoadTextureInternal(loadTexturesDirectlyFromDisk, path + "_r.png");
        Texture2D texture_m = LoadTextureInternal(loadTexturesDirectlyFromDisk, path + "_m.png");
        Texture2D texture_e = LoadTextureInternal(loadTexturesDirectlyFromDisk, path + "_e.png");

        if (texture == null)
        {
            Debug.LogError("Failed to load texture: " + path);
            return null;
        }

        TexturePacker.TextureSet res = new(texture, texture_n, texture_r, texture_m, texture_e, roughness, metallic, normalScale, emissive, brightness);
        temporaryTextures.Add(path, res);
        return res;
    }

    internal CollisionType GetCollisionType(VoxelData blockId)
    {
        BlockDefinition block = GetBlock(blockId);
        if (block == null)
        {
            return CollisionType.None;
        }

        return block.collisionType;
    }
}
