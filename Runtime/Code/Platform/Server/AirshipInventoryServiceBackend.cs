using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;
using UnityEngine;

namespace Code.Platform.Server
{ 
    public class AirshipInventoryServiceBackend
    {
        public static async Task<HttpResponse> CreateItem(string ownerResourceId, AccessoryClassInput data) {
            var jsonBlob = JsonUtility.ToJson(data);
            var httpPath = $"{AirshipPlatformUrl.contentService}/accessories/resource-id/{ownerResourceId}";
            Debug.Log("Sending Post request: " + httpPath + " blob: " + jsonBlob);
            return await InternalHttpManager.PostAsync(httpPath, jsonBlob);
        }

        public static async Task<HttpResponse> GetEquippedOutfitByUserId(string userId)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.contentService}/outfits/uid/{userId}/equipped");
        }
    }
}