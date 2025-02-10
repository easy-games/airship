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
    public class AirshipUpdateService {
        private enum EnvironmentTag {
            Production,
            Staging,
        }
        
        private static string packageRegistry = "https://registry.npmjs.org";
        
        private static PackageInfo _airshipLocalPackageInfo;
        private static bool showDialog = false;

        /// <summary>
        /// Will be true if Airship is currently updating
        /// </summary>
        public static bool IsUpdatingAirship { get; private set; }

        private struct GitCommitsResponse {
            [JsonProperty("sha")] public string SHA { get; set; }
        }

        /// <summary>
        /// The installed version of the Airship package for the project
        /// </summary>
        private static Semver InstalledVersion => Semver.Parse(_airshipLocalPackageInfo.version);

        /// <summary>
        /// Gets the remote version of an Airship package
        /// </summary>
        /// <param name="environmentTag">The environment to get the version for</param>
        /// <returns></returns>
        private static Semver? GetRemoteVersion(EnvironmentTag environmentTag) {
            var target = environmentTag switch {
                EnvironmentTag.Production => "latest",
                EnvironmentTag.Staging => "staging",
                _ => throw new ArgumentOutOfRangeException(nameof(environmentTag), environmentTag, null)
            };
            
            var command = $"view gg.easy.airship@{target} version"; // e.g. npm view gg.easy.airship@latest version - should return something like 0.1.1561
            
            if (NodePackages.GetCommandOutput(TypescriptProjectsService.Project.Directory, command, out var output)) {
                return Semver.Parse(output[^1]);
            }
      
            return null;
        }
        
        [MenuItem("Airship/Check For Updates", priority = 2000)]
        public static void CheckForAirshipPackageUpdate() {
            // Check Airship itself
            var offline = Application.internetReachability == NetworkReachability.NotReachable;
            if (offline) {
                EditorUtility.DisplayDialog("Limited Connectivity", "Cannot check for updates for Airship while your connection is limited", "OK");
                return;
            }
            
#if !AIRSHIP_INTERNAL
            showDialog = true;
            // List the current package
            _airshipPackageListRequest = Client.List(true, false);
            EditorApplication.update += AwaitAirshipPackageListResult;
#endif
      
            // Update any relevant Typescript packages
            TypescriptProjectsService.CheckTypescriptProject();

            // Update the AirshipPackages
            AirshipPackageAutoUpdater.CheckPackageVersions(ignoreUserSetting: true);
        }

        static AirshipUpdateService() {
            if (RunCore.IsClone()) return;

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
                    // Registry update check
                    case PackageSource.Registry:
                        CheckForAirshipUpdates();
                        break;
                }
                
                EditorApplication.update -= AwaitAirshipPackageListResult;
            }
        }

        private static SearchRequest _airshipPackageSearchRequest;

        private static void CheckForAirshipUpdates() {
#if AIRSHIP_STAGING
            const EnvironmentTag environment = EnvironmentTag.Staging;
#else
            const EnvironmentTag environment = EnvironmentTag.Production;
#endif
            
            var remoteVersionResult = GetRemoteVersion(environment);
            if (!remoteVersionResult.HasValue) {
                return;
            }

            var remoteVersion = remoteVersionResult.Value;
            if (remoteVersion.IsNewerThan(InstalledVersion)) {
                // If newer we'll do a request
                if (EditorUtility.DisplayDialog("Airship Update",
                        "A new version of Airship is available, would you like to update?", "Update", "Ignore")) {
                    Debug.Log($"Updating Airship, this may take a few moments...");

                    // Force restart TS if we're package updating
                    IsUpdatingAirship = true;
                    EditorCoroutines.Execute(TypescriptServices.RestartAndAwaitUpdates());
                    
                    _airshipPackageAddRequest = Client.Add($"gg.easy.airship@{remoteVersion}"); // We can thankfully install a specific version
                    
                    EditorUtility.DisplayProgressBar("Airship Editor Update", "Downloading & installing the latest version of Airship...", 0.5f);
                    EditorApplication.update += AwaitAirshipAddRequest;
                }
                else {
                    EditorUtility.ClearProgressBar();
                }
            }
            else if (showDialog) {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Already On Latest",
                    "The latest version of Airship is already installed.", "Okay");
            }
            else {
                EditorUtility.ClearProgressBar();
            }
        }

        private static AddRequest _airshipPackageAddRequest;
        private static void AwaitAirshipAddRequest() {
            if (!_airshipPackageAddRequest.IsCompleted) return;
            
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
            IsUpdatingAirship = false;
            EditorUtility.ClearProgressBar();
        }
    }
}