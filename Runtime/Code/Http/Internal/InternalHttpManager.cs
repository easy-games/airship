using System.Threading.Tasks;
using Code.Http;
using Proyecto26;
using UnityEngine;

[LuauAPI]
public class InternalHttpManager {
    public static Task<HttpGetResponse> GetAsync(string url) {
        var task = new TaskCompletionSource<HttpGetResponse>();

        RestClient.Get(url).Then((res) => {
            task.SetResult(new HttpGetResponse() {
                data = res.Text,
                statusCode = res.StatusCode
            });
        }).Catch((err) => {
            task.SetException(err);
        });

        return task.Task;
    }

    public static Task<HttpGetResponse> PostAsync(string url, string data) {
        var task = new TaskCompletionSource<HttpGetResponse>();

        Debug.Log("post.1");
        RestClient.Post(url, data).Then((res) => {
            Debug.Log("post.1.a");
            task.SetResult(new HttpGetResponse() {
                data = res.Text,
                statusCode = res.StatusCode
            });
            Debug.Log("post.1.b");
        }).Catch((err) => {
            task.SetException(err);
        });

        Debug.Log("post.2");
        return task.Task;
    }
}