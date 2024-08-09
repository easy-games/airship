using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Client
{
    [LuauAPI]
    public class UsersServiceBackend
    {
        public static async Task<HttpResponse> GetUserByUsername(string username)
        {
            return await InternalHttpManager.GetAsync(
                $"{AirshipPlatformUrl.gameCoordinator}/users/user?username={username}");
        }

        public static async Task<HttpResponse> GetUserById(string userId)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.gameCoordinator}/users/uid/{userId}");
        }

        public static async Task<HttpResponse> GetUsersById(string query)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.gameCoordinator}/users?{query}");
        }
    }
}