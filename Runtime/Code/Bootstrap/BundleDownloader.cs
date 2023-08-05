using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Code.Bootstrap;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Networking;

public readonly struct RemoteBundleFile
{
	public string fileName { get; }
	public string Url { get; }
	public string BundleId { get; }

	public RemoteBundleFile(string fileName, string url, string bundleId)
	{
		this.fileName = fileName;
		this.Url = url;
		this.BundleId = bundleId;
	}
}

public class BundleDownloader : MonoBehaviour
{
	public static string GetBundleVersionCacheFilePath(string bundleId) {
		var versionCacheFileName = $"{bundleId}_bundle_version";
		return Path.Join(AssetBridge.BundlesPath, versionCacheFileName);
	}

	public IEnumerator DownloadBundles(StartupConfig startupConfig, AirshipBundle[] bundles, [CanBeNull] RemoteBundleFile[] privateRemoteFiles = null) {
		// Find which bundles we can skip due to caching
		List<AirshipBundle> bundlesToDownload = new();
		foreach (var bundle in bundles) {
			var versionCachePath = GetBundleVersionCacheFilePath(bundle.id);
			if (File.Exists(versionCachePath))
			{
				using var sr = new StreamReader(versionCachePath);
				var version = sr.ReadToEnd();
				Debug.Log($"Cached {bundle.id} bundle version: " + version);
				if (version == bundle.version)
				{
					Debug.Log($"{bundle.id} v${bundle.version} bundle is cached. Skipping download.");
					continue;
				}
			}
			bundlesToDownload.Add(bundle);
		}

		if (bundlesToDownload.Count == 0) {
			yield break;
		}

		List<RemoteBundleFile> remoteBundleFiles = new();
		var platform = GetPlatformString();
		foreach (var bundle in bundlesToDownload) {
			remoteBundleFiles.AddRange(bundle.GetClientAndSharedRemoteBundleFiles(startupConfig.CdnUrl, platform));
		}

		if (privateRemoteFiles != null)
		{
			remoteBundleFiles.AddRange(privateRemoteFiles);
		}

		var coreLoadingScreen = FindObjectOfType<CoreLoadingScreen>();

		AirshipBundle GetBundleFromId(string bundleId) {
			foreach (var bundle in bundles) {
				if (bundle.id == bundleId) {
					return bundle;
				}
			}

			return null;
		}

		// Public files
		var bundleNumber = 1;
		var requests = new List<UnityWebRequestAsyncOperation>(10);
		foreach (var remoteBundleFile in remoteBundleFiles)
		{
			var request = new UnityWebRequest(remoteBundleFile.Url);
			var bundle = GetBundleFromId(remoteBundleFile.BundleId);

			// Note: We should be downloading this into a "bedwars" and "core" folders, respectively.
			//var bundleFileName = Path.GetFileName(remoteBundleFile.File);
			string path;
			if (bundle.bundleType == AirshipBundleType.Game) {
				path = Path.Join(AssetBridge.BundlesPath, remoteBundleFile.BundleId, remoteBundleFile.fileName);
			} else {
				path = Path.Join(Path.Join(AssetBridge.BundlesPath, "imports"), remoteBundleFile.BundleId, remoteBundleFile.fileName);
			}
			
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

		HashSet<AirshipBundle> successfulDownloads = new();
		int i = 0;
		foreach (var request in requests) {
			var remoteBundleFile = remoteBundleFiles[i];
			if (request.webRequest.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"Failed to download bundle. url: {remoteBundleFile.Url}. error: {request.webRequest.error}");
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
			writer.Write(startupConfig.CoreBundleVersion);
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

	public static string GetPlatformString()
	{
		switch (Application.platform)
		{
			case RuntimePlatform.WindowsPlayer:
			case RuntimePlatform.WindowsEditor:
			case RuntimePlatform.WindowsServer:
				return "windows";
			case RuntimePlatform.OSXPlayer:
			case RuntimePlatform.OSXEditor:
			case RuntimePlatform.OSXServer:
				return "mac";
			case RuntimePlatform.Android:
				return "android";
			case RuntimePlatform.IPhonePlayer:
				return "ios";
			case RuntimePlatform.LinuxPlayer:
			case RuntimePlatform.LinuxServer:
			case RuntimePlatform.EmbeddedLinuxArm64:
			case RuntimePlatform.EmbeddedLinuxArm32:
			case RuntimePlatform.EmbeddedLinuxX64:
			case RuntimePlatform.EmbeddedLinuxX86:
				return "linux";
			default:
				return "unknown";
		}
	}
}