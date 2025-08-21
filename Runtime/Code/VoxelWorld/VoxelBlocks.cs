using System;
using System.Globalization;
using System.Linq;
using Assets.Airship.VoxelRenderer;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

using VoxelData = System.UInt16;
using BlockId = System.UInt16;
using UnityEngine.Tilemaps;
using VoxelWorldStuff;

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

        public List<VoxelMeshCopy[]> meshContexts = new();
        public int[] meshContextsRandomTable = new int[0];

        public Texture2D editorTexture; //Null in release

        public Rect topUvs;
        public Rect bottomUvs;
        public Rect sideUvs;


        /// <summary>
        /// Read only! To write to materials use SetMaterial
        /// </summary>
        public Material[] materials = new Material[6];
        public int[] materialInstanceIds = new int[6];
        
        private Material _meshMaterial;

        public Material meshMaterial {
            get => _meshMaterial;
            set {
                _meshMaterial = value;
                meshMaterialInstanceId = value.GetInstanceID();
                MeshProcessor.materialIdToMaterial[meshMaterialInstanceId] = _meshMaterial;
            }
        }
        public int meshMaterialInstanceId;
        
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
        
        [HideFromTS]
        public void SetMaterial(int index, Material material) {
            this.materials[index] = material;
            
            var instanceId = material.GetInstanceID();
            this.materialInstanceIds[index] = instanceId;

            // Register material
            MeshProcessor.materialIdToMaterial[instanceId] = material;
        }
    }


    //Configuration
    [SerializeField]
    public int maxResolution = 256;
    [SerializeField]
    public int atlasWidthTextures = 15;
    
    /// <summary>
    /// TODO: Currently not serialized as this setting likely will not work without supporting _SpecialTex in our
    /// atlas shader. If this is a requested feature we can support it easily (probably best to make a new atlas
    /// shader that takes in normals).
    /// </summary>
    [Tooltip("If true we will pack texture normals into the texture atlas. This will double the RenderTexture memory cost. " +
             "If you don't have custom normals leave this off.")]
    [NonSerialized] private bool packNormalsIntoAtlas = false;
    private int atlasPaddingPx = 32;

    public int atlasSize {
        get => atlasWidthTextures * (maxResolution);
    }
    [SerializeField]
    public bool pointFiltering = false;

    //RunTime data
    [NonSerialized] private BlockId blockIdCounter = 0;

    [NonSerialized][HideInInspector] public Material atlasMaterial;
    [NonSerialized] public TexturePacker atlas = new TexturePacker();
    //[NonSerialized] public Dictionary<string, Material> materials = new();

    [NonSerialized] private Dictionary<int, TexturePacker.TextureSet> temporaryTextures = new();

    [NonSerialized] private Dictionary<string, BlockId> blockIdLookup = new();
    [NonSerialized] public List<BlockDefinition> loadedBlocks = new();

    [NonSerialized] public string rootAssetPath;
    [NonSerialized] public List<string> m_bundlePaths = null;

    [FormerlySerializedAs("blockDefinionLists")]
    [SerializeField] public List<VoxelBlockDefinitionList> blockDefinitionLists = new();

    /// <summary>
    /// When application is playing we only want to load VoxelBlocks once. This prevents multiple VoxelWorlds
    /// from loading the same VoxelBlocks and regenerating the same atlas.
    ///
    /// Note: If we do have a reason to want reloading VoxelBlocks during play in the future we could
    /// change this to only run the texture packing process once.
    /// </summary>
    private bool hasBegunLoading = false;
    private TaskCompletionSource<bool> loadedTask = new TaskCompletionSource<bool>(false);

    public BlockDefinition GetBlock(BlockId index) {
        var ix = VoxelWorld.VoxelDataToBlockId(index); //safety
        return loadedBlocks[ix];
    }

    [HideFromTS]
    public bool TryGetBlock(BlockId index, out BlockDefinition blockDefinition) {
        if (index >= loadedBlocks.Count) {
            blockDefinition = null;
            return false;
        }
        blockDefinition = loadedBlocks[index];
        return true;
    }
    
    public BlockDefinition GetBlockDefinitionByStringId(string blockTypeId) {
        foreach (var block in this.loadedBlocks) {
            if (block.blockTypeId == blockTypeId)
                return block;
        }

        return null;
    }

    public BlockDefinition GetBlockDefinitionFromBlockId(int index) {
        index = VoxelWorld.VoxelDataToBlockId(index);
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
    public BlockId 
        SearchForBlockIdByString(string stringId) {

        foreach (var block in this.loadedBlocks) {
            if (block.blockTypeId.Contains(stringId, StringComparison.OrdinalIgnoreCase)) {
                return block.blockId;
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

        blockVoxelId = VoxelWorld.VoxelDataToBlockId(blockVoxelId); //anti foot gun
        
        var block = TryGetBlock(blockVoxelId, out var blockDefinition);
        if (block) {
            return blockDefinition.blockTypeId;
        }
        else {
            return null;
        }
    }

    private void Clear() {
        blockIdCounter = 0;
        atlasMaterial = null;
        
        atlas.Dispose();
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
    
    public async Task WaitForLoaded() {
        if (loadedTask.Task.IsCompleted) return;
        await loadedTask.Task;
    }

    private void ParseQuarterBlock(BlockDefinition block) {

        if (block.definition.contextStyle != ContextStyle.QuarterBlocks) {
            return;
        }

        if (block.definition.quarterBlockMeshes == null) {
            Debug.LogWarning($"[VoxelWorld] {block.blockTypeId} is a QuarterBlock but doesn't have any QuarterBlockMeshes.");
            return;
        } 
        foreach (var quarter in block.definition.quarterBlockMeshes) {
            ParseQuarterBlockItem(block, quarter);
        }
        int resolution = 100;
        int probabilityCount = block.definition.quarterBlockMeshes.Length * resolution;
        block.meshContextsRandomTable = new int[probabilityCount];

        //Horrible, but very fast to access later
        int index = 0;
        for (int i = 0; i < block.definition.quarterBlockMeshes.Length; i++) {
            for (int j = 0; j < block.definition.quarterBlockMeshes[i].probablity * resolution; j++) {
                block.meshContextsRandomTable[index] = i;
                index++;
            }
        }
    }
    public static VoxelMeshCopy[] GetRandomMeshContext(BlockDefinition block) {
        int randomIndex = UnityEngine.Random.Range(0, block.meshContextsRandomTable.Length);
        int meshIndex = block.meshContextsRandomTable[randomIndex];
        return block.meshContexts[meshIndex];
    }
    public static VoxelMeshCopy[] GetRandomMeshContext(BlockDefinition block, Vector3 origin, int offset) {

        // Use a more varied hash function
        int hash = (int)(((int)origin.x * 73856093) ^ ((int)origin.y * 19349663) ^ ((int)origin.z * 83492791) ^ (offset * 1548585));
        
        // Ensure the result is within the range of the random table length
        int randomIndex = Mathf.Abs(hash) % block.meshContextsRandomTable.Length;
 
        int meshIndex = block.meshContextsRandomTable[randomIndex];
        return block.meshContexts[meshIndex];
    }



    private void ParseQuarterBlockItem(BlockDefinition block, VoxelQuarterBlockMeshDefinition source) {

        if (source == null || block.definition.contextStyle != ContextStyle.QuarterBlocks) {
            return;
        }

        block.meshMaterial = block.definition.meshMaterial;
        block.meshMaterialInstanceId = block.meshMaterial.GetInstanceID();
        var meshList = new VoxelMeshCopy[(int)QuarterBlockTypes.MAX];
        block.meshContexts.Add(meshList); //Random variation
        //VoxelQuarterBlockMeshDefinition source = block.definition.quarterBlockMesh;

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
                    VoxelMeshCopy meshSrc = meshList[ (int)QuarterBlockSubstitutions[i]];
                    if (meshSrc != null && meshSrc.surfaces != null) {
                        VoxelMeshCopy meshCopySub = new VoxelMeshCopy(meshSrc);
                        meshCopySub.FlipHorizontally();
                        meshToAdd = meshCopySub;
                         
                    }
                }

                if (meshToAdd == null) {
                    
                    if (i >= (int)QuarterBlockTypes.DA) {

                        //Can we flip the upwards one?
                        VoxelMeshCopy meshSrc = meshList[i - (int)QuarterBlockTypes.DA];

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

            meshList[i] = meshToAdd;
        }
    }

    private void OnDestroy() {
        atlas?.Dispose();
    }

    public void Load(bool loadTexturesDirectlyFromDisk = false) {
        //clear everything
        Clear();
                
        //Profiler.BeginSample("VoxelBlocks.Load");
        temporaryTextures.Clear();
        
        //Add air
        BlockDefinition airBlock = new BlockDefinition();
        
        airBlock.definition = ScriptableObject.CreateInstance<VoxelBlockDefinition>();
        airBlock.definition.blockName = "Air";
        airBlock.definition.solid = false;
        airBlock.definition.collisionType = CollisionType.None;
        airBlock.blockTypeId = "air";
        airBlock.blockId = blockIdCounter++;

        loadedBlocks.Add(airBlock);
        blockIdLookup.Add("air", airBlock.blockId);

        foreach (VoxelBlockDefinitionList voxelDefinitionList in blockDefinitionLists) {
            foreach (VoxelBlockDefinition voxelBlockDefinition in voxelDefinitionList.blockDefinitions) {
                if (voxelBlockDefinition == null) continue;
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

                loadedBlocks.Add(block);
            }
        }
        
#if !UNITY_SERVER
        SetupAtlas();
#endif
        // Profiler.EndSample();
        loadedTask.TrySetResult(true);
    }

    private void SetupAtlas() {
        atlas = new TexturePacker();
        atlasMaterial = Resources.Load<Material>("VoxelWorldMatURP");

        //Create atlas
        int numMips = 8;    //We use a restricted number of mipmaps because after that we start spilling into other regions and you get distant shimmers
        int defaultTextureSize = maxResolution - atlasPaddingPx * 2;
        
        atlas.PackTextures(temporaryTextures, atlasPaddingPx, atlasSize, atlasSize, numMips, defaultTextureSize, packNormalsIntoAtlas);
        temporaryTextures.Clear();

        atlasMaterial.SetTexture("_MainTex", atlas.diffuse);
        if (packNormalsIntoAtlas) atlasMaterial.SetTexture("_SpecialTex", atlas.normals);
        atlasMaterial.SetFloat("_AtlasWidthTextures", atlasWidthTextures);
        atlasMaterial.SetFloat("_PaddingOverWidth", atlasPaddingPx / (float) atlasSize);
        atlasMaterial.SetFloat("_TexWidthOverWidth", (maxResolution - atlasPaddingPx * 2) / (float) atlasSize);

        //create the materials
        Profiler.BeginSample("CreateMaterials");
        foreach (var blockRec in loadedBlocks) {
            for (int i = 0; i < 6; i++) {
                blockRec.SetMaterial(i, atlasMaterial);
            }

            Material fullMaterial = blockRec.definition.topTexture.material;
            if (fullMaterial != null) {
                for (var i = 0; i < 6; i++) {
                    blockRec.SetMaterial(i, fullMaterial);
                }
            }

            if (blockRec.definition.topTexture.material != null) {
                blockRec.SetMaterial(4, blockRec.definition.topTexture.material);
            }
            
            if (blockRec.definition.sideTexture.material != null) {
                for (var i = 0; i < 4; i++) {
                    blockRec.SetMaterial(i, blockRec.definition.sideTexture.material);
                }
            }

            if (blockRec.definition.bottomTexture.material != null) {
                blockRec.SetMaterial(5, blockRec.definition.bottomTexture.material);
            }

        }
        Profiler.EndSample();
        
        //Finalize uvs etc
        foreach (var blockRec in loadedBlocks) {
            if (blockRec.definition.topTexture.diffuse != null) {
                blockRec.topUvs = atlas.GetUVs(blockRec.definition.topTexture.diffuse);
            }
            else {
                blockRec.topUvs = new Rect(0, 0, 0, 0);
            }

            if (blockRec.definition.sideTexture.diffuse != null) {
                blockRec.sideUvs = atlas.GetUVs(blockRec.definition.sideTexture.diffuse);
            }
            else {
                blockRec.sideUvs = blockRec.topUvs; //Use other Uvs if none available
            }

            if (blockRec.definition.bottomTexture.diffuse != null) {
                blockRec.bottomUvs = atlas.GetUVs(blockRec.definition.bottomTexture.diffuse);
            }
            else {
                blockRec.bottomUvs = blockRec.topUvs; //use top uvs if bottom uvs not available
            }
        }
    }

    //Fix a voxel value up with its solid mask bit
    public VoxelData AddSolidMaskToVoxelValue(VoxelData voxelValue) {
        BlockId blockid = VoxelWorld.VoxelDataToBlockId(voxelValue);
        BlockDefinition block = GetBlock(blockid);

        if (block == null) {
            return voxelValue;
        }
        //Set bit 0x8000 based on wether block.solid is true
        if (block.definition.solid && !block.definition.halfBlock) {
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
        // Only load VoxelBlocks once while application is running
        if (Application.isPlaying && hasBegunLoading) return;
        hasBegunLoading = true;

        Load(useTexturesDirectlyFromDisk);
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

        loadedBlocks.Add(blockDef);
        blockIdLookup.Add(name, blockDef.blockId);

#if !UNITY_SERVER
        for (int i = 0; i < 6; i++) {
            if (atlasMaterial) blockDef.SetMaterial(i, atlasMaterial);
        }
#endif

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