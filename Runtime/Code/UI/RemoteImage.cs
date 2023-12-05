using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Code.UI {
    public class RemoteImage : MonoBehaviour {
        public string url;
        public Image image;

        private void Start() {
            StartCoroutine(this.DownloadImage(this.url));
        }

        IEnumerator DownloadImage(string url) {
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
            yield return request.SendWebRequest();
            if (request.isNetworkError || request.isHttpError) {
                Debug.LogWarning(request.error);
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            var sprite = Sprite.Create(texture, new Rect(0.0f, 0.0f, texture.width, texture.height), Vector2.one * 0.5f);
            this.image.sprite = sprite;
        }
    }
}