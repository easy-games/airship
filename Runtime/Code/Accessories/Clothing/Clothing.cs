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
    public class Clothing : ScriptableObject {
        public string classId;
        public AccessoryComponent[] accessoryPrefabs;

        public static async Task<Clothing> DownloadAsync(string classId, string airId, string versionHash) {
            var url = $"{AirshipPlatformUrl.cdn}/airassets/${airId}";
            var hash = Hash128.Parse(versionHash);

            // Check if we already loaded an asset bundle that contains this clothing piece.
            if (ClothingManager.Instance.loadedClothingBundles.TryGetValue(airId, out var loadedBundleInfo)) {
                foreach (var clothing in loadedBundleInfo.manifest.clothingList) {
                    if (clothing.classId == classId) {
                        return clothing;
                    }
                }
            }

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