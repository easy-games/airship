using System.Threading.Tasks;
using Code.Http.Public;
using UnityEngine;

namespace Code.Http.Internal {

    [LuauAPI]
    public class InternalHttpManager {
        private static string authToken = "";

        public static Task<HttpGetResponse> GetAsync(string url) {
            return HttpManager.GetAsync(url, GetHeaders());
        }

        public static Task<HttpGetResponse> PostAsync(string url, string data) {
            return HttpManager.PostAsync(url, data, GetHeaders());
        }

        public static Task<HttpGetResponse> PutAsync(string url, string data) {
            return HttpManager.PutAsync(url, data, GetHeaders());
        }

        public static Task<HttpGetResponse> PatchAsync(string url, string data) {
            return HttpManager.PatchAsync(url, data, GetHeaders());
        }

        public static Task<HttpGetResponse> DeleteAsync(string url, string data) {
            return HttpManager.DeleteAsync(url, data, GetHeaders());
        }

        private static string GetHeaders() {
            if (RunCore.IsServer()) {
                var serverBootstrap = GameObject.FindObjectOfType<ServerBootstrap>();
                return $"Authorization=Bearer {serverBootstrap.airshipJWT}";
            } else {
                return $"Authorization=Bearer {authToken}";
            }
        }

        public static void SetAuthToken(string authToken) {
            InternalHttpManager.authToken = authToken;
        }
    }
}