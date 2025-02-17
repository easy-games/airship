using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Code.Accessories.Clothing;
using Code.Bootstrap;
using UnityEditor;
using UnityEngine;

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
                CreateAssetBundles.AddAllGameBundleScenes();
            }
        }

        private async void BuildPlatform(AirshipPlatform platform) {
            var manifest = (ClothingBundleManifest)this.target;

            string airId = manifest.GetAirIdForPlatform(platform);

            var buildPath = $"bundles/clothing/{airId}.bundle";
            List<AssetBundleBuild> builds = CreateAssetBundles.GetPackageAssetBundleBuilds();
        }

        private List<AssetBundleBuild> GetAssetBundleBuildList() {
			List<AssetBundleBuild> builds = new();

			var manifest = (ClothingBundleManifest)this.target;
			var folderPath = AssetDatabase.GetAssetPath(this.target);

			// ------------------
			// todo: make sure we link to CoreMaterials properly

			var orgDirs = Directory.GetDirectories(Path.Join("Assets", "AirshipPackages"), "*", SearchOption.TopDirectoryOnly);
			foreach (var orgDir in orgDirs) {
				var packageDirs = Directory.GetDirectories(orgDir);
				var orgName = Path.GetFileName(orgDir);
				foreach (var packageDir in packageDirs) {
					var packageName = Path.GetFileName(packageDir);
					var assetBundleName = $"{orgName}/{packageName}_shared/resources".ToLower();
					Debug.Log("asset bundle name: " + assetBundleName);
					var assetGuids = AssetDatabase.FindAssets("*", new string[] { packageDir }).ToList();

					if (assetBundleName == "@easy/corematerials_shared/resources") {
						var addUrpFiles = new Action<string>((string path) => {
							var urpGuids = AssetDatabase.FindAssets("*",
								new string[] { path });
							assetGuids.AddRange(urpGuids);
						});

						if (!EditorIntegrationsConfig.instance.selfCompileAllShaders) {
							Debug.Log("Adding URP assets to CoreMaterials bundle.");
							addUrpFiles("Packages/com.unity.render-pipelines.universal/Shaders");
							addUrpFiles("Packages/com.unity.render-pipelines.universal/ShaderLibrary");
							addUrpFiles("Packages/com.unity.render-pipelines.universal/Textures");
						}
					}

					var assetPaths = assetGuids.Select((guid) => {
						var path = AssetDatabase.GUIDToAssetPath(guid);
						return path;
					})
						.Where((p) => !AssetDatabase.IsValidFolder(p))
						.Where((p) => !p.EndsWith(".unity"))
						.Where((p) => !p.EndsWith(".cs"))
						.Where((p) => !p.EndsWith(".d.ts"))
						.Where((p) => !p.Contains("Packages/com.unity.render-pipelines.universal/Editor"))
						.ToArray();
					var addressableNames = assetPaths.Select((p) => p.ToLower())
						.ToArray();

					var build = new AssetBundleBuild() {
						assetBundleName = assetBundleName,
						assetNames = assetPaths.ToArray(),
						addressableNames = addressableNames
					};
					builds.Add(build);
				}
			}

			return builds;
		}
    }
}