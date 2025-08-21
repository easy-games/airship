using System;
using System.Collections.Generic;
using System.Linq;
using Code.GameBundle;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Airship/GameConfig", order = 100)]
public class GameConfig : ScriptableObject {
    public string gameId;
#if UNITY_EDITOR
    public SceneAsset startingScene;
#endif
    public Object[] gameScenes;

    [Obsolete]
    private string startingSceneName;

    public List<AirshipPackageDocument> packages = new();

    [HideInInspector] public List<string> tags = new();
    [HideInInspector] public string[] gameLayers;
    [HideInInspector] public string[] gameTags;
    [HideInInspector] public bool[] physicsMatrix;
    [HideInInspector] public bool[] physicsMatrix2D;

    //3D Physics
    [HideInInspector] public Vector3 gravity = new(0, -9.81f, 0);
    [HideInInspector] public float bounceThreshold = 2;
    [HideInInspector] public float defaultMaxDepenetrationVelocity = 10;
    [HideInInspector] public float sleepThreshold = 0.005f;
    [HideInInspector] public float defaultContactOffset = 0.01f;
    [HideInInspector] public int defaultSolverIterations = 6;
    [HideInInspector] public int defaultSolverVelocityIterations = 1;
    [HideInInspector] public bool queriesHitBackfaces = false;
    [HideInInspector] public bool queriesHitTriggers = true;
    [HideInInspector] public float fixedDeltaTime = .025f;

    [HideInInspector] public Vector3 gravity2D = new(0, -9.81f, 0);
    [HideInInspector] public int velocityIterations2D = 8;
    [HideInInspector] public int positionIterations2D = 3;
    [HideInInspector] public float bounceThreshold2D = 1;
    [HideInInspector] public float maxLinearCorrection2D = .2f;
    [HideInInspector] public float maxAngularCorrection2D = 8;
    [HideInInspector] public float maxTranslationSpeed2D = 100;
    [HideInInspector] public float maxRotationSpeed2D = 360;
    [HideInInspector] public float baumgarteScale2D = .2f;
    [HideInInspector] public float baumgarteTOIScale2D = .75f;
    [HideInInspector] public float timeToSleep2D = .5f;
    [HideInInspector] public float linearSleepTolerance2D = .01f;
    [HideInInspector] public float angularSleepTolerance2D = 2;
    [HideInInspector] public float defaultContactOffset2D = .01f;
    [HideInInspector] public float contactThreshold2D = 0f;
    [HideInInspector] public bool queriesHitTriggers2D = true;
    [HideInInspector] public bool queriesStartInColliders2D = true;
    [HideInInspector] public bool callbacksOnDisable2D = true;
    [HideInInspector] public bool reuseCollisionCallbacks2D = true;
    [HideInInspector] public bool autoSyncTransforms2D = false;

    [HideInInspector] public bool supportsMobile;

    [HideInInspector] public bool compileURPShaders = false;

    private const string TagPrefix = "AirshipTag";
    public const int MaximumTags = 64;

    public bool TryGetRuntimeTag(string userTag, out string runtimeTag) {
        var index = Array.IndexOf(gameTags, userTag);
        if (index != -1 && index < MaximumTags) {
            runtimeTag = TagPrefix + index;
            return true;
        }

        runtimeTag = null;
        return false;
    }

    public bool TryGetUserTag(string runtimeTag, out string userTag) {
        if (!runtimeTag.StartsWith(TagPrefix)) {
            userTag = null;
            return false;
        }

        var offset = int.Parse(runtimeTag[TagPrefix.Length..]);
        userTag = gameTags[offset];
        return userTag != null;
    }

    public static GameConfig Load() {
#if UNITY_EDITOR
        var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
        // I believe AssetDatabase might not have loaded GameConfig sometimes (like during a publish)
        // TODO if file doesn't exist we could generate GameConfig here
        if (gameConfig == null) {
            return null;
        }

#if !AIRSHIP_PLAYER && !AIRSHIP_INTERNAL
        if (gameConfig.packages.Find((p) => p.id == "@Easy/Core") == null) {
            gameConfig.packages.Add(new AirshipPackageDocument() {
                id = "@Easy/Core",
                defaultPackage = true,
                forceLatestVersion = true,
            });
        }
        if (gameConfig.packages.Find((p) => p.id == "@Easy/CoreMaterials") == null) {
            gameConfig.packages.Add(new AirshipPackageDocument() {
                id = "@Easy/CoreMaterials",
                defaultPackage = true,
                forceLatestVersion = true,
            });
        }
#endif

        return gameConfig;
#endif

        return null;
    }

