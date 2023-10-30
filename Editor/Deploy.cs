using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Code.Bootstrap;
using Editor.Packages;
using UnityEditor;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class Deploy
{
	[MenuItem("Airship/🌎️ Publish", priority = 50)]
	public static void DeployToStaging()
	{
		BuildAndDeploy(AirshipPlatformUtil.livePlatforms);
	}

#if AIRSHIP_INTERNAL
	[MenuItem("Airship/🕊️ Publish Game & Core", priority = 50)]
#endif
	public static void PublishGameAndCore() {
		var st = Stopwatch.StartNew();
		BuildAndDeploy(AirshipPlatformUtil.livePlatforms);
		AirshipPackagesWindow packagesWindow = (AirshipPackagesWindow) AirshipPackagesWindow.GetWindow(typeof(AirshipPackagesWindow), true, "Airship Packages");
		var gameConfig = GameConfig.Load();
		var core = gameConfig.packages.Find(p => p.id == "@Easy/Core");
		if (core != null) {
			packagesWindow.PublishPackage(core, false);
		}
		Debug.Log($"Finished \"Publish Game & Core\" in {st.Elapsed.TotalSeconds} seconds.");
	}

	[MenuItem("Airship/⚡️ Quick Publish/Mac + Linux", priority = 51)]
	public static void DeployToStagingMacAndLinux()
	{
		BuildAndDeploy(new[] { AirshipPlatform.Mac, AirshipPlatform.Linux });
	}

	[MenuItem("Airship/⚡️ Quick Publish/Windows + Linux", priority = 52)]
	public static void DeployToStagingWindowsAndLinux()
	{
		BuildAndDeploy(new[] { AirshipPlatform.Mac, AirshipPlatform.Linux });
	}

	[MenuItem("Airship/⚡️ Quick Publish/Mac + Windows + Linux", priority = 53)]
	public static void DeployToStagingMacAndWindowsAndLinux()
	{
		BuildAndDeploy(new[] { AirshipPlatform.Mac, AirshipPlatform.Windows, AirshipPlatform.Linux });
	}

	// ********************** //
	// **** Re Upload ******* //
	// ********************** //

	[MenuItem("Airship/⬆️ Re-upload/Mac + Linux", priority = 54)]
	public static void ReUploadMacAndLinux()
	{
		BuildAndDeploy(new[] { AirshipPlatform.Mac, AirshipPlatform.Linux }, true);
	}

	[MenuItem("Airship/⬆️ Re-upload/Windows + Linux", priority = 55)]
	public static void ReUploadWindowsAndLinux()
	{
		BuildAndDeploy(new[] { AirshipPlatform.Windows, AirshipPlatform.Linux }, true);
	}

	[MenuItem("Airship/⬆️ Re-upload/Mac + Windows + Linux", priority = 56)]
	public static void ReUploadMacAndWindowsAndLinux()
	{
		BuildAndDeploy(new[] { AirshipPlatform.Mac, AirshipPlatform.Windows, AirshipPlatform.Linux }, true);
	}

	private static void BuildAndDeploy(AirshipPlatform[] platforms, bool skipBuild = false)
	{
		var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
		if (gameConfig == null)
		{
			Debug.LogError("Missing GameConfig.");
			return;
		}

		if (!skipBuild) {
			var success = CreateAssetBundles.BuildPlatforms(platforms);
			if (!success) {
				Debug.Log("Cancelled publish.");
				return;
			}
		}

		// Save gameConfig.json
		var gameConfigJson = gameConfig.ToJson();
		var gameConfigPath = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild", "gameConfig.json");
		File.WriteAllText(gameConfigPath, gameConfigJson);

		List<IMultipartFormSection> formData = new()
		{
			new MultipartFormDataSection("gameId", gameConfig.gameId),
			new MultipartFormDataSection("minPlayerVersion", "1"),
			new MultipartFormDataSection("defaultScene", gameConfig.startingSceneName)
		};

		formData.Add(new MultipartFormFileSection(
			"gameConfig.json",
			File.ReadAllBytes(gameConfigPath),
			"gameConfig.json",
			"multipart/form-data"
		));

		foreach (var platform in platforms) {
			var gameDir = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild", platform.ToString());
			if (!Directory.Exists(gameDir) || IsDirectoryEmpty(gameDir))
			{
				Debug.LogError($"Missing {platform} platform build. Please install required Unity build tool modules before publishing!");
				return;
			}

			foreach (var relativeBundlePath in AirshipPackagesWindow.assetBundleFiles)
			{
				var bundleFilePath = gameDir + "/" + relativeBundlePath.ToLower();
				var bytes = File.ReadAllBytes(bundleFilePath);
				// var manifestBytes = File.ReadAllBytes(bundleFilePath + ".manifest");

				formData.Add(new MultipartFormFileSection(
					$"{platform}/{relativeBundlePath.ToLower()}",
					bytes,
					relativeBundlePath,
					"multipart/form-data"));

				// formData.Add(new MultipartFormFileSection(
				// 	$"{platform}/{relativeBundlePath.ToLower()}.manifest",
				// 	manifestBytes,
				// 	relativeBundlePath + ".manifest",
				// 	"multipart/form-data"));
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
