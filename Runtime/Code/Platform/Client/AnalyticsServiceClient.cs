using System.Threading.Tasks;
using Code.Http;
using Code.Http.Internal;
using Code.Platform.Shared;
using UnityEngine;

namespace Code.Platform.Client
{
    public class AnalyticsServiceClient
    {
        public static async Task<HttpResponse> SendClientAnalytics(AirshipAnalyticsClientDto analytics)
        {
            return await InternalHttpManager.PostAsync(
                $"{AirshipPlatformUrl.analyticsService}/ingest/client",
                JsonUtility.ToJson(analytics)
            );
        }
    }
}