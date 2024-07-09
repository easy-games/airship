using ElRaccoone.Tweens.Core;
using UnityEngine;

namespace ElRaccoone.Tweens {
  public static partial class NativeTween {
    public static Tween<float> LocalPositionY (this Component self, float to, float duration) =>
      Tween<float>.Add<LocalPositionYDriver> (self).Finalize (to, duration);

    public static Tween<float> LocalPositionY (this GameObject self, float to, float duration) =>
      Tween<float>.Add<LocalPositionYDriver> (self).Finalize (to, duration);

    /// <summary>
    /// The driver is responsible for updating the tween's state.
    /// </summary>
    private class LocalPositionYDriver : TweenComponent<float, Transform> {
      private Vector3 localPosition;

      /// <summary>
      /// Overriden method which is called when the tween starts and should
      /// return the tween's initial value.
      /// </summary>
      public override float OnGetFrom () {
        return this.transform.localPosition.y;
      }

      /// <summary>
      /// Overriden method which is called every tween update and should be used
      /// to update the tween's value.
      /// </summary>
      /// <param name="easedTime">The current eased time of the tween's step.</param>
      public override void OnUpdate (float easedTime) {
        this.localPosition = this.transform.localPosition;
        this.valueCurrent = this.InterpolateValue (this.valueFrom, this.valueTo, easedTime);
        this.localPosition.y = this.valueCurrent;
        this.transform.localPosition = this.localPosition;
      }
    }
  }
}