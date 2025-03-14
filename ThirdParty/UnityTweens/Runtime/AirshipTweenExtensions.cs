using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using UnityEngine;

namespace ElRaccoone.Tweens {
    public static partial class NativeTween {
        public static Tween<Color> RendererColor(this Component self, Color from, Color to, float duration) {
            return NativeTween.ValueColor(self, to, duration, new RendererColorDriver(self.gameObject).OnUpdate).SetFrom(from);
        }

        public static Tween<Color> RendererColor(this GameObject self, Color from, Color to, float duration) {
            return NativeTween.ValueColor(self, to, duration, new RendererColorDriver(self).OnUpdate).SetFrom(from);
        }

        // public static Tween<Color> TweenCanvasGroupAlpha(this GameObject self, Color from, Color to, float duration) {
        //     return self.TweenValueColor(to, duration, new Driver(self.gameObject).OnUpdate).SetFrom(from);
        // }

        private class RendererColorDriver {
            private Renderer renderer;
            private Material material;
            private Color color;

            public RendererColorDriver(GameObject go) {
                renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    material = renderer.material;
            }

            public void OnUpdate(Color newColor) {
                this.material.color = newColor;
            }
        }

        public static Tween<float> MaterialsColorProperty(this Component self, string propertyName, Color from,
            Color to, float duration) {
            var ren = self.gameObject.GetComponent<Renderer>();
            return NativeTween.ValueFloat(self.gameObject, 1, duration, delta => {
                Color newColor = Color.Lerp(from, to, delta);
                foreach (var mat in ren.materials) {
                    mat.SetColor(propertyName, newColor);
                }
            }).SetFrom(0);
        }

        public static Tween<float> MaterialsFloatProperty(this Component self, string propertyName, float from,
            float to, float duration) {
            var ren = self.gameObject.GetComponent<Renderer>();
            return NativeTween.ValueFloat(self.gameObject, 1, duration, delta => {
                foreach (var mat in ren.materials) {
                    mat.SetFloat(propertyName, Mathf.Lerp(from, to, delta));
                }
            }).SetFrom(0);
        }
        
        public static Tween<float> MaterialsVectorProperty(this Component self, string propertyName, Vector4 from,
            Vector4 to, float duration) {
            var ren = self.gameObject.GetComponent<Renderer>();
            return NativeTween.ValueFloat(self.gameObject, 1, duration, delta => {
                var newColor = Vector4.Lerp(from, to, delta);
                foreach (var mat in ren.materials) {
                    mat.SetVector(propertyName, newColor);
                }
            }).SetFrom(0);
        }
    }
}
