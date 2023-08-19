using System.Threading.Tasks;
using Code.Http;
using Proyecto26;
using RSG;

public class HttpManager : Singleton<HttpManager> {
    public Task<HttpGetResponse> GetAsync(string url) {
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
}