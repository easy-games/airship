using Code.Bootstrap;
using UnityEngine;

namespace Code.Accessories.Clothing {
    [CreateAssetMenu(menuName = "Airship/Clothing Bundle Manifest", fileName = "Clothing Bundle Manifest")]
    public class ClothingBundleManifest : ScriptableObject {
        public Clothing[] clothingList;
        public string macAirId;
        public string windowsAirId;
        public string linuxAirId;
        public string iosAirId;
        public string androidAirId;

        public string GetAirIdForPlatform(AirshipPlatform platform) {
            switch (platform) {
                case AirshipPlatform.Android:
                    return this.androidAirId;
                case AirshipPlatform.Linux:
                    return this.linuxAirId;
                case AirshipPlatform.Mac:
                    return this.macAirId;
                case AirshipPlatform.Windows:
                    return this.windowsAirId;
                case AirshipPlatform.iOS:
                    return this.iosAirId;
                default:
                    return "";
            }
        }
    }
}