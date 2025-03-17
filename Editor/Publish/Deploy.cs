using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Airship.Editor;
using Code.Authentication;
using Code.Bootstrap;
using Code.Http.Internal;
using Code.Platform.Shared;
using Editor.Packages;
using Luau;
using Proyecto26;
using Unity.VisualScripting.IonicZip;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

public class UploadInfo {
	public bool failed;
	public float uploadProgressPercent;
	public float uploadedBytes;
	public float sizeBytes;
	public bool isComplete;
	public AirshipPlatform? platform;
}

public class Deploy {
	private static Dictionary<string, UploadInfo> uploadProgress = new();
	private static GameDto activeDeployTarget;
	public const ulong MAX_UPLOAD_KB = 500_000;

	public static void PublishGame()
	{
		// Make sure we generate and write all `NetworkPrefabCollection`s before we
		// build the game.
		// NetworkPrefabManager.WriteAllCollections();
		// Sort the current platform first to speed up build time
		List<AirshipPlatform> platforms = new();
		var gameConfig = CreateAssetBundles.BuildGameConfig();
		if (gameConfig.supportsMobile) {
			platforms.Add(AirshipPlatform.iOS);
			platforms.Add(AirshipPlatform.Android);
		}

		// We want to end up on our editor machine's platform
#if UNITY_EDITOR_OSX
		platforms.Add(AirshipPlatform.Windows);
		platforms.Add(AirshipPlatform.Mac);
#else
		platforms.Add(AirshipPlatform.Mac);
		platforms.Add(AirshipPlatform.Windows);
#endif
		EditorCoroutines.Execute((BuildAndDeploy(platforms.ToArray(), false)));
	}

	[MenuItem("Airship/Publish Game (Code Only)", priority = 50)]
	public static void DeployCodeOnly()
	{
		EditorCoroutines.Execute((BuildAndDeploy(Array.Empty<AirshipPlatform>(), true, true)));
	}

	[MenuItem("Airship/Publish Game (No Cache)", priority = 51)]
	public static void PublishWithoutCache()
	{
		// Make sure we generate and write all `NetworkPrefabCollection`s before we
		// build the game.
		// NetworkPrefabManager.WriteAllCollections();
		EditorCoroutines.Execute((BuildAndDeploy(AirshipPlatformUtil.livePlatforms, false, false)));
	}

