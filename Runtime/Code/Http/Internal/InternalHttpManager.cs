using System;
using System.IO;
using System.Threading.Tasks;
using Code.Http.Public;
using Proyecto26;
using UnityEditor;
using UnityEngine;

namespace Code.Http.Internal {

    [LuauAPI]
    public static class InternalHttpManager {
        public static string editorUserId;
        public static string editorAuthToken = "";
        public static string authToken = "";
        private static TaskCompletionSource<bool> authTokenSetTaskCompletionSource = new();

        static InternalHttpManager() {
            if (RunCore.IsServer()) {
                authTokenSetTaskCompletionSource.TrySetResult(true);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void OnLoad() {
            if (RunCore.IsServer()) {
                authTokenSetTaskCompletionSource.TrySetResult(true);
            }
        }

        public static Task<HttpResponse> GetAsync(string url) {
#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                return HttpManager.GetAsync(url, GetHeaders());
            }
#endif

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
            #if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return HttpManager.PostAsync(url, data, GetHeaders());
            #endif
            return authTokenSetTaskCompletionSource.Task.ContinueWith(_ => 
                HttpManager.PostAsync(url, data, GetHeaders()), TaskScheduler.FromCurrentSynchronizationContext()
            ).Unwrap();
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
            var bytes = File.ReadAllBytes(filePath);
            var stringValue = Convert.ToBase64String(bytes); //bytes.ToString();//
            var extension = Path.GetExtension(filePath);
            var fileType = "";
            if(extension == ".png"){
                fileType = "image/png";
            }else if (extension == ".jpg"){
                fileType = "image/jpeg";
            }
            return HttpManager.PutAsync( new RequestHelper(){
                Uri = url,
                BodyRaw = bytes
            }, GetHeaders($"Content-Type={fileType}"));//,Content-Length={bytes.Length}"));
        }

        public static Task<HttpResponse> PatchAsync(string url, string data) {
            return HttpManager.PatchAsync(url, data, GetHeaders());
        }

        public static Task<HttpResponse> DeleteAsync(string url) {
            return HttpManager.DeleteAsync(url, GetHeaders());
        }

        private static string GetHeaders(string additionalHeaders = "") {
            var internalHeader = RunCore.IsInternal() ? "x-airship-ignore-rate-limit=true":"";
            #if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                return $"Authorization=Bearer {editorAuthToken},{additionalHeaders},{internalHeader}";
            }
            #endif
            if (RunCore.IsClient()) {
                return $"Authorization=Bearer {authToken},{additionalHeaders},{internalHeader}";
            } else {
                var serverBootstrap = GameObject.FindAnyObjectByType<ServerBootstrap>();
                return $"Authorization=Bearer {serverBootstrap.airshipJWT},{additionalHeaders},{internalHeader}";
            }
        }

        public static void SetAuthToken(string authToken) {
            InternalHttpManager.authToken = authToken;
            authTokenSetTaskCompletionSource.TrySetResult(true);
        }
        
        public static void SetEditorAuthToken(string authToken) {
            InternalHttpManager.editorAuthToken = authToken;
            // authTokenSetTaskCompletionSource.TrySetResult(true);
        }
    }
}