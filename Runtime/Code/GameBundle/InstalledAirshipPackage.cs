using System;

namespace Code.GameBundle {

    [Serializable]
    public class InstalledAirshipPackage {
        public string id;
        public bool localSource = false;
        public bool disabled = false;
    }
}