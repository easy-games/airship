using System;
using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using UnityEngine;

public class ForceClose : MonoBehaviour {
    public RectTransform container;
    public RectTransform fill;
    public float holdTimeRequired = 4f;

    [NonSerialized] private float holdTime;
    [NonSerialized] private ITween tween;
    [NonSerialized] private bool isShown = false;

    private void Start() {

    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Mouse1)) {
            if (!this.isShown) {
                this.isShown = true;
                this.holdTime = 0f;
                if (this.tween != null) {
                    this.tween.Cancel();
                    this.tween = null;
                }

                NativeTween.AnchoredPositionY(this.container, -10f, 0.18f).SetEaseBounceOut();
            }
            this.holdTime += Time.deltaTime;

            float fillAmount = Math.Min(this.holdTime / this.holdTimeRequired, 1);
            this.fill.anchorMax = new Vector2(fillAmount, this.fill.anchorMax.y);
        }
    }
}