using UnityEngine;
using UnityEngine.Networking;
using System;
using Proyecto26;

public static class UnityWebRequestProxyHelper
{
    private const string ProxyBaseUrl = "http://localhost:8080"; // Update this with your actual proxy base URL

    public static string ProxyAuthCredentials { get; set; }

    public static UnityWebRequest ApplyProxySettings(UnityWebRequest request)
    {
        if (string.IsNullOrEmpty(ProxyAuthCredentials)) {
            Debug.Log("Proxy credentials not set, skipping proxy settings");
            return request;
        }
        Debug.Log("Applying proxy settings");

        var uri = new Uri(request.url);
        request.url = $"{ProxyBaseUrl}/proxy{uri.AbsolutePath}{uri.Query}";

        request.SetRequestHeader("x-easyproxy-host", uri.Host);
        request.SetRequestHeader("x-easyproxy-port", uri.Port.ToString());
        request.SetRequestHeader("x-easyproxy-proto", uri.Scheme);
        request.SetRequestHeader("x-easyproxy-authorization", ProxyAuthCredentials);
        return request;
    }


    public static RequestHelper ApplyProxySettings(RequestHelper request)
    {
        if (string.IsNullOrEmpty(ProxyAuthCredentials)) {
            Debug.Log("Proxy credentials not set, skipping proxy settings");
            return request;
        }
        Debug.Log("Applying proxy settings");

        var uri = new Uri(request.Uri);
        request.Uri = $"{ProxyBaseUrl}/proxy{uri.AbsolutePath}{uri.Query}";


        request.Headers.Add("x-easyproxy-host", uri.Host);
        request.Headers.Add("x-easyproxy-port", uri.Port.ToString());
        request.Headers.Add("x-easyproxy-proto", uri.Scheme);
        request.Headers.Add("x-easyproxy-authorization", ProxyAuthCredentials);
        return request;
    }
}