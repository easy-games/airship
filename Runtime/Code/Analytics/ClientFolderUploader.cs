using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Code.Http.Internal;
using Code.Http.Public;
using Code.Platform.Shared;
using Proyecto26;
using UnityEngine;

namespace Code.Analytics
{
    [Serializable]
    public class PostArtifactResponse
    {
        public string id { get; set; }
        public string url { get; set; }
    }

    [Serializable]
    public class RequestBody
    {
        public string type;
        public string name;
        public string contentType;
        public long contentLength;
    }

    public class ClientFolderUploader : MonoBehaviour
    {
        public void ButtonClick()
        {
            Debug.Log("[ClientFolderUploader] Button clicked, starting upload...");
            _ = UploadAsync();
        }

        private async Task UploadAsync()
        {
            try
            {
                await Upload();
                Debug.Log("[ClientFolderUploader] Upload completed successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientFolderUploader] Upload failed: {ex.Message}");
            }
        }

        public async Task Upload()
        {
            Debug.Log("[ClientFolderUploader] Starting log upload process...");

            try
            {
                var path = Path.GetDirectoryName(Application.consoleLogPath);
                Debug.Log($"[ClientFolderUploader] Log directory path: {path}");

                var zipPath = Path.Combine(Application.temporaryCachePath, "logs.zip");
                if (File.Exists(zipPath))
                {
                    Debug.Log("[ClientFolderUploader] Existing logs.zip found, deleting...");
                    File.Delete(zipPath);
                }

                var playerLogFile = Path.Combine(path, "Player.log");
                var playerPrevLogFile = Path.Combine(path, "Player-prev.log");

                Debug.Log($"[ClientFolderUploader] Checking for log files:");
                Debug.Log($"[ClientFolderUploader] - Player.log: {playerLogFile} (exists: {File.Exists(playerLogFile)})");
                Debug.Log($"[ClientFolderUploader] - Player-prev.log: {playerPrevLogFile} (exists: {File.Exists(playerPrevLogFile)})");

                Debug.Log("[ClientFolderUploader] Creating zip archive of specific log files...");

                try
                {
                    using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        var fileExists = false;
                        if (File.Exists(playerLogFile))
                        {
                            fileExists = true;
                            Debug.Log("[ClientFolderUploader] Adding Player.log to archive...");
                            archive.CreateEntryFromFile(playerLogFile, "Player.log");
                        }

                        if (File.Exists(playerPrevLogFile))
                        {
                            fileExists = true;
                            Debug.Log("[ClientFolderUploader] Adding Player-prev.log to archive...");
                            archive.CreateEntryFromFile(playerPrevLogFile, "Player-prev.log");
                        }

                        if (!fileExists)
                        {
                            Debug.Log("[ClientFolderUploader] No log files found to send. Exiting.");
                            return;
                        }
                    }
                    Debug.Log("[ClientFolderUploader] Zip creation completed successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ClientFolderUploader] Zip creation failed: {ex.Message}");
                    throw;
                }

                var contentType = "application/zip";
                var contentLength = new FileInfo(zipPath).Length;
                Debug.Log($"[ClientFolderUploader] Zip file created successfully (size: {contentLength} bytes)");

                var body = new RequestBody
                {
                    type = "CLIENT_DEBUG_ARCHIVE",
                    name = "PlayerLogs.zip",
                    contentType = contentType,
                    contentLength = contentLength,
                };

                Debug.Log($"[ClientFolderUploader] Request body: {JsonUtility.ToJson(body)}");
                Debug.Log($"[ClientFolderUploader] Auth token present: {!String.IsNullOrEmpty(InternalHttpManager.authToken)}");

                var url = $"{AirshipPlatformUrl.contentService}/artifacts/platform/signed-url";

                Http.HttpResponse res;
                if (!String.IsNullOrEmpty(InternalHttpManager.authToken))
                {
                    Debug.Log($"[ClientFolderUploader] Making authenticated request to: {url}");
                    res = await HttpManager.PostAsync(url, JsonUtility.ToJson(body));

                }
                else
                {
                    Debug.Log($"[ClientFolderUploader] Making internal request to: {url}");
                    res = await InternalHttpManager.PostAsync(url, JsonUtility.ToJson(body));
                }

                if (res.statusCode < 200 || res.statusCode >= 300)
                {
                    Debug.LogError($"[ClientFolderUploader] Error response from server: {res.statusCode} - {res.error}");
                    throw new Exception($"Failed to get signed URL: {res.error}");
                }

                Debug.Log($"[ClientFolderUploader] Signed URL response received (length: {res.data?.Length ?? 0})");
                Debug.Log($"[ClientFolderUploader] Signed URL response: {(res.data?.Length > 100 ? res.data.Substring(0, 1000) + "..." : (res.data ?? "null"))}");

                var response = JsonUtility.FromJson<PostArtifactResponse>(res.data);

                Debug.Log($"[ClientFolderUploader] Upload URL received: {(response?.url?.Length > 100 ? response.url.Substring(0, 100) + "..." : response?.url ?? "null")}");
                Debug.Log($"[ClientFolderUploader] Artifact ID: {response?.id}");

                Debug.Log("[ClientFolderUploader] Starting file upload...");
                await HttpManager.PutAsync(new RequestHelper()
                {
                    Uri = response.url,
                    BodyRaw = File.ReadAllBytes(zipPath),
                    Headers = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "Content-Type", contentType },
                        { "Content-Length", contentLength.ToString() },
                    },
                }, null);

                Debug.Log("[ClientFolderUploader] Upload completed successfully!");

                Debug.Log("[ClientFolderUploader] Deleting zip file after upload...");
                File.Delete(zipPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientFolderUploader] Error during upload process: {ex.Message}");
                Debug.LogError($"[ClientFolderUploader] Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}