using UnityEngine;

namespace Code.Bundles {
    public class AirshipVersion {
        private static string versionHash = null;
        public static string GetVersionHash() {
            if (versionHash == null) {
                versionHash = "unknown";
                var gitHash = UnityEngine.Resources.Load<TextAsset>("GitHash");
                if (gitHash != null) {
                    versionHash = gitHash.text;
                }
            }
            return versionHash;
        }
    }
}