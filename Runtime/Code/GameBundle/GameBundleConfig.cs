using UnityEngine;
using Object = UnityEngine.Object;

[CreateAssetMenu(fileName = "GameBundleConfig", menuName = "EasyGG/GameBundleConfig", order = 100)]
public class GameBundleConfig : ScriptableObject
{
    // IE. EasyEngine version, 92. This is what we deply to the app store.
    public int minimumPlayerVersion;

    // IE. bedwars
    public string gameId;
    public Object[] gameScenes;
}