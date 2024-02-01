using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Airship.Editor;
using Newtonsoft.Json;
using Unity.VisualScripting;
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
        
        public void AddScopedRegistry(ScopedRegistry registry) {
            this.scopedRegistries.Add(registry);
        }
        
        public void Save() {
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }

    
    [InitializeOnLoad]
    public class AirshipPackageManager {
        private static string packageRegistry = "https://registry-staging.airship.gg";
        
        private static PackageInfo _airshipLocalPackageInfo;
        private static PackageInfo _airshipRemotePackageInfo;
        
        private static bool showDialog = false;

        private struct GitCommitsResponse {
            [JsonProperty("sha")] public string SHA { get; set; }
        }

        #if !AIRSHIP_INTERNAL
        [MenuItem("Airship/Check For Updates", priority = 2000)]
        #endif
        public static void CheckForAirshipPackageUpdate() {
            showDialog = true;
            CheckForAirshipUpdates();
        }

        static AirshipPackageManager() {
            if (RunCore.IsClone()) return;

            // Ensure this only runs ON LOAD. No script recompiling...
            if (!SessionState.GetBool("AirshipUpdateCheck", false)) {
                SessionState.SetBool("AirshipUpdateCheck", true);

                var manifest = ManifestJson.Load();

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

                // Resolve & lookup the airship package on the registrye
                Client.Resolve();

                // List the current package
                _airshipPackageListRequest = Client.List();
                EditorApplication.update += AwaitAirshipPackageListResult;
            }
        }

        private static readonly ListRequest _airshipPackageListRequest;
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
                    // Do nothing if local
                    case PackageSource.Local:
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
                        _airshipPackageAddRequest = Client.Add("gg.easy.airship");
                        EditorApplication.update += AwaitAirshipAddRequest;
                    }
                } else if (showDialog) {
                    EditorUtility.DisplayDialog("Already On Latest", "The latest version of Airship is already installed.", "Okay");
                }
            }
        }

        private static void CheckForAirshipUpdates() {
            // Search for the latest package information
            _airshipPackageSearchRequest = Client.Search("gg.easy.airship");
            EditorApplication.update = AwaitAirshipSearchRequest;
        }

        private static AddRequest _airshipPackageAddRequest;
        private static void AwaitAirshipAddRequest() {
            if (_airshipPackageAddRequest.IsCompleted) {
                var result = _airshipPackageAddRequest.Result;
                Debug.Log($"Updated Airship to v{result.version}");
                EditorApplication.update -= AwaitAirshipAddRequest;
            }
        }
    }
}