	private static IEnumerator BuildAndDeploy(AirshipPlatform[] platforms, bool skipBuild = false, bool useCache = true) {
		var possibleKeys = new List<string>() { AuthConfig.instance.deployKey, InternalHttpManager.editorAuthToken };
		possibleKeys.RemoveAll(string.IsNullOrEmpty);
		if (possibleKeys.Count == 0) {
			Debug.LogError("[Airship]: You aren't signed in. You can sign in by going to Airship->Sign in");
			yield break;
		}

		var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
		if (gameConfig == null) {
			Debug.LogError("Missing GameConfig.");
			yield break;
		}

		if (!skipBuild) {
			var didVerify = AirshipPackagesWindow.VerifyBuildModules(gameConfig.supportsMobile);
			if (!didVerify) {
				Debug.LogErrorFormat("Missing build modules. Install missing modules in Unity Hub and restart Unity to publish game.");
				yield break;
			}
		}



		var confirmedSaveState = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
		if (!confirmedSaveState  || SceneManager.GetActiveScene().isDirty) { // User clicked "cancel"
			Debug.LogError("[Airship]: Cancelling publish: you must save or discard scene changes.");
			yield break;
		}

		foreach (var scene in gameConfig.gameScenes) {
			if (LuauCore.IsProtectedSceneName(scene.name)) {
				Debug.LogError($"Game scene name not allowed: {scene.name}");
				yield break;
			}

			if (scene.name.Contains(" ")) {
				Debug.LogError("Scenes are not allowed to have spaces in their name. Please fix: \"" + scene.name + "\"");
				yield break;
			}
		}

		if (LuauCore.IsProtectedSceneName(gameConfig.startingScene.name)) {
			Debug.LogError($"Game starting scene not allowed: {gameConfig.startingScene}");
			yield break;
		}

		// Rebuild Typescript
		var shouldRecompile = !skipBuild;
		var shouldResumeTypescriptWatch = shouldRecompile && TypescriptCompilationService.IsWatchModeRunning;
		
		// We want to do a full publish
		
		if (shouldRecompile) {
			TypescriptCompilationService.StopCompilers();
			
			var compileFlags = TypeScriptCompileFlags.Publishing | TypeScriptCompileFlags.DisplayProgressBar; // FullClean will clear the incremental file & Publishing will omit editor data

			if (skipBuild) {
				compileFlags |= TypeScriptCompileFlags.SkipReimportQueue; // code publish does not require asset reimport
			}
			
			TypescriptCompilationService.BuildTypescript(compileFlags);
		}
		
		if (TypescriptCompilationService.ErrorCount > 0) {
			Debug.LogError($"Could not publish the project with {TypescriptCompilationService.ErrorCount} compilation error{(TypescriptCompilationService.ErrorCount == 1 ? "" : "s")}");
			if (shouldResumeTypescriptWatch) TypescriptCompilationService.StartCompilerServices();
			yield break;
		}

		// Create deployment
		DeploymentDto deploymentDto = null;
		string devKey = null;
		{
			List<string> platformStrings = new();
			platformStrings.Add("Mac");
			platformStrings.Add("Windows");
			if (gameConfig.supportsMobile) {
				platformStrings.Add("iOS");
				platformStrings.Add("Android");
			}
			var packageSlugs = gameConfig.packages.Select((p) => p.id);
			for (int i = 0; i < possibleKeys.Count; i++) {
				devKey = possibleKeys[i];
				using UnityWebRequest req = UnityWebRequest.Post(
					$"{AirshipPlatformUrl.deploymentService}/game-versions/create-deployment", JsonUtility.ToJson(
						new CreateGameDeploymentDto() {
							gameId = gameConfig.gameId,
							minPlayerVersion = "1",
							defaultScene = gameConfig.startingScene.name,
							deployCode = true,
							deployAssets = platforms.Length > 0,
							packageSlugs = packageSlugs.ToArray(),
							platforms = platformStrings.ToArray(),
						}), "application/json");
				req.SetRequestHeader("Authorization", "Bearer " + devKey);
				yield return req.SendWebRequest();
				while (!req.isDone) {
					yield return null;
				}

				var lastPossibleKey = i == possibleKeys.Count - 1;
				if (req.result != UnityWebRequest.Result.Success) {
					if (lastPossibleKey) {
						Debug.LogError("Failed to create deployment: " + req.error + " " + req.downloadHandler.text);
						yield break;
					} else {
						continue;
					}
				}

				deploymentDto = JsonUtility.FromJson<DeploymentDto>(req.downloadHandler.text);
				break;
			}
		}
		
		// We shouldn't get here. It will fail above.
		if (deploymentDto == null || devKey == null) {
			Debug.LogError("No valid authorization.");
			yield break;
		}
		
		// code.zip
		AirshipEditorUtil.EnsureDirectory(Path.Join("bundles", "uploads"));
		var codeZipPath = Path.Join("bundles", "uploads", "code.zip");
		{
			var st = Stopwatch.StartNew();
			var binaryFileGuids = AssetDatabase.FindAssets("t:" + nameof(AirshipScript));
			var paths = new List<string>();
			foreach (var guid in binaryFileGuids) {
				var path = AssetDatabase.GUIDToAssetPath(guid).ToLower();
				if (path.StartsWith("assets/airshippackages")) {
					continue;
				}
				paths.Add(path);
			}
			
			var airshipBuildInfoGuids = AssetDatabase.FindAssets("t:" + nameof(AirshipBuildInfo));
			foreach (var guid in airshipBuildInfoGuids) {
				var path = AssetDatabase.GUIDToAssetPath(guid).ToLower();
				paths.Add(path);
			}

			if (File.Exists(codeZipPath)) {
				File.Delete(codeZipPath);
			}
			var codeZip = new ZipFile();
			foreach (var path in paths) {
				if (path.EndsWith(".asbuildinfo")) {
					codeZip.AddEntry(path, File.ReadAllBytes(path));
					continue;
				}
				
				// GetOutputPath is case sensitive so hacky workaround is to make our path start with capital "A"
				var luaOutPath = TypescriptProjectsService.Project.GetOutputPath(path.Replace("assets/", "Assets/"));
				if (!File.Exists(luaOutPath)) {
					Debug.LogWarning("Missing lua file: " + luaOutPath);
					continue;
				}

				// We want a .lua in the same spot the .ts would be
				var luaFakePath = path.Replace(".ts", ".lua");
				var bytes = File.ReadAllBytes(luaOutPath);
				codeZip.AddEntry(luaFakePath, bytes);

				var jsonPath = luaOutPath + ".json~";
				if (File.Exists(jsonPath)) {
					// var jsonBytes = File.ReadAllBytes(jsonPath);
					codeZip.AddEntry(luaFakePath + ".json~", "");
				}
			}
			codeZip.Save(codeZipPath);

			Debug.Log("Created code.zip in " + st.ElapsedMilliseconds + " ms.");
		}

		// Save starting build target so we can swap back to it after completion.
		var startingBuildTarget = EditorUserBuildSettings.activeBuildTarget;
		var startingBuildGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

		// Build the game
		if (!skipBuild) {
			var success = CreateAssetBundles.BuildPlatforms(platforms, useCache);
			if (!success) {
				Debug.Log("Cancelled publish.");
				yield break;
			}
		}

		if (EditorIntegrationsConfig.instance.buildWithoutUpload) {
			if (shouldResumeTypescriptWatch) TypescriptCompilationService.StartCompilerServices();
			Debug.Log("Build without upload is enabled. Ending early. You can now view bundles using AssetBundle browser.");
			yield break;
		}

		// Save gameConfig.json so we can upload it
		var gameConfigJson = gameConfig.ToJson();
		var gameConfigParentPath = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild");
		Directory.CreateDirectory(gameConfigParentPath);
		var gameConfigPath = Path.Combine(gameConfigParentPath, "gameConfig.json");
		File.WriteAllText(gameConfigPath, gameConfigJson);

		var urls = deploymentDto.urls;

		var uploadList = new List<IEnumerator>() {
			UploadSingleGameFile(urls.gameConfig, "gameConfig.json", null),
			UploadSingleGameFile(urls.code, codeZipPath, null, true),
		};

		if (platforms.Length > 0) {
			uploadList.AddRange(new List<IEnumerator>() {
				// UploadSingleGameFile(urls.Linux_client_resources, $"{AirshipPlatform.Windows}/client/resources", AirshipPlatform.Linux),
				// UploadSingleGameFile(urls.Linux_client_scenes, $"{AirshipPlatform.Windows}/client/scenes", AirshipPlatform.Linux),
				// UploadSingleGameFile(urls.Linux_shared_resources, $"{AirshipPlatform.Windows}/shared/resources", AirshipPlatform.Linux),
				// UploadSingleGameFile(urls.Linux_shared_scenes, $"{AirshipPlatform.Windows}/shared/scenes", AirshipPlatform.Linux),
				// UploadSingleGameFile(urls.Linux_server_resources, $"{AirshipPlatform.Windows}/server/resources", AirshipPlatform.Linux),
				// UploadSingleGameFile(urls.Linux_server_scenes, $"{AirshipPlatform.Windows}/server/scenes", AirshipPlatform.Linux),

				// UploadSingleGameFile(urls.Mac_client_resources, $"{AirshipPlatform.Mac}/client/resources", AirshipPlatform.Mac),
				// UploadSingleGameFile(urls.Mac_client_scenes, $"{AirshipPlatform.Mac}/client/scenes", AirshipPlatform.Mac),
				UploadSingleGameFile(urls.Mac_shared_resources, $"{AirshipPlatform.Mac}/shared/resources", AirshipPlatform.Mac),
				UploadSingleGameFile(urls.Mac_shared_scenes, $"{AirshipPlatform.Mac}/shared/scenes", AirshipPlatform.Mac),

				// UploadSingleGameFile(urls.Windows_client_resources, $"{AirshipPlatform.Windows}/client/resources", AirshipPlatform.Windows),
				// UploadSingleGameFile(urls.Windows_client_scenes, $"{AirshipPlatform.Windows}/client/scenes", AirshipPlatform.Windows),
				UploadSingleGameFile(urls.Windows_shared_resources, $"{AirshipPlatform.Windows}/shared/resources", AirshipPlatform.Windows),
				UploadSingleGameFile(urls.Windows_shared_scenes, $"{AirshipPlatform.Windows}/shared/scenes", AirshipPlatform.Windows),
			});

			if (gameConfig.supportsMobile) {
				uploadList.AddRange(new List<IEnumerator>() {
					UploadSingleGameFile(urls.iOS_shared_resources, $"{AirshipPlatform.iOS}/shared/resources", AirshipPlatform.iOS),
					UploadSingleGameFile(urls.iOS_shared_scenes, $"{AirshipPlatform.iOS}/shared/scenes", AirshipPlatform.iOS),

					UploadSingleGameFile(urls.Android_shared_resources, $"{AirshipPlatform.Android}/shared/resources", AirshipPlatform.Android),
					UploadSingleGameFile(urls.Android_shared_scenes, $"{AirshipPlatform.Android}/shared/scenes", AirshipPlatform.Android),
				});
			}
		}

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
		float totalCodeSize = 0;
		foreach (var (_, uploadInfo) in uploadProgress) {
			totalSize += uploadInfo.sizeBytes;
			if (uploadInfo.platform == AirshipPlatform.Mac) {
				totalMacSize += uploadInfo.sizeBytes;
			} else if (uploadInfo.platform == null) {
				totalCodeSize += uploadInfo.sizeBytes;
			}
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
				if (uploadInfo.failed) {
					Debug.LogError("Publish failed due to upload error.");
					EditorUtility.ClearProgressBar();
					yield break;
				}
				if (!uploadInfo.isComplete) {
					finishedUpload = false;
				}

				totalBytes += uploadInfo.uploadedBytes;
				totalProgress += uploadInfo.uploadProgressPercent * (uploadInfo.sizeBytes / totalSize);
			}

			/*
			bool cancelled = EditorUtility.DisplayCancelableProgressBar("Publishing game", $"Uploading Game: {getSizeText(totalBytes)} / {getSizeText(totalSize)}", totalProgress);
			if (cancelled) {
				Debug.Log("Publish cancelled.");
				EditorUtility.ClearProgressBar();
				yield break;
			}
			*/
			Debug.Log($"Uploading Game: {AirshipEditorUtil.GetFileSizeText(totalBytes)} / {AirshipEditorUtil.GetFileSizeText(totalSize)}");
			yield return null;
		}
		EditorUtility.ClearProgressBar();

