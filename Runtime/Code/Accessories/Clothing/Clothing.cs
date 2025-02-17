using System.Threading.Tasks;
using Code.Platform.Shared;
using Code.Player.Accessories;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Accessories.Clothing {
    /**
     * Clothing exists on the backend and consists of one or many accessories.
     * Usually it's just one accessory (ie: a hat)
     */
    public class Clothing : ScriptableObject {
        public string className;
        public AccessoryComponent[] accessoryPrefabs;

        public static async Task<Clothing> DownloadAsync(string className, string airId, string versionHash) {
            var url = $"{AirshipPlatformUrl.cdn}/airassets/${airId}";
            var hash = Hash128.Parse(versionHash);

            var req = UnityWebRequestAssetBundle.GetAssetBundle(url, hash);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) {
                Debug.LogError("Failed to download clothing bundle.");
                return null;
            }

            AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(req);
            var manifestReq = bundle.LoadAssetAsync<ClothingBundleManifest>("ClothingBundleManifest");
            await manifestReq;
            var manifest = (ClothingBundleManifest) manifestReq.asset;
            foreach (var clothing in manifest.clothingList) {
                if (clothing.className == className) {
                    return clothing;
                }
            }

            return null;
        }
    }



}