using UnityEngine;
using UnityEngine.Networking;
using System;
using Proyecto26;

public static class UnityWebRequestProxyHelper
{
    private const string ProxyBaseUrl = "http://cluster-gameserver-proxy:8080";

    public static string ProxyAuthCredentials { get; set; }

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
}