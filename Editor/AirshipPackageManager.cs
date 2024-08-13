using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Airship.Editor;
using Editor.Packages;
using Newtonsoft.Json;
using ParrelSync;
using Unity.Multiplayer.Playmode;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Editor {
    public class ScopedRegistry {
        public string name;
        public string url;
        public string[] scopes;
    }

    public class ManifestJson {
        public Dictionary<string,string> dependencies = new Dictionary<string, string>();
        public List<ScopedRegistry> scopedRegistries = new List<ScopedRegistry>();

        static string manifestPath = Path.Combine(Application.dataPath, "..", "Packages/manifest.json");
        public static ManifestJson Load() {
            var manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonConvert.DeserializeObject<ManifestJson>(manifestJson);
            return manifest;
        }

        public ScopedRegistry FindRegistryByUrl(string url) {
            return this.scopedRegistries.Find(f => f.url == url);
        }

        public ScopedRegistry FindRegistryByName(string name) {
            return this.scopedRegistries.Find(f => f.name == name);
        }

        public bool FindRegistryByName(string name, out ScopedRegistry scopedRegistry) {
            var matchingRegistry = this.scopedRegistries.Any(f => f.name == name);
            if (matchingRegistry) {
                scopedRegistry = this.scopedRegistries.Find(f => f.name == name);
                return true;
            }

            scopedRegistry = null;
            return false;
        }
        
        public void AddScopedRegistry(ScopedRegistry registry) {
            this.scopedRegistries.Add(registry);
        }
        
        public void Save() {
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }

    
    [InitializeOnLoad]
    public class AirshipPackageManager {
        private static string packageRegistry = "https://registry.npmjs.org";
        
        private static PackageInfo _airshipLocalPackageInfo;
        private static PackageInfo _airshipRemotePackageInfo;
        
        private static bool showDialog = false;

        private struct GitCommitsResponse {
            [JsonProperty("sha")] public string SHA { get; set; }
        }
        
        [MenuItem("Airship/Check For Updates", priority = 2000)]
        public static void CheckForAirshipPackageUpdate() {
            // Check Airship itself
#if !AIRSHIP_INTERNAL
            
            showDialog = true;
            // List the current package
            _airshipPackageListRequest = Client.List();
            EditorApplication.update += AwaitAirshipPackageListResult;
#endif
      
            // Update any relevant Typescript packages
            TypescriptProjectsService.CheckTypescriptProject();

            // Update the AirshipPackages
            AirshipPackageAutoUpdater.CheckPackageVersions(ignoreUserSetting: true);
            
        }

        static AirshipPackageManager() {
            if (RunCore.IsClone()) return;
            if (CurrentPlayer.ReadOnlyTags().Length > 0 || ClonesManager.IsClone()) return;

            // Ensure this only runs ON LOAD. No script recompiling...
            if (!SessionState.GetBool("AirshipUpdateCheck", false)) {
                SessionState.SetBool("AirshipUpdateCheck", true);

                var manifest = ManifestJson.Load();

                if (manifest.FindRegistryByName("Airship", out var airshipRegistry)) {
                    // Ensure it's the correct registry URL
                    airshipRegistry.url = packageRegistry;
                    manifest.Save();
                }
                else {
                    // Ensure the project has the registry
                    if (manifest.FindRegistryByUrl(packageRegistry) == null) {
                        manifest.AddScopedRegistry(
                            new ScopedRegistry {
                                name = "Airship",
                                url = packageRegistry,
                                scopes = new[] {
                                    "gg.easy"
                                }
                            });

                        manifest.Save();
                    }
                }
                
                // List the current package
                _airshipPackageListRequest = Client.List(true, false);
                EditorApplication.update += AwaitAirshipPackageListResult;
            }
        }

        private static ListRequest _airshipPackageListRequest;
        private static void AwaitAirshipPackageListResult() {
            if (_airshipPackageListRequest.IsCompleted) {
                // Get the airship package, if not null then check version etc.
                var collection = _airshipPackageListRequest.Result;
                var airshipPackage = collection.First(item => item.name == "gg.easy.airship");
                _airshipLocalPackageInfo = airshipPackage;
                
                switch (airshipPackage.source) {
                    // Upgrade our legacy git buddies to the registry
                    case PackageSource.Git:
                        CheckForAirshipUpdates();
                        break;
                    // Registry update check
                    case PackageSource.Registry:
                        CheckForAirshipUpdates();
                        break;
                }
                
                EditorApplication.update -= AwaitAirshipPackageListResult;
            }
        }

        private static SearchRequest _airshipPackageSearchRequest;
        private static void AwaitAirshipSearchRequest() {
            if (_airshipPackageSearchRequest.IsCompleted) {
                var result = _airshipPackageSearchRequest.Result.First();
                _airshipRemotePackageInfo = result;
                EditorApplication.update -= AwaitAirshipSearchRequest;
                
                // If not version match, update (show dialog first if applicable)
                if (_airshipLocalPackageInfo.version != _airshipRemotePackageInfo.version) {
                    if (EditorUtility.DisplayDialog("Airship Update",
                            "A new version of Airship is available, would you like to update?", "Update", "Ignore")) {
                        Debug.Log($"Updating Airship, this may take a few moments...");
                        _airshipPackageAddRequest = Client.Add("gg.easy.airship");
                        EditorUtility.DisplayProgressBar("Airship Editor Update", "Downloading & installing the latest version of Airship...", 0.5f);
                        EditorApplication.update += AwaitAirshipAddRequest;
                    }
                    else {
                        EditorUtility.ClearProgressBar();
                    }
                } else if (showDialog) {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Already On Latest",
                        "The latest version of Airship is already installed.", "Okay");
                }
                else {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        private static void CheckForAirshipUpdates() {
            // Search for the latest package information
            _airshipPackageSearchRequest = Client.Search("gg.easy.airship");

            EditorApplication.update += AwaitAirshipSearchRequest;
            var cancelled = EditorUtility.DisplayCancelableProgressBar("Airship Editor Update", "Requesting Airship package information from registry...", 0f);
            if (cancelled) {
                EditorApplication.update -= AwaitAirshipSearchRequest;
                EditorUtility.ClearProgressBar();
            }
        }

        private static AddRequest _airshipPackageAddRequest;
        private static void AwaitAirshipAddRequest() {
            if (_airshipPackageAddRequest.IsCompleted) {
                var result = _airshipPackageAddRequest.Result;
                if (result != null) {
                    Debug.Log($"Updated Airship to v{result.version}");
                }
                else if (_airshipPackageAddRequest.Error != null) {
                    Debug.LogWarning($"Unable to update Airship: {_airshipPackageAddRequest.Error.message}");
                }
                else {
                    Debug.LogWarning($"Unable to update Airship");
                }

                EditorApplication.update -= AwaitAirshipAddRequest;
                EditorUtility.ClearProgressBar();
            }
        }
    }
}