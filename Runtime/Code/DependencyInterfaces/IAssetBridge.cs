using UnityEngine;

public interface IAssetBridge
{
    public AssetBundle GetAssetBundle(string name);
    public Object LoadAsset(string path);
    public Object LoadAssetIfExists(string path);
    public T LoadAssetIfExistsInternal<T>(string path) where T : Object;
    public bool IsLoaded();
    public T LoadAssetInternal<T>(string path, bool printErrorOnFail = true) where T : Object;
    public string[] GetAllBundlePaths();
    public string[] GetAllGameRootPaths();
    public string[] GetAllAssets();
}
