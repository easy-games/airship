using System;
using UnityEditor;
using UnityEngine;
using Code.Bootstrap;

public static class AirshipBuildAgentEntry {
	public static void BuildGameAssetBundlesFromCommandLine() {
		if (!Application.isBatchMode) {
			Debug.LogError("[AirshipBuildAgent] This method is only intended for -batchmode builds.");
			return;
		}

		var args = ParseArgs();
		if (!args.TryGetValue("airshipBuildPlatform", out var platformValue) || string.IsNullOrEmpty(platformValue)) {
			Debug.LogError("[AirshipBuildAgent] Missing required argument: -airshipBuildPlatform");
			EditorApplication.Exit(201);
			return;
		}

		if (!Enum.TryParse(platformValue, true, out AirshipPlatform platform)) {
			Debug.LogError($"[AirshipBuildAgent] Invalid platform '{platformValue}'.");
			EditorApplication.Exit(202);
			return;
		}

		var useCache = true;
		if (args.TryGetValue("airshipBuildUseCache", out var useCacheArg) && !string.IsNullOrEmpty(useCacheArg)) {
			if (!bool.TryParse(useCacheArg, out useCache)) {
				Debug.LogWarning($"[AirshipBuildAgent] Invalid bool '{useCacheArg}' for -airshipBuildUseCache. Defaulting to true.");
				useCache = true;
			}
		}

		Debug.Log($"[AirshipBuildAgent] Starting batch bundle build. platform={platform}, useCache={useCache}");
		var success = false;
		try {
			success = CreateAssetBundles.BuildGameAssetBundles(platform, useCache);
		} catch (Exception ex) {
			Debug.LogException(ex);
			success = false;
		}

		EditorApplication.Exit(success ? 0 : 1);
	}

	private static System.Collections.Generic.Dictionary<string, string> ParseArgs() {
		var parsed = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		var args = Environment.GetCommandLineArgs();
		for (var current = 0; current < args.Length; current++) {
			var arg = args[current];
			if (!arg.StartsWith("-")) {
				continue;
			}

			var key = arg.TrimStart('-');
			var hasValue = current + 1 < args.Length && !args[current + 1].StartsWith("-");
			var value = hasValue ? args[current + 1] : string.Empty;
			parsed[key] = value;
		}

		return parsed;
	}
}
