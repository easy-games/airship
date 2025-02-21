using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Bootstrap;
using Code.Platform.Shared;
using Code.Player.Accessories;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace Code.Accessories.Clothing {
    /**
     * Clothing exists on the backend and consists of one or many accessories.
     * Usually it's just one accessory (ie: a hat)
     */
    [CreateAssetMenu(menuName = "Airship/Clothing")]
    [Icon("Packages/gg.easy.airship/Editor/icons/hat-wizard-solid.png")]
    [LuauAPI(LuauContext.Protected)]
    public class Clothing : ScriptableObject {
        public string classId;
        public AccessoryComponent[] accessoryPrefabs;
        public AccessoryFace face;

        public static Dictionary<string, Task<bool>> inProgressDownloads = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void OnReload() {
            inProgressDownloads.Clear();
        }

        public static async Task<Clothing> DownloadYielding(string classId, string airId) {
            Debug.Log("Clothing.DownloadYielding");
            var platformString = AirshipPlatformUtil.GetStringName(AirshipPlatformUtil.GetLocalPlatform());
            var url = $"{AirshipPlatformUrl.gameCdn}/airassets/{airId}/{platformString}";

            // Check for in-progress downloads
            if (inProgressDownloads.TryGetValue(airId, out var task)) {
                // By the time we've finished awaiting this, the below existing bundle check will handle this.
                Debug.Log("Waiting for in-progress...");
                await task;
                Debug.Log("Done waiting for in-progress.");
            }

            // Check if we already loaded an asset bundle that contains this clothing piece.
            if (ClothingManager.Instance.loadedClothingBundles.TryGetValue(airId, out var loadedBundleInfo)) {
                foreach (var clothing in loadedBundleInfo.manifest.clothingList) {
                    if (clothing.classId == classId) {
                        return clothing;
                    }
                }
            }

            var inProgressTask = new TaskCompletionSource<bool>();
            inProgressDownloads.Add(airId, inProgressTask.Task);

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
                // foreach (var pair in headReq.GetResponseHeaders()) {
                //     Debug.Log($"{pair.Key}: {pair.Value}");
                // }
            }

            Debug.Log("Clothing -> Send web request: " + url);
            var req = UnityWebRequestAssetBundle.GetAssetBundle(url, hash);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download clothing bundle.");
                inProgressTask.SetResult(false);
                return null;
            }

            AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(req);
            // foreach (var asset in bundle.GetAllAssetNames()) {
            //     Debug.Log("  - " + asset);
            // }
            var manifestReq = bundle.LoadAssetAsync<ClothingBundleManifest>("clothing bundle manifest");

            await manifestReq;
            var manifest = (ClothingBundleManifest) manifestReq.asset;
            ClothingManager.Instance.loadedClothingBundles[airId] = new ClothingBundleInfo(bundle, manifest);
            foreach (var clothing in manifest.clothingList) {
                if (clothing.classId == classId) {
                    inProgressTask.SetResult(true);
                    return clothing;
                }
            }

            inProgressTask.SetResult(false);
            return null;
        }
    }

}