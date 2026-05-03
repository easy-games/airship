using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Code.Bootstrap;
using Easy.Airship.Editor.Packages;
using Easy.Airship.Editor.Publish.Callback;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Pipeline.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using Easy.Airship.Editor.Quality;
#endif
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public static class CreateAssetBundles {
	public static bool buildingBundles = false;
	public const BuildAssetBundleOptions BUILD_OPTIONS = BuildAssetBundleOptions.ChunkBasedCompression;

	public static bool PrePublishChecks() {
		var terrains = GameObject.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		foreach (var terrain in terrains) {
			if (terrain.drawInstanced) {
				Debug.LogError("Terrain with DrawInstancing found in scene " + terrain.gameObject.scene.name + ". DrawInstancing must be disabled.");
				return false;
			}
		}
		return true;
	}

	private static void LogPublishedShaderUsage(List<AssetBundleBuild> builds, AirshipPlatform platform) {
		var reportPath = GetShaderUsageReportPath();
		if (builds == null) {
			var emptyMessage = $"[Airship] Shader usage report ({platform}): no build inputs.\nReport file: {reportPath}";
			WriteShaderUsageReportToFile(reportPath, emptyMessage);
			Debug.Log(emptyMessage);
			return;
		}

		// Only include actual game bundle content. Package bundles (including synthetic CoreMaterials
		// reference bundles) are intentionally excluded from this report.
		var gameBuilds = builds.Where((b) => IsGameBundleName(b.assetBundleName)).ToList();
		if (gameBuilds.Count == 0) {
			var noInputMessage = $"[Airship] Shader usage report ({platform}): no game bundle inputs found.\nReport file: {reportPath}";
			WriteShaderUsageReportToFile(reportPath, noInputMessage);
			Debug.Log(noInputMessage);
			return;
		}

		var publishedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var build in gameBuilds) {
			if (build.assetNames == null) {
				continue;
			}

			foreach (var assetPath in build.assetNames) {
				if (!string.IsNullOrEmpty(assetPath)) {
					publishedAssetPaths.Add(assetPath);
				}
			}
		}

		if (publishedAssetPaths.Count == 0) {
			var emptyBundlesMessage = $"[Airship] Shader usage report ({platform}): game bundles are empty.\nReport file: {reportPath}";
			WriteShaderUsageReportToFile(reportPath, emptyBundlesMessage);
			Debug.Log(emptyBundlesMessage);
			return;
		}

		var materialPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var dependencyPaths = AssetDatabase.GetDependencies(publishedAssetPaths.ToArray(), true);
		foreach (var depPath in dependencyPaths) {
			if (depPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)
			    && depPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) < 0) {
				materialPaths.Add(depPath);
			}
		}

		var byShader = new Dictionary<string, ShaderUsageInfo>(StringComparer.Ordinal);
		foreach (var materialPath in materialPaths) {
			var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
			if (material == null || material.shader == null) {
				continue;
			}

			var shader = material.shader;
			var shaderPath = AssetDatabase.GetAssetPath(shader);
			if (string.IsNullOrEmpty(shaderPath)) {
				shaderPath = "<builtin>";
			}

			var normalizedKeywords = (material.shaderKeywords ?? Array.Empty<string>())
				.Where((kw) => !string.IsNullOrWhiteSpace(kw))
				.Distinct(StringComparer.Ordinal)
				.OrderBy((kw) => kw, StringComparer.Ordinal)
				.ToArray();
			var keywordSet = normalizedKeywords.Length == 0 ? "(none)" : string.Join(", ", normalizedKeywords);

			var shaderKey = $"{shader.name}|{shaderPath}";
			if (!byShader.TryGetValue(shaderKey, out var info)) {
				info = new ShaderUsageInfo {
					shaderName = shader.name,
					shaderPath = shaderPath,
				};
				byShader[shaderKey] = info;
			}

			foreach (var kw in normalizedKeywords) {
				info.allKeywords.Add(kw);
			}

			if (info.keywordSetCounts.TryGetValue(keywordSet, out var count)) {
				info.keywordSetCounts[keywordSet] = count + 1;
			} else {
				info.keywordSetCounts[keywordSet] = 1;
			}

			info.materialUsages.Add(new MaterialUsageInfo {
				materialPath = materialPath,
				keywordSet = keywordSet,
			});
		}

		var allShaders = byShader.Values
			.OrderBy((s) => s.shaderName, StringComparer.OrdinalIgnoreCase)
			.ThenBy((s) => s.shaderPath, StringComparer.OrdinalIgnoreCase)
			.ToList();
		var coreMaterialsShaders = allShaders
			.Where(IsCoreMaterialsAttributedShader)
			.ToList();
		var nonCoreMaterialsShaders = allShaders
			.Where((s) => !IsCoreMaterialsAttributedShader(s))
			.ToList();

		var sb = new StringBuilder();
		sb.AppendLine($"[Airship] Shader usage report ({platform})");
		sb.AppendLine($"Report file: {reportPath}");
		sb.AppendLine("Game bundle scope: shared/resources + shared/scenes");
		sb.AppendLine($"Published assets: {publishedAssetPaths.Count}");
		sb.AppendLine($"Materials: {materialPaths.Count}");
		sb.AppendLine($"Unique shaders: {allShaders.Count}");
		sb.AppendLine();
		AppendShaderUsageSection(sb, "CoreMaterials Shaders", coreMaterialsShaders);
		sb.AppendLine();
		AppendShaderUsageSection(sb, "Other Shaders", nonCoreMaterialsShaders);
		var reportText = sb.ToString();
		WriteShaderUsageReportToFile(reportPath, reportText);
		Debug.Log(reportText);
	}

	private static string GetShaderUsageReportPath() {
		var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		var buildFolder = Path.Combine(projectRoot, "build");
		return Path.Combine(buildFolder, "shader_usage_report.txt");
	}

	private static void WriteShaderUsageReportToFile(string reportPath, string text) {
		try {
			var folder = Path.GetDirectoryName(reportPath);
			if (!string.IsNullOrEmpty(folder)) {
				Directory.CreateDirectory(folder);
			}

			File.WriteAllText(reportPath, text ?? string.Empty);
		} catch (Exception ex) {
			Debug.LogWarning($"[Airship] Failed to write shader usage report file at {reportPath}: {ex.Message}");
		}
	}

	private static bool IsCoreMaterialsAttributedShader(ShaderUsageInfo shader) {
		if (shader == null) {
			return false;
		}

		// CoreMaterials-owned shader assets.
		if (shader.shaderPath.StartsWith("Assets/AirshipPackages/@Easy/CoreMaterials/", StringComparison.OrdinalIgnoreCase)) {
			return true;
		}

		// URP shaders should be attributed to CoreMaterials because CoreMaterials carries the URP shader payload.
		if (shader.shaderPath.StartsWith("Packages/com.unity.render-pipelines.universal/", StringComparison.OrdinalIgnoreCase)) {
			return true;
		}

		if (shader.shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)) {
			return true;
		}

		return false;
	}

	private static void AppendShaderUsageSection(StringBuilder sb, string sectionName, List<ShaderUsageInfo> shaders) {
		sb.AppendLine(sectionName + ":");
		if (shaders.Count == 0) {
			sb.AppendLine(" - (none)");
			return;
		}

		foreach (var shader in shaders) {
			sb.AppendLine($" - {shader.shaderName} ({shader.shaderPath})");
			sb.AppendLine($"   Materials: {shader.materialUsages.Count}");
			sb.AppendLine($"   All keywords: {(shader.allKeywords.Count == 0 ? "(none)" : string.Join(", ", shader.allKeywords))}");
			sb.AppendLine("   Keyword sets:");
			foreach (var kv in shader.keywordSetCounts.OrderByDescending((p) => p.Value).ThenBy((p) => p.Key, StringComparer.Ordinal)) {
				sb.AppendLine($"    - {kv.Key} ({kv.Value})");
			}
			sb.AppendLine("   Material usages:");
			foreach (var usage in shader.materialUsages.OrderBy((m) => m.materialPath, StringComparer.OrdinalIgnoreCase)) {
				sb.AppendLine($"    - {usage.materialPath} | {usage.keywordSet}");
			}
		}
	}

	private class ShaderUsageInfo {
		public string shaderName;
		public string shaderPath;
		public SortedSet<string> allKeywords = new(StringComparer.Ordinal);
		public Dictionary<string, int> keywordSetCounts = new(StringComparer.Ordinal);
		public List<MaterialUsageInfo> materialUsages = new();
	}

	private class MaterialUsageInfo {
		public string materialPath;
		public string keywordSet;
	}

	private static bool IsGameBundleName(string assetBundleName) {
		if (string.IsNullOrEmpty(assetBundleName)) {
			return false;
		}

		return assetBundleName.Equals("shared/resources", StringComparison.OrdinalIgnoreCase)
		       || assetBundleName.Equals("shared/scenes", StringComparison.OrdinalIgnoreCase);
	}

	// [MenuItem("Airship/Tag Asset Bundles")]
	public static bool FixBundleNames() {
		// Set NetworkObject GUIDs
		// var networkPrefabGUIDS = AssetDatabase.FindAssets("t:NetworkPrefabCollection");
		// foreach (var npGuid in networkPrefabGUIDS) {
		// 	var path = AssetDatabase.GUIDToAssetPath(npGuid);
		// 	var prefabCollection = AssetDatabase.LoadAssetAtPath<NetworkPrefabCollection>(path);
		// 	foreach (var prefab in prefabCollection.networkPrefabs) {
		// 		if (prefab is GameObject) {
		// 			var go = (GameObject) prefab;
		// 			var nob = go.GetComponent<NetworkObject>();
		// 			if (nob == null) {
		// 				Debug.LogError($"GameObject {go.name} in {path} was missing a NetworkObject.");
		// 				continue;
		// 			}
		//
		// 			nob.airshipGUID = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(nob.gameObject)).ToString();
		// 			EditorUtility.SetDirty(nob);
		// 		}
		// 	}
		// }
		// AssetDatabase.SaveAssets();

		// foreach (var assetBundleName in AssetDatabase.GetAllAssetBundleNames()) {
		// 	var paths = AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName);
		// 	foreach (var path in paths) {
		// 		var importer = AssetImporter.GetAtPath(path);
		// 		importer.assetBundleName = null;
		// 	}
		// }

		return true;

		string[] bundleFiles = new[] {
			// "client/resources",
			// "client/scenes",
			// "server/resources",
			// "server/scenes",
			"shared/resources",
			"shared/scenes"
		};
		// Game Folders
		// foreach (var assetBundleFile in bundleFiles) {
		// 	var isSceneBundle = assetBundleFile.Contains("scenes");
		//
		// 	string folderPath = "assets";
		// 	if (!isSceneBundle) {
		// 		folderPath = "assets/resources";
		// 		var assetImporter = AssetImporter.GetAtPath(folderPath);
		// 		if (assetImporter == null) {
		// 			Debug.LogWarning("No Assets/Resources folder found. Only code and assets in your scenes will be included in your deploy.");
		// 			continue;
		// 		}
		//
		// 		assetImporter.assetBundleName = assetBundleFile;
		// 	} else { // isSceneBundle == true
		// 		folderPath = "assets/scenes";
		// 	}
		//
		// 	var filter = "*";
		// 	if (isSceneBundle) filter = "t:Scene";
		//
		// 	string[] children = AssetDatabase.FindAssets(filter, new []{ folderPath });
		//
		// 	foreach (string childGuid in children) {
		// 		var path = AssetDatabase.GUIDToAssetPath(childGuid);
		// 		var childAssetImporter = AssetImporter.GetAtPath(path);
		// 		childAssetImporter.assetBundleName = $"{assetBundleFile}";
		//
		// 		if (isSceneBundle) {
		// 			if (path.StartsWith("assets/airshippackages")) continue;
		// 		} else {
		// 			if (path.EndsWith(".ts") || path.EndsWith(".d.ts")) continue;
		// 		}
		//
		// 		// Find lighting data.
		// 		if (isSceneBundle) {
		// 			var sceneLightingFolderPath = path.Replace(".unity", "");
		// 			if (!AssetDatabase.AssetPathExists(sceneLightingFolderPath)) continue;
		// 			var lightingChildren = AssetDatabase.FindAssets("*", new[] { sceneLightingFolderPath });
		// 			foreach (string lightingAssetGuid in lightingChildren) {
		// 				var lightingAssetPath = AssetDatabase.GUIDToAssetPath(lightingAssetGuid);
		// 				var lightingAssetImporter = AssetImporter.GetAtPath(lightingAssetPath);
		// 				if (lightingAssetPath.EndsWith("comp_shadowmask.png")) {
		// 					lightingAssetImporter.assetBundleName = null;
		// 				} else {
		// 					lightingAssetImporter.assetBundleName = "shared/resources";
		// 				}
		// 			}
		// 		}
		// 	}
		// }

		// Package folders
		string[] importFolders = AssetDatabase.GetSubFolders("assets/airshippackages");
		foreach (var importFolder in importFolders) {
			if (!importFolder.Contains("@")) continue;

			string[] innerFolders = AssetDatabase.GetSubFolders(importFolder);
			foreach (var packageFolder in innerFolders) {
				var split = packageFolder.Split("/");
				string packageId = split[split.Length - 2] + "/" + split[split.Length - 1];
				var assetImporter = AssetImporter.GetAtPath(packageFolder);
				if (!assetImporter.assetPath.Contains(".unity")) {
					assetImporter.assetBundleName = $"{packageId}_shared/resources";
				}

				foreach (var bundleFile in bundleFiles) {
					var isSceneBundle = bundleFile.Contains("scenes");

					string[] children;
					if (isSceneBundle) {
						children = AssetDatabase.FindAssets("t:Scene", new[] { packageFolder });
					} else {
						children = AssetDatabase
							.FindAssets("*", new[] { packageFolder })
							.ToArray();
					}

					foreach (string childGuid in children) {
						var path = AssetDatabase.GUIDToAssetPath(childGuid);
						if (!isSceneBundle) {
							if (path.EndsWith(".unity") || path.Contains("/Editor/") || path.EndsWith(".ts") || path.EndsWith(".d.ts")) {
								continue;
							}
						}
						var childAssetImporter = AssetImporter.GetAtPath(path);
						childAssetImporter.assetBundleName = $"{packageId}_{bundleFile}";
					}

				}
			}
		}

		return true;
	}

	/// <summary>
	/// Creates an AssetBundleBuild for every AirshipPackage in the project.
	/// </summary>
	/// <returns></returns>
	public static List<AssetBundleBuild> GetPackageAssetBundleBuilds(bool compileURPShaders) {
		List<AssetBundleBuild> builds = new();

		if (!Directory.Exists(Path.Join("Assets", "AirshipPackages"))) {
			throw new Exception("Missing \"Assets/AirshipPackages\" folder.");
		}

		var orgDirs = Directory.GetDirectories(Path.Join("Assets", "AirshipPackages"), "*", SearchOption.TopDirectoryOnly);
		foreach (var orgDir in orgDirs) {
			var packageDirs = Directory.GetDirectories(orgDir);
			var orgName = Path.GetFileName(orgDir);
			foreach (var packageDir in packageDirs) {
				var packageName = Path.GetFileName(packageDir);
				var assetBundleName = $"{orgName}/{packageName}_shared/resources".ToLowerInvariant();
				Debug.Log("asset bundle name: " + assetBundleName);
				var assetGuids = AssetDatabase.FindAssets("*", new string[] { packageDir }).ToList();

				if (assetBundleName == "@easy/corematerials_shared/resources") {
					var addUrpFiles = new Action<string>((string path) => {
						var urpGuids = AssetDatabase.FindAssets("*",
							new string[] { path });
						assetGuids.AddRange(urpGuids);
					});


					if (!compileURPShaders) {
						// This adds a reference to Core's URP shaders which will prevent them from being duplicated
						// in the game's bundle. Note that these URP shaders are hardcoded to not compile in the Scriptable
						// Build Pipeline source in this scenario.
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
				var addressableNames = assetPaths.Select((p) => p.ToLowerInvariant())
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

#if UNITY_EDITOR
    [MenuItem("Airship/Misc/Test Build Game Config")]
	public static void TestBuildGameConfig(){
		BuildGameConfig();
	}
#endif

	public static GameConfig BuildGameConfig() {
		GameConfig gameConfig = GameConfig.Load();
		try {
			gameConfig.SerializeSettings();
		} catch (Exception ex) {
			Debug.LogError("Error when copying Unity properties to GameConfig: " + ex);
			return null;
		}

		// Local source packages set to forceLatest
		foreach (var package in gameConfig.packages) {
			if (package.localSource) {
				package.forceLatestVersion = true;
			}
		}
		
		EditorUtility.SetDirty(gameConfig);
		AssetDatabase.SaveAssetIfDirty(gameConfig);
		return gameConfig;
	}

	[MenuItem("Airship/Misc/View Shader Usage Report")]
	public static void ViewShaderUsageReport() {
		var gameConfig = GameConfig.Load();
		if (!gameConfig) {
			Debug.LogError("Missing GameConfig.");
			return;
		}

		if (!PrePublishChecks()) {
			return;
		}

		if (!FixBundleNames()) {
			Debug.LogError("Failed to tag asset bundles.");
			return;
		}

		var builds = CollectGameAssetBundleBuilds(gameConfig, includePackageBuilds: false);
		LogPublishedShaderUsage(builds, AirshipPlatformUtil.GetLocalPlatform());
	}

	private static List<AssetBundleBuild> CollectGameAssetBundleBuilds(GameConfig gameConfig, bool includePackageBuilds) {
		var builds = includePackageBuilds
			? GetPackageAssetBundleBuilds(gameConfig.compileURPShaders)
			: new List<AssetBundleBuild>();

		// Moving MonoScript/code assets out of scene bundle ownership can break runtime Behaviour resolution.
		bool IsCodeAssetPath(string path) {
			return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
			       || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)
			       || path.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase)
			       || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
		}

		bool IsPackageOwnedPath(string path) {
			return path.StartsWith("Assets/AirshipPackages/", StringComparison.OrdinalIgnoreCase)
			       || path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
		}

		var scenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var scene in gameConfig.gameScenes) {
			var scenePath = AssetDatabase.GetAssetPath((SceneAsset)scene);
			if (!string.IsNullOrEmpty(scenePath)) {
				scenePaths.Add(scenePath);
			}
		}

		var explicitlyAddedScenePaths = AssetDatabase.GetAssetPathsFromAssetBundle("scenes");
		var explicitlyAddedNonScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var explicitlyPinnedScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (explicitlyAddedScenePaths.Length > 0) {
			Debug.Log($"Found {explicitlyAddedScenePaths.Length} explicit assets for scenes bundle.");
			foreach (var path in explicitlyAddedScenePaths) {
				if (string.IsNullOrEmpty(path)) {
					continue;
				}

				if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) {
					scenePaths.Add(path);
				} else if (IsCodeAssetPath(path)) {
					explicitlyPinnedScenePaths.Add(path);
				} else {
					explicitlyAddedNonScenePaths.Add(path);
				}
			}
		}

		var sceneReferencedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (scenePaths.Count > 0) {
			var dependencies = AssetDatabase.GetDependencies(scenePaths.ToArray(), true);
			foreach (var dependency in dependencies) {
				if (string.IsNullOrEmpty(dependency)) {
					continue;
				}

				if (scenePaths.Contains(dependency)) {
					continue;
				}

				if (IsCodeAssetPath(dependency)) {
					continue;
				}

				if (IsPackageOwnedPath(dependency)) {
					continue;
				}

				sceneReferencedAssetPaths.Add(dependency);
			}
		}
		sceneReferencedAssetPaths.UnionWith(explicitlyAddedNonScenePaths.Where((p) => !IsPackageOwnedPath(p)));

		foreach (var assetBundleFile in AirshipPackagesWindow.assetBundleFiles) {
			var assetBundleName = assetBundleFile.ToLowerInvariant();
			if (assetBundleName == "shared/scenes") {
				string[] assetPaths = scenePaths
					.Concat(explicitlyPinnedScenePaths)
					.Where((path) => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || IsCodeAssetPath(path))
					.ToArray();

				var sb = new StringBuilder();
				sb.AppendLine("[Airship] Publishing with the following scenes:");
				int i = 1;
				foreach (var p in assetPaths) {
					sb.AppendLine($"  {i}. " + p);
					i++;
				}

				sb.AppendLine("Configure published scenes in the Game Config. Each scene will add to your games total download size.");
				Debug.Log(sb.ToString());

				var addressableNames = assetPaths.Select((p) => p.ToLowerInvariant()).ToArray();
				var build = new AssetBundleBuild() {
					assetBundleName = assetBundleName,
					assetNames = assetPaths,
					addressableNames = addressableNames
				};
				builds.Add(build);
			} else if (assetBundleName == "shared/resources") {
				var assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				var resourceGuids = AssetDatabase.FindAssets("*", new[] { "Assets/Resources" });
				foreach (var guid in resourceGuids) {
					var path = AssetDatabase.GUIDToAssetPath(guid);
					if (!string.IsNullOrEmpty(path)) {
						assetPaths.Add(path);
					}
				}

				if (AssetDatabase.AssetPathExists("Assets/Airship.asbuildinfo")) {
					assetPaths.Add("Assets/Airship.asbuildinfo");
				}
				if (AssetDatabase.AssetPathExists("Assets/GameConfig.asset")) {
					assetPaths.Add("Assets/GameConfig.asset");
				}
				if (AssetDatabase.AssetPathExists("Assets/NetworkPrefabCollection.asset")) {
					assetPaths.Add("Assets/NetworkPrefabCollection.asset");
				}

				var explicitlyAddedPaths = AssetDatabase.GetAssetPathsFromAssetBundle("resources");
				if (explicitlyAddedPaths.Length > 0) {
					Debug.Log($"Found {explicitlyAddedPaths.Length} explicit assets for resources bundle.");
					foreach (var path in explicitlyAddedPaths) {
						if (!string.IsNullOrEmpty(path) && !IsCodeAssetPath(path) && !IsPackageOwnedPath(path)) {
							assetPaths.Add(path);
						}
					}
				}

				foreach (var sceneReferencedAssetPath in sceneReferencedAssetPaths) {
					assetPaths.Add(sceneReferencedAssetPath);
				}

				var finalAssetPaths = assetPaths
					.Where((p) => !(p.EndsWith(".lua") || p.EndsWith(".json~") || p.EndsWith(".d.ts")))
					.Where((p) => !p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
					.Where((p) => !IsCodeAssetPath(p))
					.Where((p) => !IsPackageOwnedPath(p))
					.Where((path) => !path.ToLowerInvariant().Contains("editor/"))
					.Where((p) => AssetDatabase.AssetPathExists(p))
					.Where((p) => !AssetDatabase.IsValidFolder(p))
					.ToArray();
				var addressableNames = finalAssetPaths.Select((p) => p.ToLowerInvariant()).ToArray();
				builds.Add(new AssetBundleBuild() {
					assetBundleName = assetBundleName,
					assetNames = finalAssetPaths,
					addressableNames = addressableNames
				});
			}
		}

		return builds;
	}

	public static bool BuildGameAssetBundles(AirshipPlatform platform, bool useCache = true, bool skipPrePublishChecks = false) {
		ResetScenes();

		if (!skipPrePublishChecks && !PrePublishChecks()) {
			return false;
		}

		if (!FixBundleNames()) {
			Debug.LogError("Failed to tag asset bundles.");
			return false;
		}

		var sw = Stopwatch.StartNew();
		var gameConfig = GameConfig.Load();
		if(!gameConfig){
			return false;
		}

		var buildPath = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild", platform.ToString());
		if (!Directory.Exists(buildPath)) {
			Directory.CreateDirectory(buildPath);
		}
		Debug.Log($"[Editor]: Building {platform} asset bundles...");
		Debug.Log("[Editor]: Build path: " + buildPath);

		if (platform == AirshipPlatform.iOS || platform == AirshipPlatform.Android) {
			SwapToQualityLevel("Low");
		} else {
			SwapToQualityLevel("Normal");
		}

		// Act as if we are building all asset bundles (including CoreMaterials).
		// This is so our current build target will have references to those asset bundles.
		// This is paired with changes to Scriptable Build Pipeline that prevent these bundles from actually being built.
		var builds = CollectGameAssetBundleBuilds(gameConfig, includePackageBuilds: true);

		LogPublishedShaderUsage(builds, platform);

		// var tasks = DefaultBuildTasks.Create(DefaultBuildTasks.Preset.AssetBundleBuiltInShaderExtraction);
		var buildTarget = AirshipPlatformUtil.ToBuildTarget(platform);

		if (platform == AirshipPlatform.Android) {
			PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, false);
			PlayerSettings.SetGraphicsAPIs(buildTarget, new GraphicsDeviceType[]
			{
				GraphicsDeviceType.Vulkan
			});
		} else {
			PlayerSettings.SetUseDefaultGraphicsAPIs(buildTarget, true);
		}

		var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
		if (platform is AirshipPlatform.Windows or AirshipPlatform.Mac or AirshipPlatform.Linux) {
			buildTargetGroup = BuildTargetGroup.Standalone;
		}
		EditorUserBuildSettings.SwitchActiveBuildTarget(buildTargetGroup, buildTarget);
		var buildParams = new BundleBuildParameters(
			buildTarget,
			buildTargetGroup,
			buildPath
		) {
			WriteLinkXML = false,
		};
		buildParams.UseCache = useCache;
		EditorUserBuildSettings.switchRomCompressionType = SwitchRomCompressionType.Lz4;
		buildParams.BundleCompression = BuildCompression.LZ4;
		var buildContent = new BundleBuildContent(builds);

		ContentPipeline.BuildCallbacks.PostPackingCallback = (parameters, data, arg3) => {
			return ReturnCode.Success;
		};

		AirshipPackagesWindow.buildingPackageId = "game";
		buildingBundles = true;
		AirshipScriptableBuildPipelineConfig.buildingGameBundles = true;
		// Allow other logic to hook into pre build game bundles (used for setting up scriptable shader
		// scripting based on target).
		BuildAirshipGameBundleProcessor.InvokePreBuildGameBundle(buildTarget);
		ReturnCode returnCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out var result);
		buildingBundles = false;
		AirshipScriptableBuildPipelineConfig.buildingGameBundles = false;
		if (returnCode != ReturnCode.Success) {
			Debug.LogError("Failed to build asset bundles. ReturnCode=" + returnCode);
			return false;
		}

		// Debug.Log("----------------------");
		// Debug.Log("Airship Build Report");
		// Debug.Log("----------------------");
		// {
		// 	Debug.Log("Serialized Files:");
		// 	foreach (var pair in result.WriteResults) {
		// 		Debug.Log("  " + pair.Key + ":");
		// 		for (int i = 0; i < pair.Value.serializedObjects.Count; i++) {
		// 			var objects = pair.Value.serializedObjects[i];
		// 			var sizeKb = objects.rawData.size / 1000;
		// 			Debug.Log($"    {i}. ({sizeKb:n0} kb) {AssetDatabase.GUIDToAssetPath(objects.serializedObject.guid)}");
		// 		}
		// 	}
		// }
		// {
		// 	foreach (var pair in result.BundleInfos) {
		// 		Debug.Log($"{pair.Key} Dependencies:");
		// 		for (int i = 0; i < pair.Value.Dependencies.Length; i++) {
		// 			Debug.Log($"  {i}. {pair.Value.Dependencies[i]}");
		// 		}
		// 	}
		// }
		// {
		// 	Debug.Log("Asset results:");
		// 	foreach (var pair in result.AssetResults) {
		// 		Debug.Log("  " + AssetDatabase.GUIDToAssetPath(pair.Key) + ":");
		// 		for (int i = 0; i < pair.Value.IncludedObjects.Count; i++) {
		// 			var includedObject = pair.Value.IncludedObjects[i];
		// 			Debug.Log($"    {i}. {AssetDatabase.GUIDToAssetPath(includedObject.guid)}");
		// 		}
		// 		Debug.Log("  Referenced Objects:");
		// 		if (pair.Value.ReferencedObjects != null) {
		// 			for (int i = 0; i < pair.Value.ReferencedObjects.Count; i++) {
		// 				var referencedObject = pair.Value.ReferencedObjects[i];
		// 				Debug.Log($"    {i}. (dep) {AssetDatabase.GUIDToAssetPath(referencedObject.guid)}");
		// 			}
		// 		}
		//
		// 	}
		// }

		Debug.Log($"[Editor]: Finished building {platform} asset bundles in {sw.Elapsed.TotalSeconds} seconds.");

		return true;
	}

