using System.Threading.Tasks;
using Code.Http;
using Proyecto26;
using RSG;
using UnityEngine;

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
                success = true,
                data = res.Text,
                statusCode = (int)res.StatusCode
            });
        }).Catch((err) => {
            var error = err as RequestException;
            Debug.LogError(error);
            Debug.LogError("Response: " + error.Response);
            task.SetResult(new HttpGetResponse() {
                success = false,
                error = error.Response,
                statusCode = (int) error.StatusCode,
            });
        });

        return task.Task;
    }

    public static Task<HttpGetResponse> GetAsync(string url) {
        return GetAsync(url, "");
    }

    public static Task<HttpGetResponse> PostAsync(string url, string data) {
        return PostAsync(url, data, "");
    }

    public static Task<HttpGetResponse> PostAsync(string url, string data, string headers) {
        var task = new TaskCompletionSource<HttpGetResponse>();

        var options = new RequestHelper {
            Uri = url,
            BodyString = data
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

        RestClient.Post(options).Then((res) => {
            task.SetResult(new HttpGetResponse() {
                success = true,
                data = res.Text,
                statusCode = (int)res.StatusCode
            });
        }).Catch((err) => {
            var error = err as RequestException;
            Debug.LogError(error);
            Debug.LogError("Response: " + error.Response);
            task.SetResult(new HttpGetResponse() {
                success = false,
                statusCode = (int) error.StatusCode,
                error = error.Response
            });
        });

        return task.Task;
    }
}