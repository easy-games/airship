using System.Collections.Generic;
using Easy.Airship.Editor.EditorInternal;
using Luau;
using UnityEditor;

[FilePath("Temp/AirshipCustomMenuState", FilePathAttribute.Location.ProjectFolder)]
internal class AirshipCustomMenus : ScriptableSingleton<AirshipCustomMenus> {
    public Dictionary<string, string> assetPathToMenuItemPaths = new ();
    
    internal void AddCreateAssetMenu(string scriptPath, string fileName, string menuItem, int priority = 0) {
        if (AirshipEditorInternals.HasUnityMenuItem(menuItem)) return;

        if (assetPathToMenuItemPaths.TryGetValue(scriptPath, out var existingMenuItem)) {
            if (existingMenuItem != menuItem) {
                AirshipEditorInternals.RemoveUnityMenuItem(existingMenuItem);
            }
        }
        
        AirshipEditorInternals.AddUnityMenuItem(menuItem, "", false, priority, () => {
            var asset = AssetDatabase.LoadAssetAtPath<AirshipScript>(scriptPath);
            AirshipScriptableObjectEditor.CreateAirshipScriptableObject(asset, fileName);
        }, () => true);
        assetPathToMenuItemPaths.TryAdd(scriptPath, menuItem);
    }
    
    internal void RegisterMenus() {
        List<string> assetPathsToRemove = new List<string>();
        foreach (var assetPathToMenuItemPath in assetPathToMenuItemPaths) {
            var asset = AssetDatabase.LoadAssetAtPath<AirshipScript>(assetPathToMenuItemPath.Key);
            if (asset == null) {
                assetPathsToRemove.Add(assetPathToMenuItemPath.Key);
            }
        }

        foreach (var assetPath in assetPathsToRemove) {
            if (assetPathToMenuItemPaths.TryGetValue(assetPath, out var existingMenuItem)) {
                AirshipEditorInternals.RemoveUnityMenuItem(existingMenuItem);
            }

            assetPathToMenuItemPaths.Remove(assetPath);
        }
    }

    internal void Save() {
        Save(false);
    }
}