#if UNITY_EDITOR
	[MenuItem("Airship/Set Quality/Normal", false, 1901)]
	static void MenuSetQualityLevel() {
		CreateAssetBundles.SwapToQualityLevel("Normal");
	}

	[MenuItem("Airship/Set Quality/Low", false, 1902)]
	static void MenuSetQualityLevelLow() {
		CreateAssetBundles.SwapToQualityLevel("Low");
	}
#endif

	public static void SwapToQualityLevel(string name) {
#if UNITY_EDITOR
		if (name == "Low") {
			QualityConfig.ConfigureLowQualityLevel();
		} else if (name == "Normal") {
			QualityConfig.ConfigureNormalQualityLevel();
		}
#endif

		int index = System.Array.IndexOf(QualitySettings.names, name);
		if (index < 0) {
			Debug.LogError($"Quality level '{name}' not found. Please report this issue to Airship devs.");
			return;
		}

		QualitySettings.SetQualityLevel(index, applyExpensiveChanges: true);
	}

	static IList<IBuildTask> GetBuildTasks()
	{
		var buildTasks = new List<IBuildTask>();

		// Setup
		buildTasks.Add(new SwitchToBuildPlatform());
		buildTasks.Add(new RebuildSpriteAtlasCache());

		// Player Scripts
		buildTasks.Add(new BuildPlayerScripts());
		buildTasks.Add(new PostScriptsCallback());

		// Dependency
		buildTasks.Add(new CalculateSceneDependencyData());
#if UNITY_2019_3_OR_NEWER
		buildTasks.Add(new CalculateCustomDependencyData());
#endif
		buildTasks.Add(new CalculateAssetDependencyData());
		buildTasks.Add(new StripUnusedSpriteSources());
		// if (shaderTask)
		// 	buildTasks.Add(new CreateBuiltInBundle("UnityBuiltIn.bundle"));
		// if (monoscriptTask)
		// 	buildTasks.Add(new CreateMonoScriptBundle("UnityMonoScripts.bundle"));
		buildTasks.Add(new PostDependencyCallback());

		// Packing
		buildTasks.Add(new GenerateBundlePacking());
		// if (shaderTask || monoscriptTask)
		// 	buildTasks.Add(new UpdateBundleObjectLayout());
		buildTasks.Add(new GenerateBundleCommands());
		buildTasks.Add(new GenerateSubAssetPathMaps());
		buildTasks.Add(new GenerateBundleMaps());
		buildTasks.Add(new PostPackingCallback());

		// Writing
		buildTasks.Add(new WriteSerializedFiles());
		buildTasks.Add(new ArchiveAndCompressBundles());
		buildTasks.Add(new AppendBundleHash());
		buildTasks.Add(new GenerateLinkXml());
		buildTasks.Add(new PostWritingCallback());

		return buildTasks;
	}

	public static void BuildLocalAssetBundles()
	{
		BuildGameAssetBundles(AirshipPlatformUtil.GetLocalPlatform());
	}

