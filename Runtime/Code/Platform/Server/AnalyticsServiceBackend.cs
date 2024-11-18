using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;
using UnityEngine;

namespace Code.Platform.Server
{
    public class AnalyticsServiceServerBackend
    {
        public static async Task<HttpResponse> SendServerAnalytics(AirshipAnalyticsServerDto analytics)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.analyticsService}/ingest/gameserver",
                JsonUtility.ToJson(analytics)
            );
        }
    }
}