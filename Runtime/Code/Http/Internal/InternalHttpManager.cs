using System.Threading.Tasks;
using Code.Http.Public;
using UnityEngine;

namespace Code.Http.Internal {

    [LuauAPI]
    public class InternalHttpManager {
        private static string authToken = "";

        public static Task<HttpResponse> GetAsync(string url) {
            return HttpManager.GetAsync(url, GetHeaders());
        }

        public static Task<HttpResponse> PostAsync(string url, string data) {
            return HttpManager.PostAsync(url, data, GetHeaders());
        }

        public static Task<HttpResponse> PostAsync(string url) {
            return HttpManager.PostAsync(url, null, GetHeaders());
        }
        
        public static Task<HttpResponse> PutAsync(string url, string data) {
            return HttpManager.PutAsync(url, data, GetHeaders());
        }

        public static Task<HttpResponse> PatchAsync(string url, string data) {
            return HttpManager.PatchAsync(url, data, GetHeaders());
        }

        public static Task<HttpResponse> DeleteAsync(string url) {
            return HttpManager.DeleteAsync(url, GetHeaders());
        }

        private static string GetHeaders() {
            if (RunCore.IsClient()) {
                return $"Authorization=Bearer {authToken}";
            } else {
                var serverBootstrap = GameObject.FindAnyObjectByType<ServerBootstrap>();
                return $"Authorization=Bearer {serverBootstrap.airshipJWT}";
            }
        }

        public static void SetAuthToken(string authToken) {
            InternalHttpManager.authToken = authToken;
        }
    }
}