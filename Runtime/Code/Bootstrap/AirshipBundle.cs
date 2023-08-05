using System.Collections.Generic;

namespace Code.Bootstrap {
    public enum AirshipBundleType {
        Game,
        Bundle,
    }

    public class AirshipBundle {
        public string id;
        public string version;
        public AirshipBundleType bundleType;

        public AirshipBundle(string id, string version, AirshipBundleType bundleType) {
            this.id = id;
            this.version = version;
            this.bundleType = bundleType;
        }

        public List<RemoteBundleFile> GetClientAndSharedRemoteBundleFiles(string cdnUrl, string platform) {
            List<RemoteBundleFile> results = new();

            void AddRemoteBundleFile(string fileName)
            {
                var url = $"{cdnUrl}/{this.id}/{this.version}/{platform}/{fileName}";
                results.Add(new RemoteBundleFile(fileName, url, this.id));
                results.Add(new RemoteBundleFile(fileName + ".manifest", url + ".manifest", this.id));
            }

            AddRemoteBundleFile("client/resources");
            AddRemoteBundleFile("client/scenes");
            AddRemoteBundleFile("shared/resources");
            AddRemoteBundleFile("shared/scenes");

            return results;
        }
    }
}