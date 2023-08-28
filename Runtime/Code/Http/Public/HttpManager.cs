using System.Threading.Tasks;
using Code.Http;
using Proyecto26;
using RSG;

[LuauAPI]
public class HttpManager {
    public static Task<HttpGetResponse> GetAsync(string url, string headers) {
        var task = new TaskCompletionSource<HttpGetResponse>();

        var options = new RequestHelper {
            Uri = url,
        };
        if (headers != "") {
            var split = headers.Split(",");
            foreach (var s in split) {
                var entry = s.Split("=");
                if (entry.Length == 2) {
                    options.Headers.Add(entry[0], entry[1]);
                }
            }
        }

        RestClient.Get(options).Then((res) => {
            task.SetResult(new HttpGetResponse() {
                data = res.Text,
                statusCode = res.StatusCode
            });
        }).Catch((err) => {
            task.SetException(err);
        });

        return task.Task;
    }

    public static Task<HttpGetResponse> GetAsync(string url) {
        return GetAsync(url, "");
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