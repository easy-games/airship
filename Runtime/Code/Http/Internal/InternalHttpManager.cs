using System.Threading.Tasks;
using Code.Http;
using Proyecto26;
using UnityEngine;

[LuauAPI]
public class InternalHttpManager {
    public static Task<HttpGetResponse> GetAsync(string url) {
        return HttpManager.GetAsync(url);
    }

    public static Task<HttpGetResponse> PostAsync(string url, string data) {
        return HttpManager.PostAsync(url, data);
    }
}