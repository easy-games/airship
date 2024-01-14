using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Code.Bootstrap;
using Code.GameBundle;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Proyecto26;
using RSG;
using Unity.VisualScripting.IonicZip;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using Formatting = Unity.Plastic.Newtonsoft.Json.Formatting;
using ZipFile = Unity.VisualScripting.IonicZip.ZipFile;

namespace Editor.Packages {
    public class AirshipPackagesWindow : EditorWindow {
        private GameConfig gameConfig;
        private Dictionary<string, string> packageUploadProgress = new();
        private Dictionary<string, bool> packageVersionToggleBools = new();
        public static string deploymentUrl = "https://deployment-service-fxy2zritya-uc.a.run.app";
        public static string cdnUrl = "https://gcdn-staging.easy.gg";

        private bool createFoldoutOpened = false;
        private string createPackageId = "PackageId";
        private bool addFoldoutOpened = false;
        private bool addVersionToggle = false;
        private string addPackageId = "PackageId";
        private string addPackageVersion = "0";

        public static List<AirshipPackageDocument> defaultPackages = new List<AirshipPackageDocument>() {
            new() {
                id = "@easy/core",
                localSource = false,
                game = false,
                version = "",
                defaultPackage = true,
                forceLatestVersion = true
            },
        };

        public static string[] assetBundleFiles = {
            "Shared/Resources",
            "Shared/Scenes",
            "Client/Resources",
            "Client/Scenes",
            "Server/Resources",
            "Server/Scenes"
        };

        [MenuItem("Airship/Packages")]
        public static void ShowWindow() {
            var window = EditorWindow.GetWindow(typeof(AirshipPackagesWindow), false, "Airship Packages", true);
            window.minSize = new Vector2(400, 550);
        }

        private void OnEnable() {
            this.gameConfig = GameConfig.Load();
        }

        private void Awake() {
            this.createFoldoutOpened = false;
            this.createPackageId = "PackageId";
            this.addFoldoutOpened = false;
            this.addVersionToggle = false;
            this.addPackageId = "PackageId";
            this.addPackageVersion = "0";
        }

        private void OnGUI() {
            GUILayout.Label("Packages", EditorStyles.largeLabel);
            AirshipEditorGUI.HorizontalLine();
            foreach (var package in this.gameConfig.packages) {
                packageVersionToggleBools.TryAdd(package.id, false);

                GUILayout.BeginHorizontal();
                GUILayout.Label(package.id, new GUIStyle(GUI.skin.label) { fixedWidth = 160 });
                GUILayout.Label("v" + package.version, new GUIStyle(GUI.skin.label) { fixedWidth = 35 });
                var localSourceStyle = new GUIStyle(GUI.skin.label);
                localSourceStyle.fontStyle = FontStyle.Italic;
                GUILayout.Label( package.localSource ? "Local Source" : "", localSourceStyle, GUILayout.Width(100));
                if (package.localSource) {
                    if (packageUploadProgress.TryGetValue(package.id, out var progress)) {
                        GUILayout.Label(progress);
                    } else {
                        if (GUILayout.Button("Publish")) {
                            PublishPackage(package, false);
                        }
                        // if (GUILayout.Button("⬆️ Upload Only")) {
                        //     this.PublishPackage(package, true);
                        // }
                    }
                } else {
                    GUILayout.BeginVertical();
                    if (GUILayout.Button("Redownload")) {
                        EditorCoroutines.Execute(DownloadPackage(package.id, package.version));
                    }

                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Update to Latest")) {
                        EditorCoroutines.Execute(DownloadLatestVersion(package.id));
                    }

                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Remove")) {
                        RemovePackage(package.id);
                    }

                    GUILayout.EndVertical();
                }

                GUILayout.EndHorizontal();

                if (!package.localSource) {
                    packageVersionToggleBools[package.id] =
                        EditorGUILayout.BeginFoldoutHeaderGroup(packageVersionToggleBools[package.id], "Change Version",
                            null, null);
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
                            EditorCoroutines.Execute(DownloadPackage(package.id, version + ""));
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndFoldoutHeaderGroup();
                }

                AirshipEditorGUI.HorizontalLine();
            }

            EditorGUILayout.Space(10);

