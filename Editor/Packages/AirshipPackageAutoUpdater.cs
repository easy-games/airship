using System.Collections;
using Code.GameBundle;
using ParrelSync;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Editor.Packages {
    [InitializeOnLoad]
    public class AirshipPackageAutoUpdater {
        private static double lastChecked = 0;
        private const double checkInterval = 30;
        static AirshipPackageAutoUpdater() {
            if (ClonesManager.IsClone()) return;
            EditorApplication.update += Update;
        }

        static void Update() {
            if (EditorApplication.timeSinceStartup > lastChecked + checkInterval) {
                lastChecked = EditorApplication.timeSinceStartup;
                CheckPackageVersions();
            }
        }

        public static void CheckPackageVersions() {
            var gameConfig = GameConfig.Load();
            foreach (var package in gameConfig.packages) {
                if (!package.forceLatestVersion) continue;
                EditorCoroutines.Execute(CheckPackage(package));
            }
        }

        public static IEnumerator CheckPackage(AirshipPackageDocument package) {
            var url = $"{AirshipPackagesWindow.deploymentUrl}/package-versions/packageId/{package.id}";
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SendWebRequest();
            yield return new WaitUntil(() => request.isDone);

            if (request.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to fetch latest package version: " + request.error);
                Debug.LogError("result=" + request.result);
                ;
                yield break;
            }

            PackageLatestVersionResponse res =
                JsonUtility.FromJson<PackageLatestVersionResponse>(request.downloadHandler.text);

            if (res.package.assetVersionNumber.ToString() != package.version) {
                Debug.Log($"[Airship]: Updating default package {package.id} to v{res.package.assetVersionNumber}");
                yield return AirshipPackagesWindow.DownloadPackage(package.id, res.package.assetVersionNumber.ToString());
            }
        }
    }
}