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
    record PostArtifactResponse
    {
        public string id { get; set; }
        public string url { get; set; }
    }

    public class ClientFolderUploader : MonoBehaviour
    {
        public async Task ButtonClick()
        {
            Debug.Log("[ClientFolderUploader] Starting log upload process...");

            try
            {
                var path = Path.GetDirectoryName(Application.consoleLogPath);
                Debug.Log($"[ClientFolderUploader] Log directory path: {path}");

                var zipPath = Path.Combine(path, "logs.zip");
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

                Debug.Log("[ClientFolderUploader] Creating zip archive of log files...");
                ZipFile.CreateFromDirectory(path, zipPath);

                var contentType = "application/zip";
                var contentLength = new FileInfo(zipPath).Length;
                Debug.Log($"[ClientFolderUploader] Zip file created successfully (size: {contentLength} bytes)");

                var body = new
                {
                    type = "CLIENT_DEBUG_ARCHIVE",
                    name = "PlayerLogs.zip",
                    contentType,
                    contentLength,
                };

                Debug.Log($"[ClientFolderUploader] Request body: {JsonUtility.ToJson(body)}");
                Debug.Log($"[ClientFolderUploader] Auth token present: {!String.IsNullOrEmpty(InternalHttpManager.authToken)}");


                var url = $"{AirshipPlatformUrl.contentService}/artifacts/platform/signed-url";

                PostArtifactResponse response;
                if (!String.IsNullOrEmpty(InternalHttpManager.authToken))
                {
                    Debug.Log($"[ClientFolderUploader] Making authenticated request to: {url}");
                    var res = await HttpManager.PostAsync(url, JsonUtility.ToJson(body));
                    Debug.Log($"[ClientFolderUploader] Signed URL response received (length: {res.data?.Length ?? 0})");
                    response = JsonUtility.FromJson<PostArtifactResponse>(res.data);
                }
                else
                {
                    Debug.Log($"[ClientFolderUploader] Making internal request to: {url}");
                    var res = await InternalHttpManager.PostAsync(url, JsonUtility.ToJson(body));
                    Debug.Log($"[ClientFolderUploader] Signed URL response received (length: {res.data?.Length ?? 0})");
                    response = JsonUtility.FromJson<PostArtifactResponse>(res.data);
                }

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
