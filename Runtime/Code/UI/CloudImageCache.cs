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
            //Debug.Log("CloudImage: " + message);
        }

        public static bool RemoveCachedItem(CloudImage image){
            //Remove image from cache
            if(cachedImages.TryGetValue(image.loadedUrl, out var cachedItem)){
                Print("Removing cached image: " + image.gameObject.name + " url: " + image.loadedUrl);
                cachedItem.images.Remove(image);
                if(cachedItem.images.Count <= 0){
                    Print("Fully removing url: " + image.loadedUrl);
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
            string message = "CLOUD IMAGE CACHE --- ";
            int imageCount = 0;
            int nestedCount = 0;
            foreach (var item in cachedImages)
            {
                if(item.Value == null || item.Value.sprite == null){
                    Debug.LogError("CORRUPTED ITEM IN CACHE: " + item.Key);
                    continue;
                }
                message+="\n\nCACHED ITEM: " + item.Key;
                imageCount++;
                foreach (var image in item.Value.images)
                {
                    if(image == null || image.gameObject == null){
                        Debug.LogError("CORRUPTED IMAGE IN CACHE: " + item.Key);
                        continue;
                    }
                    message+="\n-- Image Go: " + image.gameObject?.name;
                    nestedCount++;
                }
            }
            message+= "\n\nTotal Unique Images: " + imageCount + "\nTotal Instances: " + nestedCount + "\nTotal Saved: " + (nestedCount-imageCount);
            Debug.Log(message);
            
            imageCount = 0;
            nestedCount = 0;
            message = "CLOUD IMAGE PENDING --- ";
            foreach (var download in pendingDownloads){
                message+= "\nPENDING ITEM: " + download.Key;
                imageCount++;
                foreach(var item in download.Value){
                    nestedCount++;
                    message+= "\n-- ITEM: " + item.image.gameObject.name;
                }
            }
            message+= "\n\nTotal Pending Images: " + imageCount + "\nTotal Instances: " + nestedCount + "\nTotal Saved: " + (nestedCount-imageCount);
            Debug.Log(message);
        }

        public static IEnumerator QueueDownload(CloudImage cloudImage, Action<bool, string, Sprite> OnDownloadComplete){
            string targetUrl = cloudImage.url;

            //Check to see if image is cached
            if (cachedImages.TryGetValue(targetUrl, out var existingImage)) {
                existingImage.images.Add(cloudImage);
                OnDownloadComplete(true, targetUrl, existingImage.sprite);
                yield break;
            }

            //Check to see if this url is activley loading
            if (pendingDownloads.TryGetValue(targetUrl, out var existingLoads)) {
                existingLoads.Add(new PendingDownload(){image = cloudImage, OnCompleteCallback = OnDownloadComplete});
                yield break;
            }

            Print("Starting a new download: " + targetUrl);
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
            if(pendingDownloads.TryGetValue(url, out var pendingDownload)){
                var cachedItem = new CloudImageCachedItem(){sprite = sprite, images = new List<CloudImage>()};
                //Complete all pending listeners
                foreach (var item in pendingDownload){
                    //Add this item to the cache
                    cachedItem.images.Add(item.image);
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
