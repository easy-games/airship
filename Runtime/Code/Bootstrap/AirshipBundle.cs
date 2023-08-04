using System.Collections.Generic;

namespace Code.Bootstrap {
    public class AirshipBundle {
        public string id;
        public string version;

        public AirshipBundle(string id, string version) {
            this.id = id;
            this.version = version;
        }

        public List<RemoteBundleFile> GetClientAndSharedRemoteBundleFiles(string cdnUrl, string platform) {
            List<RemoteBundleFile> results = new();

            void AddRemoteBundleFile(string bundleName)
            {
                var url = $"{cdnUrl}/{this.id}/{this.version}/{platform}/{bundleName}";
                results.Add(new RemoteBundleFile(bundleName, url, this.id));
                results.Add(new RemoteBundleFile(bundleName + ".manifest", url + ".manifest", this.id));
            }

            AddRemoteBundleFile("client/resources");
            AddRemoteBundleFile("client/scenes");
            AddRemoteBundleFile("shared/resources");
            AddRemoteBundleFile("shared/scenes");

            return results;
        }
    }
}