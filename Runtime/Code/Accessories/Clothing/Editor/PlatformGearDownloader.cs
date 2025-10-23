#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Accessories.Clothing.Editor {
    public class PlatformGearDownloader : EditorWindow {
        private string classId = "";
        private bool isDownloading = false;
        private float progress = 0f;
        private string status = "Idle";
        private CancellationTokenSource cts;

        [MenuItem("Airship/Download Platform Gear", false, 100)]
        public static void ShowWindow() {
            var window = GetWindow<PlatformGearDownloader>(true, "Download Platform Gear");
            window.minSize = new Vector2(360, 150);
            window.ShowUtility();
        }

        private void OnGUI() {
            GUILayout.Space(10);

            using (new EditorGUI.DisabledScope(isDownloading)) {
                EditorGUILayout.LabelField("Enter Gear Class ID", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("You can find this by right clicking clothing in the Avatar Editor.");
                EditorGUILayout.Space(2);
                classId = EditorGUILayout.TextField("Class ID:", classId);
            }

            GUILayout.Space(6);

            // Progress + status
            // var rect = GUILayoutUtility.GetRect(18, 20);
            // EditorGUILayout.BeginHorizontal();
            // GUILayout.Space(6);
            // EditorGUI.ProgressBar(rect, progress, isDownloading ? status : "Ready");
            // GUILayout.Space(6);
            // GUILayout.EndHorizontal();
            // GUILayout.Space(2);
            // EditorGUILayout.LabelField(isDownloading ? "Downloading..." : "Idle", EditorStyles.miniLabel);

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(isDownloading || string.IsNullOrWhiteSpace(classId))) {
                    if (GUILayout.Button("Download", GUILayout.Width(110), GUILayout.Height(24))) {
                        StartDownloadAsync(classId);
                    }
                }

                using (new EditorGUI.DisabledScope(!isDownloading)) {
                    if (GUILayout.Button("Cancel", GUILayout.Width(90), GUILayout.Height(24))) {
                        cts?.Cancel();
                    }
                }
            }
        }

        private async void StartDownloadAsync(string classId) {
            isDownloading = true;
            progress = 0f;
            status = "Starting…";
            Repaint();

            cts = new CancellationTokenSource();

            try {
                var gear = await PlatformGear.DownloadYielding(classId);
                if (gear == null) {
                    status = "Error";
                    progress = 0f;
                    Repaint();
                    Debug.LogError($"[Airship] Clothing download failed.");
                    EditorUtility.DisplayDialog("Download Error", "Clothing was null.", "OK");
                } else {
                    this.SpawnClothing(gear);
                    // PlatformGear.UnloadAssetBundle(gear.classId);
                    status = "Complete";
                    progress = 1f;
                    Repaint();
                    // EditorUtility.DisplayDialog("Airship", $"Clothing {classId} downloaded & added to scene.", "OK");
                }
            } catch (Exception ex) {
                status = "Error";
                progress = 0f;
                Repaint();
                Debug.LogError($"[Airship] Download failed: {ex}");
                EditorUtility.DisplayDialog("Download Error", ex.Message, "OK");
            } finally {
                isDownloading = false;
                cts?.Dispose();
                cts = null;
            }
        }

        private async Task SpawnClothing(PlatformGear platformGear) {
            // Example if your payload is an AssetBundle:
            // var req = AssetBundle.LoadFromMemoryAsync(bytes);
            // while (!req.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
            // var bundle = req.assetBundle;
            // var prefab = bundle.LoadAsset<GameObject>("ClothingPrefabName");
            // PrefabUtility.InstantiatePrefab(prefab);
            // bundle.Unload(false);

            var spawned = new List<GameObject>();
            foreach (var accessory in platformGear.accessoryPrefabs) {
                var acc = Instantiate(accessory);
                spawned.Add(acc.gameObject);

                if (acc.skinnedToCharacter) {
                    var skinnedMeshRenderers = acc.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var smr in skinnedMeshRenderers) {
                        foreach (var mat in smr.sharedMaterials) {
                            if (!mat.shader.isSupported) {
                                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                            }
                        }
                    }
                } else {
                    var meshRenderers = acc.gameObject.GetComponentsInChildren<MeshRenderer>();
                    foreach (var meshRenderer in meshRenderers) {
                        foreach (var mat in meshRenderer.sharedMaterials) {
                            if (!mat.shader.isSupported) {
                                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                            }
                        }
                    }
                }
            }

            Selection.objects = spawned.ToArray();
            SceneView.lastActiveSceneView?.FrameSelected(); // zooms to them

            await Task.Yield();
            Debug.Log($"<color=green>[Airship] Spawned gear {classId}.</color>");
        }
    }
}
#endif
