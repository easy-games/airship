using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Server
{
    [LuauAPI]
    public class CacheStoreServiceBackend
    {
        public static async Task<HttpResponse> GetKey(string key, int? expireTimeSec = null)
        {
            var query = expireTimeSec != null ? $"?expiry={expireTimeSec}" : "";
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.dataStoreService}/cache/key/{key}/ttl{query}"
            );
        }

        public static async Task<HttpResponse> SetKey(string key, int expireTimeSec, string body)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.dataStoreService}/cache/key/{key}?expiry={expireTimeSec}",
                body
            );
        }

        public static async Task<HttpResponse> SetKeyTTL(string key, int expireTimeSec)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.dataStoreService}/cache/key/{key}/ttl?expiry={expireTimeSec}");
        }
    }
}