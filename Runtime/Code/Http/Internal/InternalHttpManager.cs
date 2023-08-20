using System.Threading.Tasks;
using Code.Http;
using Proyecto26;
using UnityEngine;

[LuauAPI]
public class InternalHttpManager {
    public static Task<HttpGetResponse> GetAsync(string url) {
        var task = new TaskCompletionSource<HttpGetResponse>();

        Debug.Log("get.1");
        RestClient.Get(url).Then((res) => {
            Debug.Log("get.1.a");
            task.SetResult(new HttpGetResponse() {
                data = res.Text,
                statusCode = res.StatusCode
            });
        }).Catch((err) => {
            Debug.Log("get.1.b: " + err);
            task.SetException(err);
        });

        return task.Task;
    }

    public static Task<HttpGetResponse> PostAsync(string url, string data) {
        var task = new TaskCompletionSource<HttpGetResponse>();

        RestClient.Post(url, data).Then((res) => {
            task.SetResult(new HttpGetResponse() {
                data = res.Text,
                statusCode = res.StatusCode
            });
        }).Catch((err) => {
            task.SetException(err);
        });

        return task.Task;
    }
}