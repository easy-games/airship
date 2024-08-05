using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Client
{
    [LuauAPI]
    public class MatchmakingControllerBackend
    {
        public static async Task<HttpResponse> GetStatus()
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.GameCoordinator}/matchmaking/queue/status");
        }
    }
}