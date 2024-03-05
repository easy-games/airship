using System;
using System.IO;
using System.Threading.Tasks;
using Code.Http.Public;
using Codice.Client.BaseCommands;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Code.Http.Internal {

    [LuauAPI]
    public class InternalHttpManager {
        public static string authToken = "";
        private static TaskCompletionSource<bool> authTokenSetTaskCompletionSource = new();

        static InternalHttpManager() {
            if (RunCore.IsServer()) {
                authTokenSetTaskCompletionSource.TrySetResult(true);
            }
        }

        public static Task<HttpResponse> GetAsync(string url) {
            return authTokenSetTaskCompletionSource.Task.ContinueWith(_ => {
                return HttpManager.GetAsync(url, GetHeaders());
            }, TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
        }
        public static Task<HttpResponse> GetAsyncWithHeaders(string url, string headers) {
            return authTokenSetTaskCompletionSource.Task.ContinueWith(_ => {
                return HttpManager.GetAsync(url, GetHeaders(headers));
            }, TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
        }

        public static Task<HttpResponse> PostAsync(string url, string data) {
            return authTokenSetTaskCompletionSource.Task.ContinueWith(_ => HttpManager.PostAsync(url, data, GetHeaders()), TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
            // return HttpManager.PostAsync(url, data, GetHeaders());
        }

        public static Task<HttpResponse> PostAsync(string url) {
            return authTokenSetTaskCompletionSource.Task.ContinueWith(_ => HttpManager.PostAsync(url, null, GetHeaders()), TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
        }
        
        public static Task<HttpResponse> PutAsync(string url, string data) {
            return HttpManager.PutAsync(url, data, GetHeaders());
        }

        public static Task<HttpResponse> PutImageAsync(string url, string filePath){
            if(!File.Exists(filePath)){
                Debug.LogError("Image Upload unable to find file: " + filePath);
                return null;
            }
            Debug.Log("Uploading image: " + filePath);
            var bytes = File.ReadAllBytes(filePath);
            var stringValue = Convert.ToBase64String(bytes); //bytes.ToString();//
            var extension = Path.GetExtension(filePath);
            var fileType = "";
            if(extension == ".png"){
                fileType = "Image/png";
            }else if (extension == ".jpg"){
                fileType = "Image/jpeg";
            }
            Debug.Log("Extension type: " + fileType);
            return HttpManager.PutAsync(url, stringValue, 
                GetHeaders($"Content-Type={fileType}"));//,Content-Length={bytes.Length}"));
        }

        public static Task<HttpResponse> PatchAsync(string url, string data) {
            return HttpManager.PatchAsync(url, data, GetHeaders());
        }

        public static Task<HttpResponse> DeleteAsync(string url) {
            return HttpManager.DeleteAsync(url, GetHeaders());
        }

        private static string GetHeaders(string additionalHeaders = "") {
            if (RunCore.IsClient()) {
                return $"Authorization=Bearer {authToken},{additionalHeaders}";
            } else {
                var serverBootstrap = GameObject.FindAnyObjectByType<ServerBootstrap>();
                return $"Authorization=Bearer {serverBootstrap.airshipJWT},{additionalHeaders}";
            }
        }

        public static void SetAuthToken(string authToken) {
            InternalHttpManager.authToken = authToken;
            authTokenSetTaskCompletionSource.TrySetResult(true);
        }
    }
}