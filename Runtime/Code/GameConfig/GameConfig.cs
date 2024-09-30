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
public class GameConfig : ScriptableObject
{
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
    [HideInInspector] public Vector3 physicsGravity;

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
        if (gameConfig == null) return null;

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
        if (this.startingScene == null && !string.IsNullOrEmpty(this.startingSceneName)) {
            var guids = AssetDatabase.FindAssets("t:Scene").ToList();
            var paths = guids.Select((guid) => AssetDatabase.GUIDToAssetPath(guid));
            foreach (var path in paths) {
                if (path.StartsWith("Assets/")) {
                    if (path.EndsWith(this.startingSceneName + ".unity")) {
                        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                        this.startingScene = sceneAsset;
                        this.startingSceneName = "";
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

    public void SerializeSettings(){
		//Update physics matrix        
		bool[] areLayersIgnored = new bool[15 * 32];
		string TheMatrixLog = "SAVING GAME LAYER MATRIX: \n";
		//15 Game Layers and how they collide with all 32 layers
        for (int i = 0; i < 15; i++) {
			//Check
            for (int otherLayerI = 0; otherLayerI < 32; otherLayerI++) {
				int gameLayerI = 17 + i;
				bool ignored = Physics.GetIgnoreLayerCollision(gameLayerI, otherLayerI);
            	areLayersIgnored[i * 32 + otherLayerI] = ignored;
				TheMatrixLog += "GameLayer" + i + " and Layer: " + otherLayerI +" ignored: " + ignored + " \n";
            }
        }
        this.physicsMatrix = areLayersIgnored;
		this.physicsGravity = Physics.gravity;
    }

    public void DeserializeSettings(){
		//15 Game Layers and how they collide with all 32 layers
		string TheMatrixLog = "LOADING GAME LAYER MATRIX: \n";
        for (int i = 0; i < 15; i++) {
            for (int otherLayerI = 0; otherLayerI < 32; otherLayerI++) {
				int gameLayerI = 17 + i;
                bool ignored = this.physicsMatrix[i];
                Physics.IgnoreLayerCollision(gameLayerI, otherLayerI, ignored);
				TheMatrixLog += "GameLayer" + i + " and Layer: " + otherLayerI +" ignored: " + ignored + " \n";
            }
        }
        Physics.gravity = this.physicsGravity;
    }
}