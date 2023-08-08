using System.Collections.Generic;
using Code.GameBundle;
using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Airship/GameConfig", order = 100)]
public class GameConfig : ScriptableObject
{
    // IE. EasyEngine version, 92. This is what we deply to the app store.
    public int minimumPlayerVersion;

    // IE. bedwars
    public string gameId;
    public Object[] gameScenes;

    public List<InstalledAirshipPackage> packages = new();
}