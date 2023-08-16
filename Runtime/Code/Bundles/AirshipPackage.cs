using System.Collections.Generic;
using System.IO;

namespace Code.Bootstrap {
    public enum AirshipPackageType {
        Game,
        Package,
    }

    public class AirshipPackage {
        public string id;
        public string version;
        public AirshipPackageType packageType;

        public AirshipPackage(string id, string version, AirshipPackageType packageType) {
            this.id = id;
            this.version = version;
            this.packageType = packageType;
        }

        public List<RemoteBundleFile> GetPublicRemoteBundleFiles(string cdnUrl, AirshipPlatform platform) {
            List<RemoteBundleFile> results = new();

            void AddRemoteBundleFile(string fileName)
            {
                var url = $"{cdnUrl}/{(this.packageType == AirshipPackageType.Game ? "game" : "package")}{this.id}/{this.version}/{platform}/{fileName}";
                results.Add(new RemoteBundleFile(fileName, url, this.id, this.version));
                results.Add(new RemoteBundleFile(fileName + ".manifest", url + ".manifest", this.id, this.version));
            }

            AddRemoteBundleFile("client/resources");
            AddRemoteBundleFile("client/scenes");
            AddRemoteBundleFile("shared/resources");
            AddRemoteBundleFile("shared/scenes");

            if (this.packageType == AirshipPackageType.Package) {
                AddRemoteBundleFile("server/resources");
                AddRemoteBundleFile("server/scenes");
            }

            return results;
        }

        public string GetBuiltAssetBundleDirectory() {
            if (this.packageType == AirshipPackageType.Game) {
                return Path.Join(AssetBridge.GamesPath, this.id + "_v" + this.version);
            } else {
                return Path.Join(AssetBridge.PackagesPath, this.id + "_v" + this.version);
            }
        }
    }
}