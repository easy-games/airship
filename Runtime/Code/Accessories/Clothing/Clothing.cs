using System.Threading.Tasks;
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

        public static async Task<Clothing> DownloadYielding(string classId, string airId) {
            var url = $"{AirshipPlatformUrl.gameCdn}/airassets/{airId}";

            // Check if we already loaded an asset bundle that contains this clothing piece.
            if (ClothingManager.Instance.loadedClothingBundles.TryGetValue(airId, out var loadedBundleInfo)) {
                foreach (var clothing in loadedBundleInfo.manifest.clothingList) {
                    if (clothing.classId == classId) {
                        return clothing;
                    }
                }
            }

            // Get latest hash
            Hash128 hash;
            {
                var headReq = UnityWebRequest.Head(url);
                await headReq.SendWebRequest();

                var etag = headReq.GetResponseHeader("ETag");
                if (string.IsNullOrEmpty(etag)) {
                    Debug.LogError("Failed to get latest version hash for airId " + airId);
                    return null;
                }

                hash = Hash128.Parse(etag);
                // foreach (var pair in headReq.GetResponseHeaders()) {
                //     Debug.Log($"{pair.Key}: {pair.Value}");
                // }
            }

            var req = UnityWebRequestAssetBundle.GetAssetBundle(url, hash);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download clothing bundle.");
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
                    return clothing;
                }
            }

            return null;
        }
    }

}