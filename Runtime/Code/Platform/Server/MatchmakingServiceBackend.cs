using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Server
{
    [LuauAPI]
    public class MatchmakingServiceBackend
    {
        public static async Task<HttpResponse> GetMatchmakingRegions()
        {
            return await InternalHttpManager.GetAsync(AirshipPlatformUrl.GameCoordinator + "/matchmaking/regions");
        }

        public static async Task<HttpResponse> JoinPartyToQueue(string partyId, string body)
        {
            return await InternalHttpManager.PostAsync(
                AirshipPlatformUrl.GameCoordinator + $"/matchmaking/party-id/{partyId}/queue", body);
        }

        public static async Task<HttpResponse> RemovePartyFromQueue(string partyId)
        {
            return await InternalHttpManager.PostAsync(
                AirshipPlatformUrl.GameCoordinator + $"/matchmaking/party-id/{partyId}/dequeue", "");
        }
    }
}