		string sizeSnippet = $" (game size: {AirshipEditorUtil.GetFileSizeText(totalMacSize)})";
		if (platforms.Length == 0) {
			sizeSnippet = $" (code size: {AirshipEditorUtil.GetFileSizeText(totalCodeSize)})";
		}

		Debug.Log($"Completed upload{sizeSnippet}. Finalizing publish...");

		// Complete deployment
		{
			List<string> uploadedFileIds = new();
			uploadedFileIds.Add("Mac_shared_resources");
			uploadedFileIds.Add("Mac_shared_scenes");
			uploadedFileIds.Add("Windows_shared_resources");
			uploadedFileIds.Add("Windows_shared_scenes");
			if (gameConfig.supportsMobile) {
				uploadedFileIds.Add("iOS_shared_resources");
				uploadedFileIds.Add("iOS_shared_scenes");
				uploadedFileIds.Add("Android_shared_resources");
				uploadedFileIds.Add("Android_shared_scenes");
			}

			int attemptNum = 0;
			while (attemptNum < 5) {
				// Debug.Log("Complete. GameId: " + gameConfig.gameId + ", assetVersionId: " + deploymentDto.version.assetVersionNumber);
				UnityWebRequest req = UnityWebRequest.Post(
					$"{AirshipPlatformUrl.deploymentService}/game-versions/complete-deployment", JsonUtility.ToJson(
						new CompleteGameDeploymentDto() {
							gameId = gameConfig.gameId,
							gameVersionId = deploymentDto.version.gameVersionId,
							uploadedFileIds = uploadedFileIds.ToArray(),
						}), "application/json");
				req.SetRequestHeader("Authorization", "Bearer " + devKey);
				yield return req.SendWebRequest();
				while (!req.isDone) {
					yield return null;
				}

				if (req.result == UnityWebRequest.Result.Success) {
					break;
				} else {
					Debug.LogError("Failed to complete deployment: " + req.error + " " + req.downloadHandler.text);
					if (req.responseCode == 400) {
						// don't retry on 400
						yield break;
					}

                    if (attemptNum == 4) {
	                    // Out of retry attempts so we end it here.
                    	yield break;
                    }

                    // Wait one second and try again.
                    int waitTime = 1;
                    if (attemptNum >= 3) {
	                    waitTime = 3;
                    }
                    Debug.Log($"Retrying in {waitTime}s...");
                    attemptNum++;
                    yield return new WaitForSeconds(waitTime);
				}
			}
		}

