using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Code.Bootstrap;
using Code.Platform.Shared;
using Editor.Packages;
using Proyecto26;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class UploadInfo {
	public float uploadProgressPercent;
	public float uploadedBytes;
	public float sizeBytes;
	public AirshipPlatform platform;
}

public class Deploy {
	private static Dictionary<string, UploadInfo> uploadProgress = new();

	[MenuItem("Airship/Publish", priority = 50)]
	public static void DeployToStaging()
	{
		EditorCoroutines.Execute((BuildAndDeploy(AirshipPlatformUtil.livePlatforms, false)));
	}

	[MenuItem("Airship/Publish (No Cache)", priority = 51)]
	public static void PublishWithoutCache()
	{
		EditorCoroutines.Execute((BuildAndDeploy(AirshipPlatformUtil.livePlatforms, false, false)));
	}

	private static IEnumerator BuildAndDeploy(AirshipPlatform[] platforms, bool skipBuild = false, bool useCache = true) {
		var devKey = AuthConfig.instance.deployKey;
		if (string.IsNullOrEmpty(devKey)) {
			Debug.LogError("[Airship]: Missing Airship API key. Add your API key inside menu Airship->Configuration");
			yield break;
		}

		var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
		if (gameConfig == null) {
			Debug.LogError("Missing GameConfig.");
			yield break;
		}

		if (string.IsNullOrEmpty(gameConfig.gameId) || gameConfig.gameId.Length != "6536ee084c9987573c3a3c03".Length) {
			Debug.LogError("Invalid GameId. Set your GameId in Assets/GameConfig.asset. You obtain a GameId from create.airship.gg. Here is an example GameId: \"6536ee084c9987573c3a3c03\"");
			yield break;
		}

		// Create deployment
		DeploymentDto deploymentDto;
		{
			UnityWebRequest req = UnityWebRequest.Post(
				$"{AirshipUrl.DeploymentService}/game-versions/create-deployment", JsonUtility.ToJson(
					new CreateGameDeploymentDto() {
						gameId = gameConfig.gameId,
						minPlayerVersion = "1",
						defaultScene = gameConfig.startingSceneName
					}), "application/json");
			req.SetRequestHeader("Authorization", "Bearer " + devKey);
			yield return req.SendWebRequest();
			while (!req.isDone) {
				yield return null;
			}

			if (req.result != UnityWebRequest.Result.Success) {
				Debug.LogError("Failed to create deployment: " + req.error + " " + req.downloadHandler.text);
				yield break;
			}

			Debug.Log("Deployment: " + req.downloadHandler.text);
			deploymentDto = JsonUtility.FromJson<DeploymentDto>(req.downloadHandler.text);
		}

		// Build the game
		if (!skipBuild) {
			var success = CreateAssetBundles.BuildPlatforms(platforms, useCache);
			if (!success) {
				Debug.Log("Cancelled publish.");
				yield break;
			}
		}

		// Save gameConfig.json so we can upload it
		var gameConfigJson = gameConfig.ToJson();
		var gameConfigPath = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild", "gameConfig.json");
		File.WriteAllText(gameConfigPath, gameConfigJson);

		var urls = deploymentDto.urls;

		var uploadList = new List<IEnumerator>() {
			UploadSingleGameFile(urls.gameConfig, "gameConfig.json", null),

			UploadSingleGameFile(urls.Linux_client_resources, $"{AirshipPlatform.Linux}/client/resources", AirshipPlatform.Linux),
			UploadSingleGameFile(urls.Linux_client_scenes, $"{AirshipPlatform.Linux}/client/scenes", AirshipPlatform.Linux),
			UploadSingleGameFile(urls.Linux_shared_resources, $"{AirshipPlatform.Linux}/shared/resources", AirshipPlatform.Linux),
			UploadSingleGameFile(urls.Linux_shared_scenes, $"{AirshipPlatform.Linux}/shared/scenes", AirshipPlatform.Linux),
			UploadSingleGameFile(urls.Linux_server_resources, $"{AirshipPlatform.Linux}/server/resources", AirshipPlatform.Linux),
			UploadSingleGameFile(urls.Linux_server_scenes, $"{AirshipPlatform.Linux}/server/scenes", AirshipPlatform.Linux),

			UploadSingleGameFile(urls.Mac_client_resources, $"{AirshipPlatform.Mac}/client/resources", AirshipPlatform.Mac),
			UploadSingleGameFile(urls.Mac_client_scenes, $"{AirshipPlatform.Mac}/client/scenes", AirshipPlatform.Mac),
			UploadSingleGameFile(urls.Mac_shared_resources, $"{AirshipPlatform.Mac}/shared/resources", AirshipPlatform.Mac),
			UploadSingleGameFile(urls.Mac_shared_scenes, $"{AirshipPlatform.Mac}/shared/scenes", AirshipPlatform.Mac),

			UploadSingleGameFile(urls.Windows_client_resources, $"{AirshipPlatform.Windows}/client/resources", AirshipPlatform.Windows),
			UploadSingleGameFile(urls.Windows_client_scenes, $"{AirshipPlatform.Windows}/client/scenes", AirshipPlatform.Windows),
			UploadSingleGameFile(urls.Windows_shared_resources, $"{AirshipPlatform.Windows}/shared/resources", AirshipPlatform.Windows),
			UploadSingleGameFile(urls.Windows_shared_scenes, $"{AirshipPlatform.Windows}/shared/scenes", AirshipPlatform.Windows),
		};

		// wait for all
		uploadProgress.Clear();
		foreach (var co in uploadList) {
			EditorCoroutines.Execute(co);
		}

		// skip frame so all coroutines can begin
		yield return null;

		// track progress
		bool finishedUpload = false;
		float totalProgress = 0;
		float totalBytes = 0;
		float totalSize = 0;
		// Track the size of the mac platform files to report expected client download size
		float totalMacSize = 0;
		foreach (var (_, uploadInfo) in uploadProgress) {
			totalSize += uploadInfo.sizeBytes;
			if (uploadInfo.platform == AirshipPlatform.Mac) {
				totalMacSize += uploadInfo.sizeBytes;
			}
		}
		
		string getSizeText(float sizeBytes) {
			if (sizeBytes < Math.Pow(10, 3)) return $"{sizeBytes}b";
			if (sizeBytes < Math.Pow(10, 6)) return $"{Math.Round(sizeBytes / Math.Pow(10, 3), 1)}kb";
			return $"{Math.Round(sizeBytes / Math.Pow(10, 6), 1)}mb";
		}
		
		long prevCheckTime = 0;
		while (!finishedUpload) {
			long diff = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000) - prevCheckTime;
			if (diff < 1) {
				yield return null;
				continue;
			}
			prevCheckTime = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000);

