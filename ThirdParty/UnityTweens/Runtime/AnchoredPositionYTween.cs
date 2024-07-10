using ElRaccoone.Tweens.Core;
using UnityEngine;

namespace ElRaccoone.Tweens {
  public static partial class NativeTween {
    public static Tween<float> AnchoredPositionY (this Component self, float to, float duration) =>
      Tween<float>.Add<AnchoredPositionYDriver> (self).Finalize (to, duration);

    public static Tween<float> AnchoredPositionY (this GameObject self, float to, float duration) =>
      Tween<float>.Add<AnchoredPositionYDriver> (self).Finalize (to, duration);

    /// <summary>
    /// The driver is responsible for updating the tween's state.
    /// </summary>
    private class AnchoredPositionYDriver : TweenComponent<float, RectTransform> {
      private Vector2 vector2Allocation;

      /// <summary>
      /// Overriden method which is called when the tween starts and should
      /// return the tween's initial value.
      /// </summary>
      public override float OnGetFrom () {
        return this.component.anchoredPosition.y;
      }

      /// <summary>
      /// Overriden method which is called every tween update and should be used
      /// to update the tween's value.
      /// </summary>
      /// <param name="easedTime">The current eased time of the tween's step.</param>
      public override void OnUpdate (float easedTime) {
        this.vector2Allocation = this.component.anchoredPosition;
        this.valueCurrent = this.InterpolateValue (this.valueFrom, this.valueTo, easedTime);
        this.vector2Allocation.y = this.valueCurrent;
        this.component.anchoredPosition = this.vector2Allocation;
      }
    }
  }
}