using System.Collections.Generic;
using UnityEngine;

public static class PhysicsSetup {
    public static readonly Vector3 defaultGravity = new(0, -9.81f, 0);
    private const int NumberOfCoreLayers = 13;
    private const int GameLayerStartIndex = 17;
    private static List<int> corelayers;
    private static List<int> gameLayers;

    // Setup required settings for Airship that all games need
    public static void Setup() {
        InitLayerCollection();

        // Set the physics mat
        // How the heck do I set this? 
        // UnityEditor.physicsMat??? = AssetDatabase.LoadAllAssetsAtPath("defaultphysicsmat");

        Physics.simulationMode = SimulationMode.FixedUpdate;
        Physics2D.simulationMode = SimulationMode2D.FixedUpdate;

#if UNITY_EDITOR
        //Airship Core Layers
        // 0 is built in Default
        // 1 is built in TransparentFX
        // 2 is built in Ignore Raycast
        PhysicsLayerEditor.SetLayer(3, "Character");
        // 4 is built in Water
        // 5 is built in UI
        PhysicsLayerEditor.SetLayer(6, "WorldUI");
        PhysicsLayerEditor.SetLayer(7, "Viewmodel");
        PhysicsLayerEditor.SetLayer(8, "VisuallyHidden");
        PhysicsLayerEditor.SetLayer(9, "IgnoreCollision");
        PhysicsLayerEditor.SetLayer(10, "AvatarEditor");
        PhysicsLayerEditor.SetLayer(11, "LocalStencilMask");
        PhysicsLayerEditor.SetLayer(12, "StencilMask");

        // Reserved for future use
        for (var i = NumberOfCoreLayers; i < GameLayerStartIndex; i++) {
            PhysicsLayerEditor.SetLayer(i, "");
        }
#endif

        // Create the Physics Matrix
        // Non colliding layers
        IgnoreAllLayers(LayerMask.NameToLayer("Viewmodel"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("IgnoreCollision"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("AvatarEditor"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("TransparentFX"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("Ignore Raycast"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("Water"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("UI"), true);
        IgnoreAllLayers(LayerMask.NameToLayer("WorldUI"), true);
        // Only collide with game layers
        IgnoreAllLayers(LayerMask.NameToLayer("Character"), false);
        IgnoreAllLayers(LayerMask.NameToLayer("LocalStencilMask"), false);
        IgnoreAllLayers(LayerMask.NameToLayer("StencilMask"), false);

        // 3D Matrix
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Default"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("VisuallyHidden"),
            false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Water"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("LocalStencilMask"), LayerMask.NameToLayer("Default"),
            false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("LocalStencilMask"), LayerMask.NameToLayer("VisuallyHidden"),
            false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("LocalStencilMask"), LayerMask.NameToLayer("Water"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("StencilMask"), LayerMask.NameToLayer("Default"), false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("StencilMask"), LayerMask.NameToLayer("VisuallyHidden"),
            false);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("StencilMask"), LayerMask.NameToLayer("Water"), false);

        // 2D Matrix
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Default"), false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("VisuallyHidden"),
            false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Water"), false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("LocalStencilMask"), LayerMask.NameToLayer("Default"),
            false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("LocalStencilMask"),
            LayerMask.NameToLayer("VisuallyHidden"),
            false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("LocalStencilMask"), LayerMask.NameToLayer("Water"),
            false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("StencilMask"), LayerMask.NameToLayer("Default"), false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("StencilMask"), LayerMask.NameToLayer("VisuallyHidden"),
            false);
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("StencilMask"), LayerMask.NameToLayer("Water"), false);
    }


    public static void IgnoreAllLayers(int layer, bool ignoreGameLayers) {
        foreach (var otherLayer in corelayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, true);
            Physics2D.IgnoreLayerCollision(layer, otherLayer, true);
        }

        foreach (var otherLayer in gameLayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, ignoreGameLayers);
            Physics2D.IgnoreLayerCollision(layer, otherLayer, ignoreGameLayers);
        }
    }

    public static void CollideWithAllLayers(int layer, bool collideWithGameLayers) {
        foreach (var otherLayer in corelayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, false);
            Physics2D.IgnoreLayerCollision(layer, otherLayer, false);
        }

        foreach (var otherLayer in gameLayers) {
            Physics.IgnoreLayerCollision(layer, otherLayer, !collideWithGameLayers);
            Physics2D.IgnoreLayerCollision(layer, otherLayer, !collideWithGameLayers);
        }
    }

    private static void InitLayerCollection() {
        // Compile all of the layer indexes we use
        corelayers = new List<int>();
        gameLayers = new List<int>();
        for (var i = 0; i < NumberOfCoreLayers; i++) {
            corelayers.Add(i);
        }

        for (var i = GameLayerStartIndex; i <= 31; i++) {
            gameLayers.Add(i);
        }
    }

