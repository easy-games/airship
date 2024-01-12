using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Client
{
    [LuauAPI]
    public class UsersControllerBackend
    {
        public static async Task<HttpResponse> GetUser(string username)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipUrl.GameCoordinator}/users/user?discriminatedUsername={username}");
        }
    }
}