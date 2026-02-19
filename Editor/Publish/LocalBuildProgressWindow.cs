using System.IO;
using UnityEditor;
using UnityEngine;

public class LocalBuildProgressWindow : EditorWindow {
	private const float WindowMinWidth = 620f;
	private const float WindowMinHeight = 320f;
	private const float CardSpacing = 6f;

	private static LocalBuildProgressWindow instance;
	private LocalShadowBuildAgentClient.BuildProgressInfo progress;
	private bool cancelRequested;
	private Vector2 scrollPosition;

	private GUIStyle titleStyle;
	private GUIStyle subtitleStyle;
	private GUIStyle summaryStyle;
	private GUIStyle cardTitleStyle;
	private GUIStyle cardMetaStyle;
	private GUIStyle chipStyle;
	private GUIStyle errorStyle;

	public static void ShowWindow(string title) {
		if (instance == null) {
			instance = GetWindow<LocalBuildProgressWindow>(utility: false, title: title, focus: true);
			instance.position = new Rect(120f, 120f, 760f, 420f);
		} else {
			instance.titleContent = new GUIContent(title);
			instance.Show();
		}

		instance.minSize = new Vector2(WindowMinWidth, WindowMinHeight);
		instance.cancelRequested = false;
		instance.Repaint();
	}

	public static void UpdateProgress(LocalShadowBuildAgentClient.BuildProgressInfo info) {
		if (instance == null) {
			ShowWindow("Publishing game");
		}

		instance.progress = info;
		instance.Repaint();
	}

	public static bool IsCancelRequested() {
		return instance != null && instance.cancelRequested;
	}

	public static void CloseWindowIfOpen() {
		if (instance == null) {
			return;
		}

		instance.Close();
		instance = null;
	}

	private void OnDestroy() {
		if (instance == this) {
			instance = null;
		}
	}

	private void OnGUI() {
		EnsureStyles();
		DrawHeader();

		if (progress == null) {
			EditorGUILayout.HelpBox("Preparing local shadow build...", MessageType.Info);
			DrawActions();
			return;
		}

		DrawOverall();
		DrawPlatformList();
		DrawActions();
	}

	private void EnsureStyles() {
		if (titleStyle == null) {
			titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
		}
		if (subtitleStyle == null) {
			subtitleStyle = new GUIStyle(EditorStyles.label) {
				fontSize = 11,
				normal = {
					textColor = EditorGUIUtility.isProSkin
						? new Color(0.8f, 0.82f, 0.86f)
						: new Color(0.3f, 0.32f, 0.36f)
				},
			};
		}
		if (summaryStyle == null) {
			summaryStyle = new GUIStyle(EditorStyles.miniLabel) {
				wordWrap = true,
				normal = {
					textColor = EditorGUIUtility.isProSkin
						? new Color(0.77f, 0.79f, 0.83f)
						: new Color(0.33f, 0.35f, 0.38f)
				},
			};
		}
		if (cardTitleStyle == null) {
			cardTitleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
		}
		if (cardMetaStyle == null) {
			cardMetaStyle = new GUIStyle(EditorStyles.miniLabel) {
				wordWrap = true,
				normal = {
					textColor = EditorGUIUtility.isProSkin
						? new Color(0.74f, 0.76f, 0.8f)
						: new Color(0.34f, 0.36f, 0.4f)
				},
			};
		}
		if (chipStyle == null) {
			chipStyle = new GUIStyle(EditorStyles.miniBoldLabel) {
				alignment = TextAnchor.MiddleCenter,
				normal = { textColor = Color.white },
			};
		}
		if (errorStyle == null) {
			errorStyle = new GUIStyle(EditorStyles.miniLabel) {
				wordWrap = true,
				normal = {
					textColor = EditorGUIUtility.isProSkin
						? new Color(1f, 0.62f, 0.62f)
						: new Color(0.62f, 0.12f, 0.12f)
				},
			};
		}
	}

	private void DrawHeader() {
		EditorGUILayout.LabelField("Local Shadow Build", titleStyle);
		EditorGUILayout.LabelField("Batch builds are running in isolated shadow projects.", subtitleStyle);
		EditorGUILayout.Space(8f);
	}

