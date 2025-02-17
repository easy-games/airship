using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            bool failed = false;
            // foreach (var platform in AirshipPlatformUtil.livePlatforms) {
            //     await this.BuildPlatform(platform);
            // }

            failed = await this.BuildPlatform(AirshipPlatform.Mac);

            if (failed) {
                return;
            }
            Debug.Log($"<color=green>Finished building asset bundles for all platforms in {st.Elapsed.Seconds} seconds.</color>");
        }

        private async Task<bool> BuildPlatform(AirshipPlatform platform) {
            var st = Stopwatch.StartNew();
            var manifest = (ClothingBundleManifest)this.target;
            string airId = manifest.GetAirIdForPlatform(platform);

            if (string.IsNullOrEmpty(airId)) {
                // Create new air asset
                var res = await InternalHttpManager.PostAsync(AirshipPlatformUrl.deploymentService + $"/air-assets/owner-type/ORGANIZATION/owner-id/easy", JsonUtility.ToJson(new AirAssetCreateRequest() {
                    contentType = "application/airasset",
                    contentLength = 0,
                    name = manifest.clothingList[0].name,
                    description = "Clothing",
                }));
                Debug.Log("create response: " + res.data);
                var data = JsonUtility.FromJson<AirAssetCreateResponse>(res.data);
                manifest.SetAirIdForPlatform(platform, data.airAssetId);
                return true;
            }

            var buildOutputPath = $"bundles/clothing/{airId}.bundle";
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
                .ToArray();
            builds.Add(new AssetBundleBuild() {
                assetBundleName = airId,
                assetNames = assetPaths,
                addressableNames = addressableNames
            });

            // ---------- //

            var buildTarget = AirshipPlatformUtil.ToBuildTarget(platform);
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            if (platform is AirshipPlatform.Windows or AirshipPlatform.Mac or AirshipPlatform.Linux) {
                buildTargetGroup = BuildTargetGroup.Standalone;
            }
            EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
            var buildParams = new BundleBuildParameters(
                buildTarget,
                buildTargetGroup,
                buildOutputPath
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
                return false;
            }

            Debug.Log($"Finished building {platform} in {st.Elapsed.TotalSeconds} seconds.");
            return true;
        }
    }
}