#if AIRSHIP_INTERNAL
	[MenuItem("Airship/Internal/Build iOS Game Bundles")]
	public static void BuildiOSAssetBundles() {
		BuildGameAssetBundles(AirshipPlatform.iOS);
	}
	
	[MenuItem("Airship/Internal/Build Android Game Bundles")]
	public static void BuildAndroidAssetBundles() {
		BuildGameAssetBundles(AirshipPlatform.Android);
	}
#endif

#if AIRSHIP_INTERNAL
	// [MenuItem("Airship/Misc/Build Local AssetBundles")]
#endif
	public static void BuildLocalAssetBundlesMenuItem() {
		var platform = AirshipPlatformUtil.FromRuntimePlatform(Application.platform);
		BuildPlatforms(new[] {platform});
	}

#if AIRSHIP_INTERNAL
	// [MenuItem("Airship/Misc/Delete Local AssetBundles")]
#endif
	public static void DeleteLocalAssetBundles()
	{
		Debug.Log("Deleting local asset bundles in " + AssetBridge.GamesPath);
		if (Directory.Exists(AssetBridge.GamesPath))
		{
			Directory.Delete(AssetBridge.GamesPath, true);
		}

		Debug.Log("Finished deleting local asset bundles!");
	}

	// [MenuItem("Airship/Custom Local Bundle/Linux")]
	public static void BuildLinuxPlayerAssetBundlesAsLocal()
	{
		BuildGameAssetBundles(AirshipPlatform.Linux);
	}

	// [MenuItem("Airship/Custom Local Bundle/Windows")]
	public static void BuildWindowsPlayerAssetBundlesAsLocal()
	{
		BuildGameAssetBundles(AirshipPlatform.Windows);
	}