    private void OnValidate() {
#if UNITY_EDITOR
#pragma warning disable CS0612
        if (startingScene == null && !string.IsNullOrEmpty(startingSceneName)) {
            var guids = AssetDatabase.FindAssets("t:Scene").ToList();
            var paths = guids.Select((guid) => AssetDatabase.GUIDToAssetPath(guid));
            foreach (var path in paths) {
                if (path.StartsWith("Assets/")) {
                    if (path.EndsWith(startingSceneName + ".unity")) {
                        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                        startingScene = sceneAsset;
                        startingSceneName = "";
                    }
                }
            }
        }
#pragma warning restore CS0612
#endif
    }

    public string ToJson() {
        var gameConfigDto = new GameConfigDto() {
            gameId = this.gameId,
            packages = this.packages
        };
        var json = JsonUtility.ToJson(gameConfigDto);
        return json;
    }

    public void SerializeSettings() {
        try {
            //Update physics matrix        
            var areLayersIgnored = new bool[15 * 32];
            var TheMatrixLog = "SAVING GAME LAYER MATRIX: \n";
            //15 Game Layers and how they collide with all 32 layers
            for (var i = 0; i < 15; i++) {
                //Check
                for (var otherLayerI = 0; otherLayerI < 32; otherLayerI++) {
                    var gameLayerI = 17 + i;
                    var ignored = Physics.GetIgnoreLayerCollision(gameLayerI, otherLayerI);
                    areLayersIgnored[i * 32 + otherLayerI] = ignored;
                    TheMatrixLog += "GameLayer" + i + " and Layer: " + otherLayerI + " ignored: " + ignored + " \n";
                }
            }

            //Debug.Log(TheMatrixLog);
            physicsMatrix = areLayersIgnored;

            //Physics settings
            gravity = Physics.gravity;
            bounceThreshold = Physics.bounceThreshold;
            defaultMaxDepenetrationVelocity = Physics.defaultMaxDepenetrationVelocity;
            sleepThreshold = Physics.sleepThreshold;
            defaultContactOffset = Physics.defaultContactOffset;
            defaultSolverIterations = Physics.defaultSolverIterations;
            defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations;
            queriesHitBackfaces = Physics.queriesHitBackfaces;
            queriesHitTriggers = Physics.queriesHitTriggers;
            fixedDeltaTime = Time.fixedDeltaTime;


            //Update physics 2D matrix        
            areLayersIgnored = new bool[15 * 32];
            TheMatrixLog = "SAVING 2D GAME LAYER MATRIX: \n";
            //15 Game Layers and how they collide with all 32 layers
            for (var i = 0; i < 15; i++) {
                //Check
                for (var otherLayerI = 0; otherLayerI < 32; otherLayerI++) {
                    var gameLayerI = 17 + i;
                    var ignored = Physics2D.GetIgnoreLayerCollision(gameLayerI, otherLayerI);
                    areLayersIgnored[i * 32 + otherLayerI] = ignored;
                    TheMatrixLog += "2D GameLayer" + i + " and Layer: " + otherLayerI + " ignored: " + ignored + " \n";
                }
            }

            //Debug.Log(TheMatrixLog);
            physicsMatrix2D = areLayersIgnored;

            //Physics 2D settings
            gravity2D = Physics2D.gravity;
            velocityIterations2D = Physics2D.velocityIterations;
            positionIterations2D = Physics2D.positionIterations;
            bounceThreshold2D = Physics2D.bounceThreshold;
            maxLinearCorrection2D = Physics2D.maxLinearCorrection;
            maxAngularCorrection2D = Physics2D.maxAngularCorrection;
            maxTranslationSpeed2D = Physics2D.maxTranslationSpeed;
            maxRotationSpeed2D = Physics2D.maxRotationSpeed;
            baumgarteScale2D = Physics2D.baumgarteScale;
            baumgarteTOIScale2D = Physics2D.baumgarteTOIScale;
            timeToSleep2D = Physics2D.timeToSleep;
            linearSleepTolerance2D = Physics2D.linearSleepTolerance;
            angularSleepTolerance2D = Physics2D.angularSleepTolerance;
            defaultContactOffset2D = Physics2D.defaultContactOffset;
            contactThreshold2D = Physics2D.contactThreshold;
            queriesHitTriggers2D = Physics2D.queriesHitTriggers;
            queriesStartInColliders2D = Physics2D.queriesStartInColliders;
            callbacksOnDisable2D = Physics2D.callbacksOnDisable;
            reuseCollisionCallbacks2D = Physics2D.reuseCollisionCallbacks;
            autoSyncTransforms2D = Physics2D.autoSyncTransforms;
        } catch (Exception e) {
            Debug.LogError("Error in Serialize Game Config: " + e);
        }
    }

