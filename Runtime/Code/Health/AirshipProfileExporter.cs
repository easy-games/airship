using System;
using System.IO;
using System.Threading.Tasks;
using Airship.DevConsole;
using Code.Http.Internal;
using Code.Platform.Shared;
using JetBrains.Annotations;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

struct SignedUrlRequest {
    public string type;
    public string name;
    public string contentType;
    public long contentLength;
}


struct SignedUrlResponse
{
    public string id;
    public string url;
}

struct ArtifactDownloadResponse {
    public string url;
}

struct ServerProfileRequest {
    
}

namespace Code.Health
{
    public struct ClientProfileUploadRequest : NetworkMessage
    {
        public string logLocation;
        public float duration;
        public long contentSize;
    }
    
    public struct ClientProfileUploadResponse : NetworkMessage
    {
        public string logLocation;
        public string id;
        public string url;
        public float duration;
    }
    
    public struct StartServerProfileMessage : NetworkMessage {
        public int DurationSecs;
    }

    public struct ServerProfileCompleteMessage : NetworkMessage
    {
        public string gameId;
        public string artifactId;
    } 
    
    public class AirshipProfileExporter : MonoBehaviour {
        private static AirshipProfileExporter _instance;
        public static AirshipProfileExporter Instance => _instance;
        private bool lastProfilerEnabled = false;

