using System;
using UnityEngine;

[ExecuteInEditMode]
public class FishNetSetup : MonoBehaviour
{
    private void Start()
    {
        FishNet.Configuring.Configuration.Configurations.PrefabGenerator.DefaultPrefabObjectsPath =
            "Packages/gg.easy.airship/Runtime/Code/DefaultPrefabObjects.asset";
    }
}