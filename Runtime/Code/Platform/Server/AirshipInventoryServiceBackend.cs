using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Server
{
    [LuauAPI]
    public class AirshipInventoryServiceBackend
    {
        public static async Task<HttpResponse> GrantItem(string uid,
            string classId)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.ContentService}/items/uid/{uid}/class-id/{classId}",
                "");
        }

        public static async Task<HttpResponse> GrantAccessory(string uid,
            string classId)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.ContentService}/accessories/uid/{uid}/class-id/{classId}");
        }

        public static async Task<HttpResponse> GrantProfilePicture(string uid,
            string classId)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.ContentService}/accessories/uid/{uid}/class-id/{classId}");
        }

        public static async Task<HttpResponse> DeleteItem(string itemId)
        {
            return await InternalHttpManager.DeleteAsync(
                $"{AirshipPlatformUrl.ContentService}/items/item-id/{itemId}");
        }

        public static async Task<HttpResponse> DeleteAccessory(string itemId)
        {
            return await InternalHttpManager.DeleteAsync(
                $"{AirshipPlatformUrl.ContentService}/accessories/item-id/{itemId}");
        }

        public static async Task<HttpResponse> DeleteProfilePicture(string itemId)
        {
            return await InternalHttpManager.DeleteAsync(
                $"{AirshipPlatformUrl.ContentService}/profile-pictures/item-id/{itemId}");
        }

        public static async Task<HttpResponse> GetItems(string uid, string query)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.ContentService}/items/uid/{uid}?={query}");
        }

        public static async Task<HttpResponse> GetAccessories(string uid, string query)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.ContentService}/accessories/uid/{uid}?={query}");
        }

        public static async Task<HttpResponse> GetProfilePictures(string uid, string query)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.ContentService}/profile-pictures/uid/{uid}?={query}");
        }

        public static async Task<HttpResponse> GetEquippedProfilePictureByUserId(string uid)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.ContentService}/profile-pictures/uid/{uid}/equipped");
        }

        public static async Task<HttpResponse> PerformTrade(string body)
        {
            return await InternalHttpManager.PostAsync($"{AirshipPlatformUrl.ContentService}/transactions/trade", body);
        }

        public static async Task<HttpResponse> GetEquippedOutfitByUserId(string userId)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.ContentService}/outfits/uid/{userId}/equipped");
        }
    }
}