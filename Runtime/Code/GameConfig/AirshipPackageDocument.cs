using System;

namespace Code.GameBundle {

    [Serializable]
    public class AirshipPackageDocument {
        public string id;
        public string version;
        public bool game = false;
        public bool localSource = false;
        public bool disabled = false;
    }
}