using System;

namespace Code.GameBundle {

    [Serializable]
    public class AirshipPackageDocument {
        public string id;
        public string version;
        public bool game = false;
        public bool localSource = false;
        public bool disabled = false;
        public bool defaultPackage = false;
        public bool forceLatestVersion = false;
    }
}