            // Add Existing
            this.addFoldoutOpened =
                EditorGUILayout.BeginFoldoutHeaderGroup(addFoldoutOpened, new GUIContent("Add Package"));
            if (this.addFoldoutOpened) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(12);
                EditorGUILayout.BeginVertical();
                this.addPackageId = EditorGUILayout.TextField("Package ID", this.addPackageId);
                this.addVersionToggle = EditorGUILayout.BeginToggleGroup("Version", this.addVersionToggle);
                if (this.addVersionToggle) {
                    this.addPackageVersion = EditorGUILayout.TextField("Version", this.addPackageVersion);
                }
                EditorGUILayout.EndToggleGroup();
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Add Package", GUILayout.Width(150))) {
                    if (this.addVersionToggle) {
                        EditorCoroutines.Execute(DownloadPackage(this.addPackageId, this.addPackageVersion));
                    } else {
                        EditorCoroutines.Execute(DownloadLatestVersion(this.addPackageId));
                    }
                }
                GUILayout.Space(10);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Create
            this.createFoldoutOpened =
                EditorGUILayout.BeginFoldoutHeaderGroup(createFoldoutOpened, new GUIContent("Create New Package"));
            if (this.createFoldoutOpened) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(12);
                EditorGUILayout.BeginVertical();
                this.createPackageId = EditorGUILayout.TextField("Package ID", this.createPackageId);
                EditorGUILayout.LabelField("Example: @Easy/Survival");
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Create Package", GUILayout.Width(150))) {
                    EditorCoroutines.Execute(CreateNewLocalSourcePackage(this.createPackageId));
                }
                EditorGUILayout.Space(10);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        public void PublishPackage(AirshipPackageDocument packageDoc, bool skipBuild) {
            try {
                List<AirshipPlatform> platforms = new();
                foreach (var platform in AirshipPlatformUtil.livePlatforms) {
                    platforms.Add(platform);
                }

                CreateAssetBundles.FixBundleNames();
                if (!skipBuild) {
                    packageUploadProgress[packageDoc.id] = "Building...";
                    Repaint();

                    List<AssetBundleBuild> builds = new();
                    foreach (var assetBundleFile in assetBundleFiles) {
                        var assetBundleName = $"{packageDoc.id}_{assetBundleFile}".ToLower();
                        var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
                        var addressableNames = assetPaths.Select((p) => p.ToLower()).ToArray();
                        builds.Add(new AssetBundleBuild() {
                            assetBundleName = assetBundleName,
                            assetNames = assetPaths,
                            addressableNames = addressableNames
                        });
                    }

                    foreach (var platform in platforms) {
                        var st = Stopwatch.StartNew();
                        Debug.Log($"Building {platform} bundles...");
                        var buildPath = Path.Join(AssetBridge.PackagesPath, $"{packageDoc.id}_vLocalBuild",
                            platform.ToString());
                        if (!Directory.Exists(buildPath)) {
                            Directory.CreateDirectory(buildPath);
                        }

                        try {
                            var manifest = BuildPipeline.BuildAssetBundles(
                                buildPath,
                                builds.ToArray(),
                                CreateAssetBundles.BUILD_OPTIONS,
                                AirshipPlatformUtil.ToBuildTarget(platform)
                            );
                            Debug.Log("Manifest: " + manifest);
                        }
                        catch (Exception e) {
                            Debug.LogError($"Failed to build ${platform} platform. Make sure you have installed the required editor modules.");
                            throw e;
                        }
                        Debug.Log($"Finished building {platform} bundles in {st.Elapsed.TotalSeconds} seconds.");
                    }
                }

                var importsFolder = Path.Join("Assets", "Bundles");
                var sourceAssetsFolder = Path.Join(importsFolder, packageDoc.id);
                var typesFolder = Path.Join(Path.Join("Assets", "Bundles", "Types~"), packageDoc.id);

                if (!Directory.Exists(Path.Join(Application.persistentDataPath, "Uploads"))) {
                    Directory.CreateDirectory(Path.Join(Application.persistentDataPath, "Uploads"));
                }

                // Create org scope folder (@Easy)
                string orgScopePath = Path.Join(Application.persistentDataPath, "Uploads",
                    packageDoc.id.Split("/")[0]);
                if (!Directory.Exists(orgScopePath)) {
                    Directory.CreateDirectory(orgScopePath);
                }

                var zippedSourceAssetsZipPath =
                    Path.Join(orgScopePath, packageDoc.id.Split("/")[1] + ".zip");
                if (Directory.Exists(zippedSourceAssetsZipPath)) {
                    Directory.Delete(zippedSourceAssetsZipPath);
                }

                var sourceAssetsZip = new ZipFile();
                sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Client"), "Client");
                sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Shared"), "Shared");
                sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Server"), "Server");
                // Some packages don't have any code. So Types folder is optional.
                // Example: @Easy/CoreMaterials
                if (Directory.Exists(typesFolder)) {
                    sourceAssetsZip.AddDirectory(typesFolder, "Types");
                }

                sourceAssetsZip.Save(zippedSourceAssetsZipPath);

                List<IMultipartFormSection> formData = new() {
                    new MultipartFormDataSection("packageSlug", packageDoc.id),
                    new MultipartFormDataSection("minPlayerVersion", "0"),
                };

                var sourceBytes = File.ReadAllBytes(zippedSourceAssetsZipPath);
                formData.Add(new MultipartFormFileSection(
                    "source.zip",
                    sourceBytes,
                    zippedSourceAssetsZipPath,
                    "multipart/form-data")
                );
                foreach (var platform in platforms) {
                    foreach (var assetBundleFile in assetBundleFiles) {
                        var buildFolder = Path.Join(AssetBridge.PackagesPath,
                            $"{packageDoc.id}_vLocalBuild", platform.ToString());
                        var fileName = packageDoc.id.ToLower() + "_" + assetBundleFile.ToLower();
                        var bundleFilePath = Path.Join(buildFolder, fileName);
                        if (!File.Exists(bundleFilePath)) {
                            continue;
                        }

                        var assetBundleFileBytes = File.ReadAllBytes(bundleFilePath);
                        formData.Add(new MultipartFormFileSection(
                            platform + "/" + assetBundleFile.ToLower(),
                            assetBundleFileBytes,
                            bundleFilePath.ToLower(),
                            "multipart/form-data"
                        ));

                        // var manifestFileBytes = File.ReadAllBytes(bundleFilePath + ".manifest");
                        // formData.Add(new MultipartFormFileSection(
                        //     platform + "/" + assetBundleFile.ToLower() + ".manifest",
                        //     manifestFileBytes,
                        //     bundleFilePath.ToLower() + ".manifest",
                        //     "multipart/form-data"
                        // ));
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

        private IEnumerator Upload(UnityWebRequest req, AirshipPackageDocument packageDoc) {
            packageUploadProgress[packageDoc.id] = "Uploading (0%)";
            var res = req.SendWebRequest();

            while (!req.isDone) {
                yield return res;
            }

            if (req.result != UnityWebRequest.Result.Success) {
                Debug.Log("Status: " + req.result);
                Debug.Log("Error : " + req.error);
                Debug.Log("Res: " + req.downloadHandler.text);
                Debug.Log("Err: " + req.downloadHandler.error);

            } else {
                Debug.Log("Res: " + req.downloadHandler.text);

                var response = JsonUtility.FromJson<PublishPackageResponse>(req.downloadHandler.text);
                Debug.Log("Published version " + response.assetVersionNumber);
                packageDoc.version = response.assetVersionNumber + "";
                ShowNotification(
                    new GUIContent($"Successfully published {packageDoc.id} v{response.assetVersionNumber}"));
                EditorUtility.SetDirty(gameConfig);
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
            }
        }

        private IEnumerator WatchUploadStatus(UnityWebRequest req, AirshipPackageDocument packageDoc) {
            long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000;
            long lastTime = 0;
            while (!req.isDone) {
                long timeSince = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000) - startTime;
                if (timeSince != lastTime) {
                    if (req.uploadProgress < 1) {
                        var percent = Math.Floor(req.uploadProgress * 100);
                        packageUploadProgress[packageDoc.id] = $"Uploading ({percent}%)";
                        Repaint();
                        Debug.Log("Uploading... (" + percent + "%)");
                    } else {
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
            Debug.Log($"Downloading {packageId}...");
            var gameConfig = GameConfig.Load();

            version = version.ToLower().Replace("v", "");

            // Types
            UnityWebRequest sourceZipRequest;
            string sourceZipDownloadPath;
            {
                var url = $"{cdnUrl}/package/{packageId.ToLower()}/{version}/source.zip";
                sourceZipDownloadPath =
                    Path.Join(Application.persistentDataPath, "EditorTemp", packageId + "Source.zip");
                if (File.Exists(sourceZipDownloadPath)) {
                    File.Delete(sourceZipDownloadPath);
                }

                sourceZipRequest = new UnityWebRequest(url);
                sourceZipRequest.downloadHandler = new DownloadHandlerFile(sourceZipDownloadPath);
                sourceZipRequest.SendWebRequest();
            }

            yield return new WaitUntil(() => sourceZipRequest.isDone);

            if (sourceZipRequest.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download package. Error: " + sourceZipRequest.error);
                yield break;
            }

            var packageAssetsDir = Path.Combine("Assets", "Bundles", packageId);
            var typesDir = Path.Combine("Assets", "Bundles", "Types~", packageId);
            if (!Directory.Exists(typesDir)) {
                Directory.CreateDirectory(typesDir);
            }

            if (Directory.Exists(packageAssetsDir)) {
                Directory.Delete(packageAssetsDir, true);
            }

            Directory.CreateDirectory(packageAssetsDir);

            if (Directory.Exists(typesDir)) {
                Directory.Delete(typesDir, true);
            }

            var zip = System.IO.Compression.ZipFile.OpenRead(sourceZipDownloadPath);
            foreach (var entry in zip.Entries) {
                string pathToWrite;
                if (entry.FullName.StartsWith("Client") || entry.FullName.StartsWith("Shared") ||
                    entry.FullName.StartsWith("Server")) {
                    pathToWrite = Path.Join(packageAssetsDir, entry.FullName);
                } else if (entry.FullName.StartsWith("Types")) {
                    pathToWrite = Path.Join(typesDir, entry.FullName.Replace("Types/", ""));
                } else {
                    continue;
                }

                if (Path.IsPathRooted(pathToWrite) || pathToWrite.Contains("..")) {
                    Debug.LogWarning("Skipping malicious file: " + pathToWrite);
                    continue;
                }

                if (!Directory.Exists(Path.GetDirectoryName(pathToWrite))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(pathToWrite));
                }

                // Folders have a Name of ""
                if (entry.Name != "") {
                    // Debug.Log($"Extracting {entry.FullName} to {pathToWrite}");
                    entry.ExtractToFile(pathToWrite, true);
                } else {
                    if (!Directory.Exists(pathToWrite)) {
                        Directory.CreateDirectory(pathToWrite);
                    }
                }
            }


            var existingPackageDoc = gameConfig.packages.Find((p) => p.id == packageId);
            if (existingPackageDoc != null) {
                existingPackageDoc.version = version;
            } else {
                var packageDoc = new AirshipPackageDocument() {
                    id = packageId,
                    version = version,
                };
                gameConfig.packages.Add(packageDoc);
            }
            EditorUtility.SetDirty(gameConfig);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            Debug.Log($"Finished downloading {packageId} v{version}");
            // ShowNotification(new GUIContent($"Successfully installed {packageId} v{version}"));
        }

        public static IPromise<PackageLatestVersionResponse> GetLatestPackageVersion(string packageId) {
            var url = $"{deploymentUrl}/package-versions/packageSlug/{packageId}";

            return RestClient.Get<PackageLatestVersionResponse>(new RequestHelper() {
                Uri = url,
                Headers = GetDeploymentServiceHeaders()
            });
        }

        private static Dictionary<string, string> GetDeploymentServiceHeaders() {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            dict.Add("Authorization", "Bearer " + AuthConfig.instance.deployKey);
            return dict;
        }

        public static IEnumerator DownloadLatestVersion(string packageId) {
            var url = $"{deploymentUrl}/package-versions/packageSlug/{packageId}";
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

            Debug.Log("Response: " + request.downloadHandler.text);
            PackageLatestVersionResponse response =
                JsonUtility.FromJson<PackageLatestVersionResponse>(request.downloadHandler.text);

            Debug.Log($"Found latest version of {packageId}: v{response.package.assetVersionNumber}");
            yield return DownloadPackage(packageId, response.package.assetVersionNumber + "");
        }

        public IEnumerator CreateNewLocalSourcePackage(string fullPackageId) {
            var splitId = fullPackageId.Split("/");

            if (splitId.Length != 2) {
                EditorUtility.DisplayDialog("Invalid Package ID",
                    "Please include your organization scope and unique package id. Example: @Easy/Survival", "Okay");
            }

            var orgId = splitId[0];
            var packageId = splitId[1];

            var orgDir = Path.Combine("Assets", "Bundles", orgId);
            if (!Directory.Exists(orgId)) {
                Directory.CreateDirectory(orgDir);
            }

            var assetsDir = Path.Combine(orgDir, packageId);
            if (Directory.Exists(assetsDir)) {
                Debug.LogError($"Package folder \"{packageId}\" already exists.");
                ShowNotification(new GUIContent($"Error: Package folder \"{packageId}\" already exists."));
                yield break;
            }

            var existingPackage = this.gameConfig.packages.Find((p) => p.id == packageId);
            if (existingPackage != null) {
                ShowNotification(new GUIContent($"Error: Package \"{packageId}\" already exists!"));
                Debug.LogError($"Error: Package \"{packageId}\" already exists!");
                yield break;
            }

            var url = "https://github.com/easy-games/ExamplePackage/zipball/main";
            var request = new UnityWebRequest(url);
            var zipDownloadPath = Path.Join(Application.persistentDataPath, "EditorTemp", "PackageTemplate.zip");
            request.downloadHandler = new DownloadHandlerFile(zipDownloadPath);
            request.SendWebRequest();

            yield return new WaitUntil(() => request.isDone);

            if (request.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download template package: " + request.error);
                Debug.LogError("result=" + request.result);
                yield break;
            }

            var zip = System.IO.Compression.ZipFile.OpenRead(zipDownloadPath);

            foreach (var entry in zip.Entries) {
                if (entry.Name == "") continue; // folder

                var split = entry.FullName.Split(Path.DirectorySeparatorChar);
                if (split.Length == 0) continue;
                var root = split[0] + Path.DirectorySeparatorChar;

                var pathToWrite = Path.Join(assetsDir, entry.FullName.Replace(root, ""));
                if (!Directory.Exists(Path.GetDirectoryName(pathToWrite))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(pathToWrite));
                }

                // Debug.Log("Extracting to " + pathToWrite);
                entry.ExtractToFile(pathToWrite);
            }

            this.RenamePackage(assetsDir, packageId);

            AssetDatabase.Refresh();

            var packageDoc = new AirshipPackageDocument() {
                id = fullPackageId,
                version = "0",
                localSource = true,
            };
            this.gameConfig.packages.Add(packageDoc);

            ShowNotification(new GUIContent($"Successfully created package {packageId}"));
        }

        public void RenamePackage(string path, string newId) {
            foreach (var child in Directory.GetDirectories(path)) {
                if (child.Contains("~")) {
                    var packageJson = File.ReadAllText(Path.Join(child, "package.json"));
                    JObject jsonObj = JsonConvert.DeserializeObject(packageJson) as JObject;
                    JToken nameToken = jsonObj.SelectToken("name");
                    nameToken.Replace(newId);
                    jsonObj["name"] = newId;
                    string output = jsonObj.ToString(Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(Path.Join(child, "package.json"), output);

                    Directory.Move(child, Path.Join(Path.GetDirectoryName(child), newId + "~"));
                    break;
                }
            }
        }

        public void RemovePackage(string packageId) {
            EditorCoroutines.Execute(RemovePackageOneFrameLater(packageId));
        }

        private IEnumerator RemovePackageOneFrameLater(string packageId) {
            yield return null;
            var packageDoc = this.gameConfig.packages.Find((p) => p.id == packageId);
            if (packageDoc != null) {
                this.gameConfig.packages.Remove(packageDoc);
            }

            var assetsDir = Path.Combine("Assets", "Bundles", packageId);
            if (Directory.Exists(assetsDir)) {
                Directory.Delete(assetsDir, true);
            }

            var typesDir = Path.Combine("Assets", "Bundles", "Types~", packageId);
            if (Directory.Exists(typesDir)) {
                Directory.Delete(typesDir, true);
            }

            ShowNotification(new GUIContent($"Removed Package \"{packageId}\""));
        }
    }
}
