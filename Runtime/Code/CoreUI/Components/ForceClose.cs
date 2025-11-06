using System;
using System.Collections;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using TMPro;
using UnityEngine;

namespace Code.CoreUI.Components {
    public class ForceClose : MonoBehaviour {
        const float HideTweenDuration = 0.2f;
        
        public RectTransform container;
        public RectTransform fill;
        public TMP_Text text;
        [NonSerialized] private float holdTimeRequired = 2.2f;

        [NonSerialized] private float holdTime = 0f;
        [NonSerialized] private ITween tween;
        [NonSerialized] private Coroutine hidingCoroutine;
        [NonSerialized] private bool isShown = false;
        [NonSerialized] private bool disconnected = false;
        [NonSerialized] private GameObject containerGameObject;

        private void Awake() {
            containerGameObject = container.gameObject;
            
            containerGameObject.SetActive(false);
        }

        private void Update() {
            if (Input.GetKey(KeyCode.Escape)) {
                if (!this.isShown && this.holdTime >= 0.5f) {
                    this.isShown = true;
                    this.containerGameObject.SetActive(true);
                    if (this.tween != null) {
                        this.tween.Cancel();
                        this.tween = null;
                    }
                    if (hidingCoroutine != null) {
                        StopCoroutine(hidingCoroutine);
                        hidingCoroutine = null;
                    }

                    this.tween = NativeTween.AnchoredPositionY(this.container, -10f, 0.18f).SetEaseBounceOut();
                }
                this.holdTime += Time.deltaTime;

                float fillAmount = Math.Min(this.holdTime / this.holdTimeRequired, 1);
                this.fill.anchorMax = new Vector2(fillAmount, this.fill.anchorMax.y);

                if (fillAmount >= 1 && !this.disconnected) {
                    this.disconnected = true;
                    this.text.text = "Disconnecting...";
                    TransferManager.Instance.Disconnect();
                }

                return;
            }

            this.holdTime = 0f;

            // not holding escape
            if (this.isShown) {
                this.isShown = false;
                this.tween = NativeTween.AnchoredPositionY(this.container, 71f, HideTweenDuration).SetEaseQuadOut();
                hidingCoroutine = StartCoroutine(DelayHide(HideTweenDuration));
            }
        }

        private IEnumerator DelayHide(float delay) {
            yield return new WaitForSeconds(delay);

            if (!this.isShown) {
                if (containerGameObject != null) containerGameObject.SetActive(false);
                hidingCoroutine = null;
            }
        }
    }
}