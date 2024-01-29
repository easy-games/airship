using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Code.UI {
    public class RemoteImage : MonoBehaviour {
        public string url;
        public Image image;
        public bool downloadOnStart = true;

        /**
         * Params: (bool) success
         */
        public event Action<object> OnFinishedLoading;

        private void Start() {
            var type = typeof(RemoteImage);
            if (this.downloadOnStart) {
                StartCoroutine(this.DownloadImage(this.url));
            }
        }

        public void StartDownload() {
            if (!isActiveAndEnabled) return;
            StartCoroutine(this.DownloadImage(this.url));
        }

        private IEnumerator DownloadImage(string url) {
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError) {
                Debug.LogWarning(request.error);
                OnFinishedLoading?.Invoke(false);
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            var sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), Vector2.one * 0.5f);
            this.image.sprite = sprite;
            OnFinishedLoading?.Invoke(true);
        }
    }
}