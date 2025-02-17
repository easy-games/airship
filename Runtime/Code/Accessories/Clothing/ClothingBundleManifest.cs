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

        public void SetAirIdForPlatform(AirshipPlatform platform, string airId) {
            switch (platform) {
                case AirshipPlatform.Android:
                    this.androidAirId = airId;
                    return;
                case AirshipPlatform.Linux:
                    this.linuxAirId = airId;
                    return;
                case AirshipPlatform.Mac:
                    this.macAirId = airId;
                    return;
                case AirshipPlatform.Windows:
                    this.windowsAirId = airId;
                    return;
                case AirshipPlatform.iOS:
                    this.iosAirId = airId;
                    return;
            }
        }
    }
}