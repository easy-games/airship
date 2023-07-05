using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class Deploy
{
	private static readonly Dictionary<string, List<string>> bundleIdToRelativeBundlePaths = new()
	{
		{
			"core",
			new List<string>() {
				"/coreserver/resources",
				"/coreserver/resources.manifest",
				"/coreserver/scenes",
				"/coreserver/scenes.manifest",
				"/coreclient/resources",
				"/coreclient/resources.manifest",
				"/coreclient/scenes",
				"/coreclient/scenes.manifest",
				"/coreshared/resources",
				"/coreshared/resources.manifest",
				"/coreshared/scenes",
				"/coreshared/scenes.manifest",}
		},
		{
			"bedwars",
			new List<string>()
			{
				"/server/resources",
				"/server/resources.manifest",
				"/server/scenes",
				"/server/scenes.manifest",
				"/client/resources",
				"/client/resources.manifest",
				"/client/scenes",
				"/client/scenes.manifest",
				"/shared/resources",
				"/shared/resources.manifest",
				"/shared/scenes",
				"/shared/scenes.manifest",
			}
		}
	};

	[MenuItem("EasyGG/Deploy to Staging")]
	public static void DeployToStaging()
	{
		foreach(var kvp in bundleIdToRelativeBundlePaths)
		{
			BuildAndDeploy(kvp.Key, kvp.Value, new string[] { "android", "ios", "linux", "windows", "mac" });
		}
	}

	[MenuItem("EasyGG/Deploy to Staging (Mac + Linux)")]
	public static void DeployToStagingMacAndLinux()
	{
		foreach (var kvp in bundleIdToRelativeBundlePaths)
		{
			BuildAndDeploy(kvp.Key, kvp.Value, new string[] { "mac", "linux" });
		}
	}

	[MenuItem("EasyGG/Deploy to Staging (Windows + Linux)")]
	public static void DeployToStagingWindowsAndLinux()
	{
		foreach (var kvp in bundleIdToRelativeBundlePaths)
		{
			BuildAndDeploy(kvp.Key, kvp.Value, new string[] { "windows", "linux" });
		}
	}

	[MenuItem("EasyGG/Deploy to Staging (Mac + Windows + Linux)")]
	public static void DeployToStagingMacAndWindowsAndLinux()
	{
		foreach (var kvp in bundleIdToRelativeBundlePaths)
		{
			BuildAndDeploy(kvp.Key, kvp.Value, new string[] { "mac", "windows", "linux" });
		}
	}

	private static void BuildAndDeploy(string bundleId, List<string> relativeBundlePaths, string[] platforms)
	{
		var gameConfig = Resources.Load<GameBundleConfig>("GameBundleConfig");
		if (gameConfig == null)
		{
			Debug.LogError("Missing GameBundleConfig.");
			return;
		}

		var platformMap = new Dictionary<BuildTarget, string>();

		if (platforms.Contains("windows"))
		{
			platformMap.Add(BuildTarget.StandaloneWindows, "windows");
		}

		if (platforms.Contains("ios"))
		{
			platformMap.Add(BuildTarget.iOS, "ios");
		}

		if (platforms.Contains("android"))
		{
			platformMap.Add(BuildTarget.Android, "android");
		}

		if (platforms.Contains("linux"))
		{
			platformMap.Add(BuildTarget.StandaloneLinux64, "linux");
		}

		if (platforms.Contains("mac"))
		{
			platformMap.Add(BuildTarget.StandaloneOSX, "mac");
		}

		CreateAssetBundles.BuildSelectAssetBundles(false, platformMap);

		Debug.Log("Assets Built.");

		List<IMultipartFormSection> formData = new()
		{
			new MultipartFormDataSection("bundleId", bundleId),
			new MultipartFormDataSection("minPlayerVersion", gameConfig.minimumPlayerVersion + "")
		};

		foreach (var platform in platforms)
		{
			var platformRoot = Path.Join(AssetBridge.BundlesPath, platform);
			var empty = IsDirectoryEmpty(platformRoot);
			Debug.Log("Checking platform " + platform + ". Empty: " + empty);

			if (empty)
			{
				Debug.LogWarning("Missing assets for " + platform + ". Please install required Unity build tools before deploying!");
				return;
			}

			foreach (var relativeBundlePath in relativeBundlePaths)
			{
				var bundleFilePath = platformRoot + relativeBundlePath;
				var bytes = File.ReadAllBytes(bundleFilePath);

				if(bytes.Length == 0)
				{
					Debug.LogWarning($"Bundle file is empty - this seems wrong. bundleFilePath: {bundleFilePath}");
				}

				Debug.LogWarning($"BuildAndDeploy() platform: {platform}, bundleFilePath: {bundleFilePath}, relativeBundlePath: {relativeBundlePath}");

				formData.Add(new MultipartFormFileSection(
					$"{platform}{relativeBundlePath}".Replace("coreserver", "server"),
					bytes,
					relativeBundlePath.Replace("coreserver", "server"),
					"multipart/form-data"));
			}
		}

		Debug.Log("Uploading to deploy service");
		UnityWebRequest req = UnityWebRequest.Post("https://deployment-service-fxy2zritya-uc.a.run.app/bundle-versions/upload", formData);
		req.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
		EditorCoroutines.Execute(Upload(req));
		EditorCoroutines.Execute(WatchStatus(req));
	}

	private static IEnumerator Upload(UnityWebRequest req)
	{
		var res = req.SendWebRequest();

		while (!req.isDone)
		{
			yield return res;
		}

		if (req.result != UnityWebRequest.Result.Success)
		{
			Debug.Log("Status: " + req.result);
			Debug.Log("Error while sending upload request: " + req.error);
			Debug.Log("Res: " + req.downloadHandler.text);
			Debug.Log("Err: " + req.downloadHandler.error);
		}
		else
		{
			Debug.Log("Res: " + req.downloadHandler.text);
			Debug.Log("New version deployed!");
		}
	}

	private static IEnumerator WatchStatus(UnityWebRequest req)
	{
		long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000;
		long lastTime = 0;
		while (!req.isDone)
		{
			long timeSince = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000) - startTime;
			if (timeSince != lastTime)
			{
				if (req.uploadProgress < 1)
				{
					Debug.Log("Uploading... (" + Math.Floor(req.uploadProgress * 100) + "%)");
				}
				else
				{
					Debug.Log("Waiting for server to process...");
				}
				lastTime = timeSince;
				continue;
			}
			yield return null;
		}
	}

	private static bool IsDirectoryEmpty(string path)
	{
		IEnumerable<string> items = Directory.EnumerateFileSystemEntries(path);
		using (IEnumerator<string> en = items.GetEnumerator())
		{
			return !en.MoveNext();
		}
	}
}