        private void Awake() {
            if (_instance != null && _instance != this) {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start() {
            if (RunCore.IsServer()) {
                NetworkServer.RegisterHandler<StartServerProfileMessage>(OnStartProfilingMessage, false);
                NetworkServer.RegisterHandler<ClientProfileUploadRequest>(OnClientUploadRequest, false);
            }
            if (RunCore.IsClient()) {
                NetworkClient.RegisterHandler<ServerProfileCompleteMessage>(OnServerProfileCompleteMessage);
                NetworkClient.RegisterHandler<ClientProfileUploadResponse>(OnClientUploadResponse);
            }

            DevConsole.AddCommand(Command.Create<string, int>(
                "profile", 
                "",
                "Starts and uploads a profile. Once complete the download link will be printed.", 
                    Parameter.Create("Context", "Options: Server | Client"),
                Parameter.Create("Duration", "Duration of profile in seconds (max 5s)"),
                (context, d) => {
                    if (d is < 0 or > 5) {
                        Debug.LogError("You can only profile for a max of 5s.");
                        return;
                    }

                    if (context.ToLower() == "client") {
                        StartProfiling(d, null);
                    } else if (context.ToLower() == "server") {
                        Debug.Log("Starting a server profile, view server console to monitor progress.");
                        NetworkClient.Send(new StartServerProfileMessage { DurationSecs = d });
                    }
                }));
        }

        private void Update() {
            if (Profiler.enabled != lastProfilerEnabled) {
                lastProfilerEnabled = Profiler.enabled;
                LuauPlugin.LuauSetProfilerEnabled(Profiler.enabled);
            }
        }

        public async void OnClientUploadRequest(NetworkConnectionToClient sender, ClientProfileUploadRequest msg)
        {
            var urlData = await this.GetSignedUrl(msg.duration, msg.contentSize);
            sender.Send(new ClientProfileUploadResponse
            {
                logLocation = msg.logLocation,
                duration = msg.duration,
                url = urlData.url,
                id = urlData.id
            });
        }

        public async void OnClientUploadResponse(ClientProfileUploadResponse msg)
        {
            this.Upload(new SignedUrlResponse()
            {
                id = msg.id,
                url = msg.url
            }, msg.logLocation, msg.duration, null);
        }

        public void OnStartProfilingMessage(NetworkConnectionToClient sender, StartServerProfileMessage msg) {
            // TODO Validate sender is dev
            StartProfiling(msg.DurationSecs, sender);
        }

        public async void OnServerProfileCompleteMessage(ServerProfileCompleteMessage msg)
        {
            var downloadUrl =
                await InternalHttpManager.GetAsync(
                    $"{AirshipPlatformUrl.contentService}/artifacts/artifact-id/{msg.artifactId}");
            if (!downloadUrl.success)
            {
                Debug.Log($"Profile Uploaded:\n<a href=\"https://create.airship.gg/dashboard/organization/game/artifacts?activeGame={msg.gameId}\">https://create.airship.gg/dashboard/organization/game/artifacts?activeGame={msg.gameId}</a>\n(copied to your clipboard)");
                return;
            }

            var data = JsonUtility.FromJson<ArtifactDownloadResponse>(downloadUrl.data);
            
            Debug.Log($"Profile uploaded:\n<a href=\"{data.url}\">{data.url}</a>\n(copied to your clipboard)");
            GUIUtility.systemCopyBuffer = data.url;
        }

        public void StartProfiling(int durationSecs, [CanBeNull] NetworkConnectionToClient profileInitiator) {
            // TODO check that sender is game dev
            if (Profiler.enabled) {
                Debug.LogWarning("Profiler is already running.");
                return;
            }

            var date = DateTime.Now.ToString("MM-dd-yyyy h.mm.ss");
            var logPath = Path.Combine(Application.persistentDataPath, $"Profile-{date}.raw");
            if (File.Exists(logPath)) File.WriteAllText(logPath, "");

            Profiler.logFile = logPath;
            Profiler.enableBinaryLog = true;
            
            Debug.Log($"Starting profiler for {durationSecs} seconds.");
            Profiler.enabled = true;
            StopProfilingAfterDelay(logPath, durationSecs, profileInitiator);
        }

        private async void StopProfilingAfterDelay(string logPath, float durationSecs, [CanBeNull] NetworkConnectionToClient profileInitiator) {
            await Task.Delay((int)(durationSecs * 1000));
            Profiler.enabled = false;
            var info = new FileInfo(logPath);
            
            Debug.Log($"Profiling completed. Retrieving upload URL...");
            if (RunCore.IsClient())
            {
                NetworkClient.Send(new ClientProfileUploadRequest
                {
                    contentSize = info.Length,
                    logLocation = logPath
                });
            }
            else
            {
                var urlData = await this.GetSignedUrl(durationSecs, info.Length);
                Upload(urlData, logPath, durationSecs, profileInitiator);
            }
        }

        private async Task<SignedUrlResponse> GetSignedUrl(float duration, long length)
        {
            var body = new SignedUrlRequest()
            {
                type = "MICRO_PROFILE",
                name = RunCore.IsClient() ? $"Client Profile ({duration}s)" : $"Server Profile ({duration}s)",
                contentType = "application/octet-stream",
                contentLength = length
            };
            var response = await InternalHttpManager.PostAsync($"{AirshipPlatformUrl.contentService}/artifacts/signed-url", JsonUtility.ToJson(body));
            if (!response.success)
            {
                throw new Exception("Unable to get upload URL for profile.");
            }
            return JsonUtility.FromJson<SignedUrlResponse>(response.data);
        }

        private async void Upload(SignedUrlResponse urlData, string logPath, float durationSecs, [CanBeNull] NetworkConnectionToClient profileInitiator)
        {
            Debug.Log("Uploading profile to backend...");
            var uploadFilePath = logPath;
            var fileData = await File.ReadAllBytesAsync(uploadFilePath);
            File.Delete(uploadFilePath);
            
            var form = new WWWForm();
            form.AddBinaryData("file",  fileData, Path.GetFileName(uploadFilePath));
            using var www = UnityWebRequest.Post(urlData.url, form);
            MonitorUploadProgress(www).Start();
            await UnityWebRequestProxyHelper.ApplyProxySettings(www).SendWebRequest();
            
            Debug.Log($"Upload response: {www.downloadHandler.text}");
            
            if (profileInitiator != null && profileInitiator.isReady) {
                profileInitiator.Send(new ServerProfileCompleteMessage { artifactId = urlData.id });
            }
            Debug.Log($"Profile uploaded.");
        }

        private async Task MonitorUploadProgress(UnityWebRequest req) {
            Debug.Log("Starting upload...");
            var elapsed = 0.0d;
            var timeSinceLastLog = 0.0d;
            try {
                while (!req.isDone) {
                    await Awaitable.NextFrameAsync();

                    elapsed += Time.unscaledDeltaTime;
                    timeSinceLastLog += Time.unscaledDeltaTime;
                    if (timeSinceLastLog > 5) {
                        timeSinceLastLog = 0;
                        // Debug.Log(req.uploadProgress);
                        var percentFormatted = String.Format("{0:#0.00}", req.uploadProgress * 100);
                        var timeFormatted = String.Format("{0:#.00}", elapsed);
                        Debug.Log($"Profile upload @ {percentFormatted}% ({timeFormatted}s)");
                    }
                }
            } catch (Exception ex) {
                if (ex is not NullReferenceException && ex is not ObjectDisposedException) throw;
            }
        }
    }
}