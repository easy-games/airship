using System.Threading.Tasks;
using Code.Http.Public;
using UnityEngine;

namespace Code.Http.Internal {
    [LuauAPI]
    public class InternalHttpManager {
        public static Task<HttpGetResponse> GetAsync(string url) {
            return HttpManager.GetAsync(url, GetHeaders());
        }

        public static Task<HttpGetResponse> PostAsync(string url, string data) {
            return HttpManager.PostAsync(url, data, GetHeaders());
        }

        private static string GetHeaders() {
            var serverBootstrap = GameObject.FindObjectOfType<ServerBootstrap>();
            return $"Authorization=Bearer {serverBootstrap.airshipJWT}";
        }
    }
}