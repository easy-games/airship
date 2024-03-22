using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI {
    [LuauAPI]
    public class CloudImage : MonoBehaviour {

        public string loadedUrl { get; private set;} = "";

        public string url;
        public Image image;
        public bool downloadOnStart = true;
        public bool releaseImageOnDisable = false;
        
        private string downloadingUrl = "";

        /**
         * Params: success
         */
        public event Action<object> OnFinishedLoading;
        
        private void Awake() {
            if (!image) {
                image = GetComponent<Image>();
            }
        }

        private void OnDisable() {
            if(releaseImageOnDisable){
                ReleaseImage();
            }
            downloadingUrl = "";
        }

        private void OnDestroy(){
            ReleaseImage();
        }

        private void Start() {
            if (downloadOnStart) {
                StartDownload();
            }
        }

        public void StartDownload() {
            if (!isActiveAndEnabled) {
                Debug.LogWarning("Tried to start downloading CloudImage on gameobject that is disabled.");
                return;
            }
            DownloadImage(url);
        }

        private void DownloadImage(string url) {
            if(string.IsNullOrEmpty(url)){
                return;
            }

            //Don't load the same url twice
            if(loadedUrl == url){
                //We have already loaded this image 
                OnFinishedLoading?.Invoke(true);
                return;
            }

            if(downloadingUrl == url){
                Debug.LogWarning("Attempting to double download an image. Are you calling Download and have it set to download on start?");
                return;
            }

            downloadingUrl = url;

            //If we are switching to a new url we need to release our previous one from the cache
            ReleaseImage();

            StartCoroutine(CloudImageCache.QueueDownload(this, (bool successful, string downloadedUrl, Sprite sprite)=>{
                if(successful){
                    //Apply the image
                    this.loadedUrl = downloadedUrl;
                    //TODO: Should we switch to RawImage so we don't have to create a sprite each download???
                    this.image.sprite = sprite;
                }
                downloadingUrl = "";
                OnFinishedLoading?.Invoke(successful);
            }));

        }

        public void ReleaseImage(bool notifyCache = true){
            if(string.IsNullOrEmpty(loadedUrl)){
                return;
            }
            if(notifyCache){
             CloudImageCache.RemoveCachedItem(this, loadedUrl);
            }
            loadedUrl = "";
            this.image.sprite = null;
        }
        
        public static void CleanseCache(){
            CloudImageCache.CleanseCache();
        }

        public static void ClearCache(){
            CloudImageCache.ClearCache();
        }

        public static void PrintCache(){
            CloudImageCache.PrintCache();
        }
    }
}