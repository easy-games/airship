using FishNet.Configuring;
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
        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.SearchScope =
            (int)SearchScopeType.SpecificFolders;

        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.DefaultPrefabObjectsPath =
            "Packages/gg.easy.airship/Runtime/Code/DefaultPrefabObjects.asset";

        if (FishNet.Configuring.Configuration.Configurations.PrefabGenerator.Enabled)
        {
            FishNet.Configuring.Configuration.Configurations.PrefabGenerator.Enabled = false;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}