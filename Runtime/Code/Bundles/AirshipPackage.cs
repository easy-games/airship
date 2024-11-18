using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Code.Bootstrap {
    public enum AirshipPackageType {
        Game,
        Package,
    }

    public class AirshipPackage {
        public string id;
        public string assetVersion;
        public string codeVersion;
        public string publishVersionNumber;
        public AirshipPackageType packageType;

        public AirshipPackage(string id, string assetVersion, string codeVersion, string publishVersionNumber, AirshipPackageType packageType) {
            this.id = id;
            this.assetVersion = assetVersion;
            this.codeVersion = codeVersion;
            this.publishVersionNumber = publishVersionNumber;
            this.packageType = packageType;
        }

        public List<RemoteBundleFile> GetPublicRemoteBundleFiles(string cdnUrl, AirshipPlatform platform) {
            List<RemoteBundleFile> results = new();

            // Force linux to download windows bundles
            if (platform == AirshipPlatform.Linux) {
                platform = AirshipPlatform.Windows;
            }

            void AddRemoteBundleFile(string fileName) {
                var version = this.assetVersion;
                if (this.id.ToLower() == "@easy/corematerials") {
                    // version = 72 + "";
                }
                var url = $"{cdnUrl}/{(this.packageType == AirshipPackageType.Game ? "game" : "package")}/{this.id.ToLower()}/assets/{version}/{platform}/{fileName}";
                results.Add(new RemoteBundleFile(fileName, url, this.id, this.assetVersion));
                // results.Add(new RemoteBundleFile(fileName + ".manifest", url + ".manifest", this.id, this.version));
            }

            // AddRemoteBundleFile("client/resources");
            // AddRemoteBundleFile("client/scenes");
            AddRemoteBundleFile("shared/resources");
            AddRemoteBundleFile("shared/scenes");

            if (this.packageType == AirshipPackageType.Package && RunCore.IsServer()) {
                AddRemoteBundleFile("server/resources");
                // AddRemoteBundleFile("server/scenes");
            }

            return results;
        }

        public string GetPersistentDataDirectory(AirshipPlatform platform) {
            if (this.packageType == AirshipPackageType.Game) {
                return Path.Combine(Application.persistentDataPath, "Games", this.id + "_v" + this.assetVersion, platform.ToString());
            } else {
                var split = id.Split("/");
                return Path.Combine(Application.persistentDataPath, "Packages", split[0], split[1] + "_v" + this.assetVersion, platform.ToString());
            }
        }

        public string[] GetOlderDataDirectories(AirshipPlatform platform) {
            var assetVersionInt = Int32.Parse(this.assetVersion);
            if (this.packageType == AirshipPackageType.Game) {
                // folders to delete
                var folders = Directory.GetDirectories(Application.persistentDataPath, "Games")
                    .Where((path) => path.Contains(this.id + "_v"))
                    .Where((path) => {
                        try {
                            var otherVersion = Int32.Parse(path.Split(this.id + "_v")[1]);
                            return otherVersion < assetVersionInt;
                        } catch (Exception e) {
                            Debug.LogException(e);
                        }
                        return false;
                    });
                return folders.ToArray();
            } else {
                var split = id.Split("/");
                var folders = Directory.GetDirectories(Path.Join(Application.persistentDataPath, "Packages", split[0]))
                    .Where((path) => path.Contains(split[1] + "_v"))
                    .Where((path) => {
                        try {
                            var otherVersion = Int32.Parse(path.Split(split[1] + "_v")[1]);
                            return otherVersion < assetVersionInt;
                        } catch (Exception e) {
                            Debug.LogException(e);
                        }
                        return false;
                    });
                return folders.ToArray();
            }
        }

        public string GetPersistentDataDirectory() {
            if (this.packageType == AirshipPackageType.Game) {
                return Path.Combine(Application.persistentDataPath, "Games", this.id + "_v" + this.assetVersion);
            } else {
                var split = id.Split("/");
                return Path.Combine(Application.persistentDataPath, "Packages", split[0], split[1] + "_v" + this.assetVersion);
            }
        }
    }
}