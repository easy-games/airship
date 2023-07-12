using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Object = System.Object;

public class MinecraftMapLoader : MonoBehaviour {
    [SerializeField]
    public TextAsset mapJson;

    private static string[] ignoreIds = new[]
    {
        "175:2", // small bush 
        "12000:2", // large bush
        "31:1", "37", // purple flower
        "175:10",
    };

    private static ushort fallbackId = 11;

    public void LoadMap() {
        Debug.Log("Loading minecraft map...");
        
        // Clear out existing world.
        var world = GetVoxelWorld();
        world.chunks.Clear();

        string contents = world.blockDefines.text;
        world.blocks = new VoxelBlocks();
        world.blocks.Load(contents);
        
        var mapData = JsonConvert.DeserializeObject<MinecraftMapSchema>(mapJson.text);
        List<MinecraftBlock> minecraftBlocks = new List<MinecraftBlock>();

        // Cache the conversion keys
        Dictionary<string, ushort> minecraftIdToBlockId = new();
        foreach (var pair in world.blocks.loadedBlocks)
        {
            if (pair.Value.minecraftConversions != null)
            {
                foreach (var id in pair.Value.minecraftConversions)
                {
                    if (!minecraftIdToBlockId.TryAdd(id, pair.Key))
                    {
                        Debug.LogError("Minecraft conversion entry duplicate: blockId=" + pair.Key + ", minecraftId=" + id);
                        return;
                    }
                }
            }
        }

        // Blocks are partitioned into size 8 chunks.
        for (var i = 0; i < mapData.blocks.Count; i += 8) {
            var x = mapData.blocks[i];
            var y = mapData.blocks[i + 1];
            var z = mapData.blocks[i + 2];
            var blockId = mapData.blocks[i + 3];
            var blockData = mapData.blocks[i + 4];
            var widthX = mapData.blocks[i + 5];
            var widthY = mapData.blocks[i + 6];
            var widthZ = mapData.blocks[i + 7];
            
            /* Unwrap greedily meshed region. */
            for (var xx = x; xx < x + widthX; xx++) {
                for (var yy = y; yy < y + widthY; yy++) {
                    for (var zz = z; zz < z + widthZ; zz++) {
                        var block = new MinecraftBlock(xx, yy, zz, blockId, blockData);
                        minecraftBlocks.Add(block);
                    }
                }
            }
        }

        // Write each block.
        foreach (var minecraftBlock in minecraftBlocks)
        {
            ushort blockId = fallbackId; // grass

            string key = minecraftBlock.blockId + "";
            if (minecraftBlock.blockData != 0)
            {
                key += ":" + minecraftBlock.blockData;
            }

            if (ignoreIds.Contains(key))
            {
                continue;
            }
            
            if (minecraftIdToBlockId.TryGetValue(key, out ushort value))
            {
                blockId = value;
            } else
            {
                var strippedKey = key.Split(":")[0];
                if (minecraftIdToBlockId.TryGetValue(strippedKey, out ushort value2))
                {
                    blockId = value2;
                } else
                {
                    Debug.Log("Block not found with minecraft id " + key);
                }
            }
            world.WriteVoxelAt(new Vector3(minecraftBlock.x, minecraftBlock.y, minecraftBlock.z), blockId, false);
        }

        // Signs
        foreach (MinecraftSign sign in mapData.signs)
        {
            if (sign.text[0] != "")
            {
                if(world.worldPositionEditorIndicators.TryGetValue(sign.text[0], out var existing))
                {
                    var worldPosition = existing.gameObject.GetComponent<VoxelWorldPositionIndicator>();
                    if (worldPosition.doNotOverwrite)
                    {
                        continue;
                    }
                    world.worldPositionEditorIndicators.Remove(sign.text[0]);
                    DestroyImmediate(existing.gameObject);
                }

                // add world position
                world.AddWorldPosition(new VoxelBinaryFile.WorldPosition(sign.text[0], new Vector3(sign.pos[0] + 0.5f, sign.pos[1], sign.pos[2] + 0.5f), Quaternion.identity));
            }
        }

        world.RegenerateAllMeshes();
        Debug.Log("Finished loading minecraft map!");
    }
    
    private VoxelWorld GetVoxelWorld()
    {
        GameObject go = GameObject.Find("VoxelWorld");
        if (go == null)
        {
            go = new GameObject("VoxelWorld");
            go.AddComponent<VoxelWorld>();
        }
        return go.GetComponent<VoxelWorld>();
    }
}

[Serializable]
public class MinecraftSign
{
    public int[] pos;
    public string[] text;
}

[Serializable]
public class MinecraftMapSchema {
    public Object positionConfig;
    public List<int> blocks;
    public List<MinecraftSign> signs;
}

public class MinecraftBlock {
    public int x;
    public int y;
    public int z;
    public int blockId;
    public int blockData;

    public MinecraftBlock(int x, int y, int z, int blockId, int blockData) {
        this.x = x;
        this.y = y;
        this.z = z;
        this.blockId = blockId;
        this.blockData = blockData;
    }
}