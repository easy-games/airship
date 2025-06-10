using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Airship.DevConsole;
using Code.Http.Internal;
using Code.Platform.Shared;
using Code.UI;
using JetBrains.Annotations;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

struct SignedUrlRequest {
    public string type;
    public string name;
    [CanBeNull]
    public string note;
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
        public string fileName;
        public float duration;
        public long contentSize;
    }
    
    public struct ClientProfileUploadResponse : NetworkMessage
    {
        public string logLocation;
        public string id;
        public string url;
    }
    
    public struct StartServerProfileMessage : NetworkMessage {
        public int DurationSecs;
        public bool CallstacksEnabled;
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

            DevConsole.AddCommand(Command.Create<string, int, bool>(
                "profile", 
                "",
                "Starts and uploads a profile. Once complete the download link will be printed.", 
                    Parameter.Create("Context", "Options: Server | Client"),
                Parameter.Create("Duration", "Duration of profile in seconds (max 5s)"),
                Parameter.Create("Callstacks", "Enable callstacks for profile (this is laggy)"), 
                (context, d, callstacks) => {
                    if (d is < 0 or > 5) {
                        Debug.LogError("You can only profile for a max of 5s.");
                        return;
                    }

                    if (context.Equals("client", StringComparison.OrdinalIgnoreCase)) {
                        if (!Debug.isDebugBuild) {
                            Debug.Log(
                                "Unable to capture profile log because debug mode is not enabled. Use the development build branch on Steam to enable debug mode.");
                            return;
                        }
                        StartProfiling(d, null, callstacks);
                    } else if (context.Equals("server", StringComparison.OrdinalIgnoreCase)) {
                        Debug.Log("Starting a server profile, view server console to monitor progress.");
                        NetworkClient.Send(new StartServerProfileMessage { DurationSecs = d, CallstacksEnabled = callstacks });
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
            var urlData = await this.GetSignedUrl(msg.fileName, msg.duration, msg.contentSize);
            sender.Send(new ClientProfileUploadResponse
            {
                logLocation = msg.logLocation,
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
            }, msg.logLocation, null);
        }

        public void OnStartProfilingMessage(NetworkConnectionToClient sender, StartServerProfileMessage msg, bool enableCallstacks) {
            // TODO Validate sender is dev
            StartProfiling(msg.DurationSecs, sender, enableCallstacks);
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

        public void StartProfiling(int durationSecs, [CanBeNull] NetworkConnectionToClient profileInitiator, bool enableCallstacks) {
            // TODO check that sender is game dev
            // if (Profiler.enabled) {
            //     Debug.LogWarning("Profiler is already running.");
            //     return;
            // }

            var date = DateTime.Now.ToString("MM-dd-yyyy h.mm.ss");
            var fileName = RunCore.IsClient() ?  $"Client-Profile-{date}.raw" :  $"Server-Profile-{date}.raw";
            if (!Directory.Exists(Path.Combine(Application.persistentDataPath, "ClientProfiles"))) {
                Directory.CreateDirectory(Path.Combine(Application.persistentDataPath, "ClientProfiles"));
            }
            var logPath = Path.Combine(Application.persistentDataPath, "ClientProfiles", fileName);
            if (File.Exists(logPath)) File.WriteAllText(logPath, "");

            Profiler.logFile = logPath;
            Profiler.enableBinaryLog = true;
            
            Debug.Log($"Starting profiler for {durationSecs} seconds.");
            Profiler.enabled = true;
            Profiler.enableAllocationCallstacks = enableCallstacks;
            StopProfilingAfterDelay(logPath, fileName, durationSecs, profileInitiator);
        }

        private async void StopProfilingAfterDelay(string logPath, string fileName, float durationSecs, [CanBeNull] NetworkConnectionToClient profileInitiator) {
            await Task.Delay((int)(durationSecs * 1000));
            Profiler.enabled = false;
            Profiler.enableAllocationCallstacks = false;
            var info = new FileInfo(logPath);

            Debug.Log($"Profiling completed. Retrieving upload URL...");
            if (RunCore.IsClient()) {
                var profilesFolder = Path.Combine(Application.persistentDataPath, "ClientProfiles");
                try {
                    CrossPlatformFileAPI.OpenPath(profilesFolder);
                } catch (Exception e) {
                    Debug.LogError(e);
                }

                NetworkClient.Send(new ClientProfileUploadRequest {
                    contentSize = info.Length,
                    logLocation = logPath,
                    fileName = fileName
                });
            } else {
                var urlData = await this.GetSignedUrl(fileName, durationSecs, info.Length);
                Upload(urlData, logPath, profileInitiator);
            }
        }

        private async Task<SignedUrlResponse> GetSignedUrl(string fileName, float duration, long length)
        {
            var body = new SignedUrlRequest()
            {
                type = "MICRO_PROFILE",
                name = fileName,
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

        private async void Upload(SignedUrlResponse urlData, string logPath, [CanBeNull] NetworkConnectionToClient profileInitiator)
        {
            Debug.Log("Uploading profile to backend...");
            var uploadFilePath = logPath;
            var fileData = await File.ReadAllBytesAsync(uploadFilePath);
            // File.Delete(uploadFilePath);
            
            using var www = UnityWebRequest.Put(urlData.url, fileData);
            MonitorUploadProgress(www);
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