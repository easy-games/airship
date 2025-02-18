using System.Collections.Generic;
using UnityEngine;

namespace Code.Accessories.Clothing {
    public class ClothingBundleInfo {
        public AssetBundle assetBundle;
        public ClothingBundleManifest manifest;

        public ClothingBundleInfo(AssetBundle assetBundle, ClothingBundleManifest manifest) {
            this.assetBundle = assetBundle;
            this.manifest = manifest;
        }
    }

    [LuauAPI(LuauContext.Protected)]
    public class ClothingManager : Singleton<ClothingManager> {
        /// <summary>
        /// AirId to asset bundle
        /// </summary>
        public Dictionary<string, ClothingBundleInfo> loadedClothingBundles = new();


    }
}