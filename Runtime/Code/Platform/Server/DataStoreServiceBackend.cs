using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Server
{
    [LuauAPI]
    public class DataStoreServiceBackend
    {
        public static async Task<HttpResponse> GetKey(string key)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.DataStoreService}/data/key/{key}");
        }

        public static async Task<HttpResponse> SetKey(string key, string body)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.DataStoreService}/data/key/{key}",
                body
            );
        }

        public static async Task<HttpResponse> DeleteKey(string key)
        {
            return await InternalHttpManager.DeleteAsync($"{AirshipPlatformUrl.DataStoreService}/data/key/{key}");
        }
    }
}