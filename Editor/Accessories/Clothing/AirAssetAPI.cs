using System;
using System.Collections.Generic;
using Code.Bootstrap;

// ReSharper disable InconsistentNaming

namespace Editor.Accessories.Clothing {
    [Serializable]
    public class AirAssetCreateRequest {
        public string contentType;
        public int contentLength;
        public string name;
        public string description;
        public string[] platforms;
    }

    [Serializable]
    public class AirAssetCreateResponse {
        public string airAssetId;
        public AirAssetCreateUrls urls;
        public AirAssetKeyValuePair[] headers;
    }

    [Serializable]
    public class AirAssetCreateUrls {
        public string Windows;
        public string Mac;
        public string Linux;
        public string iOS;
        public string Android;

        public string UrlFromPlatform(AirshipPlatform platform) {
            switch (platform) {
                case AirshipPlatform.Windows:
                    return this.Windows;
                case AirshipPlatform.iOS:
                    return this.iOS;
                case AirshipPlatform.Mac:
                    return this.Mac;
                case AirshipPlatform.Android:
                    return this.Android;
                case AirshipPlatform.Linux:
                    return this.Linux;
                default:
                    return Windows;
            }
        }
    }

    [Serializable]
    public class AirAssetKeyValuePair {
        public string key;
        public string value;
    }
}