using System.Threading.Tasks;
using Code.Http;
using Proyecto26;
using RSG;

[LuauAPI]
public class HttpManager {
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