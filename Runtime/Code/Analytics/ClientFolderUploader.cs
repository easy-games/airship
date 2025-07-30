using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Code.Http.Internal;
using Code.Http.Public;
using Code.Platform.Shared;
using Proyecto26;
using UnityEngine;

namespace Code.Analytics {
    [Serializable]
    public class PostArtifactResponse {
        public string id;
        public string url;
    }

    [Serializable]
    public class RequestBody {
        public string type;
        public string name;
        public string contentType;
        public long contentLength;
    }

    public class ClientFolderUploader : MonoBehaviour {

        // Start in the past otherwise they can't do this for the first 10 seconds since time starts at 0.
        private float timeOfLastUpload = -10;

        public void ButtonClick() {
            if (Time.time - timeOfLastUpload < 10) {
                Debug.LogWarning("[ClientFolderUploader] Upload already in progress or too soon after last upload");
                return;
            }
            timeOfLastUpload = Time.time;

            _ = UploadAsync();
        }

        private async Task UploadAsync() {
            try {
                await Upload();
                Debug.Log("[ClientFolderUploader] Upload completed successfully");
            } catch (Exception ex) {
                Debug.LogError($"[ClientFolderUploader] Upload failed: {ex.Message}");
            }
        }

        public async Task Upload() {
            try {
                var path = Path.GetDirectoryName(Application.consoleLogPath);

                var zipPath = Path.Combine(Application.temporaryCachePath, "logs.zip");
                if (File.Exists(zipPath)) {
                    File.Delete(zipPath);
                }

                var playerLogFile = Path.Combine(path, "Player.log");
                var playerPrevLogFile = Path.Combine(path, "Player-prev.log");
                var editorLogFile = Path.Combine(path, "Editor.log");
                var editorPrevLogFile = Path.Combine(path, "Editor-prev.log");

                Debug.Log("[ClientFolderUploader] Creating zip archive");
                try {
                    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create)) {
                        var fileExists = false;
                        if (File.Exists(playerLogFile)) {
                            fileExists = true;
                            Debug.Log("[ClientFolderUploader] Adding Player.log");
                            archive.CreateEntryFromFile(playerLogFile, "Player.log");
                        }

                        if (File.Exists(playerPrevLogFile)) {
                            fileExists = true;
                            Debug.Log("[ClientFolderUploader] Adding Player-prev.log");
                            archive.CreateEntryFromFile(playerPrevLogFile, "Player-prev.log");
                        }

                        if (File.Exists(editorLogFile)) {
                            fileExists = true;
                            Debug.Log("[ClientFolderUploader] Adding Editor.log");
                            archive.CreateEntryFromFile(editorLogFile, "Editor.log");
                        }

                        if (File.Exists(editorPrevLogFile)) {
                            fileExists = true;
                            Debug.Log("[ClientFolderUploader] Adding Editor-prev.log");
                            archive.CreateEntryFromFile(editorPrevLogFile, "Editor-prev.log");
                        }

                        if (!fileExists) {
                            return;
                        }
                    }
                } catch (Exception ex) {
                    Debug.LogError($"[ClientFolderUploader] Zip creation failed: {ex.Message}");
                    throw;
                }

                var contentType = "application/zip";
                var contentLength = new FileInfo(zipPath).Length;

                var body = new RequestBody {
                    type = "CLIENT_DEBUG_ARCHIVE",
                    name = "PlayerLogs.zip",
                    contentType = contentType,
                    contentLength = contentLength,
                };

                Debug.Log("[ClientFolderUploader] Creating artifact");
                var url = $"{AirshipPlatformUrl.contentService}/artifacts/platform/signed-url";

                Http.HttpResponse res;
                if (String.IsNullOrEmpty(InternalHttpManager.authToken)) {
                    res = await HttpManager.PostAsync(url, JsonUtility.ToJson(body));
                } else {
                    res = await InternalHttpManager.PostAsync(url, JsonUtility.ToJson(body));
                }

                if (res.statusCode < 200 || res.statusCode >= 300) {
                    Debug.LogError($"[ClientFolderUploader] Error response from server: {res.statusCode} - {res.error}");
                    throw new Exception($"Failed to get signed URL: {res.error}");
                }

                var response = JsonUtility.FromJson<PostArtifactResponse>(res.data);

                Debug.Log("[ClientFolderUploader] Uploading file");
                await HttpManager.PutAsync(new RequestHelper() {
                    Uri = response.url,
                    BodyRaw = File.ReadAllBytes(zipPath),
                    Headers = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "Content-Type", contentType },
                    },
                }, "");

                File.Delete(zipPath);
            } catch (Exception ex) {
                Debug.LogError($"[ClientFolderUploader] Error during upload process: {ex.Message}");
                throw;
            }
        }
    }
}