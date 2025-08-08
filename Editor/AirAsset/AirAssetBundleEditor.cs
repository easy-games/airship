using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Code.Accessories.Clothing;
using Code.AirAssetBundle;
using Code.Bootstrap;
using Code.Http.Internal;
using Code.Platform.Shared;
using Editor.Accessories.Clothing;
using Editor.Packages;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Editor.AirAsset {
    [CustomEditor(typeof(AirAssetBundle))]
    [CanEditMultipleObjects]
    public class AirAssetBundleEditor : UnityEditor.Editor {
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
            var airAssetBundle = (AirAssetBundle)this.target;

            if (!CreateAssetBundles.PrePublishChecks()) return;

            string airId = airAssetBundle.airId;

            var contentName = airAssetBundle.name;
            var contentDescription = "Air Asset Bundle";

            if (string.IsNullOrEmpty(airId)) {
                // Create new air asset
                var req = UnityWebRequest.PostWwwForm(
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
                airAssetBundle.airId = data.airAssetId;
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
                updateReq.SetRequestHeader("x-airship-ignore-rate-limit", "true");
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
                    putReq.SetRequestHeader("x-airship-ignore-rate-limit", "true");

                    Debug.Log("Uploading asset bundle...");
                    await putReq.SendWebRequest();
                    if (putReq.result != UnityWebRequest.Result.Success) {
                        Debug.LogError(putReq.error);
                        Debug.LogError(putReq.downloadHandler.text);
                        return;
                    }
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
            var airAssetBundle = (AirAssetBundle)this.target;

            var buildOutputFolder = "bundles/airassetbundle/";
            var buildOutputFile = $"bundles/airassetbundle/{airId}_{AirshipPlatformUtil.GetStringName(platform)}.bundle";
            var sourceFolderPath = Path.GetRelativePath(".", Directory.GetParent(AssetDatabase.GetAssetPath(airAssetBundle))!.FullName);

            List<AssetBundleBuild> builds = CreateAssetBundles.GetPackageAssetBundleBuilds(false);

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
                    if (p.Contains(target.name.ToLower() + ".asset")) {
                        // custom name so it's easier to find when loading
                        return "_AirAssetBundle";
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
    }
}