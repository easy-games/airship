using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Http.Internal;
using JetBrains.Annotations;
using Proyecto26;
using SFB;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[Serializable]
class CreateImage {
    public string ownerId;
    public string ownerType;
    public string namespace1;
    public string contentType;
    public int contentLength;
}

class CreateImageResponse {
    public string imageId;
    public string url;
}

class UserProfileImagePatch {
    public string profileImageId;
}

[LuauAPI(LuauContext.Protected)]
public class ProfileManager {
    public static Task<bool> UploadProfilePictureYielding([CanBeNull] RawImage previewImage, string ownerId) {
        var task = new TaskCompletionSource<bool>();

        var extensions = new [] {
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg" ),
        };
        StandaloneFileBrowser.OpenFilePanelAsync("Upload Profile Picture", "", extensions, false, async paths => {
            if (paths.Length == 0) {
                task.SetResult(false);
                return;
            }

            string path = paths[0];

            var request = UnityWebRequestTexture.GetTexture("file://" + path);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success) {
                Debug.Log("Load texture failed: " + request.error);
                task.SetResult(false);
                return;
            }

            Texture2D originalTexture = DownloadHandlerTexture.GetContent(request);

            // Crop into a square
            int width = Mathf.Min(originalTexture.width, originalTexture.height);
            var croppedTexture = new Texture2D(width, width);
            var pixels = originalTexture.GetPixels((int)(originalTexture.width / 2f - width / 2f), (int)(originalTexture.height / 2f - width / 2f), width, width);
            croppedTexture.SetPixels(pixels);
            croppedTexture.Apply();

            // Update preview image
            if (previewImage != null) {
                previewImage.texture = croppedTexture;
            }

            // Upload
            Debug.Log("Creating upload url..");
            var bytes = croppedTexture.EncodeToJPG();
            var createImageUrl = await InternalHttpManager.PostAsync("https://content-service-fxy2zritya-uc.a.run.app/images", JsonUtility.ToJson(new CreateImage() {
                ownerId = ownerId,
                ownerType = "USER",
                namespace1 = "profile-pictures",
                contentType = "image/jpeg",
                contentLength = bytes.Length
            }).Replace("namespace1", "namespace")); // dirty hack because we can't serialize "namespace" field ;_;
            if (!createImageUrl.success) {
                Debug.LogError(createImageUrl.error);
                task.SetResult(false);
                return;
            }
            var createImageResponse = JsonUtility.FromJson<CreateImageResponse>(createImageUrl.data);

            Debug.Log("Uploading image to url " + createImageResponse.url);
            {
                var req = UnityWebRequest.Put(createImageResponse.url, bytes);
                req.SetRequestHeader("Content-Type", "image/jpeg");
                await req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success) {
                    Debug.LogError("Upload image failed: " + req.error + " " + req.downloadHandler.error);
                    task.SetResult(false);
                    return;
                }
                Debug.Log("upload response: " + req.downloadHandler.text);
            }

            // Patch user with the uploaded imageId
            {
                var options = new RequestHelper();
                options.Uri = "https://game-coordinator-fxy2zritya-uc.a.run.app/users";
                options.Body = new UserProfileImagePatch() {
                    profileImageId = createImageResponse.imageId
                };
                options.Headers.Add("Authorization", "Bearer " + InternalHttpManager.authToken);
                RestClient.Patch(options).Then((res) => {
                    task.SetResult(true);
                }).Catch((err) => {
                    Debug.LogError("Failed to patch user: " + err);
                    task.SetResult(false);
                });
            }
        });

        return task.Task;
    }

    public static Texture2D ResizeTexture2D(Texture2D originalTexture, int resizedWidth, int resizedHeight)
    {
        RenderTexture renderTexture = new RenderTexture(resizedWidth, resizedHeight, 32);
        RenderTexture.active = renderTexture;
        Graphics.Blit(originalTexture, renderTexture);
        Texture2D resizedTexture = new Texture2D(resizedWidth, resizedHeight);
        resizedTexture.ReadPixels(new Rect(0, 0, resizedWidth, resizedHeight), 0, 0);
        resizedTexture.Apply();
        return resizedTexture;
    }
}