using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Server
{
    public class UsersServiceBackend
    {
        public static async Task<HttpResponse> GetUserByUsername(string username)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.gameCoordinator}/users/user?username={username}");
        }
    }
}