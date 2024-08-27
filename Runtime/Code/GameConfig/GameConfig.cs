using System;
using System.Collections.Generic;
using System.Linq;
using Code.GameBundle;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
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

    private const string TagPrefix = "AirshipTag";
    private const int TagMax = 64;

    public bool TryGetRuntimeTag(string userTag, out string runtimeTag) {
        var index = Array.IndexOf(gameTags, userTag);
        if (index != -1 && index < TagMax) {
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
}