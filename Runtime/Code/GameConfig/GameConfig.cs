using System.Collections.Generic;
using System.IO;
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
    public string startingSceneName;
    public Object[] gameScenes;


    public List<AirshipPackageDocument> packages = new();

    [HideInInspector] public List<string> tags = new();
    [HideInInspector] public string[] gameLayers;
    
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

    public string ToJson() {
        var gameConfigDto = new GameConfigDto() {
            gameId = this.gameId,
            packages = this.packages
        };
        var json = JsonUtility.ToJson(gameConfigDto);
        return json;
    }
}