using System;

namespace Code.GameBundle {

    [Serializable]
    public class AirshipPackageDocument {
        public string id;
        public string version;
        public bool game = false;
        public bool localSource = false;
        public bool disabled = false;

        public AirshipPackageDocument(string id, string version, bool game, bool localSource, bool disabled) {
            this.id = id;
            this.version = version;
            this.game = game;
            this.localSource = localSource;
            this.disabled = disabled;
        }
    }
}