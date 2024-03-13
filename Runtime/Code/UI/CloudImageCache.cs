using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;


namespace Code.UI {
    public static class CloudImageCache {

        //The main cache of loaded images
        private static Dictionary<string, CloudImageCachedItem> cachedImages = new();
        
        //A list of callbacks to fire once the image is done loading
        private static Dictionary<string, List<PendingDownload>> pendingDownloads = new();

        internal class CloudImageCachedItem{
            public Sprite sprite;
            public List<CloudImage> images;
        }

        internal class PendingDownload{
            public CloudImage image;
            public Action<bool, string, Sprite> OnCompleteCallback;
        }

        private static void Print(string message){
            Debug.Log("CloudImage: " + message);
        }

        public static bool RemoveCachedItem(CloudImage image){
            //Remove image from cache
            if(cachedImages.TryGetValue(image.loadedUrl, out var cachedItem)){
                cachedItem.images.Remove(image);
                if(cachedItem.images.Count <= 0){
                    //Delete this cache since no one is using it anymore
                    cachedImages.Remove(image.loadedUrl);
                }
                return true;
            }

            //Remove image from pending order
            if(pendingDownloads.TryGetValue(image.loadedUrl, out var pendingItem)){
                int foundIndex = -1;
                for (int i = 0; i < pendingItem.Count; i++)
                {
                    PendingDownload item = pendingItem[i];
                    if(item.image = image){
                        foundIndex = i;
                        break;
                    }
                }
                if(foundIndex>= 0){
                    pendingItem.RemoveAt(foundIndex);
                }
            }
            return false;
        }

        public static void CleanseCache(){
            //TODO remove any cached images that don't have any instances anymore
            foreach (var cache in cachedImages){
                List<CloudImage> toDelete = new List<CloudImage>();
                foreach(var image in cache.Value.images){
                    if(image == null || image.loadedUrl != cache.Key){
                        //Image no longer requires this cache
                        toDelete.Add(image);
                    }
                }
            }
        }

        public static void ClearCache(){
            Debug.LogWarning("Fully cleared cloud image cache");
            cachedImages.Clear();
            pendingDownloads.Clear();
            //TODO: Stop all downloading coroutines
        }

        public static void PrintCache(){
            foreach (var item in cachedImages)
            {
                Debug.Log("CACHED ITME: " + item.Key);
                foreach (var image in item.Value.images)
                {
                    Debug.Log("-- Image: " + image.gameObject.name);
                }
            }
        }

        public static IEnumerator QueueDownload(CloudImage cloudImage, Action<bool, string, Sprite> OnDownloadComplete){
            string targetUrl = cloudImage.url;

            //Check to see if image is cached
            if (cachedImages.TryGetValue(targetUrl, out var existingImage)) {
                Print("existing: " + targetUrl + ": " + existingImage.sprite.texture);
                existingImage.images.Add(cloudImage);
                OnDownloadComplete(true, targetUrl, existingImage.sprite);
                yield break;
            }

            //Check to see if this url is activley loading
            if (pendingDownloads.TryGetValue(targetUrl, out var existingLoads)) {
                Print("Adding to pending download: " + targetUrl);
                existingLoads.Add(new PendingDownload(){image = cloudImage, OnCompleteCallback = OnDownloadComplete});
                yield break;
            }

            Print("New Image Download: " + targetUrl);

            //Add to the pending queue
            pendingDownloads.Add(targetUrl, 
                new List<PendingDownload>{
                    new PendingDownload() { image = cloudImage, OnCompleteCallback = OnDownloadComplete }
                });

            //Download the image
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(targetUrl);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError) {
                //Failed Request
                Debug.LogWarning(request.error);
                CompleteDownload(false, targetUrl, null);
                yield break;
            }

            //Convert the texture to a sprite
            var texture = DownloadHandlerTexture.GetContent(request);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            CompleteDownload(true, targetUrl, Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), Vector2.one * 0.5f));


        }

        private static void CompleteDownload(bool successful, string url, Sprite sprite){
            Print("Download complete: " + successful + " for: " + url);
            if(pendingDownloads.TryGetValue(url, out var pendingDownload)){
                var cachedItem = new CloudImageCachedItem(){sprite = sprite, images = new List<CloudImage>()};
                //Complete all pending listeners
                foreach (var item in pendingDownload){
                    //Add this item to the cache
                    cachedItem.images.Add(item.image);
                    Print("Compliting pending item: " + item.image.gameObject.name);
                    item.OnCompleteCallback(successful, url, sprite);
                }
                
                //Save the cache
                if(successful){
                    cachedImages.Add(url, cachedItem);
                }

                //Clear this pending download
                pendingDownloads.Remove(url);
            }
        }
    }

}
