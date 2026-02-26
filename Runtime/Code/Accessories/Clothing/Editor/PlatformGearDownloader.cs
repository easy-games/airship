#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Code.Platform.Shared;
using Code.Player.Accessories;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Code.Accessories.Clothing.Editor {
    public class PlatformGearDownloader : EditorWindow {
        private string classId = "";
        private string outfitData = "";
        private string path = "Assets/AirshipGear";
        private bool isDownloading = false;
        private float progress = 0f;
        private string status = "Idle";
        private CancellationTokenSource cts;

        [MenuItem("Airship/Download Platform Gear", false, 100)]
        public static void ShowWindow() {
            var window = GetWindow<PlatformGearDownloader>("Download Platform Gear");
            window.minSize = new Vector2(360, 150);
            window.ShowUtility();
        }

        private void OnGUI() {
            GUILayout.Space(10);

            using (new EditorGUI.DisabledScope(isDownloading)) {
                EditorGUILayout.LabelField("Enter Comma Seperated Gear Class ID's", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("You can find this by right clicking clothing in the Avatar Editor.");
                EditorGUI.EndDisabledGroup();
                classId = EditorGUILayout.TextField("Class ID:", classId);
                EditorGUILayout.Space(2);
                // if (!string.IsNullOrEmpty(classId)) {
                //     outfitData = "";
                // }
                
                EditorGUILayout.LabelField("Enter the Outfit Data");
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("You can find this by right clicking the outfit in the Avatar Editor.");
                EditorGUI.EndDisabledGroup();
                outfitData = EditorGUILayout.TextField("Outfit Data:", outfitData);
                EditorGUILayout.Space(2);
                // if (!string.IsNullOrEmpty(outfitData)) {
                //     classId = "";
                // }
                // Using the focused folder in the project window. Felt to easy to mess up
                // var selected = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.TopLevel);
                // path = selected.Length == 0 ? "Assets/AirshipGear" : AssetDatabase.GetAssetPath(selected[0]);
                // if (Path.HasExtension(path)) {
                //     path = Path.GetDirectoryName(path);
                // }
                // EditorGUILayout.LabelField("Select a folder in the project window that you want to save the gear");
                EditorGUILayout.LabelField("Source files saved in folder:");
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(path);
                EditorGUI.EndDisabledGroup();
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

                using (new EditorGUI.DisabledScope(isDownloading || (string.IsNullOrWhiteSpace(classId) && string.IsNullOrWhiteSpace(outfitData)))) {
                    if (GUILayout.Button("Download", GUILayout.Width(110), GUILayout.Height(24))) {
                        if (!string.IsNullOrEmpty(classId)) {
                            StartDownloadAsync(classId.Split(','));
                        }

                        if (!string.IsNullOrEmpty(outfitData)) {
                            var outfit = JsonConvert.DeserializeObject<OutfitDto>(outfitData);
                            if (outfit != null) {
                                var classIds = new string[outfit.gear.Length];
                                var i = 0;
                                foreach (var gear in outfit.gear) {
                                    classIds[i] = gear.@class.classId;
                                    i++;
                                }
                                StartDownloadAsync(classIds);
                            } else {
                                Debug.LogError("Unable to parse outfit json. Right click an outfit in the avatar editor to copy and outfits json data.");
                            }
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(!isDownloading)) {
                    if (GUILayout.Button("Cancel", GUILayout.Width(90), GUILayout.Height(24))) {
                        cts?.Cancel();
                        isDownloading = false;
                    }
                }
            }
        }

        private async void StartDownloadAsync(string[] classIds) {
            isDownloading = true;
            progress = 0f;
            status = "Starting…";
            Repaint();

            cts = new CancellationTokenSource();
            
            Debug.Log("Starting Download of " + classIds.Length + " gear items to " + path);
            try {
                foreach(string classId in classIds) {
                    Debug.Log("Downloading gear: " + classId);
                    var gear = await PlatformGear.DownloadYielding(classId.Trim());
                    if (gear == null) {
                        status = "Error";
                        progress = 0f;
                        Repaint();
                        Debug.LogError($"[Airship] Clothing download failed.");
                        EditorUtility.DisplayDialog("Download Error", "Clothing was null.", "OK");
                    } else {
                        Debug.Log("Spawning gear: " + classId);
                        await SpawnClothing(gear);
                        status = "Complete";
                        progress += Mathf.Clamp01(1.0f/classIds.Length);
                        Repaint();
                        // EditorUtility.DisplayDialog("Airship", $"Clothing {classId} downloaded & added to scene.", "OK");
                    }
                    PlatformGear.UnloadAssetBundle(gear.classId);
                }
                AssetDatabase.Refresh();
            } catch (Exception ex) {
                status = "Error";
                progress = 0f;
                Repaint();
                Debug.LogError($"[Airship] Download failed: {ex}");
                EditorUtility.DisplayDialog("Download Error", ex.Message +"\n" + ex.StackTrace, "OK");
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
            // PrefabUtility.InstantiatePrefab(prefab)
            // bundle.Unload(false);

            var rootGearPath = Path.Combine(path, platformGear.name);
            
            var texturePath = Path.Combine(rootGearPath, "Textures");
            var faceTexture = platformGear.face?.decalTexture;
            if (faceTexture) {
                // Download face texture
                SaveTextureAsset(faceTexture, texturePath);
                Debug.Log($"<color=green>[Airship] Saved Face gear {classId}.</color>");
                return;
            }
            
            Debug.Log("No face texture");

            var spawned = new List<GameObject>();
            foreach (var accessory in platformGear.accessoryPrefabs) {
                var acc = Instantiate(accessory);
                spawned.Add(acc.gameObject);
                
                //AssetDatabase.StartAssetEditing();

                var meshFolderPath = Path.Combine(rootGearPath, "Meshes");
                var materialsFolderPath = Path.Combine(rootGearPath, "Materials");
                if (acc.skinnedToCharacter) {
                    var skinnedMeshRenderers = acc.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                    foreach (var smr in skinnedMeshRenderers) {
                        // Save the mesh into the asset folder
                        var projectMesh = SaveMeshAsset(smr.sharedMesh, meshFolderPath, smr.sharedMesh.name);
                        if (projectMesh) {
                            // Swap the saved mesh into the renderer
                            smr.sharedMesh = projectMesh;
                        }
                        
                        // Save materials into the asset folder
                        var i = 0;
                        foreach (var mat in smr.sharedMaterials) {
                            if (!mat.shader.isSupported) {
                                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                            }

                            var projectMat = await SaveMaterialAsset(mat, materialsFolderPath, texturePath, mat.name);
                            if (projectMat) {
                                var mats = smr.sharedMaterials;
                                mats[i] = projectMat;
                                smr.sharedMaterials = mats;
                            } else {
                                Debug.LogWarning("Unable to assign new material");
                            }

                            i++;
                        }
                    }
                } else {
                    var meshRenderers = acc.gameObject.GetComponentsInChildren<MeshRenderer>();
                    foreach (var meshRenderer in meshRenderers) {
                        // Save the mesh into the asset folder
                        MeshFilter filter = meshRenderer.gameObject.GetComponent<MeshFilter>();
                        if (filter && filter.sharedMesh) {

                            var projectMesh = SaveMeshAsset(filter.sharedMesh, meshFolderPath, filter.sharedMesh.name);
                            if (projectMesh) {
                                // Swap the saved mesh into the renderer
                                filter.sharedMesh = projectMesh;
                            }
                        }
                        
                        // Save materials into the asset folder
                        var i = 0;
                        foreach (var mat in meshRenderer.sharedMaterials) {
                            if (!mat.shader.isSupported) {
                                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                            }

                            var projectMat = await SaveMaterialAsset(mat, materialsFolderPath, texturePath, mat.name);
                            if (projectMat) {
                                Debug.Log("Assigned new material to renderer: " + meshRenderer.gameObject.name);
                                var mats = meshRenderer.sharedMaterials;
                                mats[i] = projectMat;
                                meshRenderer.sharedMaterials = mats;
                            } else {
                                Debug.LogWarning("Unable to assign new material");
                            }

                            i++;
                        }
                    }
                }
                //AssetDatabase.StopAssetEditing();

                // Save the prefab into the asset folder
                acc = RetargetAccessoryComponent(acc);
                SaveAsPrefab(acc.gameObject, rootGearPath, "AirshipAcc_" + accessory.name);
                await Awaitable.NextFrameAsync();
            }
            Selection.objects = spawned.ToArray();
            SceneView.lastActiveSceneView?.FrameSelected(); // zooms to them

            await Task.Yield();
            Debug.Log($"<color=green>[Airship] Spawned gear {classId}.</color>");
        }
    
    
        /// <summary>
        /// Saves a GameObject as a prefab at the given folder path.
        /// </summary>
        /// <param name="go">The GameObject to save</param>
        /// <param name="folderPath">Example: "Assets/Prefabs/MyFolder"</param>
        /// <param name="prefabName">Without .prefab</param>
        public static GameObject SaveAsPrefab(GameObject go, string folderPath, string prefabName) {
            if (go == null) {
                Debug.LogError("GameObject is null.");
                return null;
            }
            folderPath = folderPath.Replace("\\", "/");

            if (!folderPath.StartsWith("Assets")) {
                Debug.LogError("Path: " + folderPath + " must start with 'Assets/'.");
                return null;
            }

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath)) {
                CreateFoldersRecursively(folderPath);
            }

            string prefabPath = Path.Combine(folderPath, prefabName + ".prefab");

            // Save prefab
            GameObject prefab =
                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    go,
                    prefabPath,
                    InteractionMode.UserAction
                );
            
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();

            return prefab;
        }
        
        private static Mesh SaveMeshAsset(Mesh source, string folderPath, string assetName){
            if (source == null) {
                return null;
            }

            if (!folderPath.StartsWith("Assets")) {
                throw new Exception("Path must start with Assets/");
            }

            // Ensure folder exists
            folderPath = folderPath.Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(folderPath)) {
                CreateFoldersRecursively(folderPath);
            }
            

            // Duplicate mesh (critical!)
            Mesh meshCopy = Instantiate(source);
            meshCopy.name = assetName;

            string assetPath = Path.Combine(folderPath, assetName + ".asset");

            AssetDatabase.CreateAsset(meshCopy, assetPath);
            AssetDatabase.SaveAssets();

            return meshCopy;
        }
        
        private static async Task<Material> SaveMaterialAsset(
            Material source,
            string folderPath,
            string imageFolderPath,
            string assetName)
        {
            if (source == null) {
                Debug.LogWarning("Trying to save null material");
                return null;
            }
            
            if (!folderPath.StartsWith("Assets")) {
                Debug.LogError("Path must start with Assets/");
                return null;
            }

            // Ensure folder exists
            folderPath = folderPath.Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(folderPath)) {
                CreateFoldersRecursively(folderPath);
            }

            // Duplicate material (CRITICAL)
            Material matCopy = new Material(source);
            matCopy.name = assetName;

            // Ensure shader exists in project
            if (!AssetDatabase.Contains(matCopy.shader))
            {
                Shader fallback = Shader.Find(matCopy.shader.name);
                if (fallback != null)
                    matCopy.shader = fallback;
                else
                    Debug.LogWarning($"Shader not found in project: {matCopy.shader.name}");
            }

            string assetPath = Path.Combine(folderPath, assetName + ".mat");
                
            // Check for Textures on the material
            Debug.Log("Checking textures on material");
            foreach (var textureId in matCopy.GetTexturePropertyNameIDs()) {
                Debug.Log("Found texture ID");
                var texture = matCopy.GetTexture(textureId);
                if (texture) {
                    Debug.Log("Got texture: " + texture.name);
                    var texturePath = SaveTextureAsset((Texture2D)texture, imageFolderPath);
                    if (!string.IsNullOrEmpty(assetPath)) {
                        matCopy.SetTexture(textureId, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    } else {
                        Debug.LogError("Unable to get texture path");
                    }
                }
            }

            AssetDatabase.CreateAsset(matCopy, assetPath);
            AssetDatabase.SaveAssets();

            return matCopy;
        }

        private static string SaveTextureAsset(Texture2D source, string folderPath) {
            if (source == null) {
                Debug.LogWarning("Missing Texture");
                return "";
            }
            
            if (!folderPath.StartsWith("Assets")) {
                Debug.LogError("Path must start with Assets/");
                return "";
            }

            // Ensure folder exists
            folderPath = folderPath.Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(folderPath)) {
                CreateFoldersRecursively(folderPath);
            }
            
            Texture2D sourceCopy = MakeReadable(source);
            bool isNormalMap = source.name.ToLowerInvariant().Contains("normal") || source.name.ToLowerInvariant().Contains("_n");
            if (isNormalMap) {
                // Normal Map needs unpacking
                Color[] pixels = sourceCopy.GetPixels();
            
                for (int i = 0; i < pixels.Length; i++) {
                    pixels[i] = UnpackNormalDXT5nm(pixels[i]);
                }
                
                sourceCopy.SetPixels(pixels);
                sourceCopy.Apply();
            }
            
            byte[] pngData;
            try {
                pngData = sourceCopy.EncodeToPNG();
            } catch (Exception e) {
                Debug.Log(e);
                return "";
            }
            if (pngData == null) {
                Debug.LogError("Failed to encode texture to PNG");
                return "";
            }

            string assetPath = Path.Combine(folderPath, sourceCopy.name + ".png");
            Debug.Log("Saving texture: " + sourceCopy.name + " to path: " + assetPath);
            File.WriteAllBytes(assetPath, pngData);
            
            AssetDatabase.ImportAsset(assetPath);
            
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (!importer) {
                Debug.LogWarning("Unable to find texture importer of asset: " + sourceCopy.name);
                return "";
            }
            importer.textureType = isNormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.alphaIsTransparency = isNormalMap ? false : true;
            

            return assetPath;
        }
        
        public static Texture2D MakeReadable(Texture2D source) {
            if (source == null)
                return null;

            RenderTexture rt = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear
            );

            Graphics.Blit(source, rt);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(
                source.width,
                source.height,
                TextureFormat.RGBA32,
                false
            );

            readable.ReadPixels(
                new Rect(0, 0, rt.width, rt.height),
                0,
                0
            );

            readable.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            readable.name = source.name;
            return readable;
        }
        
        private static void CreateFoldersRecursively(string folderPath) {
            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"

            Debug.Log("Making Folder at: " + folderPath);
            for (int i = 1; i < parts.Length; i++) {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) {
                    Debug.Log("Creating folder: " + parts[i]);
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
        
        private static Color UnpackNormalDXT5nm(Color packedColor) {
            // In DXT5nm: 
            // Red channel is moved to Alpha
            // Green channel stays in Green
            float x = packedColor.a * 2.0f - 1.0f;
            float y = packedColor.g * 2.0f - 1.0f;
    
            // Reconstruct Z (Blue)
            float z = Mathf.Sqrt(1.0f - Mathf.Clamp01(x * x + y * y));
    
            // Convert back to 0-1 range for saving to PNG
            return new Color(x * 0.5f + 0.5f, y * 0.5f + 0.5f, z * 0.5f + 0.5f, 1.0f);
        }
        
        private static AccessoryComponent RetargetAccessoryComponent(AccessoryComponent existing) {
            if (existing == null)
                return null;

            // Add correct component
            var replacement = existing.gameObject.AddComponent<AccessoryComponent>();

            // Copy values you care about
            replacement.Copy(existing);

            // Remove the bundle component
            DestroyImmediate(existing, true);
            return replacement;
        }
    }
}
#endif