		var slug = activeDeployTarget.slug;
		if (slug == null) {
			slug = activeDeployTarget.id;
		}
		if (slug != null) {
			string gameLink;
			#if AIRSHIP_STAGING
			gameLink = $"<a href=\"https://staging.airship.gg/p/{slug}\">staging.airship.gg/p/{slug}</a>";
			#else
			gameLink = $"<a href=\"https://airship.gg/p/{slug}\">airship.gg/p/{slug}</a>";
			#endif
			EditorUtility.DisplayDialog("Your game is live!", "Your publish succeeded and your game is live on Airship.", "OK");
			Debug.Log($"<color=#77f777>Finished publish! Your game is live:</color> {gameLink}");	
		} else {
			Debug.Log("<color=#77f777>Finished publish! Your game is live.</color> ");
		}

		if (shouldResumeTypescriptWatch) TypescriptCompilationService.StartCompilerServices();
		// Switch back to starting build target
		// EditorUserBuildSettings.SwitchActiveBuildTarget(startingBuildGroup, startingBuildTarget);
	}
	
	private static IEnumerator UploadSingleGameFile(string url, string filePath, AirshipPlatform? platform, bool absolutePath = false) {
		var uploadInfo = new UploadInfo();
		if (platform.HasValue) uploadInfo.platform = platform.Value;
		uploadProgress[url] = uploadInfo;
		
		var gameConfig = GameConfig.Load();
		var gameDir = Path.Combine(AssetBridge.GamesPath, gameConfig.gameId + "_vLocalBuild");

		string bundleFilePath;
		if (absolutePath) {
			bundleFilePath = filePath;
		} else {
			bundleFilePath = gameDir + "/" + filePath;
		}
		var bytes = File.ReadAllBytes(bundleFilePath);
		uploadInfo.sizeBytes = bytes.Length;

		ulong sizeKb = (ulong)uploadInfo.sizeBytes / 1_000;
		if (sizeKb > MAX_UPLOAD_KB) {
			Debug.LogError($"Game bundle {filePath} is greater than max size (500mb). You can reduce file size by lowering max texture size. For more help on reducing file size, visit: https://docs.airship.gg/optimization/reducing-bundle-size");
			if (uploadProgress.TryGetValue(url, out var p)) {
				p.failed = true;
			}

			yield break;
		}

		var req = UnityWebRequest.Put(url, bytes);
		req.SetRequestHeader("x-goog-content-length-range", "0,500000000");
		yield return req.SendWebRequest();

		while (!req.isDone) {
			uploadInfo.uploadProgressPercent = req.uploadProgress;
			uploadInfo.uploadedBytes = req.uploadedBytes;
			yield return new WaitForEndOfFrame();
		}

		if (req.result != UnityWebRequest.Result.Success) {
			Debug.LogError("Failed to upload " + filePath + " " + req.result + " " + req.downloadHandler.text);
			if (uploadProgress.TryGetValue(url, out var p)) {
				p.failed = true;
			}
			yield break;
		}

		if (uploadProgress.TryGetValue(url, out var progress)) {
			progress.uploadProgressPercent = 1;
			progress.isComplete = true;
		}
	}

	private static void UploadPublishForm(List<IMultipartFormSection> formData) {
		Debug.Log("Uploading to deploy service");
		UnityWebRequest req = UnityWebRequest.Post(AirshipPlatformUrl.deploymentService + "/game-versions/upload", formData);
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

	[MenuItem("Airship/Publish Game", priority = 50)]
	public static void PromptPublish() {
		var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
		if (gameConfig != null) {
			DisplayPublishDialogue(gameConfig);
		} else {
			Debug.LogError("Couldn't find GameConfig (at Assets/GameConfig.asset)");
		}
	}
	
	private static async void DisplayPublishDialogue(GameConfig gameConfig) {
		var gameInfo = await GameConfigEditor.TryFetchFirstGame();
		if (!gameInfo.HasValue) {
			gameInfo = gameConfig.gameId.Length == 0
				? null
				: await EditorAuthManager.GetGameInfo(gameConfig.gameId);
		}

		if (!gameInfo.HasValue) {
            var selectTarget = EditorUtility.DisplayDialog(
                "No Publish Target",
                "You have to select a game target before you can publish your game.",
                "Select target",
                "Cancel"
            );
            if (selectTarget) {
                FocusGameConfigPublishTarget();
            }
            return;
        }

        activeDeployTarget = gameInfo.Value;
        var option = EditorUtility.DisplayDialogComplex(
            $"Publish {gameInfo.Value.name}",
            $"Are you sure you want to publish {gameInfo.Value.name}?",
            "Publish",
            "Cancel",
            "Change target");

        switch (option) {
            case 0: // Publish
                Deploy.PublishGame();
                break;
            case 1: // Cancel
                break;
            case 2: // Change target
                FocusGameConfigPublishTarget();
                break;
        }
    }

    private static void FocusGameConfigPublishTarget() {
        GameConfigEditor.markPublishTargetPinged = true;
        GameConfigEditor.FocusGameConfig();
    }
}
