using UnityEngine;
using UnityEngine.Networking;
using System;
using Proyecto26;
using System.Threading.Tasks;
using UnityEngine.InputSystem.EnhancedTouch;
using System.Net;
using System.Threading;
using System.Reflection;
using RSG;

public class AsyncWaitNotify
{
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0); // Initially locked

    // Wait method (awaitable)
    public async Task WaitAsync(TimeSpan timeout)
    {
        await _semaphore.WaitAsync(timeout);
    }

    // Notify one waiting task
    public void NotifyOne()
    {
        _semaphore.Release(1); // Release one waiter
    }

    public bool AreThereWaiters()
    {
        return _semaphore.CurrentCount > 0;
    }
}

public static class UnityWebRequestProxyHelper
{
    private const string ProxyBaseUrl = "http://cluster-gameserver-proxy:8080";

    public static string ProxyAuthCredentials { get; set; }

    private static AsyncWaitNotify ProxyRequestWaiter = new AsyncWaitNotify();

    public static UnityWebRequest ApplyProxySettings(UnityWebRequest request)
    {
        if (string.IsNullOrEmpty(ProxyAuthCredentials)) {
            return request;
        }

        var uri = new Uri(request.url);
        request.url = $"{ProxyBaseUrl}/proxy{uri.AbsolutePath}{uri.Query}";


        request.SetRequestHeader("x-easyproxy-host", uri.Host);
        if (uri.Port.ToString() is var port && !string.IsNullOrEmpty(port)) request.SetRequestHeader("x-easyproxy-port", port);
        if (!string.IsNullOrEmpty(uri.Scheme)) request.SetRequestHeader("x-easyproxy-proto", uri.Scheme);
        if (!string.IsNullOrEmpty(ProxyAuthCredentials)) request.SetRequestHeader("x-easyproxy-authorization", ProxyAuthCredentials);
        return request;
    }

    public static RequestHelper ApplyProxySettings(RequestHelper request)
    {
        if (string.IsNullOrEmpty(ProxyAuthCredentials)) {
            return request;
        }

        var uri = new Uri(request.Uri);
        request.Uri = $"{ProxyBaseUrl}/proxy{uri.AbsolutePath}{uri.Query}";


        request.Headers.Add("x-easyproxy-host", uri.Host);
        if (uri.Port.ToString() is var port && !string.IsNullOrEmpty(port)) request.Headers.Add("x-easyproxy-port", port);
        if (!string.IsNullOrEmpty(uri.Scheme)) request.Headers.Add("x-easyproxy-proto", uri.Scheme);
        if (!string.IsNullOrEmpty(ProxyAuthCredentials)) request.Headers.Add("x-easyproxy-authorization", ProxyAuthCredentials);
        return request;
    }

    public static async Task SendProxyRequest(this UnityWebRequest request)
    {
        var initialRun = true;
        do {
            if (initialRun && ProxyRequestWaiter.AreThereWaiters()) {
                await ProxyRequestWaiter.WaitAsync();
                initialRun = false;
                continue;
            }
            await request.SendWebRequest();
            if (request.responseCode != (int)HttpStatusCode.TooManyRequests) return;
            var easyProxyRateLimitHeader = request.GetResponseHeader("x-easyproxy-ratelimit");
            if (string.IsNullOrEmpty(easyProxyRateLimitHeader)) return;
            await ProxyRequestWaiter.WaitAsync();
        } while(true);
    }


    public static async Task ApplyAndSendProxyRequest(this UnityWebRequest request)
    {
        ApplyProxySettings(request);
        await request.SendProxyRequest();
    }
    public static RSG.IPromise<ResponseHelper> HandleProxyResponse(Func<IPromise<ResponseHelper>> makeRequest)
    {
        return new RSG.Promise<ResponseHelper>(async (resolve, reject) => {
            if (ProxyRequestWaiter.AreThereWaiters()) {
                await ProxyRequestWaiter.WaitAsync();
            }
            Action<Exception> rejectHandler = null;

            rejectHandler = async (err) => {
                var error = err as RequestException;
                if (error == null || error.StatusCode != (int)HttpStatusCode.TooManyRequests) reject(err);
                var easyProxyRateLimitHeader = error.unityWebRequest.GetResponseHeader("x-easyproxy-ratelimit");
                if (string.IsNullOrEmpty(easyProxyRateLimitHeader)) reject(err);
                await ProxyRequestWaiter.WaitAsync();
                makeRequest().Then(resolve).Catch(rejectHandler);
            };

            makeRequest().Then(resolve).Catch(rejectHandler);
        });
    }

    public static RSG.IPromise<ResponseHelper> ProxyPost(RequestHelper request) {
        return HandleProxyResponse(() => RestClient.Post(ApplyProxySettings(request)));
    }

    public static RSG.IPromise<ResponseHelper> ProxyPut(RequestHelper request) {
        return HandleProxyResponse(() => RestClient.Put(ApplyProxySettings(request)));
    }

    public static RSG.IPromise<ResponseHelper> ProxyPatch(RequestHelper request) {
        return HandleProxyResponse(() => RestClient.Patch(ApplyProxySettings(request)));
    }

    public static RSG.IPromise<ResponseHelper> ProxyDelete(RequestHelper request) {
        return HandleProxyResponse(() => RestClient.Delete(ApplyProxySettings(request)));
    }
}