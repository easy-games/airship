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
using Editor.Packages;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Editor.Accessories.Clothing {
    [CustomEditor(typeof(ClothingBundleManifest))]
    [CanEditMultipleObjects]
    public class ClothingBundleManifestEditor : UnityEditor.Editor {
        private static string easyOrgId = "6b62d6e3-9d74-449c-aeac-b4feed2012b1";
        public override void OnInspectorGUI() {
            base.DrawDefaultInspector();

            GUILayout.Space(20);
            AirshipEditorGUI.HorizontalLine();
            GUILayout.Space(20);

            if (GUILayout.Button("Publish")) {
                this.BuildAllPlatforms();
            }
        }

        private async void BuildAllPlatforms() {
            var st = Stopwatch.StartNew();
            bool success = true;

            List<AirshipPlatform> platforms = new();
            // platforms.AddRange(AirshipPlatformUtil.betaPlatforms);
            platforms.Add(AirshipPlatform.Mac); // debug

            // ********************************* //
            var manifest = (ClothingBundleManifest)this.target;

            string airId = manifest.airId;

            var contentName = manifest.clothingList[0].name;
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

            for (int i = 0; i < platforms.Count; i++) {
                var platform = platforms[i];
                var buildOutputFile = bundlePaths[i];

                // Update air asset
                var bytes = await File.ReadAllBytesAsync(buildOutputFile);
                Debug.Log("bytes length: " + bytes.Length + ", path: " + buildOutputFile);
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
                Debug.Log("Update response: " + updateReq.downloadHandler.text);
                var updateData = JsonUtility.FromJson<AirAssetCreateResponse>(updateReq.downloadHandler.text);
                var uploadUrl = updateData.urls.UrlFromPlatform(platform);
                Debug.Log("Got update url: " + uploadUrl);

                // Upload asset bundle
                {
                    UnityWebRequest putReq = UnityWebRequest.Put(uploadUrl, bytes);
                    foreach (var pair in updateData.headers) {
                        putReq.SetRequestHeader(pair.key, pair.value);
                    }

                    await putReq.SendWebRequest();
                    Debug.Log("Upload result: " + putReq.result);

                    if (putReq.result != UnityWebRequest.Result.Success) {
                        Debug.LogError(putReq.error);
                        Debug.LogError(putReq.downloadHandler.text);
                        return;
                    }
                }
            }

            Debug.Log($"<color=green>Finished building {bundlePaths.Count} asset bundles for all platforms in {st.Elapsed.Seconds} seconds.</color>");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="platform"></param>
        /// <returns>Path to built bundle. Empty string if it failed.</returns>
        private async Task<string> BuildPlatform(AirshipPlatform platform, string airId) {
            var st = Stopwatch.StartNew();
            var manifest = (ClothingBundleManifest)this.target;

            var buildOutputFolder = "bundles/clothing/";
            var buildOutputFile = $"bundles/clothing/{airId}_{AirshipPlatformUtil.GetStringName(platform)}.bundle";
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
                    if (p.Contains("clothing bundle manifest")) {
                        // strip the path so it's easier to load later
                        return "clothing bundle manifest";
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
            if (false) {
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
    }
}
#endif