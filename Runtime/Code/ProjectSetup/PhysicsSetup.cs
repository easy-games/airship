using System.Collections.Generic;
using UnityEngine;

public static class PhysicsSetup {
    private const int NumberOfCoreLayers= 12;
    private const int GameLayerStartIndex= 17;
    private static Vector3 defaultGravity = new Vector3(0, -24f, 0);
    private static List<int> corelayers;
    private static List<int> gameLayers;

    //Setup required settings for Airship that all games need
    public static void Setup(GameConfig config) {
        InitLayerCollection();

        //Set the physics mat
        //How the heck do I set this? 
        //UnityEditor.physicsMat??? = AssetDatabase.LoadAllAssetsAtPath("defaultphysicsmat");

        Physics.simulationMode = SimulationMode.FixedUpdate;

#if UNITY_EDITOR
        //Airship Core Layers
        PhysicsLayerEditor.SetLayer(3, "Character");
        PhysicsLayerEditor.SetLayer(6, "WorldUI");
        PhysicsLayerEditor.SetLayer(7, "Viewmodel");
        PhysicsLayerEditor.SetLayer(8, "VisuallyHidden");
        PhysicsLayerEditor.SetLayer(9, "IgnoreCollision");
        PhysicsLayerEditor.SetLayer(10, "AvatarEditor");
        PhysicsLayerEditor.SetLayer(11, "VoxelWorld");

        //Reserved for future use
        for (int i = NumberOfCoreLayers; i < GameLayerStartIndex; i++) {
            PhysicsLayerEditor.SetLayer(i, "");
        }
#endif
        
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
    }
    

    public static void IgnoreAllLayers(int layer, bool ignoreGameLayers) {
        foreach (var otherLayer in corelayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, true);
        }
        foreach (var otherLayer in gameLayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, ignoreGameLayers);
        }
    }

    public static void CollideWithAllLayers(int layer, bool collideWithGameLayers) {
        foreach (var otherLayer in corelayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, false);
        }
        foreach (var otherLayer in gameLayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, !collideWithGameLayers);
        }
    }

    private static void InitLayerCollection(){
        //Compile all of the layer indexes we use
        corelayers = new List<int>();
        gameLayers = new List<int>();
        for (int i = 0; i < NumberOfCoreLayers; i++) {
            corelayers.Add(i);
        }
        for (int i = GameLayerStartIndex; i <= 31; i++) {
            gameLayers.Add(i);
        }
    }

    //Reset physics values that users may have changed
    public static void ResetDefaults(GameConfig config){
        InitLayerCollection();

        Physics.gravity = defaultGravity;

        //Make Game Layers Collide With Everything
        int gameId = 0;
        for (int i = GameLayerStartIndex; i <= 31; i++) {        
            CollideWithAllLayers(i, true);
#if UNITY_EDITOR
            string name = "GameLayer"+gameId;
            PhysicsLayerEditor.SetLayer(i, name);
#endif
            gameId++;
        }

        //Run setup to make the game layers collide properly with core layers
        Setup(config);
    }

    public static void SetupFromGameConfig(){
#if AIRSHIP_PLAYER || !UNITY_EDITOR
		//Reset Unity to Airship defaults and GameConfig customizations
		var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
		if(gameConfig && gameConfig.physicsMatrix != null && this.physicsGravity != null){
				Debug.Log("Loading project settings from GameConfig");
				//Setup the Core Layers
				PhysicsSetup.Setup(this);
				//Load in game specific Layers and Settings
				gameConfig.DeserializeSettings();
		}else{
			//Use default Airship values if we aren't setting up game specific values
			Debug.Log("No custom GameConfig settings found. Reseting to defaults");
			PhysicsSetup.ResetDefaults(this);
		}
#endif
    }
}