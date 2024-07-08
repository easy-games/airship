using System;
using ElRaccoone.Tweens;
using UnityEngine;
using UnityEngine.UI;

namespace Code.CoreUI.Components {
    public class InternalToggle : MonoBehaviour {
        private bool value;
        public Action<bool> onValueChanged;

        public Image bgImage;
        public RectTransform handle;

        public Color activeColor;
        public Color inactiveColor;

        public void Start() {
            this.SetValueVisual(this.value, true);
        }

        public void Button_OnClick() {
            this.SetValue(!this.value);
            this.onValueChanged?.Invoke(this.value);
        }

        public void SetValue(bool val) {
            this.value = val;
            this.SetValueVisual(this.value);
        }

        private void SetValueVisual(bool val, bool instant = false) {
            this.bgImage.TweenGraphicColor(val ? activeColor : inactiveColor, instant ? 0f : 0.18f);
            this.handle.TweenAnchoredPositionX(val ? 11 : -11, instant ? 0f : 0.18f).SetEaseBounceOut();
        }
    }
}