using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Accessories.Clothing;
using Code.Bootstrap;
using Code.Platform.Shared;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace Code.AirAsset {
    [CreateAssetMenu(menuName = "Airship/Air Asset Bundle")]
    [Icon("Packages/gg.easy.airship/Editor/icons/hat-wizard-solid.png")]
    public class AirAssetBundle : ScriptableObject {
        public string airId;
        [NonSerialized] private AssetBundle assetBundle;

        private static Dictionary<string, Task<bool>> inProgressDownloads = new();
        private static Dictionary<string, AirAssetBundle> loadedBundles = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void OnReload() {
            inProgressDownloads.Clear();
            loadedBundles.Clear();
        }

        public async Task<Object> LoadAsync(string assetPath) {
            var res = this.assetBundle.LoadAssetAsync(assetPath.ToLower());
            await res;
            return res.asset;
        }

        public static async Task<AirAssetBundle> DownloadYielding(string airId) {
            var platformString = AirshipPlatformUtil.GetStringName(AirshipPlatformUtil.GetLocalPlatform());
            var url = $"{AirshipPlatformUrl.gameCdn}/airassets/{airId}/{platformString}";

            // Check for in-progress downloads
            if (inProgressDownloads.TryGetValue(airId, out var task)) {
                // By the time we've finished awaiting this, the below existing bundle check will handle this.
                await task;
            }

            // Check if we already loaded an asset bundle that contains this clothing piece.
            if (loadedBundles.TryGetValue(airId, out var airBundle)) {
                return airBundle;
            }

            var inProgressTask = new TaskCompletionSource<bool>();
            inProgressDownloads.TryAdd(airId, inProgressTask.Task);

            // Get latest hash
            Hash128 hash;
            {
                var headReq = UnityWebRequest.Head(url);
                await headReq.SendWebRequest();

                var etag = headReq.GetResponseHeader("ETag");
                if (string.IsNullOrEmpty(etag)) {
                    Debug.LogError("Failed to get latest version hash for airId " + airId);
                    inProgressTask.SetResult(false);
                    return null;
                }

                hash = Hash128.Parse(etag);
            }

            var req = UnityWebRequestAssetBundle.GetAssetBundle(url, hash);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download clothing bundle.");
                inProgressTask.SetResult(false);
                return null;
            }

            AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(req);
            // Uncomment to list all assets inside the bundle.
            // foreach (var asset in bundle.GetAllAssetNames()) {
            //     Debug.Log("  - " + asset);
            // }
            var loadReq = bundle.LoadAssetAsync<AirAssetBundle>("_AirAssetBundle");
            await loadReq;
            var airAssetBundle = (AirAssetBundle) loadReq.asset;
            airAssetBundle.assetBundle = bundle;
            loadedBundles[airId] = airAssetBundle;

            inProgressTask.SetResult(true);
            return airAssetBundle;
        }
    }
}