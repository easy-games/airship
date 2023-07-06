using System;
using FishNet.Configuring;
using UnityEngine;

[ExecuteInEditMode]
public class FishNetSetup : MonoBehaviour
{
    private void Awake()
    {
        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.SearchScope =
            (int)SearchScopeType.SpecificFolders;
    }

    private void Start()
    {
        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.SearchScope =
            (int)SearchScopeType.SpecificFolders;

        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.DefaultPrefabObjectsPath =
            "Packages/gg.easy.airship/Runtime/Code/DefaultPrefabObjects.asset";

        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.Enabled = false;
    }
}