using System.Collections.Generic;

namespace Code.Bootstrap {
    public class AirshipBundle {
        public string id;
        public string version;

        public AirshipBundle(string id, string version) {
            this.id = id;
            this.version = version;
        }

        public List<RemoteBundleFile> GetPublicRemoteFiles(string cdnUrl) {
            List<RemoteBundleFile> results = new();
            var platform = BundleDownloader.GetPlatformString();

            void AddPublicUrlsForBundle(string bundleName)
            {
                var url = $"{cdnUrl}/{this.id}/{this.version}/{platform}/{bundleName}";
                results.Add(new RemoteBundleFile(bundleName, url, this.id));
                results.Add(new RemoteBundleFile(bundleName + ".manifest", url + ".manifest", this.id));
            }

            AddPublicUrlsForBundle("client/resources");
            AddPublicUrlsForBundle("client/scenes");
            AddPublicUrlsForBundle("shared/resources");
            AddPublicUrlsForBundle("shared/scenes");

            return results;
        }
    }
}