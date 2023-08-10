using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Code.GameBundle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using ZipFile = Unity.VisualScripting.IonicZip.ZipFile;

namespace Editor.Packages {
    public class AirshipPackagesWindow : EditorWindow {
        private GameConfig gameConfig;
        private static Dictionary<string, string> packageUploadProgress = new();
        private static Dictionary<string, bool> packageVersionToggleBools = new();
        public static string deploymentUrl = "https://deployment-service-fxy2zritya-uc.a.run.app";
        public static string cdnUrl = "https://gcdn-staging.easy.gg";

        public static string[] assetBundleFiles = {
            "Shared/Resources",
            "Shared/Scenes",
            "Client/Resources",
            "Client/Scenes",
            "Server/Resources",
            "Server/Scenes"
        };

        [MenuItem ("Window/Airship Packages")]
        public static void ShowWindow () {
            EditorWindow.GetWindow(typeof(AirshipPackagesWindow), true, "Airship Packages", true);
        }

        private void OnEnable() {
            this.gameConfig = GameConfig.Load();
        }

        private void OnGUI() {
            GUILayout.Label("Packages", EditorStyles.largeLabel);
            AirshipEditorGUI.HorizontalLine();
            foreach (var package in this.gameConfig.packages) {
                packageVersionToggleBools.TryAdd(package.id, false);

                GUILayout.BeginHorizontal();
                GUILayout.Label(package.id, new GUIStyle(GUI.skin.label) { fixedWidth = 80 });
                GUILayout.Label("v" + package.version, new GUIStyle(GUI.skin.label) { fixedWidth = 25 });
                if (package.localSource) {
                    var localSourceStyle = new GUIStyle(GUI.skin.label);
                    localSourceStyle.fontStyle = FontStyle.Italic;
                    GUILayout.Label("Local Source", localSourceStyle);

                    if (packageUploadProgress.TryGetValue(package.id, out var progress)) {
                        GUILayout.Label(progress);
                    } else {
                        if (GUILayout.Button("⬆️ Publish")) {
                            PublishPackage(package, false);
                        }
                        // if (GUILayout.Button("⬆️ Upload Only")) {
                        //     this.PublishPackage(package, true);
                        // }
                    }
                } else {
                    if (GUILayout.Button("Redownload")) {
                        EditorCoroutines.Execute(DownloadPackage(package.id, package.version));
                    }
                    GUILayout.Space(5);
                    if (GUILayout.Button("Update to Latest")) {
                        UpdateToLatest(package.id);
                    }
                }
                GUILayout.EndHorizontal();

                if (!package.localSource) {
                    packageVersionToggleBools[package.id] = EditorGUILayout.BeginFoldoutHeaderGroup(packageVersionToggleBools[package.id], "Change Version", null, null);
                    if (packageVersionToggleBools[package.id]) {
                        EditorGUILayout.BeginHorizontal();
                        int currentVersion = 0;
                        try {
                            currentVersion = Int32.Parse(package.version);
                        } catch (Exception e) {
                            Debug.LogError(e);
                        }
                        var version = EditorGUILayout.IntField("Version", currentVersion);
                        if (GUILayout.Button("⬇️ Install")) {
                            DownloadPackage(package.id, version + "");
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }

                AirshipEditorGUI.HorizontalLine();
            }
        }

        public static void PublishPackage(AirshipPackageDocument packageDoc, bool skipBuild) {
            try {
                BuildTarget[] buildTargets = AirshipEditorUtil.AllBuildTargets;

                if (!skipBuild) {
                    packageUploadProgress[packageDoc.id] = "Building...";
                    CreateAssetBundles.FixBundleNames();

                    List<AssetBundleBuild> builds = new();
                    foreach (var assetBundleFile in assetBundleFiles) {
                        var assetBundleName = $"{packageDoc.id}_{assetBundleFile}".ToLower();
                        var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
                        builds.Add(new AssetBundleBuild() {
                            assetBundleName = assetBundleName,
                            assetNames = assetPaths
                        });
                    }

                    foreach (var buildTarget in buildTargets) {
                        var st = Stopwatch.StartNew();
                        Debug.Log($"Building bundles for platform {buildTarget}");
                        var buildPath = Path.Join(AssetBridge.PackagesPath, $"{packageDoc.id}_v{packageDoc.version}",
                            buildTarget.ToString());
                        if (!Directory.Exists(buildPath)) {
                            Directory.CreateDirectory(buildPath);
                        }

                        BuildPipeline.BuildAssetBundles(
                            buildPath,
                            builds.ToArray(),
                            CreateAssetBundles.BUILD_OPTIONS,
                            buildTarget
                        );
                        Debug.Log($"Finished building bundles for platform {buildTarget} in {st.ElapsedMilliseconds} ms");
                    }
                }

                var importsFolder = Path.Join("Assets", "Bundles", "Imports");
                var sourceAssetsFolder = Path.Join(importsFolder, packageDoc.id);
                var typesFolder = Path.Join(Path.Join("Assets", "Bundles", "Types~"), packageDoc.id);

                if (!Directory.Exists(Path.Join(Application.persistentDataPath, "Uploads"))) {
                    Directory.CreateDirectory(Path.Join(Application.persistentDataPath, "Uploads"));
                }

                var zippedSourceAssetsZipPath = Path.Join(Application.persistentDataPath, "Uploads", packageDoc.id + ".zip");
                if (Directory.Exists(zippedSourceAssetsZipPath)) {
                    Directory.Delete(zippedSourceAssetsZipPath);
                }
                var sourceAssetsZip = new ZipFile();
                sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Client"), "Client");
                sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Shared"), "Shared");
                sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Server"), "Server");
                sourceAssetsZip.AddDirectory(typesFolder, "Types");

                sourceAssetsZip.Save(zippedSourceAssetsZipPath);

                List<IMultipartFormSection> formData = new()
                {
                    new MultipartFormDataSection("packageId", packageDoc.id),
                    new MultipartFormDataSection("minPlayerVersion", "0"),
                };

                var sourceBytes = File.ReadAllBytes(zippedSourceAssetsZipPath);
                formData.Add(new MultipartFormFileSection(
                    "source.zip",
                    sourceBytes,
                    zippedSourceAssetsZipPath,
                    "multipart/form-data")
                );
                foreach (var builtTarget in buildTargets) {
                    foreach (var assetBundleFile in assetBundleFiles) {
                        var buildFolder = Path.Join(AssetBridge.PackagesPath,
                            $"{packageDoc.id}_v{packageDoc.version}", builtTarget.ToString());
                        var fileName = packageDoc.id.ToLower() + "_" + assetBundleFile.ToLower();
                        var bundleFilePath = Path.Join(buildFolder, fileName);
                        if (!File.Exists(bundleFilePath)) {
                            continue;
                        }
                        var assetBundleFileBytes = File.ReadAllBytes(bundleFilePath);
                        formData.Add(new MultipartFormFileSection(
                            assetBundleFile.ToLower(),
                            assetBundleFileBytes,
                            bundleFilePath.ToLower(),
                            "multipart/form-data"
                        ));

                        var manifestFileBytes = File.ReadAllBytes(bundleFilePath + ".manifest");
                        formData.Add(new MultipartFormFileSection(
                            assetBundleFile.ToLower() + ".manifest",
                            manifestFileBytes,
                            bundleFilePath.ToLower() + ".manifest",
                            "multipart/form-data"
                        ));
                    }
                }

                UnityWebRequest req = UnityWebRequest.Post($"{deploymentUrl}/package-versions/upload", formData);
                req.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
                EditorCoroutines.Execute(Upload(req, packageDoc));
                EditorCoroutines.Execute(WatchUploadStatus(req, packageDoc));
            } catch (Exception e) {
                Debug.LogError(e);
                packageUploadProgress.Remove(packageDoc.id);
            }
        }

        private static IEnumerator Upload(UnityWebRequest req, AirshipPackageDocument packageDoc) {
            packageUploadProgress[packageDoc.id] = "Uploading (0%)";
            var res = req.SendWebRequest();

            while (!req.isDone)
            {
                yield return res;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("Status: " + req.result);
                Debug.Log("Error : " + req.error);
                Debug.Log("Res: " + req.downloadHandler.text);
                Debug.Log("Err: " + req.downloadHandler.error);

            }
            else
            {
                Debug.Log("Res: " + req.downloadHandler.text);

                var response = JsonUtility.FromJson<PublishPackageResponse>(req.downloadHandler.text);
                Debug.Log("Published version " + response.assetVersionNumber);
                packageDoc.version = response.assetVersionNumber + "";
            }
        }

        private static IEnumerator WatchUploadStatus(UnityWebRequest req, AirshipPackageDocument packageDoc)
        {
            long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000;
            long lastTime = 0;
            while (!req.isDone)
            {
                long timeSince = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000) - startTime;
                if (timeSince != lastTime)
                {
                    if (req.uploadProgress < 1) {
                        var percent = Math.Floor(req.uploadProgress * 100);
                        packageUploadProgress[packageDoc.id] = $"Uploading ({percent}%)";
                        Debug.Log("Uploading... (" + percent + "%)");
                    }
                    else
                    {
                        Debug.Log("Waiting for server to process...");
                    }
                    lastTime = timeSince;
                    continue;
                }
                yield return null;
            }

            packageUploadProgress.Remove(packageDoc.id);
        }

        public static IEnumerator DownloadPackage(string packageId, string version) {
            // Types
            UnityWebRequest sourceZipRequest;
            string typesDownloadPath;
            {
                var url = $"{cdnUrl}/package/{packageId}/{version}/source.zip";
                typesDownloadPath = Path.Combine(Application.persistentDataPath, "EditorTemp", packageId + "Source.zip");
                if (File.Exists(typesDownloadPath)) {
                    File.Delete(typesDownloadPath);
                }
                sourceZipRequest = new UnityWebRequest(url);
                sourceZipRequest.downloadHandler = new DownloadHandlerFile(typesDownloadPath);
            }

            yield return new WaitUntil(() => sourceZipRequest.isDone);

            if (sourceZipRequest.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download package. Error: " + sourceZipRequest.error);
                yield break;
            }

            var typesDir = Path.Combine("Assets", "Bundles", "Types~", packageId + "Test");
            if (!Directory.Exists(typesDir)) {
                Directory.CreateDirectory(typesDir);
            }
            System.IO.Compression.ZipFile.ExtractToDirectory(typesDownloadPath, typesDir);
        }

        public static void UpdateToLatest(string packageId) {

        }

        public static void UpdateToVersion(string packageId, string version) {

        }
    }
}