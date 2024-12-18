using System;
using System.Threading;
using System.Threading.Tasks;
using Proyecto26;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Http.Public {
    [LuauAPI]
    public class HttpManager {
        public static bool loggingEnabled = false;
        // We utilize a semaphore to limit the maximum number of http requests that are made from a server / client at a time.
        // We limit this here in HttpManager since this class is directly exposed to game servers.
        // It is also used indirectly on clients (through InternalHttpManager) in the protected context to call Airship Platform services on behalf of clients
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(RunCore.IsServer() ? 75 : 25);

        public static void SetLoggingEnabled(bool val) {
            loggingEnabled = val;
        }

        public static async Task<HttpResponse> GetAsync(string url, string headers) {
            var options = new RequestHelper {
                Uri = url,
            };
            //Debug.Log("Sending to url: " + url);
            if (headers != "") {
                var split = headers.Split(",");
                foreach (var s in split) {
                    var entry = s.Split("=");
                    if (entry.Length == 2) {
                        options.Headers.Add(entry[0], entry[1]);
                        //Debug.Log("Adding header: " + entry[0] + "= " + entry[1]);
                    }
                }
            }

            try
            {
                // Try to acquire the semaphore, waiting for a maximum of 1 minute
                await _semaphore.WaitAsync(TimeSpan.FromMinutes(1));
            }
            catch (OperationCanceledException)
            {
                return new HttpResponse()
                {
                    success = false,
                    statusCode = 0,
                    error = "Request Throttled: Timeout waiting for request to be scheduled",
                    headers = { }
                };
            }

            using var req = UnityWebRequest.Get(url);

            try
            {
                foreach (var kvp in options.Headers)
                {
                    req.SetRequestHeader(kvp.Key, kvp.Value);
                }
                await UnityWebRequestProxyHelper.ApplyProxySettings(req).SendWebRequest();

                if (req.result == UnityWebRequest.Result.ProtocolError)
                {
                    return new HttpResponse()
                    {
                        success = false,
                        error = req.error,
                        statusCode = (int)req.responseCode,
                        headers = req.GetResponseHeaders()
                    };
                }

                if (req.result == UnityWebRequest.Result.ConnectionError)
                {
                    return new HttpResponse()
                    {
                        success = false,
                        error = req.error,
                        statusCode = (int)req.responseCode,
                        headers = req.GetResponseHeaders()
                    };
                }

                return new HttpResponse()
                {
                    success = true,
                    data = req.downloadHandler.text,
                    statusCode = (int)req.responseCode,
                    headers = req.GetResponseHeaders()
                };
            } finally {
                _semaphore.Release();
            }

            // RestClient.Get(options).Then((res) => {
            //     task.SetResult(new HttpResponse() {
            //         success = true,
            //         data = res.Text,
            //         statusCode = (int)res.StatusCode
            //     });
            // }).Catch((err) => {
            //     var error = err as RequestException;
            //     if (loggingEnabled) {
            //         LogRequestError(url, error);
            //     }
            //     task.SetResult(new HttpResponse() {
            //         success = false,
            //         error = error.Response,
            //         statusCode = (int) error.StatusCode,
            //     });
            // });
        }

        public static Task<HttpResponse> GetAsync(string url) {
            return GetAsync(url, "");
        }

        public static Task<HttpResponse> PostAsync(string url, string data) {
            return PostAsync(url, data, "");
        }

        public static Task<HttpResponse> PostAsync(string url, string data, string headers) {
            var task = new TaskCompletionSource<HttpResponse>();

            RequestHelper options;
            if (string.IsNullOrEmpty(data)) {
                options = new RequestHelper {
                    Uri = url,
                    IgnoreHttpException = true
                };
            } else {
                options = new RequestHelper {
                    Uri = url,
                    BodyString = data,
                    IgnoreHttpException = true
                };
            }
            if (headers != "") {
                var split = headers.Split(",");
                foreach (var s in split) {
                    var entry = s.Split("=");
                    if (entry.Length == 2) {
                        options.Headers.Add(entry[0], entry[1]);
                    }
                }
            }

            // Try to acquire the semaphore, waiting for a maximum of 1 minute
            _semaphore.WaitAsync(TimeSpan.FromMinutes(1)).ContinueWith(waitTask =>
            {
                if (waitTask.IsFaulted || waitTask.IsCanceled)
                {
                    task.SetResult(new HttpResponse
                    {
                        success = false,
                        statusCode = 0,
                        error = "Request Throttled: Timeout waiting for request to be scheduled",
                        headers = {}
                    });
                    return;
                }
                RestClient.Post(UnityWebRequestProxyHelper.ApplyProxySettings(options)).Then((res) => {
                    task.SetResult(new HttpResponse() {
                        success = 200 <= res.StatusCode && res.StatusCode < 300,
                        data = res.Text,
                        statusCode = (int)res.StatusCode,
                        headers = res.Headers
                    });
                }).Catch((err) => {
                    var error = err as RequestException;
                    if (loggingEnabled) {
                        LogRequestError(url, error);
                    }
                    task.SetResult(new HttpResponse() {
                        success = false,
                        statusCode = 0,
                        error = error.Message,
                        headers = {}
                    });
                }).Finally(() => _semaphore.Release());
            });

            return task.Task;
        }

        public static Task<HttpResponse> DeleteAsync(string url) {
            return DeleteAsync(url, "");
        }

        public static Task<HttpResponse> DeleteAsync(string url, string headers) {
            var task = new TaskCompletionSource<HttpResponse>();

            var options = new RequestHelper {
                Uri = url,
                IgnoreHttpException = true
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

            // Try to acquire the semaphore, waiting for a maximum of 1 minute
            _semaphore.WaitAsync(TimeSpan.FromMinutes(1)).ContinueWith(waitTask =>
            {
                if (waitTask.IsFaulted || waitTask.IsCanceled)
                {
                    // Handle the semaphore timeout or exception
                    task.SetResult(new HttpResponse
                    {
                        success = false,
                        statusCode = 0,
                        error = "Request Throttled: Timeout waiting for request to be scheduled",
                        headers = { }
                    });
                    return;
                }
                RestClient.Delete(UnityWebRequestProxyHelper.ApplyProxySettings(options)).Then((res) =>
                {
                    task.SetResult(new HttpResponse()
                    {
                        success = 200 <= res.StatusCode && res.StatusCode < 300,
                        data = res.Text,
                        statusCode = (int)res.StatusCode,
                        headers = res.Headers
                    });
                }).Catch((err) =>
                {
                    var error = err as RequestException;
                    if (loggingEnabled)
                    {
                        LogRequestError(url, error);
                    }
                    task.SetResult(new HttpResponse()
                    {
                        success = false,
                        statusCode = 0,
                        error = error.Message,
                        headers = { }
                    });
                }).Finally(() => _semaphore.Release());
            });

            return task.Task;
        }

        public static Task<HttpResponse> PatchAsync(string url, string data) {
            return PatchAsync(url, data, "");
        }

        public static Task<HttpResponse> PatchAsync(string url, string data, string headers) {
            var task = new TaskCompletionSource<HttpResponse>();

            var options = new RequestHelper {
                Uri = url,
                BodyString = data,
                IgnoreHttpException = true
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

            // Try to acquire the semaphore, waiting for a maximum of 1 minute
            _semaphore.WaitAsync(TimeSpan.FromMinutes(1)).ContinueWith(waitTask =>
            {
                if (waitTask.IsFaulted || waitTask.IsCanceled)
                {
                    // Handle the semaphore timeout or exception
                    task.SetResult(new HttpResponse
                    {
                        success = false,
                        statusCode = 0,
                        error = "Request Throttled: Timeout waiting for request to be scheduled",
                        headers = { }
                    });
                    return;
                }
                RestClient.Patch(UnityWebRequestProxyHelper.ApplyProxySettings(options)).Then((res) =>
                {
                    task.SetResult(new HttpResponse()
                    {
                        success = 200 <= res.StatusCode && res.StatusCode < 300,
                        data = res.Text,
                        statusCode = (int)res.StatusCode,
                        headers = res.Headers
                    });
                }).Catch((err) =>
                {
                    var error = err as RequestException;
                    if (loggingEnabled)
                    {
                        LogRequestError(url, error);
                    }
                    task.SetResult(new HttpResponse()
                    {
                        success = false,
                        statusCode = 0,
                        error = error.Message,
                        headers = { }
                    });
                }).Finally(() => _semaphore.Release());
            });

            return task.Task;
        }

        private static void LogRequestError(string url, RequestException error) {
            Debug.LogError(url + " " + error + " " + error.Message);
        }

        public static Task<HttpResponse> PutAsync(string url, string data) {
            return PutAsync(url, data, "");
        }

        public static Task<HttpResponse> PutAsync(string url, string data, string headers) {
            return PutAsync(new RequestHelper {
                Uri = url,
                BodyString = data,
                IgnoreHttpException = true
            }, headers);
        }

        public static Task<HttpResponse> PutAsync(RequestHelper options, string headers) {
            var task = new TaskCompletionSource<HttpResponse>();
            if (headers != "") {
                var split = headers.Split(",");
                foreach (var s in split) {
                    var entry = s.Split("=");
                    if (entry.Length == 2) {
                        options.Headers.Add(entry[0], entry[1]);
                        //Debug.Log("Adding header: " + entry[0] + "= " + entry[1]);
                    }
                }
            }

            // Try to acquire the semaphore, waiting for a maximum of 1 minute
            _semaphore.WaitAsync(TimeSpan.FromMinutes(1)).ContinueWith(waitTask =>
            {
                if (waitTask.IsFaulted || waitTask.IsCanceled)
                {
                    // Handle the semaphore timeout or exception
                    task.SetResult(new HttpResponse
                    {
                        success = false,
                        statusCode = 0,
                        error = "Request Throttled: Timeout waiting for request to be scheduled",
                        headers = { }
                    });
                    return;
                }
                RestClient.Put(UnityWebRequestProxyHelper.ApplyProxySettings(options)).Then((res) =>
                {
                    task.SetResult(new HttpResponse()
                    {
                        success = 200 <= res.StatusCode && res.StatusCode < 300,
                        data = res.Text,
                        statusCode = (int)res.StatusCode,
                        headers = res.Headers
                    });
                }).Catch((err) =>
                {
                    var error = err as RequestException;
                    if (loggingEnabled)
                    {
                        LogRequestError(options.Uri, error);
                    }
                    task.SetResult(new HttpResponse()
                    {
                        success = false,
                        statusCode = 0,
                        error = error.Message,
                        headers = { }
                    });
                }).Finally(() => _semaphore.Release());
            });

            return task.Task;
        }
    }
}