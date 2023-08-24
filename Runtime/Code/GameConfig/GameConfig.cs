using System.Collections.Generic;
using Code.GameBundle;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Airship/GameConfig", order = 100)]
public class GameConfig : ScriptableObject
{
    // IE. EasyEngine version, 92.
    public int minimumPlayerVersion;

    // IE. bedwars
    public string gameId;
    public SceneAsset[] gameScenes;

    public List<AirshipPackageDocument> packages = new();

    public static GameConfig Load() {
        #if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
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