#if AIRSHIP_INTERNAL
	// [MenuItem("Airship/Misc/Build All AssetBundles")]
#endif
	public static void BuildAllAssetBundles() {
		BuildPlatforms(AirshipPlatformUtil.livePlatforms);
	}

	public static bool BuildPlatforms(AirshipPlatform[] platforms, bool useCache = true) {
		var sw = Stopwatch.StartNew();
		try {
			if (!PrePublishChecks()) {
				return false;
			}

			foreach (var platform in platforms) {
				var res = BuildGameAssetBundles(platform, useCache, skipPrePublishChecks: true);
				if (!res) {
					return false;
				}
			}

			Debug.Log($"Built game asset bundles for {platforms.Length} platform{(platforms.Length > 1 ? "s" : "")} in {sw.Elapsed.TotalSeconds.ToString("0.0")}s");
		} catch (Exception e) {
			Debug.LogException(e);
			Debug.LogError($"Failed to build asset bundles.");
			return false;
		}

		AddAllGameBundleScenes();
		return true;
	}

	public static void AddAllGameBundleScenes()
	{
#if UNITY_EDITOR
		var config = FindGameConfig();

		List<EditorBuildSettingsScene> list = new();
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", true));
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true));
		list.Add(new EditorBuildSettingsScene("Assets/Scenes/Login.unity", true));
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/Disconnected.unity", true));
		list.Add(new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/AirshipUpdateApp.unity", true));

		if (config != null)
		{
			foreach (var s in config.gameScenes)
			{
				string pathToScene = AssetDatabase.GetAssetPath(s);
				list.Add(new EditorBuildSettingsScene(pathToScene, true));
			}
		}

		EditorBuildSettings.scenes = list.ToArray();
#endif
	}

	public static void ResetScenes()
	{
#if UNITY_EDITOR && AIRSHIP_PLAYER
		EditorBuildSettingsScene[] scenes = new[]
		{
			new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/MainMenu.unity", true),
			new EditorBuildSettingsScene("Packages/gg.easy.airship/Runtime/Scenes/CoreScene.unity", true),
		};
		EditorBuildSettings.scenes = scenes;
#endif
	}

	public static GameConfig FindGameConfig()
	{
		return AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
	}
}

// ensure class initializer is called whenever scripts recompile
[InitializeOnLoad]
public static class PlayModeStateChangedExample
{
	// register an event handler when the class is initialized
	static PlayModeStateChangedExample()
	{
		EditorApplication.playModeStateChanged += LogPlayModeState;
		CreateAssetBundles.ResetScenes();
	}

	private static void LogPlayModeState(PlayModeStateChange state) {
		//Debug.Log(state);
		if (state == PlayModeStateChange.ExitingEditMode) {
			CreateAssetBundles.AddAllGameBundleScenes();
			if (SceneManager.GetActiveScene().name != "CoreScene") {
				return;
			}

			// Debug.Log("[EDITOR]: Building asset bundles..");
			// CreateAssetBundles.BuildLocalAssetBundles();
		} else if (state == PlayModeStateChange.EnteredEditMode) {
			CreateAssetBundles.ResetScenes();
		}
	}
}
