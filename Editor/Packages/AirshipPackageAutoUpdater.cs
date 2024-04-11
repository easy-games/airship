using System.Collections;
using System.IO;
using Code.GameBundle;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Editor.Packages {
    [InitializeOnLoad]
    public class AirshipPackageAutoUpdater {
        private static double lastChecked = -40;
        private const double checkInterval = 30;

        private static int packagesReadyCount = 0;
        private static int packagesCount = int.MaxValue; // ensure that until packages are OK, this waits
        private static int packagesDownloadingCount = 0;

        public static bool ArePackagesReady => packagesReadyCount >= packagesCount;
        
        static AirshipPackageAutoUpdater() {

        }

        [InitializeOnLoadMethod]
        static void OnLoad() {
            if (RunCore.IsClone()) return;
            EditorApplication.update += Update;

            lastChecked = EditorApplication.timeSinceStartup;
            CheckPackageVersions();
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

        public static void CheckPackageVersions() {
            var gameConfig = GameConfig.Load();
            
            if (!EditorIntegrationsConfig.instance.autoUpdatePackages) return;
            
            foreach (var package in gameConfig.packages) {
                EditorCoroutines.Execute(CheckPackage(package));
            }
        }

        public static IEnumerator CheckPackage(AirshipPackageDocument package) {
            if (package.forceLatestVersion && !package.localSource) {
                var url = $"{AirshipPackagesWindow.deploymentUrl}/package-versions/packageSlug/{package.id}";
                var request = UnityWebRequest.Get(url);
                request.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SendWebRequest();
                yield return new WaitUntil(() => request.isDone);

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("Failed to fetch latest package version: " + request.error);
                    Debug.LogError("result=" + request.result);
                    yield break;
                }

                PackageLatestVersionResponse res =
                    JsonUtility.FromJson<PackageLatestVersionResponse>(request.downloadHandler.text);

                if (res.package.codeVersionNumber.ToString() != package.codeVersion || !package.IsDownloaded()) {
                    Debug.Log($"[Airship]: Updating default package {package.id} from v{package.codeVersion} to v{res.package.codeVersionNumber}");
                    yield return AirshipPackagesWindow.DownloadPackage(package.id, res.package.codeVersionNumber.ToString(), res.package.assetVersionNumber.ToString());
                    yield break;
                }
            }

            // Check if uninstalled
            if (!package.localSource) {
                var packageDir = Path.Combine("Assets", "Bundles", package.id);
                if (!Directory.Exists(packageDir)) {
                    Debug.Log($"[Airship]: Auto installing {package.id} v{package.codeVersion}");
                    yield return AirshipPackagesWindow.DownloadPackage(package.id, package.codeVersion, package.assetVersion);
                    yield break;
                }
            }
        }
    }
}