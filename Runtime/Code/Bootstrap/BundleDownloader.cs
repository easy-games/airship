using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public readonly struct RemoteBundleFile
{
	public string File { get; }
	public string Url { get; }
	public string BundleId { get; }

	public RemoteBundleFile(string file, string url, string bundleId)
	{
		this.File = file;
		this.Url = url;
		this.BundleId = bundleId;
	}
}

public class BundleDownloader : MonoBehaviour
{
	public IEnumerator DownloadBundles(StartupConfig startupConfig, RemoteBundleFile[] serverBundleFiles = null)
	{
		var downloadCoreBundle = true;

		// Check if core bundle version is cached. If so, skip the download.
		var coreBundleVersionPath = Path.Join(AssetBridge.BundlesPath, BootstrapHelper.CoreBundleVersionFileName);
		if (File.Exists(coreBundleVersionPath))
		{
			using var sr = new StreamReader(coreBundleVersionPath);
			var bundleVersionStr = sr.ReadToEnd();
			Debug.Log($"Cached {BootstrapHelper.CoreBundleId} bundle version: " + bundleVersionStr);
			if (bundleVersionStr == startupConfig.CoreBundleVersion)
			{
				Debug.Log("Core bundle is cached. Skipping download.");
				downloadCoreBundle = false;
			}
		}

		var downloadGameBundle = true;

		// Check if game bundle version is cached. If so, skip the download.
		var gameBundleVersionPath = Path.Join(AssetBridge.BundlesPath, BootstrapHelper.GameBundleVersionFileName);
		if (File.Exists(gameBundleVersionPath))
		{
			using var sr = new StreamReader(gameBundleVersionPath);
			var bundleVersionStr = sr.ReadToEnd();
			Debug.Log("Cached game bundle version: " + bundleVersionStr);
			if (bundleVersionStr == startupConfig.GameBundleVersion)
			{
				Debug.Log($"Game bundle is cached. Skipping download.");
				downloadGameBundle = false;
			}
		}

		// If we don't need to download either bundle, break out.
		if (!downloadCoreBundle && !downloadGameBundle)
		{
			yield break;
		}

		var baseUrl = startupConfig.CdnUrl;
		var platform = GetPlatformString();

		List<RemoteBundleFile> remoteBundleFiles = new();

		void AddPublicUrlsForBundle(string bundleId, string bundleVersion, string bundleName)
		{
			var url = $"{baseUrl}/{bundleId}/{bundleVersion}/{platform}/{bundleName}";
			remoteBundleFiles.Add(new RemoteBundleFile(bundleName, url, bundleId));
			remoteBundleFiles.Add(new RemoteBundleFile(bundleName + ".manifest", url + ".manifest", bundleId));
		}

		foreach (var bundleName in startupConfig.SharedBundles)
		{
			var isCoreBundle = bundleName.StartsWith(startupConfig.CoreBundleId);

			if (downloadCoreBundle && isCoreBundle)
			{
				AddPublicUrlsForBundle(startupConfig.CoreBundleId, startupConfig.CoreBundleVersion, bundleName);
			}

			if (downloadGameBundle && !isCoreBundle)
			{
				AddPublicUrlsForBundle(startupConfig.GameBundleId, startupConfig.GameBundleVersion, bundleName);
			}
		}

		if (RunCore.IsClient())
		{
			foreach (var bundle in startupConfig.ClientBundles)
			{
				var isCoreBundle = bundle.StartsWith(startupConfig.CoreBundleId);

				if (downloadCoreBundle && isCoreBundle)
				{
					AddPublicUrlsForBundle(startupConfig.CoreBundleId, startupConfig.CoreBundleVersion, bundle);
				}

				if (downloadGameBundle && !isCoreBundle)
				{
					AddPublicUrlsForBundle(startupConfig.GameBundleId, startupConfig.GameBundleVersion, bundle);
				}
			}
		}

		if (RunCore.IsServer() && serverBundleFiles != null)
		{
			remoteBundleFiles.AddRange(serverBundleFiles);
		}

		var coreLoadingScreen = FindObjectOfType<CoreLoadingScreen>();

		// Public files
		var bundleNumber = 1;
		var requests = new List<UnityWebRequestAsyncOperation>(10);
		foreach (var remoteBundleFile in remoteBundleFiles)
		{
			var request = new UnityWebRequest(remoteBundleFile.Url);

			// Note: We should be downloading this into a "bedwars" and "core" folders, respectively.
			//var bundleFileName = Path.GetFileName(remoteBundleFile.File);
			var path = Path.Join(AssetBridge.BundlesPath, remoteBundleFile.BundleId, remoteBundleFile.File);

			Debug.Log($"BundleDownloader.DownloadBundles() remoteBundleFile.Url: {remoteBundleFile.Url}, downloadPath: {path}");

			request.downloadHandler = new DownloadHandlerFile(path);

			if (coreLoadingScreen)
			{
				StartCoroutine(WatchDownloadStatus(request, bundleNumber, remoteBundleFiles.Count, coreLoadingScreen));
			}

			requests.Add(request.SendWebRequest());
			bundleNumber++;
		}

		yield return new WaitUntil(() => AllRequestsDone(requests));

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
				Debug.Log("Downloaded bundle " + remoteBundleFile + ": " + size + " mb.");
			}

			i++;
		}

		if (downloadCoreBundle)
		{
			using var writer = new StreamWriter(coreBundleVersionPath);
			writer.Write(startupConfig.CoreBundleVersion);
		}

		if (downloadGameBundle)
		{
			using var writer = new StreamWriter(gameBundleVersionPath);
			writer.Write(startupConfig.GameBundleVersion);
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