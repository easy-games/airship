using System.Collections.Generic;
using UnityEngine;
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

    public class PlatformGearManager : Singleton<PlatformGearManager> {
        /// <summary>
        /// AirId to asset bundle
        /// </summary>
        public Dictionary<string, PlatformGearBundleInfo> loadedPlatformGearBundles = new();
    }
}