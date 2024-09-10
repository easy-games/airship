using System;
using System.IO;
using System.Threading.Tasks;
using Airship.DevConsole;
using Mirror;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

struct FileIOCreateRequest {
    public string file;
    public int maxDownloads;
    public bool autoDelete;
}


struct FileIOCreateResponse {
    public bool success;
    public int status;

    public string key;
    public string name;
    public string link;
}

struct ServerProfileRequest {
    
}

namespace Code.Health
{
    public enum AirshipProfileContext {
        Server,
        Client,
    }
    
    public struct StartProfilingMessage : NetworkMessage {
        public int DurationSecs;
    }

    
    public class AirshipProfileExporter : MonoBehaviour {
        public static string fileIOKey = "LGF5JOI.F0YF0ET-N6PMPS6-NWGVZEW-9984TYP"; // TODO throw out this key (it lives in git).
        private static AirshipProfileExporter _instance;
        public static AirshipProfileExporter Instance => _instance;

        private void Awake() {
            if (_instance != null && _instance != this) {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start() {
            if (NetworkServer.active) {
                NetworkServer.RegisterHandler<StartProfilingMessage>(OnStartProfilingMessage, false);
            }

            DevConsole.AddCommand(Command.Create<AirshipProfileContext, int>(
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

                    if (context == AirshipProfileContext.Client) {
                        StartProfiling(d);
                    } else {
                        NetworkClient.Send(new StartProfilingMessage { DurationSecs = d });
                    }
                }));
        }

        public void OnStartProfilingMessage(NetworkConnectionToClient sender, StartProfilingMessage msg) {
            // TODO Validate sender is dev
            StartProfiling(msg.DurationSecs);
        }

        public void StartProfiling(int durationSecs) {
            // TODO check that sender is game dev
            if (Profiler.enabled) {
                Debug.LogWarning("Profiler is already running.");
                return;
            }

            var date = DateTime.Now.ToString("MM-dd-yyyy h.mm.ss");
            var logPath = Path.Combine(Application.persistentDataPath, $"Profile-{date}.raw");
            if (File.Exists(logPath)) File.WriteAllText(logPath, "");

            ProfilerDriver.ClearAllFrames();
            Profiler.logFile = logPath;
            Profiler.enableBinaryLog = true;
            
            Debug.Log($"Starting profiler for {durationSecs} seconds.");
            ProfilerDriver.enabled = true;
            Profiler.enabled = true;
            StopProfilingAfterDelay(logPath, durationSecs);
        }

        private async void StopProfilingAfterDelay(string logPath, float durationSecs) {
            await Task.Delay((int)(durationSecs * 1000));
            Profiler.enabled = false;
            Debug.Log($"Profiling completed. Uploading file...");
            Upload(logPath, durationSecs);
        }

        private async void Upload(string logPath, float durationSecs) {
            var uploadFilePath = logPath;
            
            var form = new WWWForm();
            form.AddField("title", $"Server Profile ({durationSecs}s)");
            form.AddField("description", "Learn more: https://docs.airship.gg/optimization/server-profiler");
            form.AddField("autoDelete", "false");
            form.AddField("maxDownloads", "100");
            form.AddField("expires", "1w");
            var fileData = await File.ReadAllBytesAsync(uploadFilePath);
            File.Delete(uploadFilePath);
            
            form.AddBinaryData("file",  fileData, Path.GetFileName(uploadFilePath));
            using var www = UnityWebRequest.Post("https://file.io/?expires=1w", form);
            www.SetRequestHeader("Authorization", "Bearer " + fileIOKey);
            MonitorUploadProgress(www);
            await www.SendWebRequest();
            
            var resp = JsonUtility.FromJson<FileIOCreateResponse>(www.downloadHandler.text);
            Debug.Log($"Profile uploaded: <a href=\"{resp.link}\">{resp.link}</a>");
        }

        private async void MonitorUploadProgress(UnityWebRequest req) {
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