			totalProgress = 0;
			totalBytes = 0;
			finishedUpload = true;


			foreach (var (_, uploadInfo) in uploadProgress) {
				if (uploadInfo.uploadProgressPercent < 1) {
					finishedUpload = false;
				}

				totalBytes += uploadInfo.uploadedBytes;
				totalProgress += uploadInfo.uploadProgressPercent * (uploadInfo.sizeBytes / totalSize);
			}
            
			EditorUtility.DisplayProgressBar("Publishing game", $"Upload progress: {getSizeText(totalBytes)} / {getSizeText(totalSize)}", totalProgress);
			yield return new WaitForEndOfFrame();
		}
		EditorUtility.ClearProgressBar();

		Debug.Log($"Completed upload (game size: {getSizeText(totalMacSize)}). Finalizing publish...");

		// Complete deployment
		{
			// Debug.Log("Complete. GameId: " + gameConfig.gameId + ", assetVersionId: " + deploymentDto.version.assetVersionNumber);
			UnityWebRequest req = UnityWebRequest.Post(
				$"{AirshipUrl.DeploymentService}/game-versions/complete-deployment", JsonUtility.ToJson(
					new CompleteGameDeploymentDto() {
						gameId = gameConfig.gameId,
						gameVersionId = deploymentDto.version.gameVersionId,
					}), "application/json");
			req.SetRequestHeader("Authorization", "Bearer " + devKey);
			yield return req.SendWebRequest();
			while (!req.isDone) {
				yield return null;
			}

			if (req.result != UnityWebRequest.Result.Success) {
				Debug.LogError("Failed to complete deployment: " + req.error + " " + req.downloadHandler.text);
				yield break;
			}
		}
		Debug.Log("<color=#77f777>Finished publish! Your game is live.</color>");
	}
	
	private static IEnumerator UploadSingleGameFile(string url, string filePath, AirshipPlatform? platform) {
		var uploadInfo = new UploadInfo();
		if (platform.HasValue) uploadInfo.platform = platform.Value;
		uploadProgress[url] = uploadInfo;
		
		var gameConfig = GameConfig.Load();
		var gameDir = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild");

		var bundleFilePath = gameDir + "/" + filePath;
		var bytes = File.ReadAllBytes(bundleFilePath); // this seems scary
		uploadInfo.sizeBytes = bytes.Length;
		// var manifestBytes = File.ReadAllBytes(bundleFilePath + ".manifest");

		List<IMultipartFormSection> formData = new();
		formData.Add(new MultipartFormFileSection(
			filePath,
			bytes,
			"bundle",
			"multipart/form-data"));

		var req = UnityWebRequest.Put(url, bytes);
		req.SetRequestHeader("x-goog-content-length-range", "0,200000000");
		yield return req.SendWebRequest();

		while (!req.isDone) {
			uploadInfo.uploadProgressPercent = req.uploadProgress;
			uploadInfo.uploadedBytes = req.uploadedBytes;
			yield return new WaitForEndOfFrame();
		}

		if (req.result != UnityWebRequest.Result.Success) {
			Debug.LogError("Failed to upload " + filePath + " " + req.result + " " + req.downloadHandler.text);
		}
		if (uploadProgress.TryGetValue(url, out var progress)) progress.uploadProgressPercent = 1;
	}

	private static void UploadPublishForm(List<IMultipartFormSection> formData) {
		Debug.Log("Uploading to deploy service");
		UnityWebRequest req = UnityWebRequest.Post("https://deployment-service-fxy2zritya-uc.a.run.app/game-versions/upload", formData);
		req.SetRequestHeader("Authorization", "Bearer " + AuthConfig.instance.deployKey);
		EditorCoroutines.Execute(Upload(req, formData));
		EditorCoroutines.Execute(WatchStatus(req));
	}

	private static IEnumerator Upload(UnityWebRequest req, List<IMultipartFormSection> formData)
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
			if (EditorUtility.DisplayDialog("Upload Failed",
				    "Game publish failed during upload. Would you like to retry?",
				    "Retry", "Cancel")) {
				UploadPublishForm(formData);
			}
		}
		else
		{
			Debug.Log("Res: " + req.downloadHandler.text);
			Debug.Log("New version deployed!");
		}
	}

	private static IEnumerator WatchStatus(UnityWebRequest req)
	{
		AirshipEditorUtil.FocusConsoleWindow();
		long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000;
		long lastTime = 0;
		while (!req.isDone)
		{
			long timeSince = (DateTimeOffset.Now.ToUnixTimeMilliseconds() / 1000) - startTime;
			if (timeSince != lastTime)
			{
				if (req.uploadProgress < 1)
				{
					Debug.Log($"Uploading... ({Math.Floor(req.uploadProgress * 100)}%)");
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
