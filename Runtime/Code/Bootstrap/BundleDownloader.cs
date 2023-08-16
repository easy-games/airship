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

public class BundleDownloader : MonoBehaviour
{

	public static string GetBundleVersionCacheFilePath(string bundleId) {
		var versionCacheFileName = $"{bundleId}_bundle_version";
		return Path.Join(AssetBridge.GamesPath, versionCacheFileName);
	}

	public IEnumerator DownloadBundles(string cdnUrl, AirshipPackage[] bundles, [CanBeNull] RemoteBundleFile[] privateRemoteFiles = null) {
		// Find which bundles we can skip due to caching
		List<AirshipPackage> bundlesToDownload = new();
		foreach (var bundle in bundles) {
			if (File.Exists(Path.Join(bundle.GetBuiltAssetBundleDirectory(), "successfulDownload.txt"))) {
				Debug.Log($"Bundle {bundle.id} v" + bundle.version + " is cached. Skipping download.");
				continue;
			}
			bundlesToDownload.Add(bundle);
		}

		if (bundlesToDownload.Count == 0) {
			yield break;
		}

		List<RemoteBundleFile> remoteBundleFiles = new();
		var platform = AirshipPlatformUtil.FromRuntimePlatform(Application.platform);
		foreach (var bundle in bundlesToDownload) {
			remoteBundleFiles.AddRange(bundle.GetPublicRemoteBundleFiles(cdnUrl, platform));
		}

		if (privateRemoteFiles != null)
		{
			remoteBundleFiles.AddRange(privateRemoteFiles);
		}

		var coreLoadingScreen = FindObjectOfType<CoreLoadingScreen>();

		AirshipPackage GetBundleFromId(string bundleId) {
			foreach (var bundle in bundles) {
				if (bundle.id == bundleId) {
					return bundle;
				}
			}

			return null;
		}

		// Download files
		var bundleNumber = 1;
		var requests = new List<UnityWebRequestAsyncOperation>(10);
		foreach (var remoteBundleFile in remoteBundleFiles)
		{
			var request = new UnityWebRequest(remoteBundleFile.Url);
			var bundle = GetBundleFromId(remoteBundleFile.BundleId);

			// Note: We should be downloading this into a "bedwars" and "core" folders, respectively.
			//var bundleFileName = Path.GetFileName(remoteBundleFile.File);
			string path = Path.Combine(bundle.GetBuiltAssetBundleDirectory(), platform.ToString(), remoteBundleFile.fileName);
			Debug.Log($"Downloading Airship Bundle. url={remoteBundleFile.Url}, downloadPath={path}");

			request.downloadHandler = new DownloadHandlerFile(path);

			if (coreLoadingScreen)
			{
				StartCoroutine(WatchDownloadStatus(request, bundleNumber, remoteBundleFiles.Count, coreLoadingScreen));
			}

			requests.Add(request.SendWebRequest());
			bundleNumber++;
		}

		yield return new WaitUntil(() => AllRequestsDone(requests));

		HashSet<AirshipPackage> successfulDownloads = new();
		int i = 0;
		foreach (var request in requests) {
			var remoteBundleFile = remoteBundleFiles[i];
			if (request.webRequest.result != UnityWebRequest.Result.Success)
			{
				Debug.Log($"Bundle file not found: {remoteBundleFile.Url}. This can be okay.");
			}
			else
			{
				var size = Math.Floor((request.webRequest.downloadedBytes / 1000000f) * 10) / 10;
				Debug.Log($"Downloaded bundle file {remoteBundleFile.BundleId}.${remoteBundleFile.fileName}: {size}mb.");

				var bundle = GetBundleFromId(remoteBundleFile.BundleId);
				if (bundle != null) {
					successfulDownloads.Add(bundle);
				}
			}
			i++;
		}

		// Update version cache
		foreach (var bundle in successfulDownloads) {
			var versionCachePath = GetBundleVersionCacheFilePath(bundle.id);
			using var writer = new StreamWriter(versionCachePath);
			writer.Write(bundle.version);
		}

		Debug.Log("Finished downloading bundles.");
	}

	private IEnumerator WatchDownloadStatus(UnityWebRequest req, int bundleIndex, int bundleCount, CoreLoadingScreen coreLoadingScreen)
	{
		while (!req.isDone)
		{
			var downloadedMb = req.downloadedBytes / 1_000_000f;
			var totalMb = 0.1f;
			if (req.downloadProgress > 0)
			{
				totalMb = (req.downloadedBytes * (1 / req.downloadProgress)) / 1_000_000f;
				if (totalMb < 0.1f)
				{
					totalMb = 0.1f;
				}
			}

			var progress = (bundleIndex + req.downloadProgress) / bundleCount;
			progress = 5 + progress * 40;

            // Downloading Content (0.10/2.40mb) (1/6)
            coreLoadingScreen.SetProgress(String.Format("Downloading Content ({2}/{3}) ({0:0.00}/{1:0.00}mb)", new object[] {downloadedMb, totalMb, bundleIndex, bundleCount}), progress);

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