using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Server
{
    [LuauAPI]
    public class LeaderboardServiceBackend
    {
        public static async Task<HttpResponse> Update(string leaderboardName, string body)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.dataStoreService}/leaderboards/leaderboard-id/{leaderboardName}/stats", body
            );
        }

        public static async Task<HttpResponse> GetRank(string leaderboardName, string id)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.dataStoreService}/leaderboards/leaderboard-id/{leaderboardName}/id/{id}/ranking");
        }

        public static async Task<HttpResponse> GetRankRange(string leaderboardName, int skip, int limit)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.dataStoreService}/leaderboards/leaderboard-id/{leaderboardName}/rankings?skip={skip}&limit={limit}");
        }
    }
}