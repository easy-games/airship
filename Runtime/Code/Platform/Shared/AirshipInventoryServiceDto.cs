using System;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Platform.Shared {
    [Serializable ]
     public enum ResourceType {
        GAME,
        ORGANIZATION
    }

    [Serializable]
    public class Permitted{
        public bool permitted;
    }

    [Serializable ]
     public class ItemClass {
        /** The type of resource that owns this item class */
        public ResourceType resourceType;
        /** Either the game ID or the organization ID depending on the resource type */
        public string resourceId;

        /** The ID of the item class. Item instances will reference this class ID. */
        public string classId;

        public string name;
        public string imageId; // thumbnail ID (https://easy-cdn/images/{imageId})
        public string[] tags;
        public string description;

        /** Whether or not this item will be granted by default when a player joins your game. */
        public bool @default; // whether or not it will be granted to users by default

        public Permitted tradable;
        public Permitted marketable;
    }

    [Serializable]
    public class PlatformItemCreateRequest {
        public string name;
        public string imageId; // thumbnail ID (https://easy-cdn/images/{imageId})
        public string[] tags;
        public string description;

        /** Whether or not this item will be granted by default when a player joins your game. */
        public bool @default; // whether or not it will be granted to users by default

        public bool tradable;
        public bool marketable;

        public GearCreateRequest gear;
    }

    [Serializable]
    public class GearCreateRequest : PlatformItemCreateRequest {
        public string[] airAssets;
        public string category;
        public string subcategory;
    }

    [Serializable]
    public class GearPatchRequest {
        public string[] airAssets;
    }

    [Serializable]
    public class GearClass : ItemClass {
    }

    [Serializable]
    public class ImageId{
        public string imageId;
    }

    [Serializable]
     public class ItemInstanceDto {
        public string ownerId;
        public string classId;
        public ItemClass @class;
        public string instanceId;
        public string createdAt;
        float @float;
    }

    [Serializable ]
     public class GearInstanceDto  {
        public GearClass @class;
        public string ownerId;    
        public string instanceId;
        public string createdAt;
        float @float;
    }

    [Serializable ]
     public class OutfitPatch {
        public string[] gear;
        public string skinColor;
        public string name;
    }

    [Serializable ]
     public class OutfitResponse {
        public OutfitDto outfit;
    }

    [Serializable ]
    public class OutfitDto {
        public string outfitId;
        public string owner;

        public string name;
        /** Hex public string */
        public string skinColor;
        public GearInstanceDto[] gear;

        public bool equipped;
    }

    [Serializable]
    public class UserResponse {
        public UserData user;
    }

    [Serializable ]
    public class UserData {
        public string uid;
        public string username;
        public string usernameLower;
    }

}