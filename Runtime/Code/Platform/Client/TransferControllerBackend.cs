using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Client
{
    [LuauAPI]
    public class TransferControllerBackend
    {
        public static async Task<HttpResponse> TransferToGame(string body)
        {
            return await InternalHttpManager.PostAsync(
                AirshipPlatformUrl.GameCoordinator + "/transfers/transfer/self",
                body
            );
        }

        public static async Task<HttpResponse> TransferToPartyLeader()
        {
            return await InternalHttpManager.PostAsync(AirshipPlatformUrl.GameCoordinator + "/transfers/transfer/self/party",
                "");
        }
    }
}