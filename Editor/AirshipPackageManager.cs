using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Airship.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Editor {
    
    [InitializeOnLoad]
    public class AirshipPackageManager {
        private static PackageInfo _airshipPackageInfo;
        private static ListRequest _listRequest;

        struct GitTreeInfo {
            [JsonProperty("sha")] public string Sha { get; set; }
        }
        
        struct GitCommitInfo {
            [JsonProperty("tree")] public GitTreeInfo Tree { get; set; }
        }

        struct GitCommitResponse {
            [JsonProperty("commit")] public GitCommitInfo Commit { get; set; }
        }

        static AirshipPackageManager() {
            Client.Resolve();
                
            _listRequest = Client.List();
            EditorApplication.update += ListRequestCheck;
        }

        private static async Task<GitCommitResponse?> FetchGitHash() {
            //var header = new ProductHeaderValue()
            NodePackages.LoadAuthToken();

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Unity/AirshipEditor");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AuthConfig.instance.githubAccessToken}");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            
            Debug.Log($"Run http req {client.DefaultRequestHeaders.ToString()}");
            var result = await client.GetAsync(new Uri("https://api.github.com/repos/easy-games/airship/commits/main"));
            
            if (result.IsSuccessStatusCode) {
                var body = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GitCommitResponse>(body);
            }
            else {
                Debug.Log($"Err code {result.StatusCode}: {await result.Content.ReadAsStringAsync()}");
            }

            return null;
        }

        private static async Task CheckAirshipPackageVersion() {
            var package = _airshipPackageInfo;
            if (package.git == null) {
                return;
            }
            
            Debug.Log($"Airship v{package.version} installed.");
            var localSHA = package.git.hash;
            
            var gitCommitQuery = await FetchGitHash();
            if (gitCommitQuery != null) {
                var remoteSHA = gitCommitQuery.Value.Commit.Tree.Sha;
                if (remoteSHA != localSHA) {
                    if (EditorUtility.DisplayDialog("Airship Update", "A new version of Airship is available, would you like to update?", "Update", "Ignore"))
                    {
                        var req = Client.Add("https://github.com/easy-games/airship.git")
                    }
                }
            }
        }

        private static void ListRequestCheck() {
            if (_listRequest.IsCompleted) {
                foreach (var result in _listRequest.Result) {
                    if (result.name == "gg.easy.airship") {
                        _airshipPackageInfo = result;
                        break;
                    }
                }
                    
                EditorApplication.update -= ListRequestCheck;
                if (_airshipPackageInfo != null) {
                    var _ = CheckAirshipPackageVersion();
                }
            }
        }
    }

}