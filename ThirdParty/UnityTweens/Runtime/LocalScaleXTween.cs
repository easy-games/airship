using ElRaccoone.Tweens.Core;
using UnityEngine;

namespace ElRaccoone.Tweens {
  public static partial class NativeTween {
    public static Tween<float> LocalScaleX (this Component self, float to, float duration) =>
      Tween<float>.Add<LocalScaleXDriver> (self).Finalize (to, duration);

    public static Tween<float> LocalScaleX (this GameObject self, float to, float duration) =>
      Tween<float>.Add<LocalScaleXDriver> (self).Finalize (to, duration);

    /// <summary>
    /// The driver is responsible for updating the tween's state.
    /// </summary>
    private class LocalScaleXDriver : TweenComponent<float, Transform> {
      private Vector3 localScale;

      /// <summary>
      /// Overriden method which is called when the tween starts and should
      /// return the tween's initial value.
      /// </summary>
      public override float OnGetFrom () {
        return this.component.localScale.x;
      }

      /// <summary>
      /// Overriden method which is called every tween update and should be used
      /// to update the tween's value.
      /// </summary>
      /// <param name="easedTime">The current eased time of the tween's step.</param>
      public override void OnUpdate (float easedTime) {
        this.localScale = this.component.localScale;
        this.valueCurrent = this.InterpolateValue (this.valueFrom, this.valueTo, easedTime);
        this.localScale.x = this.valueCurrent;
        this.component.localScale = this.localScale;
      }
    }
  }
}