using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;

namespace Code.Platform.Client
{
    [LuauAPI]
    public class FriendsControllerBackend
    {
        public static async Task<HttpResponse> GetFriends()
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.gameCoordinator}/friends/self");
        }

        public static async Task<HttpResponse> IsFriendsWith(string uid)
        {
            return await InternalHttpManager.GetAsync($"{AirshipPlatformUrl.gameCoordinator}/friends/uid/{uid}/status");
        }
    }
}