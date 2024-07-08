using JetBrains.Annotations;

public class AirshipScriptableBuildPipelineConfig {
    public static bool buildingGameBundles = false;
    [CanBeNull] public static string buildingPackageName = null;

    public static bool IsBuildingPackage(string assetBundleName) {
        return !string.IsNullOrEmpty(buildingPackageName) && buildingPackageName.StartsWith(assetBundleName);
    }
}