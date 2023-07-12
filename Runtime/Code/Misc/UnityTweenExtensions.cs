using ElRaccoone.Tweens;
using ElRaccoone.Tweens.Core;
using UnityEngine;

public static class UnityTweenExtensions {
    public static Tween<Color> TweenRendererColor(this Component self, Color from, Color to, float duration) {
        return self.TweenValueColor(to, duration, new Driver(self.gameObject).OnUpdate).SetFrom(from);
    }

    public static Tween<Color> TweenRendererColor(this GameObject self, Color from, Color to, float duration) {
        return self.TweenValueColor(to, duration, new Driver(self).OnUpdate).SetFrom(from);
    }

    // public static Tween<Color> TweenCanvasGroupAlpha(this GameObject self, Color from, Color to, float duration) {
    //     return self.TweenValueColor(to, duration, new Driver(self.gameObject).OnUpdate).SetFrom(from);
    // }

    private class Driver{
        private Renderer renderer;
        private Material material;
        private Color color;

        public Driver(GameObject go){
            renderer = go.GetComponent<Renderer> ();
            if (renderer != null)
                material = renderer.material;
        }

        public void OnUpdate (Color newColor) {
            this.material.color = newColor;
        }
    }

    public static Tween<float> TweenMaterialsColorProperty(this Component self, string propertyName, Color from, Color to, float duration) {
        var ren = self.gameObject.GetComponent<Renderer> ();
        return self.gameObject.TweenValueFloat(1, duration, delta => {
            Color newColor = Color.Lerp(from, to, delta);
            foreach (var mat in ren.materials) {
                mat.SetColor(propertyName, newColor);
            }
        }).SetFrom(0);
    }

    public static Tween<float> TweenMaterialsFloatProperty(this Component self, string propertyName, float from, float to, float duration) {
        var ren = self.gameObject.GetComponent<Renderer> ();
        return self.gameObject.TweenValueFloat(1, duration, delta => {
            foreach (var mat in ren.materials) {
                mat.SetFloat(propertyName, Mathf.Lerp(from, to, delta));
            }
        }).SetFrom(0);
    }
}
