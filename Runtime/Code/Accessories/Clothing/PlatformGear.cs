using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Bootstrap;
using Code.Platform.Shared;
using Code.Player.Accessories;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace Code.Accessories.Clothing {
    public class PlatformGearBundleInfo {
        public AssetBundle assetBundle;
        public PlatformGearBundleManifest manifest;

        public PlatformGearBundleInfo(AssetBundle assetBundle, PlatformGearBundleManifest manifest) {
            this.assetBundle = assetBundle;
            this.manifest = manifest;
        }
    }

    public class GearFetchResponse {
        public GearDto gear;
    }

    public class GearDto {
        public GearListingDto gear;
    }

    public class GearListingDto {
        public string category;
        public string subcategory;
        public string[] airAssets;
    }

    /**
     * Clothing exists on the backend and consists of one or many accessories.
     * Usually it's just one accessory (ie: a hat)
     */
    [CreateAssetMenu(menuName = "Airship/Platform Gear")]
    [Icon("Packages/gg.easy.airship/Editor/icons/hat-wizard-solid.png")]
    [LuauAPI]
    public class PlatformGear : ScriptableObject {
        public string classId;
        public AccessoryComponent[] accessoryPrefabs;
        public AccessoryFace face;

        public static Dictionary<string, Task<bool>> inProgressDownloads = new();
        /// <summary>
        /// AirId to asset bundle
        /// </summary>
        public static Dictionary<string, PlatformGearBundleInfo> loadedPlatformGearBundles = new();

        private static Dictionary<string, string> classIdToAirIdCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void OnReload() {
            // foreach (var bundle in loadedPlatformGearBundles) {
            //     bundle.Value.assetBundle.Unload(true);
            // }
            // loadedPlatformGearBundles.Clear();
            inProgressDownloads.Clear();
            classIdToAirIdCache.Clear();
        }

        public static async Task<PlatformGear> DownloadYielding(string classId) {
            if (classIdToAirIdCache.TryGetValue(classId, out string airId)) {
                return await DownloadYielding(classId, airId);
            }

            // Get airId from classId
            {
                var url = $"{AirshipPlatformUrl.contentService}/gear/class-id/{classId}";
                var req = UnityWebRequest.Get(url);
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) {
                    throw new Exception(req.error);
                }

                var gearRes = JsonUtility.FromJson<GearFetchResponse>(req.downloadHandler.text);
                Debug.Log("air ids: " + gearRes.gear.gear.airAssets);
                airId = gearRes.gear.gear.airAssets[0];
            }

            return await DownloadYielding(classId, airId);
        }

        public static async Task<PlatformGear> DownloadYielding(string classId, string airId) {
            var platformString = AirshipPlatformUtil.GetStringName(AirshipPlatformUtil.GetLocalPlatform());
            // var platformString = AirshipPlatformUtil.GetStringName(AirshipPlatform.Windows);
            var url = $"{AirshipPlatformUrl.gameCdn}/airassets/{airId}/{platformString}";

            // Check for in-progress downloads
            if (inProgressDownloads.TryGetValue(airId, out var task)) {
                // By the time we've finished awaiting this, the below existing bundle check will handle this.
                await task;
            }

            // Check if we already loaded an asset bundle that contains this clothing piece.
            if (loadedPlatformGearBundles.TryGetValue(airId, out var loadedBundleInfo)) {
                foreach (var clothing in loadedBundleInfo.manifest.gearList) {
                    if (clothing.classId == classId) {
                        return clothing;
                    }
                }
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
            var manifestReq = bundle.LoadAssetAsync<PlatformGearBundleManifest>("gear bundle manifest");

            await manifestReq;
            var manifest = (PlatformGearBundleManifest) manifestReq.asset;
            loadedPlatformGearBundles[airId] = new PlatformGearBundleInfo(bundle, manifest);
            foreach (var clothing in manifest.gearList) {
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