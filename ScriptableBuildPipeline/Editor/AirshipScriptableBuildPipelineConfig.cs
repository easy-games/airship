using JetBrains.Annotations;
using UnityEngine;

public class AirshipScriptableBuildPipelineConfig {
    public static bool buildingGameBundles = false;
    [CanBeNull] public static string buildingPackageName = null;

    public static bool IsBuildingPackage(string assetBundleName) {
        Debug.Log($"comparing assetBundleName={assetBundleName} buildingPackageName={buildingPackageName}");
        if (string.IsNullOrEmpty(buildingPackageName)) {
            Debug.Log("false.1");
            return false;
        }

        if (assetBundleName.ToLower().StartsWith(buildingPackageName.ToLower() + "_")) {
            Debug.Log("true");
            return true;
        }

        Debug.Log("false.2");
        return false;
    }
}