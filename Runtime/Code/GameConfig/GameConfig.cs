using System.Collections.Generic;
using Code.GameBundle;
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Airship/GameConfig", order = 100)]
public class GameConfig : ScriptableObject
{
    // IE. EasyEngine version, 92.
    public int minimumPlayerVersion;

    // IE. bedwars
    public string gameId;
    public Object[] gameScenes;

    public List<AirshipPackageDocument> packages = new();
}