using System;
using System.Collections;
using System.IO;
using System.Linq;
using Code.GameBundle;
using Code.Platform.Shared;
using Code.State;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Editor.Packages {
    [InitializeOnLoad]
    public class AirshipPackageAutoUpdater : AssetPostprocessor {
        private static double lastChecked = -40;
        private const double checkInterval = 30;
        public static bool isCoreUpdateAvailable = false;
        public static bool firstUpdateCheck = true;
        
        static AirshipPackageAutoUpdater() {

        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
            if (RunCore.IsClone()) return;
            if (!didDomainReload) return;
            EditorApplication.update += Update;

            lastChecked = EditorApplication.timeSinceStartup;
            CheckPackageVersions(ignoreUserSetting: RequiresPackageDownloads(GameConfig.Load()));
        }

        static void Update() {
#if AIRSHIP_PLAYER
            return;
#endif
            if (Application.isPlaying) return;
            if (EditorApplication.timeSinceStartup > lastChecked + checkInterval) {
                lastChecked = EditorApplication.timeSinceStartup;
                CheckPackageVersions();
            }
        }

        public static bool RequiresPackageDownloads(GameConfig config) {
            return config.packages.Any(package => {
                return !package.localSource && !package.IsDownloaded();
            });
        }
        
        public static void CheckPackageVersions(bool ignoreUserSetting = false) {
            var gameConfig = GameConfig.Load();

            var shouldUseLatestPackages = EditorIntegrationsConfig.instance.autoUpdatePackages;
            var shouldUpdate = shouldUseLatestPackages || ignoreUserSetting;
            if (!shouldUpdate && !RequiresPackageDownloads(gameConfig)) return;
            if (AirshipPackagesWindow.buildingAssetBundles || CreateAssetBundles.buildingBundles) return;
            
            EditorCoroutines.Execute(CheckAllPackages(gameConfig, useLocalVersion: !shouldUseLatestPackages, firstUpdateCheck));
            firstUpdateCheck = false;
        }

        public static IEnumerator CheckAllPackages(GameConfig gameConfig, bool useLocalVersion = false, bool immediatelyUpdateCore = false) {
            try {
                AssetDatabase.StartAssetEditing();
                foreach (var package in gameConfig.packages) {
                    yield return CheckPackage(package, useLocalVersion, immediatelyUpdateCore);
                }
            } finally {
                AssetDatabase.StopAssetEditing();
            }
        }

        public static IEnumerator CheckPackage(AirshipPackageDocument package, bool useLocalVersion = false, bool immediatelyUpdateCore = false) {
            if (package.forceLatestVersion && !package.localSource) {
                var url = $"{AirshipPlatformUrl.deploymentService}/package-versions/packageSlug/{package.id}";
                var request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SendWebRequest();
                yield return new WaitUntil(() => request.isDone);

                if (request.result == UnityWebRequest.Result.ConnectionError) {
                    // no error when in offline mode
                    yield break;
                }
                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("Failed to fetch latest package version: " + request.error);
                    Debug.LogError("result=" + request.result);
                    yield break;
                }

                PackageLatestVersionResponse res =
                    JsonUtility.FromJson<PackageLatestVersionResponse>(request.downloadHandler.text);

                if (res == null || res.package == null) {
                    Debug.LogError("[Airship]: Failed to check package: " + package.id + ". Got response: " + request.downloadHandler.text);
                    yield break;
                }

                var targetCodeVersion = useLocalVersion ? package.codeVersion : res.package.codeVersionNumber.ToString();
                var targetAssetVersion =
                    useLocalVersion ? package.assetVersion : res.package.assetVersionNumber.ToString();

                if (!package.IsDownloaded()) {
                    Debug.Log($"[Airship]: Updating default package {package.id} from v{package.codeVersion} to v{res.package.codeVersionNumber}");
                    yield return AirshipPackagesWindow.DownloadPackage(package.id, targetCodeVersion, targetAssetVersion);
                    yield break;
                }
                if (res.package.codeVersionNumber.ToString() != package.codeVersion) {
                    if (!immediatelyUpdateCore && (package.id.ToLower() == "@easy/core" || package.id.ToLower() == "@easy/corematerials")) {
                        isCoreUpdateAvailable = true;
                    } else {
                        yield return AirshipPackagesWindow.DownloadPackage(package.id, targetCodeVersion, targetAssetVersion);
                        yield break;
                    }
                }
            }

            // Check if uninstalled
            if (!package.localSource) {
                var packageDir = Path.Combine("Assets", "AirshipPackages", package.id);
                if (!Directory.Exists(packageDir)) {
                    Debug.Log($"[Airship]: Auto installing {package.id} v{package.codeVersion}");
                    yield return AirshipPackagesWindow.DownloadPackage(package.id, package.codeVersion, package.assetVersion);
                    yield break;
                }
            }
        }
    }
}