    // Reset physics values that users may have changed
    public static void ResetDefaults() {
        InitLayerCollection();

        // PHYSICS SETTINGS
        SetPhysicsSettings();
        SetPhysics2DSettings();

        // PHYSICS MATRIX
        // Make Game Layers Collide With Everything
        var gameId = 0;
        for (var i = GameLayerStartIndex; i <= 31; i++) {
            CollideWithAllLayers(i, true);
#if UNITY_EDITOR
            var name = "GameLayer" + gameId;
            PhysicsLayerEditor.SetLayer(i, name);
#endif
            gameId++;
        }

        // Run setup to make the game layers collide properly with core layers
        Setup();
    }

    private static void SetPhysicsSettings(
        Vector3? gravity = null,
        float bounceThreshold = 2,
        float defaultMaxDepenetrationVelocity = 10,
        float sleepThreshold = 0.005f,
        float defaultContactOffset = 0.01f,
        int defaultSolverIterations = 6,
        int defaultSolverVelocityIterations = 1,
        bool queriesHitBackfaces = false,
        bool queriesHitTriggers = true,
        float fixedDeltaTime = 0.025f) {
        Physics.gravity = gravity ?? new Vector3(0, -9.81f, 0);
        Physics.bounceThreshold = bounceThreshold;
        Physics.defaultMaxDepenetrationVelocity = defaultMaxDepenetrationVelocity;
        Physics.sleepThreshold = sleepThreshold;
        Physics.defaultContactOffset = defaultContactOffset;
        Physics.defaultSolverIterations = defaultSolverIterations;
        Physics.defaultSolverVelocityIterations = defaultSolverVelocityIterations;
        Physics.queriesHitBackfaces = queriesHitBackfaces;
        Physics.queriesHitTriggers = queriesHitTriggers;
        Time.fixedDeltaTime = fixedDeltaTime;
    }

    private static void SetPhysics2DSettings(
        Vector3? gravity = null,
        float bounceThreshold = 1,
        int velocityIterations = 8,
        int positionIterations = 3,
        float maxLinearCorrection = .2f,
        float maxAngularCorrection = 8,
        float maxTranslationSpeed = 100,
        float maxRotationSpeed = 360,
        float baumgarteScale = .2f,
        float baumgarteTOIScale = .75f,
        float timeToSleep = .5f,
        float linearSleepTolerance = .01f,
        float angularSleepTolerance = 2,
        float defaultContactOffset = 0.01f,
        float contactThreshold = 0,
        bool queriesHitTriggers = true,
        bool queriesStartInColliders = true,
        bool callbacksOnDisable = true,
        bool reuseCollisionCallbacks = true,
        bool autoSyncTransforms = false) {
        Physics2D.gravity = gravity ?? new Vector3(0, -9.81f, 0);
        Physics2D.velocityIterations = velocityIterations;
        Physics2D.positionIterations = positionIterations;
        Physics2D.bounceThreshold = bounceThreshold;
        Physics2D.maxLinearCorrection = maxLinearCorrection;
        Physics2D.maxAngularCorrection = maxAngularCorrection;
        Physics2D.maxTranslationSpeed = maxTranslationSpeed;
        Physics2D.maxRotationSpeed = maxRotationSpeed;
        Physics2D.baumgarteScale = baumgarteScale;
        Physics2D.baumgarteTOIScale = baumgarteTOIScale;
        Physics2D.timeToSleep = timeToSleep;
        Physics2D.linearSleepTolerance = linearSleepTolerance;
        Physics2D.angularSleepTolerance = angularSleepTolerance;
        Physics2D.defaultContactOffset = defaultContactOffset;
        Physics2D.contactThreshold = contactThreshold;
        Physics2D.queriesHitTriggers = queriesHitTriggers;
        Physics2D.queriesStartInColliders = queriesStartInColliders;
        Physics2D.callbacksOnDisable = callbacksOnDisable;
        Physics2D.reuseCollisionCallbacks = reuseCollisionCallbacks;
        Physics2D.autoSyncTransforms = autoSyncTransforms;
    }

    public static void SetupFromGameConfig() {
#if AIRSHIP_PLAYER || !UNITY_EDITOR
        // Reset Unity to Airship defaults and GameConfig customizations
        var gameConfig = AssetBridge.Instance.LoadGameConfigAtRuntime();
        if (gameConfig) {
            // Debug.Log("Loading project settings from GameConfig. Physics: " + gameConfig.gravity + " matrix size: " +
            //           gameConfig.physicsMatrix.Length);
            // Setup the Core Layers
            Setup();
            // Load in game specific Layers and Settings
            gameConfig.DeserializeSettings();
        } else {
            // Use default Airship values if we aren't setting up game specific values
            // Debug.Log("No custom GameConfig settings found. Resetting to defaults");
            ResetDefaults();
        }
#endif
    }
}