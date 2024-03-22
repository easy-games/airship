using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Code.Bootstrap;
using Code.GameBundle;
using Code.Platform.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Proyecto26;
using RSG;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using ZipFile = Unity.VisualScripting.IonicZip.ZipFile;

namespace Editor.Packages {
    public class AirshipPackagesWindow : EditorWindow {
        private static Dictionary<string, float> urlUploadProgress = new();
        private static Dictionary<string, double> packageUpdateStartTime = new();

        private GameConfig gameConfig;
        private Dictionary<string, string> packageUploadProgress = new();
        private Dictionary<string, bool> packageVersionToggleBools = new();
        public static string deploymentUrl = "https://deployment-service-fxy2zritya-uc.a.run.app";
        public static string cdnUrl = "https://gcdn-staging.easy.gg";

        private bool createFoldoutOpened = false;
        private string createPackageId = "PackageId";
        private bool addFoldoutOpened = false;
        private bool addVersionToggle = false;
        private bool publishOptionsFoldoutOpened = false;
        private bool publishOptionUseCache = true;
        private string addPackageId = "PackageId";
        private string addPackageVersion = "0";

        /**
         * "game" if building a game.
         */
        public static string buildingPackageId;

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
            window.minSize = new Vector2(430, 550);
        }

        private void OnEnable() {
            this.gameConfig = GameConfig.Load();
        }

