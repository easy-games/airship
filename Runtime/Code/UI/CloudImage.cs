using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Code.UI {
    [LuauAPI]
    public class CloudImage : MonoBehaviour {
        public string url;
        public Image image;
        public bool downloadOnStart = true;

        private void Awake() {
            if (!this.image) {
                this.image = GetComponent<Image>();
            }
        }

        /**
         * Params: (bool) success
         */
        public event Action<object> OnFinishedLoading;

        private void Start() {
            var type = typeof(CloudImage);
            if (this.downloadOnStart) {
                StartCoroutine(this.DownloadImage(this.url));
            }
        }

        public void StartDownload() {
            if (!isActiveAndEnabled) return;
            StartCoroutine(this.DownloadImage(this.url));
        }

        private IEnumerator DownloadImage(string url) {
            // if (CloudBridge.Instance.textures.TryGetValue(url, out var existing)) {
            //     print("existing: " + url + ": " + existing);
            //     var existingSprite = Sprite.Create(existing, new Rect(0.0f, 0.0f, existing.width, existing.height), Vector2.one * 0.5f);
            //     this.image.sprite = existingSprite;
            //     OnFinishedLoading?.Invoke(true);
            //     yield break;
            // }
            // print("new: " + url);

            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError) {
                Debug.LogWarning(request.error);
                OnFinishedLoading?.Invoke(false);
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            CloudBridge.Instance.textures.TryAdd(url, texture);
            var sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), Vector2.one * 0.5f);
            this.image.sprite = sprite;
            OnFinishedLoading?.Invoke(true);
        }
    }
}