	private void DrawOverall() {
		var total = Mathf.Max(1, progress.totalPlatforms);
		var done = Mathf.Clamp(progress.completedPlatforms, 0, total);
		var percent = Mathf.RoundToInt(Mathf.Clamp01(progress.totalProgress) * 100f);

		EditorGUILayout.BeginVertical("box");
		EditorGUILayout.LabelField("Overall", EditorStyles.boldLabel);
		EditorGUILayout.LabelField(progress.headline ?? "Building bundles...", summaryStyle);

		var doneCount = 0;
		var failedCount = 0;
		var runningCount = 0;
		var pendingCount = 0;
		if (progress.platforms != null) {
			foreach (var platform in progress.platforms) {
				if (platform == null) {
					continue;
				}
				switch (platform.status) {
					case "done": doneCount++; break;
					case "failed": failedCount++; break;
					case "syncing":
					case "linking":
					case "building":
					case "copying":
					case "canceling": runningCount++; break;
					default: pendingCount++; break;
				}
			}
		}
		EditorGUILayout.LabelField($"{doneCount} done • {failedCount} failed • {runningCount} active • {pendingCount} pending", summaryStyle);

		var updateAge = FormatAge(progress.nowUnixMs, progress.updatedAtUnixMs);
		EditorGUILayout.LabelField($"Last agent update: {updateAge}", summaryStyle);
		if (GetAgeSeconds(progress.nowUnixMs, progress.updatedAtUnixMs) >= 90) {
			EditorGUILayout.HelpBox("No agent heartbeat for 90s+. Open an active platform log to verify progress.", MessageType.Warning);
		}

		var overallRect = GUILayoutUtility.GetRect(22, 22, "TextField");
		EditorGUI.ProgressBar(overallRect, Mathf.Clamp01(progress.totalProgress), $"{percent}% ({done}/{total})");
		EditorGUILayout.EndVertical();
		EditorGUILayout.Space(CardSpacing);
	}

	private void DrawPlatformList() {
		EditorGUILayout.LabelField("Platforms", EditorStyles.boldLabel);
		EditorGUILayout.Space(4f);
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
		if (progress.platforms != null) {
			foreach (var platform in progress.platforms) {
				if (platform == null) {
					continue;
				}
				DrawPlatformCard(platform);
				EditorGUILayout.Space(CardSpacing);
			}
		}
		EditorGUILayout.EndScrollView();
	}

	private void DrawPlatformCard(LocalShadowBuildAgentClient.BuildPlatformProgressInfo platform) {
		EditorGUILayout.BeginVertical("box");
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.LabelField(platform.platform, cardTitleStyle);
		GUILayout.FlexibleSpace();
		DrawStatusChip(platform.status);
		EditorGUILayout.EndHorizontal();

		var message = string.IsNullOrEmpty(platform.message) ? "Queued" : platform.message;
		EditorGUILayout.LabelField(message, summaryStyle);

		if (!string.IsNullOrEmpty(platform.detail) && (IsActive(platform.status) || platform.status == "failed")) {
			EditorGUILayout.LabelField(Trim(platform.detail, 140), cardMetaStyle);
		}

		var meta = BuildPlatformMeta(platform, progress.nowUnixMs);
		if (!string.IsNullOrEmpty(meta)) {
			EditorGUILayout.LabelField(meta, cardMetaStyle);
		}

		var pct = Mathf.RoundToInt(Mathf.Clamp01(platform.progress) * 100f);
		var rect = GUILayoutUtility.GetRect(18, 18, "TextField");
		EditorGUI.ProgressBar(rect, Mathf.Clamp01(platform.progress), $"{pct}%");

		if (platform.status == "failed" && !string.IsNullOrEmpty(platform.error)) {
			EditorGUILayout.Space(4f);
			EditorGUILayout.LabelField(Trim(platform.error, 220), errorStyle);
		}

		if (!string.IsNullOrEmpty(platform.logFile) && (platform.status == "failed" || IsActive(platform.status))) {
			EditorGUILayout.Space(4f);
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(Path.GetFileName(platform.logFile), cardMetaStyle);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Open Log", GUILayout.Width(74), GUILayout.Height(21))) {
				EditorUtility.OpenWithDefaultApp(platform.logFile);
			}
			if (GUILayout.Button("Reveal", GUILayout.Width(60), GUILayout.Height(21))) {
				EditorUtility.RevealInFinder(platform.logFile);
			}
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.EndVertical();
	}

