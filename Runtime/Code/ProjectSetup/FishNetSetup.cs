using FishNet.Configuring;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class FishNetSetup : MonoBehaviour
{
    private void Awake()
    {
        Setup();
    }

    public void Setup()
    {
        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.Enabled = false;
        var prefabs =
            AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(
                "Packages/gg.easy.airship/Runtime/Code/DefaultPrefabObjects.asset");
        prefabs.Clear();
        prefabs.AddObject(AssetDatabase.LoadAssetAtPath<NetworkObject>("Packages/gg.easy.airship/Runtime/Prefabs/Player.prefab"));
        FishNet.Configuring.Configuration.Configurations.Write(true);
    }
}