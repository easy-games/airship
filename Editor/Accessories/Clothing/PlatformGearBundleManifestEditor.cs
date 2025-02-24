#if AIRSHIP_INTERNAL
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Code.Accessories.Clothing;
using Code.Bootstrap;
using Code.Http.Internal;
using Code.Platform.Shared;
using Code.Player.Accessories;
using Editor.Packages;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Editor.Accessories.Clothing {
    [CustomEditor(typeof(PlatformGearBundleManifest))]
    [CanEditMultipleObjects]
    public class PlatformGearBundleManifestEditor : UnityEditor.Editor {
        private static string easyOrgId = "6b62d6e3-9d74-449c-aeac-b4feed2012b1";
        private bool skipBuild = false;

        private void OnEnable() {
            skipBuild = false;
        }

        public override void OnInspectorGUI() {
            base.DrawDefaultInspector();

            GUILayout.Space(20);
            AirshipEditorGUI.HorizontalLine();
            GUILayout.Space(20);

            if (GUILayout.Button("Publish")) {
                this.BuildAllPlatforms();
            }

            GUILayout.Space(10);
            this.skipBuild = EditorGUILayout.Toggle("Skip Build", this.skipBuild);
        }

        private async void BuildAllPlatforms() {
            var st = Stopwatch.StartNew();
            bool success = true;

            List<AirshipPlatform> platforms = new();
            platforms.AddRange(AirshipPlatformUtil.livePlatforms);
            // platforms.Add(AirshipPlatform.Mac); // debug

            // ********************************* //
            var manifest = (PlatformGearBundleManifest)this.target;

            (string category, string subcategory) GetGearCategory(PlatformGear gear) {
                string category = "Clothing";
                string subcategory = "";
                if (gear.accessoryPrefabs.Length > 0) {
                    subcategory = gear.accessoryPrefabs[0].accessorySlot.ToString();
                } else if (gear.face != null) {
                    category = "FaceDecal";
                    subcategory = "None";
                }

                return (category, subcategory);
            }

            // Pre-build check: Make sure all gear accessory LOD's are in the right folder
            foreach (var gear in manifest.gearList) {
                if (gear.accessoryPrefabs != null) {
                    foreach (var accessory in gear.accessoryPrefabs) {
                        foreach (var mesh in accessory.meshLods) {
                            var path = AssetDatabase.GetAssetPath(mesh);
                            if (!path.StartsWith("Assets/Gear")) {
                                Debug.LogError($"{accessory.gameObject.name} has an LOD mesh outside of the gear folder. Place all assets (including LOD meshes) inside of the gear folder. Invalid mesh path: " + path);
                                return;
                            }
                        }
                    }
                }
            }


            // Create Class ID's for each gear piece
            foreach (var gear in manifest.gearList) {
                if (!string.IsNullOrEmpty(gear.classId)) continue;

                // Grab class id from the old accessory.classId
                if (gear.accessoryPrefabs.Length > 0 &&
                    !string.IsNullOrEmpty(gear.accessoryPrefabs[0].serverClassId)) {
                    gear.classId = gear.accessoryPrefabs[0].serverClassId;
                    EditorUtility.SetDirty(gear);
                    AssetDatabase.SaveAssets();
                    continue;
                }
                if (gear.face != null && !string.IsNullOrEmpty(gear.face.serverClassId)) {
                    gear.classId = gear.face.serverClassId;
                    EditorUtility.SetDirty(gear);
                    AssetDatabase.SaveAssets();
                    continue;
                }

                (string category, string subcategory) = GetGearCategory(gear);

                // Create a new class id
                var req = UnityWebRequest.Post($"{AirshipPlatformUrl.contentService}/gear/resource-id/{easyOrgId}",
                    JsonUtility.ToJson(new GearCreateRequest() {
                        name = gear.name,
                        imageId = "c0e07e88-09d4-4962-b42d-7794a7ad4cb2",
                        description = "Clothing",
                        gear = new GearCreateRequest() {
                            airAssets = new string[]{},
                            category = category,
                            subcategory = subcategory
                        }
                    }));
                req.SetRequestHeader("Authorization", "Bearer " + InternalHttpManager.editorAuthToken);
                await req.SendWebRequest();
                Debug.Log("Create classId response: " + req.downloadHandler.text);
            }

            string airId = manifest.airId;

            var contentName = manifest.gearList[0].name;
            var contentDescription = "Clothing";

            if (string.IsNullOrEmpty(airId)) {
                // Create new air asset
                var req = UnityWebRequest.Post(
                    AirshipPlatformUrl.deploymentService + $"/air-assets/owner-type/ORGANIZATION/owner-id/{easyOrgId}",
                    JsonUtility.ToJson(new AirAssetCreateRequest() {
                        contentType = "application/airasset",
                        contentLength = 1,
                        name = contentName,
                        description = contentDescription,
                        platforms = platforms.Select((p) => AirshipPlatformUtil.GetStringName(p)).ToArray(),
                    }));
                req.SetRequestHeader("Authorization", "Bearer " + InternalHttpManager.editorAuthToken);
                await req.SendWebRequest();
                Debug.Log("create response: " + req.downloadHandler.text);
                var data = JsonUtility.FromJson<AirAssetCreateResponse>(req.downloadHandler.text);
                manifest.airId = data.airAssetId;
                EditorUtility.SetDirty(this.target);
                this.SaveChanges();
                airId = data.airAssetId;
            }

            List<string> bundlePaths = new();
            foreach (var platform in platforms) {
                var path = await this.BuildPlatform(platform, airId);
                if (string.IsNullOrEmpty(path)) {
                    success = false;
                    return;
                }

                bundlePaths.Add(path);
            }

            if (!success) return;

            // ******************** //

            int bytesCount = 0;
            for (int i = 0; i < platforms.Count; i++) {
                var platform = platforms[i];
                var buildOutputFile = bundlePaths[i];

                // Update air asset
                var bytes = await File.ReadAllBytesAsync(buildOutputFile);
                Debug.Log("bytes length: " + bytes.Length + ", path: " + buildOutputFile);
                bytesCount = bytes.Length;
                var updateReq = UnityWebRequest.Put(AirshipPlatformUrl.deploymentService + $"/air-assets/{airId}",
                    JsonUtility.ToJson(new AirAssetCreateRequest() {
                        contentType = "application/airasset",
                        contentLength = bytes.Length,
                        name = contentName,
                        description = contentDescription,
                        platforms = platforms.Select((p) => AirshipPlatformUtil.GetStringName(p)).ToArray(),
                    }));
                updateReq.SetRequestHeader("Content-Type", "application/json");
                updateReq.SetRequestHeader("Authorization", "Bearer " + InternalHttpManager.editorAuthToken);
                await updateReq.SendWebRequest();
                if (updateReq.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("Failed to update air asset: " + updateReq.downloadHandler.text);
                    return;
                }
                var updateData = JsonUtility.FromJson<AirAssetCreateResponse>(updateReq.downloadHandler.text);
                var uploadUrl = updateData.urls.UrlFromPlatform(platform);

                // Upload asset bundle
                {
                    UnityWebRequest putReq = UnityWebRequest.Put(uploadUrl, bytes);
                    foreach (var pair in updateData.headers) {
                        putReq.SetRequestHeader(pair.key, pair.value);
                    }

                    Debug.Log("Uploading asset bundle...");
                    await putReq.SendWebRequest();
                    Debug.Log("Finished upload! Updating gear classes...");

                    if (putReq.result != UnityWebRequest.Result.Success) {
                        Debug.LogError(putReq.error);
                        Debug.LogError(putReq.downloadHandler.text);
                        return;
                    }
                }
            }

            // ******************** //
            // Update all ClassID's to point to the airId
            foreach (var gear in manifest.gearList) {
                (string category, string subcategory) = GetGearCategory(gear);
                var data = new GearPatchRequest() {
                    airAssets = new string[] { airId },
                    category = category,
                    subcategory = subcategory,
                };
                var req = UnityWebRequest.Put($"{AirshipPlatformUrl.contentService}/gear/class-id/{gear.classId}",
                    JsonUtility.ToJson(data));
                req.method = "PATCH";
                req.SetRequestHeader("Content-Type","application/json");
                req.SetRequestHeader("Authorization", "Bearer " + InternalHttpManager.editorAuthToken);
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("patch classId response: " + req.downloadHandler.text);
                    return;
                }
            }

            Debug.Log($"<color=green>Finished building {bundlePaths.Count} asset bundles for all platforms in {st.Elapsed.Seconds} seconds.</color> File size: " + AirshipEditorUtil.GetFileSizeText(bytesCount));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="platform"></param>
        /// <returns>Path to built bundle. Empty string if it failed.</returns>
        private async Task<string> BuildPlatform(AirshipPlatform platform, string airId) {
            var st = Stopwatch.StartNew();
            var manifest = (PlatformGearBundleManifest)this.target;

            var buildOutputFolder = "bundles/gear/";
            var buildOutputFile = $"bundles/gear/{airId}_{AirshipPlatformUtil.GetStringName(platform)}.bundle";
            var sourceFolderPath = Path.GetRelativePath(".", Directory.GetParent(AssetDatabase.GetAssetPath(manifest))!.FullName);

            List<AssetBundleBuild> builds = CreateAssetBundles.GetPackageAssetBundleBuilds();

            var assetGuids = AssetDatabase.FindAssets("*", new string[] {sourceFolderPath}).ToList();
            var assetPaths = assetGuids
                .Select((guid) => AssetDatabase.GUIDToAssetPath(guid))
                .Where((path) => !path.ToLower().Contains("editor/"))
                .Where((path) => !path.ToLower().Contains("exclude/"))
                .Where((p) => !AssetDatabase.IsValidFolder(p))
                .ToArray();
            Debug.Log("Resources:");
            foreach (var path in assetPaths) {
                Debug.Log("  - " + path);
            }
            var addressableNames = assetPaths
                .Select((p) => p.ToLower())
                .Select((p) => {
                    if (p.Contains("gear bundle manifest")) {
                        // strip the path so it's easier to load later
                        return "gear bundle manifest";
                    }
                    return p;
                })
                .ToArray();
            builds.Add(new AssetBundleBuild() {
                assetBundleName = airId + $"_{AirshipPlatformUtil.GetStringName(platform)}.bundle",
                assetNames = assetPaths,
                addressableNames = addressableNames
            });

            // --------------------- //
            // Build
            if (!this.skipBuild) {
                var buildTarget = AirshipPlatformUtil.ToBuildTarget(platform);
                var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
                if (platform is AirshipPlatform.Windows or AirshipPlatform.Mac or AirshipPlatform.Linux) {
                    buildTargetGroup = BuildTargetGroup.Standalone;
                }
                EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
                var buildParams = new BundleBuildParameters(
                    buildTarget,
                    buildTargetGroup,
                    buildOutputFolder
                );
                buildParams.UseCache = true;
                EditorUserBuildSettings.switchRomCompressionType = SwitchRomCompressionType.Lz4;
                buildParams.BundleCompression = BuildCompression.LZ4;
                var buildContent = new BundleBuildContent(builds);

                AirshipPackagesWindow.buildingPackageId = "game";
                CreateAssetBundles.buildingBundles = true;
                AirshipScriptableBuildPipelineConfig.buildingGameBundles = true;
                ReturnCode returnCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out var result);
                CreateAssetBundles.buildingBundles = false;
                AirshipScriptableBuildPipelineConfig.buildingGameBundles = false;
                if (returnCode != ReturnCode.Success) {
                    Debug.LogError("Failed to build asset bundles. ReturnCode=" + returnCode);
                    return null;
                }
                Debug.Log($"Finished building {platform} in {st.Elapsed.TotalSeconds} seconds.");
            }

            return buildOutputFile;
        }

        [MenuItem("Assets/Airship/Migrate Face Decals")]
        private static void MigrateFaceDecal() {
            Object[] selectedObjects = Selection.objects;
            foreach (Object obj in selectedObjects) {
                if (!(obj is AccessoryFace accessoryFace)) continue;

                var path = AssetDatabase.GetAssetPath(accessoryFace);
                var split = path.Split("/");
                var parentPath = path.Replace("/" + split[split.Length - 1], "");
                var newFolderName = accessoryFace.name;
                if (newFolderName.Contains(".")) {
                    newFolderName = newFolderName.Split(".")[0];
                }

                var newFolderPath = parentPath + "/" + newFolderName;
                AssetDatabase.CreateFolder(parentPath, newFolderName);

                var texturePath = AssetDatabase.GetAssetPath(accessoryFace.decalTexture);
                AssetDatabase.MoveAsset(path, newFolderPath + "/" + accessoryFace.name + ".asset");
                AssetDatabase.MoveAsset(texturePath, newFolderPath + "/" + accessoryFace.decalTexture.name + ".png");

                // -------- //

                var gear = ScriptableObject.CreateInstance<PlatformGear>();
                gear.face = accessoryFace;
                EditorUtility.SetDirty(gear);
                AssetDatabase.CreateAsset(gear, newFolderPath + "/" + accessoryFace.name + " Gear.asset");
                AssetDatabase.SaveAssets();

                var manifest = ScriptableObject.CreateInstance<PlatformGearBundleManifest>();
                manifest.gearList = new PlatformGear[] { gear };
                EditorUtility.SetDirty(manifest);
                AssetDatabase.CreateAsset(manifest, newFolderPath + "/Gear Bundle Manifest.asset");
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif