using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Airship.Editor;
using Code.Bootstrap;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

public static class LocalShadowBuildAgentClient {
	private const int AgentPort = 46330;
	private static string HealthUrl => $"http://127.0.0.1:{AgentPort}/v1/health";
	private static string BuildUrl => $"http://127.0.0.1:{AgentPort}/v1/build-game-bundles";
	private static string BuildSubmitUrl => $"http://127.0.0.1:{AgentPort}/v1/build-game-bundles/submit";
	private static string BuildStatusUrl => $"http://127.0.0.1:{AgentPort}/v1/build-game-bundles/status";
	private static string BuildCancelUrl => $"http://127.0.0.1:{AgentPort}/v1/build-game-bundles/cancel";
	private const int HealthTimeoutMs = 2000;
	private const int BuildTimeoutMs = 86_400_000;
	private const int PollTimeoutMs = 10000;

	private static Process agentProcess;

	public class BuildProgressInfo {
		public string headline;
		public float totalProgress;
		public int completedPlatforms;
		public int totalPlatforms;
		public long nowUnixMs;
		public long updatedAtUnixMs;
		public string agentState;
		public string agentMessage;
		public BuildPlatformProgressInfo[] platforms;
	}

	public class BuildPlatformProgressInfo {
		public string platform;
		public string status;
		public float progress;
		public string message;
		public string detail;
		public string error;
		public string logFile;
		public double durationSeconds;
		public long updatedAtUnixMs;
		public long statusSinceUnixMs;
		public int staleSeconds;
		public bool stalled;
		public double elapsedStatusSeconds;
	}

	public static bool ShouldUseAgent(AirshipPlatform[] platforms) {
		if (Application.isBatchMode) {
			return false;
		}

		if (platforms == null || platforms.Length <= 1) {
			return false;
		}

		// Defaults to enabled. Set AIRSHIP_DISABLE_LOCAL_BUILD_AGENT=1 to bypass.
		var disabled = Environment.GetEnvironmentVariable("AIRSHIP_DISABLE_LOCAL_BUILD_AGENT");
		return disabled != "1";
	}

	public static bool TryBuildPlatforms(AirshipPlatform[] platforms, bool useCache, out string failureReason) {
		failureReason = string.Empty;
		if (platforms == null || platforms.Length == 0) {
			return true;
		}

		if (!TryCreateBuildRequest(platforms, useCache, out var request, out failureReason)) {
			return false;
		}

		BuildAgentResponse response;
		if (!TryPostJson(BuildUrl, JsonUtility.ToJson(request), BuildTimeoutMs, out var json, out failureReason)) {
			return false;
		}

		try {
			response = JsonUtility.FromJson<BuildAgentResponse>(json);
		} catch (Exception ex) {
			failureReason = "Failed to parse build agent response: " + ex.Message;
			return false;
		}

		if (response == null) {
			failureReason = "Build agent returned an empty response.";
			return false;
		}

		if (!response.ok) {
			var details = response.results == null
				? ""
				: string.Join("; ", response.results.Select((r) => $"{r.platform}: {r.error}"));
			failureReason = string.IsNullOrEmpty(details)
				? response.message
				: $"{response.message} ({details})";
			return false;
		}

		if (response.results != null) {
			foreach (var result in response.results) {
				Debug.Log($"[LocalBuildAgent] {result.platform} built in {result.durationSeconds:0.0}s. log={result.logFile}");
			}
		}

		Debug.Log($"[LocalBuildAgent] Completed {request.platforms.Length} platform build(s) in {response.totalDurationSeconds:0.0}s.");
		return true;
	}

	public static bool TryEnsureAgentReady(out string failureReason) {
		return EnsureAgentRunning(out failureReason);
	}

	public static IEnumerator TryBuildPlatformsNonBlocking(
		AirshipPlatform[] platforms,
		bool useCache,
		Action<bool, string> onComplete,
		Action<BuildProgressInfo> onProgress = null,
		Func<bool> shouldCancel = null) {
		if (platforms == null || platforms.Length == 0) {
			onComplete?.Invoke(true, string.Empty);
			yield break;
		}

		if (!TryCreateBuildRequest(platforms, useCache, out var request, out var failureReason)) {
			onComplete?.Invoke(false, failureReason);
			yield break;
		}

		var requestJson = JsonUtility.ToJson(request);
		onProgress?.Invoke(CreateQueuedProgressInfo(request.platforms, "Queueing local shadow build...", 0.03f));
		BuildPostResult submit = null;
		string submitFailureReason = string.Empty;
		for (var attempt = 1; attempt <= 3; attempt++) {
			if (shouldCancel?.Invoke() == true) {
				onComplete?.Invoke(false, "Local shadow build canceled.");
				yield break;
			}

			var submitTask = Task.Run(() => {
				var ok = TryPostJson(BuildSubmitUrl, requestJson, PollTimeoutMs, out var responseJson, out var submitRequestFailureReason);
				return new BuildPostResult() {
					ok = ok,
					responseJson = responseJson,
					failureReason = submitRequestFailureReason,
				};
			});

			while (!submitTask.IsCompleted) {
				if (shouldCancel?.Invoke() == true) {
					onComplete?.Invoke(false, "Local shadow build canceled.");
					yield break;
				}

				yield return null;
			}

			if (submitTask.IsCanceled) {
				onComplete?.Invoke(false, "Local build agent submit request was canceled.");
				yield break;
			}

			if (submitTask.IsFaulted) {
				submitFailureReason = submitTask.Exception?.GetBaseException().Message ?? "Local build agent submit failed.";
			} else {
				submit = submitTask.Result;
				if (submit.ok) {
					break;
				}
				submitFailureReason = submit.failureReason;
			}

			if (attempt >= 3) {
				onComplete?.Invoke(false, submitFailureReason);
				yield break;
			}

			var retryUntil = Time.realtimeSinceStartup + 0.35f;
			while (Time.realtimeSinceStartup < retryUntil) {
				yield return null;
			}
		}

		BuildSubmitResponse submitResponse;
		try {
			submitResponse = JsonUtility.FromJson<BuildSubmitResponse>(submit.responseJson);
		} catch (Exception ex) {
			onComplete?.Invoke(false, "Failed to parse build submit response: " + ex.Message);
			yield break;
		}

		if (submitResponse == null || !submitResponse.ok || string.IsNullOrEmpty(submitResponse.jobId)) {
			onComplete?.Invoke(false, "Build submit response did not include a valid jobId.");
			yield break;
		}

		var cancelSent = false;
		var consecutivePollFailures = 0;
		while (true) {
			if (shouldCancel?.Invoke() == true) {
				if (!cancelSent) {
					cancelSent = true;
					SendCancelRequestBestEffort(submitResponse.jobId);
				}
				onComplete?.Invoke(false, "Local shadow build canceled.");
				yield break;
			}

			var statusUrl = BuildStatusUrl + "?jobId=" + Uri.EscapeDataString(submitResponse.jobId);
			var statusTask = Task.Run(() => {
				var ok = TryGetJson(statusUrl, PollTimeoutMs, out var statusJson, out var pollFailureReason);
				return new BuildPostResult() {
					ok = ok,
					responseJson = statusJson,
					failureReason = pollFailureReason,
				};
			});

			while (!statusTask.IsCompleted) {
				if (shouldCancel?.Invoke() == true) {
					if (!cancelSent) {
						cancelSent = true;
						SendCancelRequestBestEffort(submitResponse.jobId);
					}
					onComplete?.Invoke(false, "Local shadow build canceled.");
					yield break;
				}
				yield return null;
			}

			if (statusTask.IsCanceled) {
				onComplete?.Invoke(false, "Local build agent status polling was canceled.");
				yield break;
			}

			if (statusTask.IsFaulted) {
				onComplete?.Invoke(false, statusTask.Exception?.GetBaseException().Message ?? "Local build agent status polling failed.");
				yield break;
			}

			var statusResult = statusTask.Result;
			if (!statusResult.ok) {
				consecutivePollFailures++;
				if (consecutivePollFailures >= 3) {
					onComplete?.Invoke(false, statusResult.failureReason);
					yield break;
				}

				var retryUntil = Time.realtimeSinceStartup + 0.6f;
				while (Time.realtimeSinceStartup < retryUntil) {
					yield return null;
				}
				continue;
			}

			consecutivePollFailures = 0;
			BuildJobStatusResponse status;
			try {
				status = JsonUtility.FromJson<BuildJobStatusResponse>(statusResult.responseJson);
			} catch (Exception ex) {
				onComplete?.Invoke(false, "Failed to parse build status response: " + ex.Message);
				yield break;
			}

			if (status == null) {
				onComplete?.Invoke(false, "Build status response was empty.");
				yield break;
			}

			onProgress?.Invoke(CreateProgressInfoFromStatus(status));

			if (status.done) {
				if (status.ok) {
					if (status.results != null) {
						foreach (var result in status.results) {
							Debug.Log($"[LocalBuildAgent] {result.platform} built in {result.durationSeconds:0.0}s. log={result.logFile}");
						}
					}

					Debug.Log($"[LocalBuildAgent] Completed {request.platforms.Length} platform build(s) in {status.totalDurationSeconds:0.0}s.");
					onComplete?.Invoke(true, string.Empty);
					yield break;
				}

				var statusFailureReason = BuildFailureReasonFromStatus(status);
				onComplete?.Invoke(false, statusFailureReason);
				yield break;
			}

			var waitUntil = Time.realtimeSinceStartup + 0.5f;
			while (Time.realtimeSinceStartup < waitUntil) {
				if (shouldCancel?.Invoke() == true) {
					if (!cancelSent) {
						cancelSent = true;
						SendCancelRequestBestEffort(submitResponse.jobId);
					}
					onComplete?.Invoke(false, "Local shadow build canceled.");
					yield break;
				}
				yield return null;
			}
		}
	}

	private static bool TryCreateBuildRequest(AirshipPlatform[] platforms, bool useCache, out BuildAgentRequest request, out string failureReason) {
		request = null;
		failureReason = string.Empty;

		if (platforms == null || platforms.Length == 0) {
			failureReason = "No platforms requested.";
			return false;
		}

		var gameConfig = GameConfig.Load();
		if (gameConfig == null || string.IsNullOrEmpty(gameConfig.gameId)) {
			failureReason = "Missing GameConfig or gameId for local shadow build agent.";
			return false;
		}

		if (!EnsureAgentRunning(out failureReason)) {
			return false;
		}

		var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
		var platformNames = platforms.Select((p) => p.ToString()).ToArray();
		request = new BuildAgentRequest() {
			projectPath = projectPath,
			unityPath = EditorApplication.applicationPath,
			gameId = gameConfig.gameId,
			platforms = platformNames,
			useCache = useCache,
			maxParallel = Math.Min(platformNames.Length, 2),
		};
		Debug.Log($"[LocalBuildAgent] Requesting build via port {AgentPort}. unityPath={request.unityPath}");
		return true;
	}

	private static void SendCancelRequestBestEffort(string jobId) {
		if (string.IsNullOrEmpty(jobId)) {
			return;
		}

		Task.Run(() => {
			var payload = JsonUtility.ToJson(new BuildCancelRequest() {
				jobId = jobId,
			});
			TryPostJson(BuildCancelUrl, payload, PollTimeoutMs, out _, out _);
		});
	}

	private static string BuildFailureReasonFromStatus(BuildJobStatusResponse status) {
		var details = status.results == null
			? ""
			: string.Join("; ", status.results.Select((result) => $"{result.platform}: {result.error}"));
		return string.IsNullOrEmpty(details)
			? (string.IsNullOrEmpty(status.message) ? "Local shadow build failed." : status.message)
			: $"{status.message} ({details})";
	}

	private static string FormatBuildProgressText(BuildJobStatusResponse status) {
		var percent = Mathf.RoundToInt(Mathf.Clamp01(status.totalProgress) * 100f);
		return $"Building bundles: {percent}% ({status.completedPlatforms}/{status.totalPlatforms} complete)";
	}

	private static BuildProgressInfo CreateQueuedProgressInfo(string[] platforms, string headline, float progress) {
		var nowUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		return new BuildProgressInfo() {
			headline = headline,
			totalProgress = Mathf.Clamp01(progress),
			completedPlatforms = 0,
			totalPlatforms = platforms.Length,
			nowUnixMs = nowUnixMs,
			updatedAtUnixMs = nowUnixMs,
			agentState = "queued",
			agentMessage = headline,
			platforms = platforms.Select((platform) => new BuildPlatformProgressInfo() {
				platform = platform,
				status = "pending",
				progress = 0f,
				message = "Queued",
				detail = "",
				updatedAtUnixMs = nowUnixMs,
				statusSinceUnixMs = nowUnixMs,
				staleSeconds = 0,
				stalled = false,
				elapsedStatusSeconds = 0,
			}).ToArray(),
		};
	}

	private static BuildProgressInfo CreateProgressInfoFromStatus(BuildJobStatusResponse status) {
		return new BuildProgressInfo() {
			headline = FormatBuildProgressText(status),
			totalProgress = Mathf.Clamp01(status.totalProgress),
			completedPlatforms = status.completedPlatforms,
			totalPlatforms = status.totalPlatforms,
			nowUnixMs = status.nowUnixMs,
			updatedAtUnixMs = status.updatedAtUnixMs,
			agentState = status.state,
			agentMessage = status.message,
			platforms = status.platforms == null
				? Array.Empty<BuildPlatformProgressInfo>()
				: status.platforms.Select((platform) => new BuildPlatformProgressInfo() {
					platform = platform.platform,
					status = platform.status,
					progress = Mathf.Clamp01(platform.progress),
					message = platform.message,
					detail = platform.detail,
					error = platform.error,
					logFile = platform.logFile,
					durationSeconds = platform.durationSeconds,
					updatedAtUnixMs = platform.updatedAtUnixMs,
					statusSinceUnixMs = platform.statusSinceUnixMs,
					staleSeconds = platform.staleSeconds,
					stalled = platform.stalled,
					elapsedStatusSeconds = platform.elapsedStatusSeconds,
				}).ToArray(),
		};
	}

	private static bool EnsureAgentRunning(out string failureReason) {
		failureReason = string.Empty;
		if (agentProcess != null && agentProcess.HasExited) {
			agentProcess = null;
		}

		if (TryHealthCheck(out _)) {
			return true;
		}

		if (!TryStartAgentProcess(out failureReason)) {
			return false;
		}

		var sw = Stopwatch.StartNew();
		while (sw.ElapsedMilliseconds < 10000) {
			System.Threading.Thread.Sleep(250);
			if (TryHealthCheck(out _)) {
				return true;
			}
		}

		failureReason = "Timed out waiting for local build agent health check.";
		return false;
	}

	private static bool TryStartAgentProcess(out string failureReason) {
		failureReason = string.Empty;
		var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

		var scriptPath = ResolveAgentScriptPath();
		if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath)) {
			failureReason = "Unable to locate airship_local_build_agent.js in package files.";
			return false;
		}

		var nodePath = AirshipNodeInstallService.current?.nodePath;
		if (string.IsNullOrEmpty(nodePath)) {
			nodePath = "node";
		}

		try {
			agentProcess = new Process();
			agentProcess.StartInfo = new ProcessStartInfo(nodePath, $"\"{scriptPath}\" server --port {AgentPort}") {
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
				WorkingDirectory = projectPath,
			};
			agentProcess.EnableRaisingEvents = true;
			agentProcess.OutputDataReceived += (_, e) => {
				if (!string.IsNullOrEmpty(e.Data)) {
					Debug.Log("[LocalBuildAgent] " + e.Data);
				}
			};
			agentProcess.ErrorDataReceived += (_, e) => {
				if (!string.IsNullOrEmpty(e.Data)) {
					Debug.LogWarning("[LocalBuildAgent] " + e.Data);
				}
			};
			agentProcess.Start();
			agentProcess.BeginOutputReadLine();
			agentProcess.BeginErrorReadLine();
			return true;
		} catch (Exception ex) {
			failureReason = "Failed to start local build agent process: " + ex.Message;
			return false;
		}
	}

	private static string ResolveAgentScriptPath() {
		var package = PackageManagerPackageInfo.FindForAssembly(typeof(LocalShadowBuildAgentClient).Assembly);
		if (package != null) {
			var fromPackage = Path.Combine(package.resolvedPath, "Editor", "Build", "LocalBuildAgent~", "airship_local_build_agent.js");
			if (File.Exists(fromPackage)) {
				return fromPackage;
			}
		}

		var fallback = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Packages", "gg.easy.airship", "Editor", "Build", "LocalBuildAgent~", "airship_local_build_agent.js");
		if (File.Exists(fallback)) {
			return fallback;
		}

		return null;
	}

	private static bool TryHealthCheck(out string error) {
		if (!TryGetJson(HealthUrl, HealthTimeoutMs, out var response, out error)) {
			return false;
		}

		try {
			var health = JsonUtility.FromJson<AgentHealthResponse>(response);
			return health != null && health.ok;
		} catch (Exception ex) {
			error = ex.Message;
			return false;
		}
	}

	private static bool TryGetJson(string url, int timeoutMs, out string responseBody, out string failureReason) {
		responseBody = string.Empty;
		failureReason = string.Empty;
		try {
			var request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "GET";
			request.Timeout = timeoutMs;
			request.ReadWriteTimeout = timeoutMs;
			request.KeepAlive = false;
			request.ProtocolVersion = HttpVersion.Version10;
			using var response = (HttpWebResponse)request.GetResponse();
			using var stream = response.GetResponseStream();
			using var reader = new StreamReader(stream ?? Stream.Null);
			responseBody = reader.ReadToEnd();
			return true;
		} catch (WebException ex) {
			failureReason = ReadWebException(ex);
			return false;
		} catch (Exception ex) {
			failureReason = ex.Message;
			return false;
		}
	}

	private static bool TryPostJson(string url, string jsonPayload, int timeoutMs, out string responseBody, out string failureReason) {
		responseBody = string.Empty;
		failureReason = string.Empty;
		try {
			var payload = Encoding.UTF8.GetBytes(jsonPayload);
			var request = (HttpWebRequest)WebRequest.Create(url);
			request.Method = "POST";
			request.ContentType = "application/json";
			request.Timeout = timeoutMs;
			request.ReadWriteTimeout = timeoutMs;
			request.KeepAlive = false;
			request.ProtocolVersion = HttpVersion.Version10;
			request.ContentLength = payload.Length;

			using (var reqStream = request.GetRequestStream()) {
				reqStream.Write(payload, 0, payload.Length);
			}

			using var response = (HttpWebResponse)request.GetResponse();
			using var stream = response.GetResponseStream();
			using var reader = new StreamReader(stream ?? Stream.Null);
			responseBody = reader.ReadToEnd();
			return true;
		} catch (WebException ex) {
			failureReason = ReadWebException(ex);
			return false;
		} catch (Exception ex) {
			failureReason = ex.Message;
			return false;
		}
	}

	private static string ReadWebException(WebException ex) {
		if (ex.Response == null) {
			return ex.Message;
		}

		using var stream = ex.Response.GetResponseStream();
		using var reader = new StreamReader(stream ?? Stream.Null);
		var body = reader.ReadToEnd();
		if (string.IsNullOrEmpty(body)) {
			return ex.Message;
		}
		return body;
	}

	[Serializable]
	private class BuildAgentRequest {
		public string projectPath;
		public string unityPath;
		public string gameId;
		public string[] platforms;
		public bool useCache;
		public int maxParallel;
	}

	[Serializable]
	private class BuildSubmitResponse {
		public bool ok;
		public string jobId;
		public string state;
		public string message;
	}

	[Serializable]
	private class BuildCancelRequest {
		public string jobId;
	}

	[Serializable]
	private class BuildAgentResponse {
		public bool ok;
		public string message;
		public double totalDurationSeconds;
		public BuildResult[] results;
	}

	[Serializable]
	private class BuildJobStatusResponse {
		public bool ok;
		public bool done;
		public string jobId;
		public string state;
		public string message;
		public float totalProgress;
		public int totalPlatforms;
		public int completedPlatforms;
		public double totalDurationSeconds;
		public long nowUnixMs;
		public long updatedAtUnixMs;
		public BuildResult[] results;
		public BuildPlatformState[] platforms;
	}

	[Serializable]
	private class BuildResult {
		public string platform;
		public bool success;
		public string error;
		public string logFile;
		public double durationSeconds;
	}

	[Serializable]
	private class BuildPlatformState {
		public string platform;
		public string status;
		public float progress;
		public string message;
		public string detail;
		public bool success;
		public double durationSeconds;
		public string error;
		public string logFile;
		public string shadowProjectPath;
		public long updatedAtUnixMs;
		public long statusSinceUnixMs;
		public int staleSeconds;
		public bool stalled;
		public double elapsedStatusSeconds;
	}

	[Serializable]
	private class AgentHealthResponse {
		public bool ok;
	}

	private class BuildPostResult {
		public bool ok;
		public string responseJson;
		public string failureReason;
	}
}
