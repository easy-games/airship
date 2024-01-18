using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Code.Bootstrap;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;

public class BundleDownloader : Singleton<BundleDownloader> {
	private Dictionary<int, float> downloadProgress = new();
	private Dictionary<int, float> totalDownload = new();
	private bool isDownloading = false;
	private int bundleDownloadCount = 0;

	public static string GetBundleVersionCacheFilePath(string bundleId) {
		var versionCacheFileName = $"{bundleId}_bundle_version";
		return Path.Join(AssetBridge.GamesPath, versionCacheFileName);
	}

	public IEnumerator DownloadBundles(
		string cdnUrl,
		AirshipPackage[] packages,
		[CanBeNull] RemoteBundleFile[] privateRemoteFiles = null,
		[CanBeNull] BundleLoadingScreen loadingScreen = null
	) {
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
		List<RemoteBundleFile> filesToDownload = new();
		foreach (var remoteBundleFile in remoteBundleFiles) {
			var bundle = GetBundleFromId(remoteBundleFile.BundleId);
			string path = Path.Combine(bundle.GetBuiltAssetBundleDirectory(platform), remoteBundleFile.fileName);
			string downloadSuccessPath = path + "_downloadSuccess.txt";
			if (File.Exists(downloadSuccessPath)) {
				Debug.Log($"Skipping cached download: {remoteBundleFile.BundleId}/{remoteBundleFile.fileName}");
				continue;
			}
			filesToDownload.Add(remoteBundleFile);
		}

		// Download files
		var bundleIndex = 0;
		this.totalDownload.Clear();
		this.downloadProgress.Clear();
		var requests = new List<UnityWebRequestAsyncOperation>(10);
		this.isDownloading = true;
		foreach (var remoteBundleFile in filesToDownload)
		{
			var request = new UnityWebRequest(remoteBundleFile.Url);
			var bundle = GetBundleFromId(remoteBundleFile.BundleId);
			string path = Path.Combine(bundle.GetBuiltAssetBundleDirectory(platform), remoteBundleFile.fileName);
			Debug.Log($"Downloading Airship Bundle {remoteBundleFile.BundleId}/{remoteBundleFile.fileName}. url={remoteBundleFile.Url}, downloadPath={path}");

			request.downloadHandler = new DownloadHandlerFile(path);

			if (loadingScreen != null)
			{
				StartCoroutine(WatchDownloadStatus(request, bundleIndex));
				StartCoroutine(UpdateDownloadProgressBar(loadingScreen));
			}

			requests.Add(request.SendWebRequest());
			bundleIndex++;
		}
		this.bundleDownloadCount = bundleIndex;

		yield return new WaitUntil(() => AllRequestsDone(requests));
		this.isDownloading = false;

		HashSet<AirshipPackage> successfulDownloads = new();
		int i = 0;
		foreach (var request in requests) {
			var remoteBundleFile = filesToDownload[i];
			bool success = false;

			if (request.webRequest.result != UnityWebRequest.Result.Success) {
				var statusCode = request.webRequest.responseCode;
				if (statusCode == 404) {
					// still count this as a success so we don't try to download it again
					success = true;
					Debug.Log($"Remote bundle file 404: {remoteBundleFile.Url}");
					var bundle = GetBundleFromId(remoteBundleFile.BundleId);
					if (bundle != null) {
						string path = Path.Combine(bundle.GetBuiltAssetBundleDirectory(platform), remoteBundleFile.fileName);
						File.Delete(path);
					}
				} else {
					Debug.LogError($"Failed to download bundle file. Url={remoteBundleFile.Url} StatusCode={statusCode}");
					Debug.LogError(request.webRequest.error);
				}
			} else {
				var size = Math.Floor((request.webRequest.downloadedBytes / 1000000f) * 10) / 10;
				Debug.Log($"Downloaded bundle file {remoteBundleFile.BundleId}/{remoteBundleFile.fileName} ({size} MB)");
				success = true;
			}

			if (success) {
				var bundle = GetBundleFromId(remoteBundleFile.BundleId);
				if (bundle != null) {
					string path = Path.Combine(bundle.GetBuiltAssetBundleDirectory(platform), remoteBundleFile.fileName);
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

		Debug.Log("Finished downloading bundles.");
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
}