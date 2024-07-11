using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Code.Bootstrap;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class BundleDownloader : Singleton<BundleDownloader> {
	private Dictionary<int, float> downloadProgress = new();
	private Dictionary<int, float> totalDownload = new();
	private bool isDownloading = false;
	[NonSerialized] public bool downloadAccepted = false;

	public IEnumerator DownloadBundles(
		string cdnUrl,
		AirshipPackage[] packages,
		[CanBeNull] RemoteBundleFile[] privateRemoteFiles = null,
		[CanBeNull] BundleLoadingScreen loadingScreen = null,
		[CanBeNull] string gameCodeZipUrl = null,
		bool downloadCodeZipOnClient = false
	) {
		var totalSt = Stopwatch.StartNew();
		var platform = AirshipPlatformUtil.GetLocalPlatform();

		List<RemoteBundleFile> remoteBundleFiles = new();
		foreach (var package in packages) {
			remoteBundleFiles.AddRange(package.GetPublicRemoteBundleFiles(cdnUrl, platform));
		}

		if (privateRemoteFiles != null)
		{
			remoteBundleFiles.AddRange(privateRemoteFiles);
		}

		[CanBeNull]
		AirshipPackage GetBundleFromId(string bundleId) {
			foreach (var bundle in packages) {
				if (bundle.id == bundleId) {
					return bundle;
				}
			}

			return null;
		}

		// Filter out if cache exists
		List<RemoteBundleFile> bundleFilesToDownload = new();
		foreach (var remoteBundleFile in remoteBundleFiles) {
			var bundle = GetBundleFromId(remoteBundleFile.BundleId);
			string path = Path.Combine(bundle.GetPersistentDataDirectory(platform), remoteBundleFile.fileName);
			string downloadSuccessPath = path + "_downloadSuccess.txt";
			if (File.Exists(downloadSuccessPath)) {
				// Debug.Log($"Skipping cached download: {remoteBundleFile.BundleId}/{remoteBundleFile.fileName}");
				continue;
			}
			bundleFilesToDownload.Add(remoteBundleFile);
		}

		// Calculate total download size
		var device = DeviceBridge.GetDeviceType();
		if (device is AirshipDeviceType.Phone or AirshipDeviceType.Tablet && loadingScreen && loadingScreen.showContinueButton && bundleFilesToDownload.Count > 0) {
			var preRequests = new List<UnityWebRequestAsyncOperation>(10);
			foreach (var remoteBundleFile in bundleFilesToDownload) {
				var request = new UnityWebRequest(remoteBundleFile.Url, "HEAD");
				preRequests.Add(request.SendWebRequest());
			}

			yield return new WaitUntil(() => AllRequestsDone(preRequests));

			long totalBytes = 0;
			foreach (var request in preRequests) {
				var contentLength = request.webRequest.GetResponseHeader("content-length");
				var bytes = long.Parse(contentLength);
				totalBytes += bytes;
			}

			loadingScreen.SetTotalDownloadSize(totalBytes);
			yield return new WaitUntil(() => this.downloadAccepted);
		}

		var downloadSt = Stopwatch.StartNew();
		// Download files
		var bundleIndex = 0;
		this.totalDownload.Clear();
		this.downloadProgress.Clear();
		var requests = new List<UnityWebRequestAsyncOperation>(10);
		this.isDownloading = true;
		foreach (var remoteBundleFile in bundleFilesToDownload) {
			var request = new UnityWebRequest(remoteBundleFile.Url);
			var package = GetBundleFromId(remoteBundleFile.BundleId);
			string path = Path.Combine(package.GetPersistentDataDirectory(platform), remoteBundleFile.fileName);
			// Debug.Log($"Downloading Airship Bundle {remoteBundleFile.BundleId}/{remoteBundleFile.fileName}. url={remoteBundleFile.Url}, downloadPath={path}");

			request.downloadHandler = new DownloadHandlerFile(path);

			if (loadingScreen != null) {
				StartCoroutine(WatchDownloadStatus(request, bundleIndex));
				StartCoroutine(UpdateDownloadProgressBar(loadingScreen));
			}

			requests.Add(request.SendWebRequest());
			bundleIndex++;
		}
		this.bundleDownloadCount = bundleIndex;

		// Download code.zip
		if (RunCore.IsServer() || downloadCodeZipOnClient) {
			foreach (var package in packages) {
				string codeZipUrl;
				if (package.packageType == AirshipPackageType.Game) {
					if (string.IsNullOrEmpty(gameCodeZipUrl)) {
						throw new Exception("Expected gameCodeZipUrl to exist but was null.");
					}
					codeZipUrl = gameCodeZipUrl;
				} else {
					codeZipUrl = $"{cdnUrl}/package/{package.id.ToLower()}/code/{package.codeVersion}/code.zip";
				}

				if (File.Exists(Path.Join(package.GetPersistentDataDirectory(), "code_version_" + package.codeVersion + ".txt"))) {
					Debug.Log(package.id + " code.zip is cached. skipping.");
					continue;
				}

				var request = new UnityWebRequest(codeZipUrl);
				string path = Path.Combine(package.GetPersistentDataDirectory(), "code.zip");
				Debug.Log($"Downloading {package.id}/code.zip. url={codeZipUrl}");

				request.downloadHandler = new DownloadHandlerFile(path);
				requests.Add(request.SendWebRequest());
			}
		}

		yield return new WaitUntil(() => AllRequestsDone(requests));
		this.isDownloading = false;
		Debug.Log($"Finished downloading bundle content in {downloadSt.ElapsedMilliseconds} ms.");

		HashSet<AirshipPackage> successfulDownloads = new();
		int i = 0;
		foreach (var request in requests) {
			if (i >= bundleFilesToDownload.Count) break; // code.zip requests
			var remoteBundleFile = bundleFilesToDownload[i];
			bool success = false;

			if (request.webRequest.result != UnityWebRequest.Result.Success) {
				var statusCode = request.webRequest.responseCode;
				if (statusCode == 404) {
					// still count this as a success so we don't try to download it again
					if (RunCore.IsServer()) {
						success = true;
					}
					Debug.Log($"Remote bundle file 404: {remoteBundleFile.fileName}");
					var bundle = GetBundleFromId(remoteBundleFile.BundleId);
					if (bundle != null) {
						string path = Path.Combine(bundle.GetPersistentDataDirectory(platform),
							remoteBundleFile.fileName);
						File.Delete(path);
					}
				} else {
					Debug.LogError(
						$"Failed to download bundle file. Url={remoteBundleFile.Url} StatusCode={statusCode}");
					Debug.LogError(request.webRequest.error);
				}
			} else if (!string.IsNullOrEmpty(request.webRequest.downloadHandler.error)) {
				Debug.LogError($"File download handler failed on bundle file {remoteBundleFile.fileName}. Error: {request.webRequest.downloadHandler.error}");
			}  else {
				var size = Math.Floor((request.webRequest.downloadedBytes / 1000000f) * 10) / 10;
				Debug.Log(
					$"Downloaded bundle file {remoteBundleFile.BundleId}/{remoteBundleFile.fileName} ({size} MB)");
				success = true;
			}

			if (!success && RunCore.IsServer()) {
				var serverBootstrap = FindAnyObjectByType<ServerBootstrap>();
				if (serverBootstrap.IsAgonesEnvironment()) {
					Debug.LogError("[SEVERE] Server failed to download bundles. Shutting down!");
					serverBootstrap.agones.Shutdown().Wait();
				}
			}

			if (success) {
				var bundle = GetBundleFromId(remoteBundleFile.BundleId);
				if (bundle != null) {
					string path = Path.Combine(bundle.GetPersistentDataDirectory(platform), remoteBundleFile.fileName);
					var parentFolder = Path.GetDirectoryName(path);
					if (!Directory.Exists(parentFolder) && parentFolder != null) {
						Directory.CreateDirectory(parentFolder);
					}

					string downloadSuccessPath = path + "_downloadSuccess.txt";
					File.WriteAllText(downloadSuccessPath, "");
					successfulDownloads.Add(bundle);
				}
			}

			i++;
		}

		// code.zip: handle request results. Downloads have completed by this point.
		var unzipCodeSt = Stopwatch.StartNew();
		int packageI = 0;
		bool didCodeUnzip = false;
		for (i = i; i < requests.Count; i++) {
			var request = requests[i];
			var package = packages[packageI];
			if (request.webRequest.result != UnityWebRequest.Result.Success) {
				var statusCode = request.webRequest.responseCode;
				Debug.LogError("Failed to download code.zip. StatusCode=" + statusCode + " Error=" + request.webRequest.error + " Url=" + request.webRequest.uri + " Package=" + package.id);
				var codeZipPath = Path.Join(package.GetPersistentDataDirectory(), "code.zip");
				if (File.Exists(codeZipPath)) {
					File.Delete(codeZipPath);
				}
				loadingScreen.SetError("Failed to download Main Menu scripts.");
				if (RunCore.IsServer()) {
					var serverBootstrap = FindAnyObjectByType<ServerBootstrap>();
					if (serverBootstrap.IsAgonesEnvironment()) {
						Debug.LogError("[SEVERE] Server failed to download code.zip. Shutting down!");
						serverBootstrap.agones.Shutdown().Wait();
					}
				}
			} else {
				File.WriteAllText(Path.Join(package.GetPersistentDataDirectory(), "code_version_" + package.codeVersion + ".txt"), "success");
			}

			didCodeUnzip = true;
			packageI++;
		}

		// Delete old versions
		var st = Stopwatch.StartNew();
		foreach (var package in packages) {
			var oldVersionFolders = package.GetOlderDataDirectories(platform);
			foreach (var oldVersionPath in oldVersionFolders) {
				Debug.Log("Deleting old package folder: " + oldVersionPath);
				Directory.Delete(oldVersionPath, true);
			}
			Debug.Log($"Deleted old {package.id} versions in " + st.ElapsedMilliseconds + " ms.");
		}

		if (didCodeUnzip) {
			Debug.Log($"Unzipped code.zip in {unzipCodeSt.ElapsedMilliseconds} ms.");
		}
		Debug.Log($"Completed bundle downloader step in {totalSt.ElapsedMilliseconds} ms.");
	}

	private int bundleDownloadCount = 0;

	public static string GetBundleVersionCacheFilePath(string bundleId) {
		var versionCacheFileName = $"{bundleId}_bundle_version";
		return Path.Join(AssetBridge.GamesPath, versionCacheFileName);
	}

	private IEnumerator UpdateDownloadProgressBar(BundleLoadingScreen loadingScreen) {
		while (this.isDownloading) {
			float downloadedMb = 0f;
			float totalMb = 0f;
			for (int i = 0; i < this.bundleDownloadCount; i++) {
				if (this.downloadProgress.TryGetValue(i, out var progress)) {
					downloadedMb += progress;
				}

				if (this.totalDownload.TryGetValue(i, out var total)) {
					totalMb += total;
				} else {
					totalMb += 0.1f; // guess 0.1mb
				}
			}

			loadingScreen.SetProgress(String.Format("Downloading Content ({0:0.00}/{1:0.00} MB)", new object[] {downloadedMb, totalMb}), 0);
			yield return null;
		}
	}

	private IEnumerator WatchDownloadStatus(UnityWebRequest req, int bundleIndex)
	{
		while (!req.isDone)
		{
			var downloadedMb = req.downloadedBytes / 1_000_000f;
			this.downloadProgress[bundleIndex] = downloadedMb;

			float totalMb = 0.1f;
			if (req.downloadProgress > 0) {
				totalMb = (req.downloadedBytes * (1 / req.downloadProgress)) / 1_000_000f;
				if (totalMb < 0.1f) {
					totalMb = 0.1f;
				}
			}
			this.totalDownload[bundleIndex] = totalMb;
			yield return null;
		}
	}

	private bool AllRequestsDone(List<UnityWebRequestAsyncOperation> requests)
	{
		// A little Linq magic
		// returns true if All requests are done
		return requests.All(r => r.isDone);
	}

	public bool IsDownloading() {
		return isDownloading;
	}
}