using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Client
{
    public class AirshipInventoryControllerBackend
    {
        public static async Task<HttpResponse> HasItem()
        {
            return await InternalHttpManager.GetAsync($"{AirshipUrl.ContentService}/");
        }

        public static async Task<HttpResponse> HasAccessory()
        {
            return await InternalHttpManager.GetAsync($"{AirshipUrl.ContentService}/");
        }

        public static async Task<HttpResponse> HasProfilePicture()
        {
            return await InternalHttpManager.GetAsync($"{AirshipUrl.ContentService}/");
        }

        public static async Task<HttpResponse> GetItems()
        {
            return await InternalHttpManager.GetAsync($"{AirshipUrl.ContentService}/");
        }

        public static async Task<HttpResponse> GetAccessories()
        {
            return await InternalHttpManager.GetAsync($"{AirshipUrl.ContentService}/");
        }

        public static async Task<HttpResponse> GetProfilePictures()
        {
            return await InternalHttpManager.GetAsync($"{AirshipUrl.ContentService}/");
        }

        public static async Task<HttpResponse> GetEquippedOutfit()
        {
            return await InternalHttpManager.GetAsync($"{AirshipUrl.ContentService}/");
        }
    }
}