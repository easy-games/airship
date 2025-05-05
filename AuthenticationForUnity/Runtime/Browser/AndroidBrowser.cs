using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi;

namespace Cdm.Authentication.Browser {
    /// <summary>
    /// OAuth 2.0 verification browser that runs a local server and waits for a call with
    /// the authorization verification code.
    /// </summary>
    public class AndroidBrowser : IBrowser {
        private string _prefix;
        
        public AndroidBrowser(string prefix) {
            _prefix = prefix;
        }
        
        private TaskCompletionSource<BrowserResult> _taskCompletionSource;

        /// <summary>
        /// Gets or sets the close page response. This HTML response is shown to the user after redirection is done.
        /// </summary>
        public string closePageResponse { get; set; } = 
            "<html><body><b>DONE!</b><br>(You can close this tab/window now)</body></html>";

        public async Task<BrowserResult> StartAsync(string loginUrl, string redirectUrl, CancellationToken cancellationToken = default) {
            _taskCompletionSource = new TaskCompletionSource<BrowserResult>();

            Debug.Log("Android Browser login starting");

            cancellationToken.Register(() => {
                _taskCompletionSource?.TrySetCanceled();
            });
#if UNITY_ANDROID
            PlayGamesPlatform.DebugLogEnabled = true;
            PlayGamesPlatform.Activate();
            Debug.Log("PlayGamesPlatform activated [6]");
            Debug.Log("Authenticating now...");
            // PlayGamesPlatform.Instance.Authenticate((status) => {
            //     if (status == SignInStatus.Success) {
            //         Debug.Log("Play Games authentication successful");
            //         _taskCompletionSource.SetResult(new BrowserResult(BrowserStatus.Success, ""));
            //     } else {
            //         Debug.LogError($"Play Games authentication failed: {status}");
            //         _taskCompletionSource.SetResult(new BrowserResult(BrowserStatus.UnknownError, ""));
            //     }
            // });
            PlayGamesPlatform.Instance.ManuallyAuthenticate((status) => {
                if (status == SignInStatus.Success) {
                    Debug.Log("Play Games authentication successful. Requesting server side access...");
                    PlayGamesPlatform.Instance.RequestServerSideAccess(false, (code) => {
                        var uri = new Uri(loginUrl);
                        var query = HttpUtility.ParseQueryString(uri.Query);
                        var state = query.Get("state");
                        var redirectUriMock = $"https://airship.gg/android?code={code}&state={state}";
                        Debug.Log($"Got auth code: {redirectUriMock}");
                        _taskCompletionSource.SetResult(new BrowserResult(BrowserStatus.Success, redirectUriMock));
                    });
                } else {
                    Debug.LogError($"Play Games authentication failed: {status}");
                    _taskCompletionSource.SetResult(new BrowserResult(BrowserStatus.UnknownError, ""));
                }
            });
            return await _taskCompletionSource.Task;
#else
            return new BrowserResult(BrowserStatus.UnknownError, "");
#endif

            // using var httpListener = new HttpListener();
            //
            // try {
            //     var prefix = _prefix;
            //     prefix = AddForwardSlashIfNecessary(prefix);
            //     httpListener.Prefixes.Add(prefix);
            //     if (httpListener.IsListening) {
            //         httpListener.Stop();
            //     }
            //     httpListener.Start();
            //     httpListener.BeginGetContext(IncomingHttpRequest, httpListener);
            //     
            //     Application.OpenURL(loginUrl);
            //     
            //     return await _taskCompletionSource.Task;
            // } finally {
            //     httpListener.Stop();
            // }
        }

        private void IncomingHttpRequest(IAsyncResult result) {
            var httpListener = (HttpListener)result.AsyncState;
            var httpContext = httpListener.EndGetContext(result);
            var httpRequest = httpContext.Request;
            
            // Build a response to send an "ok" back to the browser for the user to see.
            var httpResponse = httpContext.Response;
            var buffer = System.Text.Encoding.UTF8.GetBytes(closePageResponse);

            // Send the output to the client browser.
            httpResponse.ContentLength64 = buffer.Length;
            var output = httpResponse.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();

            _taskCompletionSource.SetResult(new BrowserResult(BrowserStatus.Success, httpRequest.Url.ToString()));
        }

        /// <summary>
        /// Prefixes must end in a forward slash ("/")
        /// </summary>
        /// <see href="https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=net-7.0#remarks" />
        private string AddForwardSlashIfNecessary(string url) {
            string forwardSlash = "/";
            if (!url.EndsWith(forwardSlash)) {
                url += forwardSlash;
            }

            return url;
        }
    }
}