	private void DrawStatusChip(string status) {
		var text = FormatStatus(status).ToUpperInvariant();
		var size = chipStyle.CalcSize(new GUIContent(text));
		var rect = GUILayoutUtility.GetRect(size.x + 16f, 19f, GUILayout.Width(size.x + 16f), GUILayout.Height(19f));
		EditorGUI.DrawRect(rect, StatusColor(status));
		GUI.Label(rect, text, chipStyle);
	}

	private void DrawActions() {
		EditorGUILayout.Space(8f);
		using (new EditorGUI.DisabledScope(cancelRequested)) {
			if (GUILayout.Button("Cancel Publish", GUILayout.Height(30))) {
				cancelRequested = true;
			}
		}
		if (cancelRequested) {
			EditorGUILayout.HelpBox("Cancel requested. Stopping active platform builds...", MessageType.Warning);
		}
	}

	private static string BuildPlatformMeta(LocalShadowBuildAgentClient.BuildPlatformProgressInfo platform, long nowUnixMs) {
		var elapsed = platform.elapsedStatusSeconds > 0 ? FormatDuration(platform.elapsedStatusSeconds) : string.Empty;
		var updateAge = FormatAge(nowUnixMs, platform.updatedAtUnixMs);
		if (platform.stalled) {
			return $"No heartbeat for {platform.staleSeconds}s";
		}
		if (string.IsNullOrEmpty(elapsed)) {
			return $"Updated {updateAge}";
		}
		return $"Elapsed {elapsed} • Updated {updateAge}";
	}

	private static bool IsActive(string status) {
		return status == "syncing" || status == "linking" || status == "building" || status == "copying" || status == "canceling";
	}

	private static string FormatStatus(string status) {
		if (string.IsNullOrEmpty(status)) {
			return "pending";
		}
		switch (status) {
			case "syncing": return "syncing";
			case "linking": return "linking";
			case "building": return "building";
			case "copying": return "copying";
			case "done": return "done";
			case "failed": return "failed";
			case "canceling": return "canceling";
			default: return status;
		}
	}

	private static Color StatusColor(string status) {
		switch (status) {
			case "done": return EditorGUIUtility.isProSkin ? new Color(0.16f, 0.58f, 0.26f) : new Color(0.22f, 0.68f, 0.32f);
			case "failed": return EditorGUIUtility.isProSkin ? new Color(0.68f, 0.2f, 0.2f) : new Color(0.78f, 0.25f, 0.25f);
			case "building": return EditorGUIUtility.isProSkin ? new Color(0.2f, 0.45f, 0.8f) : new Color(0.25f, 0.5f, 0.86f);
			case "copying":
			case "syncing":
			case "linking": return EditorGUIUtility.isProSkin ? new Color(0.47f, 0.38f, 0.18f) : new Color(0.72f, 0.58f, 0.22f);
			case "canceling": return EditorGUIUtility.isProSkin ? new Color(0.45f, 0.3f, 0.3f) : new Color(0.67f, 0.47f, 0.47f);
			default: return EditorGUIUtility.isProSkin ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.55f, 0.55f, 0.55f);
		}
	}

	private static string FormatAge(long nowUnixMs, long updatedUnixMs) {
		var delta = GetAgeSeconds(nowUnixMs, updatedUnixMs);
		if (delta < 0) {
			return "unknown";
		}
		if (delta < 60) {
			return delta + "s ago";
		}
		var minutes = delta / 60;
		var seconds = delta % 60;
		return $"{minutes}m {seconds}s ago";
	}

	private static int GetAgeSeconds(long nowUnixMs, long updatedUnixMs) {
		if (nowUnixMs <= 0 || updatedUnixMs <= 0) {
			return -1;
		}
		return Mathf.Max(0, (int)((nowUnixMs - updatedUnixMs) / 1000L));
	}

	private static string FormatDuration(double seconds) {
		var total = Mathf.Max(0, Mathf.FloorToInt((float)seconds));
		var minutes = total / 60;
		var remaining = total % 60;
		if (minutes <= 0) {
			return $"{remaining}s";
		}
		return $"{minutes}m {remaining}s";
	}

	private static string Trim(string input, int maxLength) {
		if (string.IsNullOrEmpty(input) || input.Length <= maxLength) {
			return input;
		}
		return input.Substring(0, maxLength - 3) + "...";
	}
}
