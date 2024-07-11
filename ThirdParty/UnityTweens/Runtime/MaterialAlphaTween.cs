using ElRaccoone.Tweens.Core;
using UnityEngine;

namespace ElRaccoone.Tweens {
  public static partial class NativeTween {
    public static Tween<float> MaterialAlpha (this Component self, float to, float duration) =>
      Tween<float>.Add<MaterialAlphaDriver> (self).Finalize (to, duration);

    public static Tween<float> MaterialAlpha (this GameObject self, float to, float duration) =>
      Tween<float>.Add<MaterialAlphaDriver> (self).Finalize (to, duration);

    /// <summary>
    /// The driver is responsible for updating the tween's state.
    /// </summary>
    private class MaterialAlphaDriver : Tween<float> {
      private MeshRenderer meshRenderer;
      private Material material;
      private Color color;

      public override bool OnInitialize () {
        this.meshRenderer = this.gameObject.GetComponent<MeshRenderer> ();
        if (this.meshRenderer != null)
          this.material = this.meshRenderer.material;
        return this.meshRenderer != null;
      }

      /// <summary>
      /// Overriden method which is called when the tween starts and should
      /// return the tween's initial value.
      /// </summary>
      public override float OnGetFrom () {
        return this.material.color.a;
      }

      /// <summary>
      /// Overriden method which is called every tween update and should be used
      /// to update the tween's value.
      /// </summary>
      /// <param name="easedTime">The current eased time of the tween's step.</param>
      public override void OnUpdate (float easedTime) {
        this.color = this.material.color;
        this.valueCurrent = this.InterpolateValue (this.valueFrom, this.valueTo, easedTime);
        this.color.a = this.valueCurrent;
        this.material.color = this.color;
      }
    }
  }
}