        private void Awake() {
            this.createFoldoutOpened = false;
            this.createPackageId = "PackageId";
            this.addFoldoutOpened = false;
            this.addVersionToggle = false;
            this.publishOptionsFoldoutOpened = false;
            this.publishOptionUseCache = true;
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
                GUILayout.Label("v" + package.codeVersion, new GUIStyle(GUI.skin.label) { fixedWidth = 35 });
                var localSourceStyle = new GUIStyle(GUI.skin.label);
                localSourceStyle.fontStyle = FontStyle.Italic;

                GUILayout.Label( package.localSource ? "Local Source" : "", localSourceStyle, GUILayout.Width(100));
                if (package.localSource) {
                    if (packageUploadProgress.TryGetValue(package.id, out var progress)) {
                        GUILayout.Label(progress);
                    } else {
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginVertical(GUILayout.Width(100));
                        if (GUILayout.Button("Publish Code")) {
                            EditorCoroutines.Execute(PublishPackage(package, true, false));
                        }
                        if (GUILayout.Button("Publish All")) {
                            EditorCoroutines.Execute(PublishPackage(package, false, true));
                        }
                        GUILayout.EndVertical();
                        GUILayout.FlexibleSpace();
                        // if (GUILayout.Button("⬆️ Upload Only")) {
                        //     this.PublishPackage(package, true);
                        // }
                    }
                } else {
                    GUILayout.BeginVertical();
                    if (GUILayout.Button("Redownload")) {
                        EditorCoroutines.Execute(DownloadPackage(package.id, package.codeVersion, package.assetVersion));
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
                        int codeVersion = 0;
                        try {
                            codeVersion = Int32.Parse(package.codeVersion);
                        } catch (Exception e) {
                            Debug.LogError(e);
                        }
                        int assetVersion = 0;
                        try {
                            codeVersion = Int32.Parse(package.assetVersion);
                        } catch (Exception e) {
                            Debug.LogError(e);
                        }

                        EditorGUILayout.LabelField("Double version is temporary. Sorry!");
                        var codeVersionInt = EditorGUILayout.IntField("Code Version", codeVersion);
                        var assetVersionInt = EditorGUILayout.IntField("Asset Version", assetVersion);
                        if (GUILayout.Button("Install")) {
                            EditorCoroutines.Execute(DownloadPackage(package.id, codeVersionInt + "", assetVersionInt + ""));
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
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Add Package", GUILayout.Width(150))) {
                    if (this.addVersionToggle) {
                        // EditorCoroutines.Execute(DownloadPackage(this.addPackageId, this.addPackageVersion));
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

            // Publish Options
            this.publishOptionsFoldoutOpened = EditorGUILayout.BeginFoldoutHeaderGroup(this.publishOptionsFoldoutOpened,
                new GUIContent("Publish Options"));
            if (this.publishOptionsFoldoutOpened) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.Space(12);
                EditorGUILayout.BeginVertical();
                this.publishOptionUseCache = EditorGUILayout.Toggle("Use Cache", this.publishOptionUseCache);
                EditorGUILayout.Space(10);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
        }

        public IEnumerator PublishPackage(AirshipPackageDocument packageDoc, bool skipBuild, bool includeAssets) {
            var devKey = AuthConfig.instance.deployKey;
            
            var deployKeySize = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".Length;
            if (devKey == null || devKey.Length != deployKeySize) {
                Debug.LogError("Invalid deploy key detected. Please verify your deploy key is correct. (Airship -> Configuration)");
                yield break;
            } 

            var didVerify = AirshipPackagesWindow.VerifyBuildModules();
            if (!didVerify) {
                Debug.LogErrorFormat("Missing build modules detected. Install missing modules in Unity Hub and restart Unity to publish package ({0}).", packageDoc.id);
                yield break;
            }

            // Sort the current platform first to speed up build time
            List<AirshipPlatform> platforms = new();
            var currentPlatform = AirshipPlatformUtil.GetLocalPlatform();
            if (AirshipPlatformUtil.livePlatforms.Contains(currentPlatform)) {
                platforms.Add(currentPlatform);
            }
            foreach (var platform in AirshipPlatformUtil.livePlatforms) {
                if (platform == currentPlatform) continue;
                platforms.Add(platform);
            }

            CreateAssetBundles.FixBundleNames();
            if (!skipBuild) {
                packageUploadProgress[packageDoc.id] = "Building...";
                Repaint();
                yield return null; // give time to repaint

                List<AssetBundleBuild> builds = new();
                foreach (var assetBundleFile in assetBundleFiles) {
                    var assetBundleName = $"{packageDoc.id}_{assetBundleFile}".ToLower();
                    var assetPaths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
                    var addressableNames = assetPaths.Select((p) => p.ToLower())
                        .Where((p) => !(p.EndsWith(".lua") || p.EndsWith(".json~")))
                        .ToArray();
                    // Debug.Log("Bundle " + assetBundleName + ":");
                    // foreach (var path in addressableNames) {
                    //     Debug.Log("  - " + path);
                    // }
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

                    // var tasks = DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleBuiltInShaderExtraction);
                    var buildTarget = AirshipPlatformUtil.ToBuildTarget(platform);
                    var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
                    if (platform is AirshipPlatform.Windows or AirshipPlatform.Mac or AirshipPlatform.Linux) {
                        buildTargetGroup = BuildTargetGroup.Standalone;
                    }

                    var buildParams = new BundleBuildParameters(
                        buildTarget,
                        buildTargetGroup,
                        buildPath
                    );
                    buildParams.UseCache = this.publishOptionUseCache;
                    buildParams.BundleCompression = BuildCompression.LZ4;
                    EditorUserBuildSettings.switchRomCompressionType = SwitchRomCompressionType.Lz4;
                    var buildContent = new BundleBuildContent(builds);
                    AirshipPackagesWindow.buildingPackageId = packageDoc.id;
                    ReturnCode returnCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out var result);
                    if (returnCode != ReturnCode.Success) {
                        Debug.LogError("Failed to build asset bundles. ReturnCode=" + returnCode);
                        packageUploadProgress.Remove(packageDoc.id);
                        yield break;
                    }

                    // var manifest = BuildPipeline.BuildAssetBundles(
                    //     buildPath,
                    //     builds.ToArray(),
                    //     CreateAssetBundles.BUILD_OPTIONS,
                    //     AirshipPlatformUtil.ToBuildTarget(platform)
                    // );
                    // Debug.Log("Manifest: " + manifest);

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

            packageUploadProgress[packageDoc.id] = "Zipping source...";
            Repaint();
            yield return null; // give time to repaint
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

            packageUploadProgress[packageDoc.id] = "Starting upload...";
            Repaint();
            yield return null; // give time to repaint
            // Create deployment
            DeploymentDto deploymentDto;
            {
                UnityWebRequest req = UnityWebRequest.Post(
                    $"{AirshipUrl.DeploymentService}/package-versions/create-deployment", JsonUtility.ToJson(
                        new CreatePackageDeploymentDto() {
                            packageSlug = packageDoc.id.ToLower(),
                            deployCode = true,
                            deployAssets = includeAssets
                        }), "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + devKey);
                yield return req.SendWebRequest();
                while (!req.isDone) {
                    yield return null;
                }

                if (req.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("Failed to create deployment: " + req.error + " " + req.downloadHandler.text);
                    packageUploadProgress.Remove(packageDoc.id);
                    yield break;
                }

                Debug.Log("Deployment: ");
                Debug.Log(req.downloadHandler.text);
                deploymentDto = JsonUtility.FromJson<DeploymentDto>(req.downloadHandler.text);
            }

            // code.zip
            AirshipEditorUtil.EnsureDirectory(Path.Join(Application.persistentDataPath, "Uploads"));
            var codeZipPath = Path.Join(Application.persistentDataPath, "Uploads", "code.zip");
            {
                var st = Stopwatch.StartNew();
                var binaryFileGuids = AssetDatabase.FindAssets("t:BinaryFile");
                var paths = new List<string>();
                var scopedId = packageDoc.id.ToLower();
                foreach (var guid in binaryFileGuids) {
                    var path = AssetDatabase.GUIDToAssetPath(guid).ToLower();
                    if (path.StartsWith("assets/bundles/" + scopedId + "/") || path.StartsWith("assets/bundles/" + scopedId + "/") || path.StartsWith("assets/bundles/" + scopedId + "/")) {
                        paths.Add(path);
                    }
                }

                if (File.Exists(codeZipPath)) {
                    File.Delete(codeZipPath);
                }
                var codeZip = new ZipFile();
                foreach (var path in paths) {
                    var bytes = File.ReadAllBytes(path);
                    codeZip.AddEntry(path, bytes);

                    var jsonPath = path + ".json~";
                    if (File.Exists(jsonPath)) {
                        var jsonBytes = File.ReadAllBytes(jsonPath);
                        codeZip.AddEntry(jsonPath, "");
                    }
                }
                codeZip.Save(codeZipPath);

                Debug.Log("Created code.zip in " + st.ElapsedMilliseconds + " ms.");
            }

            var urls = deploymentDto.urls;
            var split = packageDoc.id.Split("/");
            var orgScope = split[0].ToLower();
            var packageIdOnly = split[1];
            var uploadList = new List<IEnumerator>() {
			    UploadSingleGameFile(urls.source, zippedSourceAssetsZipPath, packageDoc, true),
                UploadSingleGameFile(urls.code, codeZipPath, packageDoc, true),
            };
            if (includeAssets) {
                uploadList.AddRange(new List<IEnumerator>() {
                    UploadSingleGameFile(urls.Linux_client_resources, $"{AirshipPlatform.Linux}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    UploadSingleGameFile(urls.Linux_client_scenes, $"{AirshipPlatform.Linux}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    UploadSingleGameFile(urls.Linux_shared_resources, $"{AirshipPlatform.Linux}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    UploadSingleGameFile(urls.Linux_shared_scenes, $"{AirshipPlatform.Linux}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),
                    UploadSingleGameFile(urls.Linux_server_resources, $"{AirshipPlatform.Linux}/{orgScope}/{packageIdOnly}_server/resources", packageDoc),
                    UploadSingleGameFile(urls.Linux_server_scenes, $"{AirshipPlatform.Linux}/{orgScope}/{packageIdOnly}_server/scenes", packageDoc),

                    UploadSingleGameFile(urls.Mac_client_resources, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    UploadSingleGameFile(urls.Mac_client_scenes, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    UploadSingleGameFile(urls.Mac_shared_resources, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    UploadSingleGameFile(urls.Mac_shared_scenes, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),

                    UploadSingleGameFile(urls.Windows_client_resources, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    UploadSingleGameFile(urls.Windows_client_scenes, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    UploadSingleGameFile(urls.Windows_shared_resources, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    UploadSingleGameFile(urls.Windows_shared_scenes, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),

                    UploadSingleGameFile(urls.iOS_client_resources, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    UploadSingleGameFile(urls.iOS_client_scenes, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    UploadSingleGameFile(urls.iOS_shared_resources, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    UploadSingleGameFile(urls.iOS_shared_scenes, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),
                });
            }

            // wait for all
		    urlUploadProgress.Clear();
		    foreach (var co in uploadList) {
			    EditorCoroutines.Execute(co);
		    }

		    // skip frame so all coroutines can begin
		    yield return null;

		    // track progress
		    bool finishedUpload = false;
		    float totalProgress = 0;
		    long prevCheckTime = 0;
		    while (!finishedUpload) {
			    long diff = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000) - prevCheckTime;
			    if (diff < 1) {
				    yield return null;
				    continue;
			    }
			    prevCheckTime = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000);

			    totalProgress = 0;
			    finishedUpload = true;
			    foreach (var pair in urlUploadProgress) {
                    if (pair.Value <= -1) {
                        Debug.LogError("Upload failed.");
                        yield break;
                    }

                    if (pair.Value < 1) {
					    finishedUpload = false;
				    }
				    totalProgress += pair.Value;
			    }
			    totalProgress /= urlUploadProgress.Count;
			    Debug.Log("Upload Progress: " + Math.Floor(totalProgress * 100) + "%");
            }

		    Debug.Log("Completed upload. Finalizing publish...");

            // Complete deployment
            {
                // Debug.Log("Complete. GameId: " + gameConfig.gameId + ", assetVersionId: " + deploymentDto.version.assetVersionNumber);
                UnityWebRequest req = UnityWebRequest.Post(
                    $"{AirshipUrl.DeploymentService}/package-versions/complete-deployment", JsonUtility.ToJson(
                        new CompletePackageDeploymentDto() {
                            packageSlug = packageDoc.id,
                            packageVersionId = deploymentDto.version.packageVersionId,
                        }), "application/json");
                req.SetRequestHeader("Authorization", "Bearer " + devKey);
                yield return req.SendWebRequest();
                while (!req.isDone) {
                    yield return null;
                }

                if (req.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("Failed to complete deployment: " + req.error + " " + req.downloadHandler.text);
                    yield break;
                }
            }

            packageDoc.codeVersion = deploymentDto.version.codeVersionNumber.ToString();
            packageDoc.assetVersion = deploymentDto.version.assetVersionNumber.ToString();
            EditorUtility.SetDirty(gameConfig);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=#77f777>{packageDoc.id} published!</color>");
            packageUploadProgress.Remove(packageDoc.id);
            Repaint();
        }

        private static bool VerifyBuildModules() {
            var linux64 = ModuleUtil.IsModuleInstalled(BuildTarget.StandaloneLinux64);
            if (!linux64) {
                Debug.LogError("Linux Build Support (<b>Mono</b>) module not found.");
            }

            var mac = ModuleUtil.IsModuleInstalled(BuildTarget.StandaloneOSX);
            if (!mac) {
                Debug.LogError("Mac Build Support (<b>Mono</b>) module not found.");
            }

            var windows = ModuleUtil.IsModuleInstalled(BuildTarget.StandaloneWindows64);
            if (!windows) {
                Debug.LogError("Windows Build Support (<b>Mono</b>) module not found.");
            }

            var iOS = ModuleUtil.IsModuleInstalled(BuildTarget.iOS);
            if (!iOS) {
                Debug.LogError("iOS Build Support module not found.");
            }
            return linux64 && mac && windows && iOS;
        }

        private static IEnumerator UploadSingleGameFile(string url, string filePath, AirshipPackageDocument packageDoc, bool absoluteFilePath = false) {
            urlUploadProgress[url] = 0;
            var gameConfig = GameConfig.Load();
            var buildFolder = Path.Join(AssetBridge.PackagesPath,
                $"{packageDoc.id}_vLocalBuild");

            var bundleFilePath = buildFolder + "/" + filePath;
            if (absoluteFilePath) {
                bundleFilePath = filePath;
            }

            byte[] bytes;
            try {
                bytes = File.ReadAllBytes(bundleFilePath);
            } catch (Exception error) {
                Debug.LogError(error);
                urlUploadProgress[url] = -2;
                yield break;
            }

            List<IMultipartFormSection> formData = new();
            formData.Add(new MultipartFormFileSection(
                filePath,
                bytes,
                "bundle",
                "multipart/form-data"));

            var req = UnityWebRequest.Put(url, bytes);
            req.SetRequestHeader("x-goog-content-length-range", "0,200000000");
            yield return req.SendWebRequest();

            while (!req.isDone) {
                urlUploadProgress[url] = req.uploadProgress;
                yield return new WaitForSeconds(0.5f);
            }

            if (req.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to upload " + filePath + " " + req.result + " " + req.downloadHandler.text);
                urlUploadProgress[url] = -2;
            }

            urlUploadProgress[url] = 1;
        }

        public static IEnumerator DownloadPackage(string packageId, string codeVersion, string assetVersion) {
            if (packageUpdateStartTime.TryGetValue(packageId, out var updateTime)) {
                Debug.Log("Tried to download package while download is in progress. Skipping.");
                yield break;
            }

            packageUpdateStartTime[packageId] = EditorApplication.timeSinceStartup;

            Debug.Log($"Downloading {packageId}...");
            var gameConfig = GameConfig.Load();

            codeVersion = codeVersion.ToLower().Replace("v", "");

            // Types
            UnityWebRequest sourceZipRequest;
            string sourceZipDownloadPath;
            {
                var url = $"{cdnUrl}/package/{packageId.ToLower()}/code/{codeVersion}/source.zip";
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
                packageUpdateStartTime.Remove(packageId);
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

            using (var zip = System.IO.Compression.ZipFile.OpenRead(sourceZipDownloadPath)) {
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
            }
            
            // Add package to .gitignore
            
            var rootGitIgnore = $"{Path.GetDirectoryName(Application.dataPath)}/.gitignore";
            if (File.Exists(rootGitIgnore)) {
                try {
                    var lines = File.ReadLines(rootGitIgnore);

                    var srcIgnore = $"Assets/Bundles/{packageId}/*";
                    var metaIgnore = $"Assets/Bundles/{packageId}.meta";
                    var downloadSuccessIgnore = "**/airship_pkg_download_success.txt";
                    if (!lines.Contains(srcIgnore)) {
                        File.AppendAllLines(rootGitIgnore, new List<string>(){ $"\n{srcIgnore}" });
                    }
                    if (!lines.Contains(metaIgnore)) {
                        File.AppendAllLines(rootGitIgnore, new List<string>(){ $"\n{metaIgnore}" });
                    }
                    if (!lines.Contains(downloadSuccessIgnore)) {
                        File.AppendAllLines(rootGitIgnore, new List<string>(){ $"\n{downloadSuccessIgnore}" });
                    }
                } catch (Exception e) {
                    Debug.LogError("Errored while updating .gitignore: " + e);
                }
            }

            var existingPackageDoc = gameConfig.packages.Find((p) => p.id == packageId);
            if (existingPackageDoc != null) {
                existingPackageDoc.codeVersion = codeVersion;
                existingPackageDoc.assetVersion = assetVersion;
            } else {
                var packageDoc = new AirshipPackageDocument() {
                    id = packageId,
                    codeVersion = codeVersion,
                    assetVersion = assetVersion
                };
                gameConfig.packages.Add(packageDoc);
            }
            EditorUtility.SetDirty(gameConfig);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            packageUpdateStartTime.Remove(packageId);

            var downloadSuccessPath =
                Path.GetRelativePath(".", Path.Combine("Assets", "Bundles", packageId, "airship_pkg_download_success.txt"));
            File.WriteAllText(downloadSuccessPath, "success");

            Debug.Log($"Finished downloading {packageId} v{codeVersion}");
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

            Debug.Log($"Found latest version of {packageId}: v{response.package.codeVersionNumber}");
            yield return DownloadPackage(packageId, response.package.codeVersionNumber + "", response.package.assetVersionNumber + "");
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

                var split = entry.FullName.Split("/");
                if (split.Length == 0) continue;

                var root = split[0] + "/";

                var pathToWrite = Path.Join(assetsDir, entry.FullName.Replace(root, ""));
                pathToWrite = pathToWrite.Replace("ExamplePackage~", packageId + "~");
                
                var srcPath = packageId + Path.DirectorySeparatorChar + "src";
           
                if (pathToWrite.Contains(srcPath)) {
                    pathToWrite = pathToWrite.Replace(srcPath, packageId + Path.DirectorySeparatorChar);
                }
                if (!Directory.Exists(Path.GetDirectoryName(pathToWrite))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(pathToWrite));
                }
                entry.ExtractToFile(pathToWrite);
            }

            RenamePackage(assetsDir, orgId, packageId);
            this.UpdateTSConfig(assetsDir, orgId, packageId);

            AssetDatabase.Refresh();

            var packageDoc = new AirshipPackageDocument() {
                id = fullPackageId,
                codeVersion = "0",
                localSource = true,
            };
            this.gameConfig.packages.Add(packageDoc);

            ShowNotification(new GUIContent($"Successfully created package {packageId}"));
        }
        
        public static void RenamePackage(string path, string orgId, string packageId) {
            foreach (var child in Directory.GetDirectories(path)) {
                if (!child.Contains("~")) continue;
                var packageName = $"{orgId}/{packageId}";
                var packageJsonPath = Path.Join(child, "package.json");
                var packageJson = File.ReadAllText(packageJsonPath);
                var jsonObj = JsonConvert.DeserializeObject(packageJson) as JObject;
                var nameToken = jsonObj?.SelectToken("name");
                nameToken?.Replace(packageName);
                if (jsonObj != null) {
                    var output = jsonObj.ToString(Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(Path.Join(child, "package.json"), output);
                    // Not sure that we need this? Seems like the source and dest are always the same?
                    // Directory.Move(child, Path.Join(Path.GetDirectoryName(child), packageId + "~"));
                }
                break;
            }
        }

        private void UpdateTSConfig(string path, string orgId, string packageId) {
            foreach (var child in Directory.GetDirectories(path)) {
                if (!child.Contains("~")) continue;
                var packageName = $"{orgId}/{packageId}";
                var tsConfigPath = Path.Join(child, "tsconfig.types.json");
                var tsConfigTypesJson = File.ReadAllText(tsConfigPath);
                var jsonObj = JsonConvert.DeserializeObject(tsConfigTypesJson) as JObject;
                var declarationToken = jsonObj?.SelectToken("compilerOptions.declarationDir");
                declarationToken?.Replace($"../../../Types~/{packageName}");
                var pathsToken = jsonObj?.SelectToken("compilerOptions.paths");
                var updatedPaths = new JObject() {
                    new JProperty("@*", new JArray() { "../../../../Types~/@*" }),
                    new JProperty($"{packageName}/*", new JArray() { "./*" }),
                    new JProperty("Client/*", new JArray() { "./Client/*" }),
                    new JProperty("Server/*", new JArray() { "./Server/*" }),
                    new JProperty("Shared/*", new JArray() { "./Shared/*" }),
                };
                pathsToken?.Replace(updatedPaths);
                var excludeToken = jsonObj?.SelectToken("exclude");
                var updatedExclude = new JArray() {
                    "node_modules",
                    "**/*.spec.ts",
                    $"../../../Types~/{packageId}/*"
                };
                excludeToken?.Replace(updatedExclude);
                if (jsonObj != null) {
                    var output = jsonObj.ToString(Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(Path.Join(child, "tsconfig.types.json"), output);
                }
                break;
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
