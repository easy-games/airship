using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Editor.Packages;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[InitializeOnLoad]
public class ProjectConfigUpdater {

    public static bool UpdateInProgress = false;
    
    private static double lastChecked = 0;
    private const double checkInterval = 60 * 10;


    static ProjectConfigUpdater() {
        if (RunCore.IsClone()) return;
        EditorApplication.update += Update;
    }
    
    static void Update() {
#if AIRSHIP_PLAYER
            return;
#endif
        if (Application.isPlaying) return;
        if (EditorApplication.timeSinceStartup > lastChecked + checkInterval) {
            lastChecked = EditorApplication.timeSinceStartup;
            UpdateProjectConfig(false);
        }
        
    }

    public static void UpdateProjectConfig(bool force) {
        if (!EditorIntegrationsConfig.instance.manageTypescriptProject && !force) return;
        if (ProjectConfigUpdater.UpdateInProgress) return;
        ProjectConfigUpdater.UpdateInProgress = false;
        EditorCoroutines.Execute(FetchLatestConfig());

    }

    private static IEnumerator FetchLatestConfig() {
        
        var url = "https://github.com/easy-games/project-config/zipball/main";
        var request = new UnityWebRequest(url);
        
        var zipDownloadPath = Path.Join("bundles", "temp", "AirshipTemplate.zip");
        File.Delete(zipDownloadPath);
        
        request.downloadHandler = new DownloadHandlerFile(zipDownloadPath);
        request.SendWebRequest();
        
        yield return new WaitUntil(() => request.isDone);

        if (request.result != UnityWebRequest.Result.Success) {
            ProjectConfigUpdater.UpdateInProgress = false;
            yield break;
        }
        
        var zip = System.IO.Compression.ZipFile.OpenRead(zipDownloadPath);

        var typescriptRoot = $"{Application.dataPath}/Typescript~";
        

        var gameConfig = GameConfig.Load();

        foreach (var entry in zip.Entries) {
            // Directory, skip.
            if (entry.Name == "") continue;
            // Shared configuration.
            if (entry.FullName.Contains("/shared/")) {
                entry.ExtractToFile($"{typescriptRoot}/{entry.Name}", true);
                foreach (var package in gameConfig.packages) {
                    if (!package.localSource) continue;
                    var packageParts = package.id.Split("/");
                    var orgId = packageParts[0];
                    var packageId = packageParts[1];
                    var packageRoot = $"{Application.dataPath}/Bundles/{package.id}/{packageId}~";
                    entry.ExtractToFile($"{packageRoot}/{entry.Name}", true);
                    if (entry.Name == "package.json") {
                        AirshipPackagesWindow.RenamePackage($"{Application.dataPath}/Bundles/{package.id}", orgId,
                            packageId);
                    }
                }
            }
            // Package specific configuration.
            if (entry.FullName.Contains("/package/")) {
                foreach (var package in gameConfig.packages) {
                    if(!package.localSource) continue;
                    var packageParts = package.id.Split("/");
                    var orgId = packageParts[0];
                    var packageId = packageParts[1];
                    var packageRoot = $"{Application.dataPath}/Bundles/{package.id}/{packageId}~";
                    entry.ExtractToFile($"{packageRoot}/{entry.Name}", true);
                    if (entry.Name == "package.json") {
                        AirshipPackagesWindow.RenamePackage($"{Application.dataPath}/Bundles/{package.id}", orgId, packageId);
                    }
                }
            }
            // Game specific configuration.
            if (entry.FullName.Contains("/game/")) {
                entry.ExtractToFile($"{typescriptRoot}/{entry.Name}", true);
            }
        }
        
        zip.Dispose();

        ProjectConfigUpdater.UpdateInProgress = false;
        yield return null;
    }
}
