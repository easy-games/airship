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
            if (!isActiveAndEnabled) return;
            DownloadImage(url);
        }

        private void DownloadImage(string url) {
            //Don't load the same url twice
            if(loadedUrl == url){
                //We have already loaded this image 
                OnFinishedLoading?.Invoke(true);
                return;
            }

            //If we are switching to a new url we need to release our previous one from the cache
            ReleaseImage();

            StartCoroutine(CloudImageCache.QueueDownload(this, (bool successful, string downloadedUrl, Sprite sprite)=>{
                if(successful){
                    //Apply the image
                    this.loadedUrl = downloadedUrl;
                    //TODO: Should we switch to RawImage so we don't have to create a sprite each download???
                    this.image.sprite = sprite;
                }
                OnFinishedLoading?.Invoke(successful);
            }));
        }

        private void ReleaseImage(){
            if(string.IsNullOrEmpty(this.loadedUrl)){
                return;
            }
            CloudImageCache.RemoveCachedItem(this);
            this.loadedUrl = "";
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