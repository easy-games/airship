using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Airship.Editor;
using Newtonsoft.Json;
using Unity.VisualScripting;
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
        private static AddRequest _addRequest;

        private struct GitCommitsResponse {
            [JsonProperty("sha")] public string SHA { get; set; }
        }

        static AirshipPackageManager() {
            // Ensure this only runs ON LOAD. No script recompiling...
            if (!SessionState.GetBool("AirshipUpdateCheck", false)) {
                SessionState.SetBool("AirshipUpdateCheck", true);
                Client.Resolve();
                
                // This should fetch all the packages we have, then we use the `update` event to wait for it to complete
                _listRequest = Client.List();
                EditorApplication.update += ListRequestCheck;
            }
        }

        /// <summary>
        /// Send a network request to Github to fetch the latest airship repository commit info
        /// </summary>
        private static async Task<GitCommitsResponse?> FetchAirshipCommits() {
            NodePackages.LoadAuthToken();

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Unity/AirshipEditor");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AuthConfig.instance.githubAccessToken}");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            
            var result = await client.GetAsync(new Uri("https://api.github.com/repos/easy-games/airship/commits/main"));
            
            if (result.IsSuccessStatusCode) {
                var body = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<GitCommitsResponse>(body);
            }
            else {
                Debug.Log($"Failed to fetch Airship repository information for update check - do you have the auth token set?");
            }

            return null;
        }

        /// <summary>
        /// Will check the airship package version against the remote github version
        /// </summary>
        private static async Task CheckAirshipPackageVersion() {
            // If this package is locally installed, it wont have git info - so the update check can be skipped this way.
            var package = _airshipPackageInfo;
            if (package.git == null) {
                return;
            }
            
            var localSHA = package.git.hash;
            
            // We can then fetch the remote git commit info, check the remote SHA hash against the local hash
            var gitCommitQuery = await FetchAirshipCommits();
            if (gitCommitQuery != null) {
                var remoteSHA = gitCommitQuery.Value.SHA;
                if (remoteSHA != localSHA) {
                    // Prompt the user to update - if they accept we add the specific remote hash as a unity package (update it)
                    if (EditorUtility.DisplayDialog("Airship Update", "A new version of Airship is available, would you like to update?", "Update", "Ignore")) {
                        var req = Client.Add($"https://github.com/easy-games/airship.git#{remoteSHA}");

                        _addRequest = req;
                        EditorApplication.update += AddRequestCheck; // await the add request result using the update event
                    }
                }
            }
        }

        /// <summary>
        /// Update event for when the add request is in progress
        /// </summary>
        private static void AddRequestCheck() {
            if (_addRequest.IsCompleted) {
                if (_addRequest.Result != null) {
                    Debug.Log($"Airship updated.");
                }
                else {
                    Debug.LogError($"Failed to update Airship: {_addRequest.Error.message}");
                }
 
                EditorApplication.update -= AddRequestCheck;
            }
        }

        /// <summary>
        /// Update event for when the list request is in progress
        /// </summary>
        private static void ListRequestCheck() {
            if (_listRequest.IsCompleted) {
                // Once complete, then we grab the gg.easy.airship package - and check the git version against remote
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