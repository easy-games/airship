using System.Collections.Generic;
using UnityEditor;

#if UNITY_EDITOR
using UnityEngine;
#endif

public static class PhysicsSetup
{
    private static List<int> layers;

    public static void Setup(GameConfig config) {
#if UNITY_EDITOR
        //Set the physics mat
        //How the heck do I set this? 
        //UnityEditor.physicsMat??? = AssetDatabase.LoadAllAssetsAtPath("defaultphysicsmat");

        //Airship Core Layers
        PhysicsLayerEditor.SetLayer(3, "Character");
        PhysicsLayerEditor.SetLayer(6, "WorldUI");
        PhysicsLayerEditor.SetLayer(7, "Viewmodel");
        PhysicsLayerEditor.SetLayer(8, "VisuallyHidden");
        PhysicsLayerEditor.SetLayer(9, "IgnoreCollision");
        PhysicsLayerEditor.SetLayer(10, "AvatarEditor");
        PhysicsLayerEditor.SetLayer(11, "VoxelWorld");

        Physics.simulationMode = SimulationMode.FixedUpdate;

        //Reserved for future use
        for (int i = 12; i <= 16; i++) {
            PhysicsLayerEditor.SetLayer(i, "");
        }

        //Airship Game Layers
        // int gameId = 0;
        // for (int i = 17; i <= 31; i++) {
        //     if (PhysicsLayerEditor.LayerExists(LayerMask.LayerToName(i))) {
        //         gameId++;
        //         continue;
        //     }
        //
        //     string name = "GameLayer"+gameId;
        //     PhysicsLayerEditor.SetLayer(i, name);
        //     gameId++;
        // }
        
        //Compile all of the layer indexes we use
        layers = new List<int>();
        for (int i = 0; i <= 10; i++) {
            layers.Add(i);
        }
        for (int i = 17; i <= 31; i++) {
            layers.Add(i);
        }

        
        //Create the Physics Matrix
            //Non colliding layers
        IgnoreAllLayers(LayerMask.NameToLayer("Viewmodel"));
        IgnoreAllLayers(LayerMask.NameToLayer("IgnoreCollision"));
        IgnoreAllLayers(LayerMask.NameToLayer("AvatarEditor"));
        IgnoreAllLayers(LayerMask.NameToLayer("TransparentFX"));
        IgnoreAllLayers(LayerMask.NameToLayer("Ignore Raycast"));
        IgnoreAllLayers(LayerMask.NameToLayer("Water"));
        IgnoreAllLayers(LayerMask.NameToLayer("UI"));
        IgnoreAllLayers(LayerMask.NameToLayer("WorldUI"));

            //Character
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Character"), true);

            //Voxel World
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("VoxelWorld"), LayerMask.NameToLayer("VoxelWorld"), true);
#endif
    }

    public static void IgnoreAllLayers(int layer)
    {
        foreach (var otherLayer in layers)
        {
#if UNITY_EDITOR
            Physics.IgnoreLayerCollision(layer, otherLayer, true);
#endif
        }
    }
}