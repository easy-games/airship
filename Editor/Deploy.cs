using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Editor.Packages;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class Deploy
{
	[MenuItem("Airship/🌎️ Publish", priority = 50)]
	public static void DeployToStaging()
	{
		BuildAndDeploy(new[] { "android", "ios", "linux", "windows", "mac" });
	}

	[MenuItem("Airship/⚡️ Quick Publish/Mac + Linux", priority = 51)]
	public static void DeployToStagingMacAndLinux()
	{
		BuildAndDeploy(new[] { "mac", "linux" });
	}

	[MenuItem("Airship/⚡️ Quick Publish/Windows + Linux", priority = 52)]
	public static void DeployToStagingWindowsAndLinux()
	{
		BuildAndDeploy(new[] { "windows", "linux" });
	}

	[MenuItem("Airship/⚡️ Quick Publish/Mac + Windows + Linux", priority = 53)]
	public static void DeployToStagingMacAndWindowsAndLinux()
	{
		BuildAndDeploy(new[] { "mac", "windows", "linux" });
	}

	private static void BuildAndDeploy(string[] platforms)
	{
		var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
		if (gameConfig == null)
		{
			Debug.LogError("Missing GameConfig.");
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
			new MultipartFormDataSection("bundleId", gameConfig.gameId),
			new MultipartFormDataSection("minPlayerVersion", gameConfig.minimumPlayerVersion + "")
		};

		foreach (var platform in platforms)
		{
			var platformRoot = Path.Join(AssetBridge.GamesPath, platform);
			var empty = IsDirectoryEmpty(platformRoot);
			Debug.Log("Checking platform " + platform + ". Empty: " + empty);

			if (empty)
			{
				Debug.LogWarning("Missing assets for " + platform + ". Please install required Unity build tools before deploying!");
				return;
			}

			foreach (var relativeBundlePath in AirshipPackagesWindow.assetBundleFiles)
			{
				var bundleFilePath = platformRoot + "/" + relativeBundlePath.ToLower();
				var bytes = File.ReadAllBytes(bundleFilePath);

				if(bytes.Length == 0)
				{
					Debug.LogWarning($"Bundle file is empty - this seems wrong. bundleFilePath: {bundleFilePath}");
				}

				Debug.LogWarning($"BuildAndDeploy() platform: {platform}, bundleFilePath: {bundleFilePath}, relativeBundlePath: {relativeBundlePath}");

				formData.Add(new MultipartFormFileSection(
					$"{platform}/{relativeBundlePath}",
					bytes,
					relativeBundlePath,
					"multipart/form-data"));
			}
		}

		Debug.Log("Uploading to deploy service");
		UnityWebRequest req = UnityWebRequest.Post("https://deployment-service-fxy2zritya-uc.a.run.app/game-versions/upload", formData);
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