using System;
using System.Collections;
using System.Collections.Generic;
using Cdm.Authentication.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


namespace Code.UI {
    public static class CloudImageCache {
        private const int maxCachedItems = 100;
        public static bool AutoClearEmptyCaches = false;


        //The main cache of loaded images
        private static Dictionary<string, CloudImageCachedItem> cachedImages = new();
        
        //A list of callbacks to fire once the image is done loading
        private static Dictionary<string, List<PendingDownload>> pendingDownloads = new();
        private static Queue<string> orderedUrls = new Queue<string>();//An ordererd queue so we can limit how many items are loaded at a time

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

        private static void AddCachedItem(CloudImage item){
            orderedUrls.Enqueue(item.url);
        }

        public static bool RemoveCachedItem(CloudImage image, string url){
            bool didRemove = false;
            //Remove image from cache
            if(cachedImages.TryGetValue(url, out var cachedItem)){
                Print("Removing cached image: " + image.gameObject.name + " url: " + url);
                cachedItem.images.Remove(image);
                if(AutoClearEmptyCaches && cachedItem.images.Count <= 0){
                    Print("Fully removing url: " + url);
                    //Delete this cache since no one is using it anymore
                    cachedImages.Remove(url);
                }
                didRemove = true;
            }

            //Remove image from pending order
            if(pendingDownloads.TryGetValue(url, out var pendingItem)){
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
                didRemove = true;
            }
            return didRemove;
        }

        /// <summary>
        /// Clear any caches that don't have references anymore
        /// </summary>
        public static void CleanseCache(){
            List<string> cacheToDelete = new List<string>();
            foreach (var cache in cachedImages){
                List<CloudImage> imagesToDelete = new List<CloudImage>();
                foreach(var image in cache.Value.images){
                    if(image == null || image.loadedUrl != cache.Key){
                        //Image no longer requires this cache
                        imagesToDelete.Add(image);
                    }
                }
                //Delete images
                foreach(var image in imagesToDelete){
                    cache.Value.images.Remove(image);
                }

                //See if we still need this cache
                if(cache.Value.images.Count <= 0){
                    cacheToDelete.Add(cache.Key);
                }
            }

            //Delete unused caches
            foreach(var key in cacheToDelete){
                cachedImages.Remove(key);

            }
        }

        public static void ClearCache(){
            Print("Fully cleared cloud image cache");
            foreach(var cache in cachedImages){
                Print("Clearing image: " + cache.Key);
                foreach(var image in cache.Value.images){
                    if(image != null){
                        Print("Releasing image: " + image.name);
                        image.ReleaseImage(false);
                    }
                }
                GameObject.Destroy(cache.Value.sprite);
            }
            cachedImages.Clear();
            pendingDownloads.Clear();
            //TODO: Stop all downloading coroutines
        }        

        public static IEnumerator QueueDownload(CloudImage cloudImage, Action<bool, string, Sprite> OnDownloadComplete, bool hideErrors){
            if(!cloudImage){
                Debug.LogWarning("Trying to download cloud image that doesn't exist");
                yield break;
            }

            Print("Starting Download: " + cloudImage.gameObject.name);
            string targetUrl = cloudImage.url;

            //Check to see if image is cached
            if (cachedImages.TryGetValue(targetUrl, out var existingImage)) {
                Print("Cache for image already exists");
                existingImage.images.Add(cloudImage);
                OnDownloadComplete(true, targetUrl, existingImage.sprite);
                yield break;
            }

            //Check to see if this url is activley loading
            if (pendingDownloads.TryGetValue(targetUrl, out var existingLoads)) {
                Print("URL is already downloading");
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
            UnityWebRequest request = UnityWebRequestProxyHelper.ApplyProxySettings(UnityWebRequestTexture.GetTexture(targetUrl));
            Print("Sending web request");
            yield return request.SendProxyRequest();
            Print("Web request sent");
            if (request.result != UnityWebRequest.Result.Success) {
                //Failed Request
                if (!hideErrors) {
                    Debug.LogError($"[CloudImage] Download error ({targetUrl}): " + request.error + " " + request.downloadHandler.error);
                }
                CompleteDownload(false, targetUrl, null);
                yield break;
            }

            Print("Creating Sprite");
            //Convert the texture to a sprite
            var texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null) {
                throw new Exception("Downloaded texture was null from url: " + targetUrl);
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            CompleteDownload(true, targetUrl, Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), Vector2.one * 0.5f));
        }

        private static void CompleteDownload(bool successful, string url, Sprite sprite){
            Print("Trying to complete Download: " + url);
            if(pendingDownloads.TryGetValue(url, out var pendingDownload)){
                Print("Completing Download: " + url);
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
                    //Only preserve a certain amount of cached items
                    if(cachedImages.Count > maxCachedItems){
                        string removeUrl = "";
                        //Have to loop in case a cached image has been released but not removed from queue
                        while(orderedUrls.Count > 0){
                            removeUrl = orderedUrls.Dequeue();
                            if(cachedImages.ContainsKey(removeUrl)){
                                cachedImages.Remove(removeUrl);
                            }
                        }
                    }
                }

                //Clear this pending download
                pendingDownloads.Remove(url);
            }
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
                    if(item == null || item.image == null){
                        Debug.LogError("Image has been destroyed but is still in pending list");
                        continue;
                    }
                    message+= "\n-- ITEM: " + item.image.gameObject.name;
                }
            }
            message+= "\n\nTotal Pending Images: " + imageCount + "\nTotal Instances: " + nestedCount + "\nTotal Saved: " + (nestedCount-imageCount);
            Debug.Log(message);
        }
    }

}
