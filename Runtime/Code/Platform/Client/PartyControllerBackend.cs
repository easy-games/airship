using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Client
{
    [LuauAPI]
    public class PartyControllerBackend
    {
        public static async Task<HttpResponse> GetParty()
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.GameCoordinator}/parties/party/self");
        }
    }
}