    public void DeserializeSettings() {
        try { 
            //15 Game Layers and how they collide with all 32 layers
            var gameLayerI = 17;
            var TheMatrixLog = "LOADING GAME LAYER MATRIX: \n";
            for (var byteI = 0; byteI < physicsMatrix.Length; byteI += 32) {
                for (var otherLayerI = 0; otherLayerI < 32; otherLayerI++) {
                    var ignored = physicsMatrix[byteI + otherLayerI];
                    Physics.IgnoreLayerCollision(gameLayerI, otherLayerI, ignored);
				    TheMatrixLog += "GameLayer" + gameLayerI + " and Layer: " + otherLayerI +" ignored: " + ignored + " \n";
                }

                gameLayerI++;
            }
            //Debug.Log(TheMatrixLog);

            //Physics Settings
            Physics.gravity = gravity;
            Physics.bounceThreshold = bounceThreshold;
            Physics.defaultMaxDepenetrationVelocity = defaultMaxDepenetrationVelocity;
            Physics.sleepThreshold = sleepThreshold;
            Physics.defaultContactOffset = defaultContactOffset;
            Physics.defaultSolverIterations = defaultSolverIterations;
            Physics.defaultSolverVelocityIterations = defaultSolverVelocityIterations;
            Physics.queriesHitBackfaces = queriesHitBackfaces;
            Physics.queriesHitTriggers = queriesHitTriggers;
            Time.fixedDeltaTime = fixedDeltaTime;


            //2D Setup
            if (physicsMatrix2D != null && physicsMatrix2D.Length > 0) {
                gameLayerI = 17;
                TheMatrixLog = "LOADING 2D GAME LAYER MATRIX: \n";
                for (var byteI = 0; byteI < physicsMatrix2D.Length; byteI += 32) {
                    for (var otherLayerI = 0; otherLayerI < 32; otherLayerI++) {
                        var ignored = physicsMatrix2D[byteI + otherLayerI];
                        Physics2D.IgnoreLayerCollision(gameLayerI, otherLayerI, ignored);
                        TheMatrixLog += "2D GameLayer" + gameLayerI + " and Layer: " + otherLayerI + " ignored: " + ignored +
                                        " \n";
                    }

                    gameLayerI++;
                }
                //Debug.Log(TheMatrixLog);
                
                //Physics 2D Settings
                Physics2D.gravity = gravity2D;
                Physics2D.velocityIterations = velocityIterations2D;
                Physics2D.positionIterations = positionIterations2D;
                Physics2D.bounceThreshold = bounceThreshold2D;
                Physics2D.maxLinearCorrection = maxLinearCorrection2D;
                Physics2D.maxAngularCorrection = maxAngularCorrection2D;
                Physics2D.maxTranslationSpeed = maxTranslationSpeed2D;
                Physics2D.maxRotationSpeed = maxRotationSpeed2D;
                Physics2D.baumgarteScale = baumgarteScale2D;
                Physics2D.baumgarteTOIScale = baumgarteTOIScale2D;
                Physics2D.timeToSleep = timeToSleep2D;
                Physics2D.linearSleepTolerance = linearSleepTolerance2D;
                Physics2D.angularSleepTolerance = angularSleepTolerance2D;
                Physics2D.defaultContactOffset = defaultContactOffset2D;
                Physics2D.contactThreshold = contactThreshold2D;
                Physics2D.queriesHitTriggers = queriesHitTriggers2D;
                Physics2D.queriesStartInColliders = queriesStartInColliders2D;
                Physics2D.callbacksOnDisable = callbacksOnDisable2D;
                Physics2D.reuseCollisionCallbacks = reuseCollisionCallbacks2D;
                Physics2D.autoSyncTransforms = autoSyncTransforms2D;
            } else {
                Debug.LogError("Game hasn't generated a 2D game config yet");
            }
        
        } catch (Exception e) {
            Debug.LogError("Error in Deserialize Game Config: " + e);
        }
    }
}