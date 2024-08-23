using System.Collections.Generic;
using UnityEditor;

#if UNITY_EDITOR
using UnityEngine;
#endif

public static class PhysicsSetup
{
    private const int NumberOfCoreLayers= 12;
    private const int GameLayerStartIndex= 17;
    private static List<int> corelayers;
    private static List<int> gameLayers;

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
        for (int i = NumberOfCoreLayers; i < GameLayerStartIndex; i++) {
            PhysicsLayerEditor.SetLayer(i, "");
        }
        
        //Compile all of the layer indexes we use
        corelayers = new List<int>();
        gameLayers = new List<int>();
        for (int i = 0; i < NumberOfCoreLayers; i++) {
            corelayers.Add(i);
        }
        for (int i = GameLayerStartIndex; i <= 31; i++) {
            gameLayers.Add(i);
        }

        //Airship Game Layers
        int gameId = 0;
        for (int i = GameLayerStartIndex; i <= 31; i++) {        
            CollideWithAllLayers(i, true);
            string name = "GameLayer"+gameId;
            gameId++;
        }
        
        //Create the Physics Matrix
            //Non colliding layers
        IgnoreAllLayers(LayerMask.NameToLayer("Viewmodel"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("IgnoreCollision"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("AvatarEditor"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("TransparentFX"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("Ignore Raycast"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("Water"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("UI"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("WorldUI"), true);
            //Only collide with game layers
        IgnoreAllLayers(LayerMask.NameToLayer("Character"), false);
        IgnoreAllLayers(LayerMask.NameToLayer("VoxelWorld"), false);

        //Character
            //Collides with
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Default"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("VisuallyHidden"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("VoxelWorld"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Water"), false);
        
        //VoxelWorld
            //Collides with
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("VoxelWorld"), LayerMask.NameToLayer("Default"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("VoxelWorld"), LayerMask.NameToLayer("VisuallyHidden"), false);
#endif
    }

    public static void IgnoreAllLayers(int layer, bool ignoreGameLayers) {
#if UNITY_EDITOR
        foreach (var otherLayer in corelayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, true);
        }
        foreach (var otherLayer in gameLayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, ignoreGameLayers);
        }
#endif
    }

    public static void CollideWithAllLayers(int layer, bool collideWithGameLayers) {
#if UNITY_EDITOR
        foreach (var otherLayer in corelayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, false);
        }
        foreach (var otherLayer in gameLayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, !collideWithGameLayers);
        }
#endif
    }
}