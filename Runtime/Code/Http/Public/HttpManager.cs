using System.Threading.Tasks;
using Proyecto26;
using UnityEngine;

namespace Code.Http.Public {
    [LuauAPI]
    public class HttpManager {
        public static bool loggingEnabled = false;

        public static void SetLoggingEnabled(bool val) {
            loggingEnabled = val;
        }

        public static Task<HttpResponse> GetAsync(string url, string headers) {
            var task = new TaskCompletionSource<HttpResponse>();

            var options = new RequestHelper {
                Uri = url,
            };
            Debug.Log("Sending to url: " + url);
            if (headers != "") {
                var split = headers.Split(",");
                foreach (var s in split) {
                    var entry = s.Split("=");
                    if (entry.Length == 2) {
                        options.Headers.Add(entry[0], entry[1]);
                        Debug.Log("Adding header: " + entry[0] + "= " + entry[1]);
                    }
                }
            }

            RestClient.Get(options).Then((res) => {
                task.SetResult(new HttpResponse() {
                    success = true,
                    data = res.Text,
                    statusCode = (int)res.StatusCode
                });
            }).Catch((err) => {
                var error = err as RequestException;
                if (loggingEnabled) {
                    LogRequestError(url, error);
                }
                task.SetResult(new HttpResponse() {
                    success = false,
                    error = error.Response,
                    statusCode = (int) error.StatusCode,
                });
            });

            return task.Task;
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
                };
            } else {
                options = new RequestHelper {
                    Uri = url,
                    BodyString = data
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

            RestClient.Post(options).Then((res) => {
                task.SetResult(new HttpResponse() {
                    success = true,
                    data = res.Text,
                    statusCode = (int)res.StatusCode
                });
            }).Catch((err) => {
                var error = err as RequestException;
                if (loggingEnabled) {
                    LogRequestError(url, error);
                }
                task.SetResult(new HttpResponse() {
                    success = false,
                    statusCode = (int) error.StatusCode,
                    error = error.Response
                });
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

            RestClient.Delete(options).Then((res) => {
                task.SetResult(new HttpResponse() {
                    success = true,
                    data = res.Text,
                    statusCode = (int)res.StatusCode
                });
            }).Catch((err) => {
                var error = err as RequestException;
                if (loggingEnabled) {
                    LogRequestError(url, error);
                }
                task.SetResult(new HttpResponse() {
                    success = false,
                    statusCode = (int) error.StatusCode,
                    error = error.Response
                });
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

            RestClient.Patch(options).Then((res) => {
                task.SetResult(new HttpResponse() {
                    success = true,
                    data = res.Text,
                    statusCode = (int)res.StatusCode
                });
            }).Catch((err) => {
                var error = err as RequestException;
                if (loggingEnabled) {
                    LogRequestError(url, error);
                }
                task.SetResult(new HttpResponse() {
                    success = false,
                    statusCode = (int) error.StatusCode,
                    error = error.Response
                });
            });

            return task.Task;
        }

        private static void LogRequestError(string url, RequestException error) {
            Debug.LogError(url + " " + error + " " + error.Response);
        }

        public static Task<HttpResponse> PutAsync(string url, string data) {
            return PutAsync(url, data, "");
        }

        public static Task<HttpResponse> PutAsync(string url, string data, string headers) {
            var task = new TaskCompletionSource<HttpResponse>();

            var options = new RequestHelper {
                Uri = url,
                BodyString = data,
            };
            if (headers != "") {
                var split = headers.Split(",");
                foreach (var s in split) {
                    var entry = s.Split("=");
                    if (entry.Length == 2) {
                        options.Headers.Add(entry[0], entry[1]);
                        Debug.Log("Adding header: " + entry[0] + "= " + entry[1]);
                    }
                }
            }

            RestClient.Put(options).Then((res) => {
                task.SetResult(new HttpResponse() {
                    success = true,
                    data = res.Text,
                    statusCode = (int)res.StatusCode
                });
            }).Catch((err) => {
                var error = err as RequestException;
                if (loggingEnabled) {
                    LogRequestError(url, error);
                }
                task.SetResult(new HttpResponse() {
                    success = false,
                    statusCode = (int) error.StatusCode,
                    error = error.Response
                });
            });

            return task.Task;
        }
    }
}