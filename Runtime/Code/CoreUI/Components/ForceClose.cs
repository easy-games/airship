using System;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using TMPro;
using UnityEngine;

namespace Code.CoreUI.Components {
    public class ForceClose : MonoBehaviour {
        public RectTransform container;
        public RectTransform fill;
        public TMP_Text text;
        [NonSerialized] private float holdTimeRequired = 2.2f;

        [NonSerialized] private float holdTime = 0f;
        [NonSerialized] private ITween tween;
        [NonSerialized] private bool isShown = false;
        [NonSerialized] private bool disconnected = false;

        private void Update() {
            if (Input.GetKey(KeyCode.Escape)) {
                if (!this.isShown && this.holdTime >= 0.5f) {
                    this.isShown = true;
                    if (this.tween != null) {
                        this.tween.Cancel();
                        this.tween = null;
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

            // not holding escape
            if (this.isShown) {
                this.isShown = false;
                this.holdTime = 0f;
                this.tween = NativeTween.AnchoredPositionY(this.container, 71f, 0.2f).SetEaseQuadOut();
            }
        }
    }
}