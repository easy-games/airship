using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Airship.Editor;
using Code.Bootstrap;
using Code.GameBundle;
using Code.Platform.Shared;
using CsToTs.TypeScript;
using JetBrains.Annotations;
using Luau;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Proyecto26;
using RSG;
using Unity.EditorCoroutines.Editor;
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
        private static string addPackageError = "";

        private GameConfig gameConfig;
        private Dictionary<string, string> packageUploadProgress = new();
        private Dictionary<string, bool> packageVersionToggleBools = new();
        public static string deploymentUrl = "https://deployment-service-fxy2zritya-uc.a.run.app";
        public static string cdnUrl = "https://gcdn-staging.easy.gg";
        /// <summary>
        /// List of downloads actively in progress
        /// </summary>
        public static HashSet<string> activeDownloads = new();
        public static bool buildingAssetBundles = false;

        private bool createFoldoutOpened = false;
        private string createPackageId = "";
        private bool addFoldoutOpened = false;
        private bool addVersionToggle = false;
        private bool publishOptionsFoldoutOpened = false;
        private bool publishOptionUseCache = true;
        private string addPackageId = "";
        private string addPackageVersion = "0";
        private GUIStyle errorTextStyle;
        private Vector2 scrollHeight = new Vector2(0, 0);

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
            this.createPackageId = "";
            this.addFoldoutOpened = false;
            this.addVersionToggle = false;
            this.publishOptionsFoldoutOpened = false;
            this.publishOptionUseCache = true;
            this.addPackageId = "";
            this.addPackageVersion = "0";
            this.scrollHeight = new Vector2(0, 0);
            
            errorTextStyle = new GUIStyle(EditorStyles.label);
            errorTextStyle.normal.textColor = Color.red;
            addPackageError = "";
        }
        

        private void OnGUI() {
            this.scrollHeight = GUILayout.BeginScrollView(this.scrollHeight);
            
            GUILayout.Label("Packages", EditorStyles.largeLabel);
            AirshipEditorGUI.HorizontalLine();
            foreach (var package in this.gameConfig.packages) {
                packageVersionToggleBools.TryAdd(package.id, false);

                GUILayout.BeginHorizontal();
                GUILayout.Label(package.id.Split("/")[1], new GUIStyle(GUI.skin.label) { fixedWidth = 150, fontStyle = FontStyle.Bold});
                if (package.localSource) {
                    if (packageUploadProgress.TryGetValue(package.id, out var progress)) {
                        GUILayout.Label(progress);
                    } else {
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginVertical(GUILayout.Width(120));
                        if (GUILayout.Button("Publish Code")) {
                            EditorCoroutineUtility.StartCoroutineOwnerless(PublishPackage(package, true, false));
                        }
                        if (GUILayout.Button("Publish All")) {
                            // Make sure we generate and write all `NetworkPrefabCollection`s before we
                            // build the package.
                            NetworkPrefabManager.WriteAllCollections();
                            EditorCoroutineUtility.StartCoroutineOwnerless(PublishPackage(package, false, true));
                        }
                        GUILayout.EndVertical();
                        GUILayout.FlexibleSpace();
                    }
                } else {
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginVertical(GUILayout.Width(120));
                    
                    if (GUILayout.Button("Actions")) {
                        GenericMenu menu = new GenericMenu();

                        menu.AddItem(new GUIContent("Update to Latest"), false, () => {
                            EditorCoroutineUtility.StartCoroutineOwnerless(DownloadLatestVersion(package.id, this));
                        });

                        menu.AddItem(new GUIContent("Redownload"), false, () => {
                            EditorCoroutineUtility.StartCoroutineOwnerless(DownloadPackage(package.id, package.codeVersion, package.assetVersion));
                        });
                        
                        // Remove button is disabled for core packages
                        if (package.id.ToLower().StartsWith("@easy/core")) {
                            menu.AddDisabledItem(new GUIContent("Remove"));
                        } else {
                            menu.AddItem(new GUIContent("Remove"), false, () => { RemovePackage(package.id); });
                        }
                        menu.ShowAsContext();
                    }
                    
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                }

                GUILayout.EndHorizontal();
                
                GUILayout.BeginHorizontal();
                var style = new GUIStyle(GUI.skin.label);
                style.normal.textColor = Color.gray;
                GUILayout.Label(package.id + " v" + package.codeVersion + (package.localSource ? " (local)" : ""), style);

                GUILayout.EndHorizontal();

                if (!package.localSource) {
                    // var changeVersionStyle = new GUIStyle(EditorStyles.foldout);
                    // changeVersionStyle.fontStyle = FontStyle.Normal;
                    // packageVersionToggleBools[package.id] =
                    //     EditorGUILayout.BeginFoldoutHeaderGroup(packageVersionToggleBools[package.id], "Change Version",
                    //         changeVersionStyle, null);
                    // if (packageVersionToggleBools[package.id]) {
                    //     EditorGUILayout.BeginHorizontal();
                    //     int codeVersion = 0;
                    //     try {
                    //         codeVersion = Int32.Parse(package.codeVersion);
                    //     } catch (Exception e) {
                    //         Debug.LogError(e);
                    //     }
                    //     int assetVersion = 0;
                    //     try {
                    //         codeVersion = Int32.Parse(package.assetVersion);
                    //     } catch (Exception e) {
                    //         Debug.LogError(e);
                    //     }
                    //
                    //     EditorGUILayout.LabelField("Double version is temporary. Sorry!");
                    //     var codeVersionInt = EditorGUILayout.IntField("Code Version", codeVersion);
                    //     var assetVersionInt = EditorGUILayout.IntField("Asset Version", assetVersion);
                    //     if (GUILayout.Button("Install")) {
                    //         EditorCoroutineUtility.StartCoroutineOwnerless(DownloadPackage(package.id, codeVersionInt + "", assetVersionInt + ""));
                    //     }
                    //
                    //     EditorGUILayout.EndHorizontal();
                    // }
                    //
                    // EditorGUILayout.EndFoldoutHeaderGroup();
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
                var style = new GUIStyle(EditorStyles.textField);
                
                this.addPackageId = EditorGUILayout.TextField("Package ID", this.addPackageId);
                EditorGUILayout.LabelField("Example: @Easy/Survival");
                EditorGUILayout.Space(4);

                var addPackagePressed = GUILayout.Button("Add Package", GUILayout.Width(150));
                if (addPackagePressed) {
                    addPackageError = "";
                    
                    var attemptDownload = true;
                    if (this.addPackageId.Length == 0) {
                        addPackageError = "No package id specified";
                        Repaint();
                        attemptDownload = false;
                    } else if (!Regex.IsMatch(this.addPackageId, "@.+/.+")) {
                        // Basic check that packages matches "@x/x" format
                        addPackageError = "Invalid package id, should look like: @Easy/Survival";
                        Repaint();
                        attemptDownload = false;
                    }

                    if (attemptDownload) {
                        if (this.addVersionToggle) {
                            // EditorCoroutines.Execute(DownloadPackage(this.addPackageId, this.addPackageVersion));
                        }
                        else {
                            EditorCoroutineUtility.StartCoroutineOwnerless(DownloadLatestVersion(this.addPackageId, this, true));
                        }
                    }
                }
                if (addPackageError.Length > 0) {
                    EditorGUILayout.LabelField(addPackageError, errorTextStyle);
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
                    EditorCoroutineUtility.StartCoroutineOwnerless(CreateNewLocalSourcePackage(this.createPackageId));
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
            
            GUILayout.EndScrollView();
        }

        public IEnumerator PublishPackage(AirshipPackageDocument packageDoc, bool skipBuild, bool includeAssets) {
            var devKey = AuthConfig.instance.deployKey;
            
            var deployKeySize = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".Length;
            if (devKey == null || devKey.Length != deployKeySize) {
                Debug.LogError("Invalid deploy key detected. Please verify your deploy key is correct. (Airship -> Configuration)");
                yield break;
            } 
            
            {
                using var permReq = UnityWebRequest.Get($"{AirshipUrl.DeploymentService}/keys/key/permissions");
                permReq.SetRequestHeader("Authorization", "Bearer " + devKey);
                permReq.downloadHandler = new DownloadHandlerBuffer();
                yield return permReq.SendWebRequest();
                while (!permReq.isDone) {
                    yield return null;
                }

                if (permReq.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("Failed to create deployment: " + permReq.error + " " + permReq.downloadHandler.text);
                    yield break;
                }

                var permissionDto = JsonUtility.FromJson<ApiKeyPermissionDto>("{\"elements\":" + permReq.downloadHandler.text + "}");
                var hasPermission = false;
                foreach (var perm in permissionDto.elements) {
                    if (perm.data.slug.ToLower().Equals(packageDoc.id.ToLower())) {
                        hasPermission = true;
                        break;
                    }
                }

                if (!hasPermission) {
                    Debug.LogError("Your deploy key does not have access to " + packageDoc.id);
                    yield break;
                }
            }

            var didVerify = AirshipPackagesWindow.VerifyBuildModules();
            if (!didVerify) {
                Debug.LogErrorFormat("Missing build modules. Install missing modules in Unity Hub and restart Unity to publish package ({0}).", packageDoc.id);
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
            platforms.Remove(AirshipPlatform.Linux);

            if (!CreateAssetBundles.PrePublishChecks()) {
                yield break;
            }

            CreateAssetBundles.FixBundleNames();

            if (!skipBuild) {
                packageUploadProgress[packageDoc.id] = "Building...";
                Repaint();
                yield return null; // give time to repaint

                List<AssetBundleBuild> builds = CreateAssetBundles.GetPackageAssetBundleBuilds();

                foreach (var platform in platforms) {
                    var st = Stopwatch.StartNew();
                    Debug.Log($"Building {platform} asset bundles...");
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
                    Debug.Log("Building package " + packageDoc.id + " with cache=" + this.publishOptionUseCache);
                    buildParams.BundleCompression = BuildCompression.LZ4;
                    EditorUserBuildSettings.switchRomCompressionType = SwitchRomCompressionType.Lz4;
                    var buildContent = new BundleBuildContent(builds);
                    AirshipPackagesWindow.buildingPackageId = packageDoc.id;
                    buildingAssetBundles = true;
                    AirshipScriptableBuildPipelineConfig.buildingGameBundles = false;
                    AirshipScriptableBuildPipelineConfig.buildingPackageName = packageDoc.id;
                    ReturnCode returnCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out var result);
                    buildingAssetBundles = false;
                    AirshipScriptableBuildPipelineConfig.buildingPackageName = null;
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

                    Debug.Log($"Finished building {platform} asset bundles in {st.Elapsed.TotalSeconds} seconds.");
                }
            }

            var importsFolder = Path.Join("Assets", "AirshipPackages");
            var sourceAssetsFolder = Path.Join(importsFolder, packageDoc.id);
            var typesFolder = Path.Join(Path.Join("Assets", "AirshipPackages", "Types~"), packageDoc.id);

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
            sourceAssetsZip.AddDirectory(sourceAssetsFolder, "/");
            // sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Client"), "Client");
            // sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Shared"), "Shared");
            // sourceAssetsZip.AddDirectory(Path.Join(sourceAssetsFolder, "Server"), "Server");

            // Some packages don't have any code. So Types folder is optional.
            // Example: @Easy/CoreMaterials
            if (Directory.Exists(typesFolder)) {
                sourceAssetsZip.AddDirectory(typesFolder, "Types");
            }
            sourceAssetsZip.Save(zippedSourceAssetsZipPath);

            packageUploadProgress[packageDoc.id] = "Starting upload...";
            Repaint();
            // if (true) {
            //     packageUploadProgress.Remove(packageDoc.id);
            //     Repaint();
            //     yield break;
            // }
            yield return null; // give time to repaint
            // Create deployment
            DeploymentDto deploymentDto;
            {
                using var req = UnityWebRequest.Post(
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
                var binaryFileGuids = AssetDatabase.FindAssets("t:" + nameof(AirshipScript));
                var paths = new List<string>();
                var scopedId = packageDoc.id.ToLower();
                foreach (var guid in binaryFileGuids) {
                    var path = AssetDatabase.GUIDToAssetPath(guid).ToLower();
                    if (path.StartsWith("assets/airshippackages/" + scopedId + "/")) {
                        paths.Add(path);
                    }
                }

                if (File.Exists(codeZipPath)) {
                    File.Delete(codeZipPath);
                }
                var codeZip = new ZipFile();
                foreach (var path in paths) {
                    // GetOutputPath is case sensitive so hacky workaround is to make our path start with capital "A"
                    string luaOutPath;
                    if (path.EndsWith(".lua")) {
                        // This is the case for .lua files in the source code.
                        luaOutPath = path;
                    } else {
                        // Get the lua path from a .ts file.
                        luaOutPath = TypescriptProjectsService.Project.GetOutputPath(path.Replace("assets/", "Assets/"));
                        if (!File.Exists(luaOutPath)) {
                            Debug.LogWarning("Missing lua file: " + luaOutPath);
                            continue;
                        }
                    }

                    // We want a .lua in the same spot the .ts would be
                    var luaFakePath = path.Replace(".ts", ".lua");
                    var bytes = File.ReadAllBytes(luaOutPath);
                    codeZip.AddEntry(luaFakePath, bytes);

                    var jsonPath = luaOutPath + ".json~";
                    if (File.Exists(jsonPath)) {
                        // var jsonBytes = File.ReadAllBytes(jsonPath);
                        codeZip.AddEntry(luaFakePath + ".json~", "");
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
                    // UploadSingleGameFile(urls.Linux_client_resources, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    // UploadSingleGameFile(urls.Linux_client_scenes, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    // UploadSingleGameFile(urls.Linux_shared_resources, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    // UploadSingleGameFile(urls.Linux_shared_scenes, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),
                    // UploadSingleGameFile(urls.Linux_server_resources, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_server/resources", packageDoc),
                    // UploadSingleGameFile(urls.Linux_server_scenes, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_server/scenes", packageDoc),

                    // UploadSingleGameFile(urls.Mac_client_resources, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    // UploadSingleGameFile(urls.Mac_client_scenes, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    UploadSingleGameFile(urls.Mac_shared_resources, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    UploadSingleGameFile(urls.Mac_shared_scenes, $"{AirshipPlatform.Mac}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),

                    // UploadSingleGameFile(urls.Windows_client_resources, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    // UploadSingleGameFile(urls.Windows_client_scenes, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    UploadSingleGameFile(urls.Windows_shared_resources, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    UploadSingleGameFile(urls.Windows_shared_scenes, $"{AirshipPlatform.Windows}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),

                    // UploadSingleGameFile(urls.iOS_client_resources, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_client/resources", packageDoc),
                    // UploadSingleGameFile(urls.iOS_client_scenes, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_client/scenes", packageDoc),
                    UploadSingleGameFile(urls.iOS_shared_resources, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_shared/resources", packageDoc),
                    UploadSingleGameFile(urls.iOS_shared_scenes, $"{AirshipPlatform.iOS}/{orgScope}/{packageIdOnly}_shared/scenes", packageDoc),
                });
            }

            // wait for all
		    urlUploadProgress.Clear();
		    foreach (var co in uploadList) {
                EditorCoroutineUtility.StartCoroutineOwnerless(co);
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
                if (urlUploadProgress.Count < 2) {
                    yield return null;
                    continue;
                }

			    prevCheckTime = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000);

			    totalProgress = 0;
			    finishedUpload = true;
                foreach (var pair in urlUploadProgress) {
                    if (float.IsNaN(pair.Value)) {
                        Debug.LogError("Upload progress was NaN.");
                        finishedUpload = false;
                        continue;
                    }
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
                using var req = UnityWebRequest.Post(
                    $"{AirshipUrl.DeploymentService}/package-versions/complete-deployment", JsonUtility.ToJson(
                        new CompletePackageDeploymentDto() {
                            packageSlug = packageDoc.id,
                            packageVersionId = deploymentDto.version.packageVersionId,
                            uploadedFileIds = new [] {
                                // "Linux_shared_resources",
                                "Mac_shared_resources",
                                "Windows_shared_resources",
                                "iOS_shared_resources",

                                // "Linux_shared_scenes",
                                // "Mac_shared_scenes",
                                // "Windows_shared_scenes",
                                // "iOS_shared_scenes",
                            },
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

        public static bool VerifyBuildModules() {
            // var linux64 = ModuleUtil.IsModuleInstalled(BuildTarget.StandaloneLinux64);
            // if (!linux64) {
            //     Debug.LogError("Linux Build Support (<b>Mono</b>) module not found.");
            // }

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
            return mac && windows && iOS;
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

            if (!File.Exists(bundleFilePath)) {
                Debug.Log("Bundle file did not exist. Skipping. Path: " + bundleFilePath);
                urlUploadProgress[url] = 1;
                yield break;
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

            using var req = UnityWebRequest.Put(url, bytes);
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
        
        public static bool IsDownloadingPackages => activeDownloads.Count > 0;
        
        public static IEnumerator DownloadPackage(string packageId, string codeVersion, string assetVersion) {
            if (packageUpdateStartTime.TryGetValue(packageId, out var updateTime)) {
                Debug.Log("Tried to download package while download is in progress. Skipping.");
                yield break;
            }

            packageUpdateStartTime[packageId] = EditorApplication.timeSinceStartup;

            Debug.Log($"Downloading {packageId}...");
            var gameConfig = GameConfig.Load();

            codeVersion = codeVersion.ToLower().Replace("v", "");

            // Source.zip
            var url = $"{cdnUrl}/package/{packageId.ToLower()}/code/{codeVersion}/source.zip";
            var sourceZipDownloadPath =
                Path.Join(Application.persistentDataPath, "EditorTemp", packageId + "Source.zip");
            if (File.Exists(sourceZipDownloadPath)) {
                File.Delete(sourceZipDownloadPath);
            }

            activeDownloads.Add(packageId);
            
            using var sourceZipRequest = new UnityWebRequest(url);
            sourceZipRequest.downloadHandler = new DownloadHandlerFile(sourceZipDownloadPath);
            sourceZipRequest.SendWebRequest();

            // Tell the compiler to restart soon™
            EditorCoroutines.Execute(TypescriptServices.RestartTypescriptRuntimeForPackageUpdates());
            
            yield return new WaitUntil(() => sourceZipRequest.isDone);

            if (sourceZipRequest.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download package. Error: " + sourceZipRequest.error);
                packageUpdateStartTime.Remove(packageId);
                activeDownloads.Remove(packageId);
                yield break;
            }

            var packageAssetsDir = Path.Combine("Assets", "AirshipPackages", packageId);
            if (Directory.Exists(packageAssetsDir)) {
                Directory.Delete(packageAssetsDir, true);
            }

            Directory.CreateDirectory(packageAssetsDir);

            try {
                 using (var zip = System.IO.Compression.ZipFile.OpenRead(sourceZipDownloadPath)) {
                    foreach (var entry in zip.Entries) {
                        string pathToWrite;
                        if (entry.FullName.StartsWith("Types")) {
                            // // Only delete the first instance of "Types/" from full name
                            // var regex = new Regex(Regex.Escape("Types/"));
                            // var pathWithoutTypesPrefix = regex.Replace(entry.FullName, "", 1);
                            // pathToWrite = Path.Join(typesDir, pathWithoutTypesPrefix);
                            continue;
                        }

                        pathToWrite = Path.Join(packageAssetsDir, entry.FullName);

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

                         var srcIgnore = $"Assets/AirshipPackages/{packageId}/*";
                         var metaIgnore = $"Assets/AirshipPackages/{packageId}.meta";
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

                 try {
                     EditorUtility.SetDirty(gameConfig);
                     AssetDatabase.SaveAssets();
                     AssetDatabase.Refresh();
                 } catch (Exception e) {
                     Debug.LogException(e);
                 }
            } catch (Exception e) {
                Debug.LogError("Failed to download package.");
                Debug.LogException(e);
                packageUpdateStartTime.Remove(packageId);
                activeDownloads.Remove(packageId);
            }

            packageUpdateStartTime.Remove(packageId);
            activeDownloads.Remove(packageId);

            var downloadSuccessPath =
                Path.GetRelativePath(".", Path.Combine("Assets", "AirshipPackages", packageId, "airship_pkg_download_success.txt"));
            File.WriteAllText(downloadSuccessPath, "success");
            AssetDatabase.Refresh();

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

        private static IEnumerator WaitForWebRequest(UnityWebRequest request) {
            yield return new WaitUntil(() => request.isDone);
        }
        
        [ItemCanBeNull]
        private static async Task<string> GetPackageSlugProperCase(string packageId) {
            var url = $"{AirshipUrl.ContentService}/packages/slug/{packageId}";
            using var request = UnityWebRequest.Get(url);
            request.downloadHandler = new DownloadHandlerBuffer();
            await request.SendWebRequest();
            
            if (request.result != UnityWebRequest.Result.Success) {
                addPackageError = $"No package exists with id {packageId}";
                return null;
            }

            var response = JsonUtility.FromJson<PackageSlugResponse>(request.downloadHandler.text);
            return response.slugProperCase;
        }


        public static IEnumerator DownloadLatestVersion(string packageId, AirshipPackagesWindow window, bool getProperCaseSlug = false) {
            // Grab proper case slug when trying to install a package for the first time
            if (getProperCaseSlug) {
                var getSlugTask = GetPackageSlugProperCase(packageId);
                yield return new WaitUntil(() => getSlugTask.IsCompleted);
                if (getSlugTask.Result == null) {
                    window.Repaint();
                    yield break;
                }

                packageId = getSlugTask.Result;
            }

            // Debug.Log("Downloading latest version of " + packageId + "...");
            var url = $"{deploymentUrl}/package-versions/packageSlug/{packageId}";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SendWebRequest();
            yield return new WaitUntil(() => request.isDone);

            if (request.result != UnityWebRequest.Result.Success) {
                if (request.result == UnityWebRequest.Result.ProtocolError) {
                    addPackageError = $"There are no published versions of package {packageId}";
                } else {
                    addPackageError = $"Unknown error, check console";
                }
                window.Repaint();
                yield break;
            }

            PackageLatestVersionResponse response =
                JsonUtility.FromJson<PackageLatestVersionResponse>(request.downloadHandler.text);

            // Debug.Log($"Found latest version of {packageId}: v{response.package.codeVersionNumber}");
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

            var orgDir = Path.Combine("Assets", "AirshipPackages", orgId);
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
                if (entry.Name.Contains("PackageComponent")) {
                    var fullDir = Path.Join(assetsDir, "Code");
                    if (!Directory.Exists(fullDir)) {
                        Directory.CreateDirectory(fullDir);
                    }
                    var packageComponent = Path.Join(fullDir, "PackageComponent.ts");
                    if (!File.Exists(packageComponent)) {
                        var file = File.Create(packageComponent);
                        file.Close();
                    }
                    entry.ExtractToFile(packageComponent, true);
                }
            }
            
            zip.Dispose();
            
            AssetDatabase.Refresh();

            var packageDoc = new AirshipPackageDocument() {
                id = fullPackageId,
                codeVersion = "0",
                localSource = true,
            };
            this.gameConfig.packages.Add(packageDoc);

            ShowNotification(new GUIContent($"Successfully created package {packageId}"));

            // Install TS + compile on package create
            EditorUtility.DisplayProgressBar("Compiling TypeScript Projects", $"Compiling new package '{packageId}'...", 0.5f);
            var codeDir = TypeScriptDirFinder.FindTypeScriptDirectoryByPackage(packageDoc);
            // TypescriptCompilationService.CompileTypeScriptProject(codeDir, TypeScriptCompileFlags.Setup);
            TypescriptProjectsService.ReloadProjects();
            EditorUtility.ClearProgressBar();
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

        public void RemovePackage(string packageId) {
            EditorCoroutineUtility.StartCoroutineOwnerless(RemovePackageOneFrameLater(packageId));
        }

        private IEnumerator RemovePackageOneFrameLater(string packageId) {
            yield return null;
            var packageDoc = this.gameConfig.packages.Find((p) => p.id == packageId);
            if (packageDoc != null) {
                this.gameConfig.packages.Remove(packageDoc);
            }
            
            var assetsDir = Path.Combine("Assets", "AirshipPackages", packageId);
            var assetsMetaFile = Path.Combine("Assets", "AirshipPackages", packageId + ".meta");
            if (File.Exists(assetsMetaFile)) {
                File.Delete(assetsMetaFile);
            }
            if (Directory.Exists(assetsDir)) {
                Directory.Delete(assetsDir, true);
            }
            
            // Check if org has any files
            var orgStr = packageId.Split("/")[0]; // Should be something like @Easy
            var orgDir = Path.Combine("Assets", "AirshipPackages", orgStr);
            var orgMetaFile = Path.Combine("Assets", "AirshipPackages", orgStr + ".meta");
            // We only check if there are no subdirectories. No files should live in the org folder.
            if (Directory.Exists(orgDir) && Directory.GetDirectories(orgDir).Length == 0) {
                if (File.Exists(orgMetaFile)) File.Delete(orgMetaFile);
                Directory.Delete(orgDir, true);
            }

            var typesDir = Path.Combine("Assets", "AirshipPackages", "Types~", packageId);
            if (Directory.Exists(typesDir)) {
                Directory.Delete(typesDir, true);
            }

            ShowNotification(new GUIContent($"Removed Package \"{packageId}\""));
        }
    }
}
