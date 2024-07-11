using JetBrains.Annotations;
using UnityEngine;

public class AirshipScriptableBuildPipelineConfig {
    public static bool buildingGameBundles = false;
    [CanBeNull] public static string buildingPackageName = null;

    public static bool IsBuildingPackage(string assetBundleName) {
        Debug.Log($"comparing building={buildingPackageName} input={assetBundleName}");
        return !string.IsNullOrEmpty(buildingPackageName) && assetBundleName.ToLower().StartsWith(buildingPackageName.ToLower());
    }
}