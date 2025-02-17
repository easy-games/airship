using System;

namespace Editor.Accessories.Clothing {
    [Serializable]
    public class AirAssetCreateRequest {
        public string contentType;
        public int contentLength;
        public string name;
        public string description;
    }

    [Serializable]
    public class AirAssetCreateResponse {
        public string airAssetId;
        public string url;
    }
}