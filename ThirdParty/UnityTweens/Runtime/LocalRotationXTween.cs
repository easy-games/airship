using ElRaccoone.Tweens.Core;
using UnityEngine;

namespace ElRaccoone.Tweens {
  public static partial class NativeTween {
    public static Tween<float> LocalRotationX (this Component self, float to, float duration) =>
      Tween<float>.Add<LocalRotationXDriver> (self).Finalize (to, duration);

    public static Tween<float> LocalRotationX (this GameObject self, float to, float duration) =>
      Tween<float>.Add<LocalRotationXDriver> (self).Finalize (to, duration);

    /// <summary>
    /// The driver is responsible for updating the tween's state.
    /// </summary>
    private class LocalRotationXDriver : TweenComponent<float, Transform> {
      private Quaternion quaternionValueFrom;
      private Quaternion quaternionValueTo;

      /// <summary>
      /// Overriden method which is called when the tween starts and should
      /// return the tween's initial value.
      /// </summary>
      public override float OnGetFrom () {
        return this.component.localEulerAngles.x;
      }

      /// <summary>
      /// Overriden method which is called every tween update and should be used
      /// to update the tween's value.
      /// </summary>
      /// <param name="easedTime">The current eased time of the tween's step.</param>
      public override void OnUpdate (float easedTime) {
        this.quaternionValueFrom = Quaternion.Euler (this.valueFrom, this.component.localEulerAngles.y, this.component.localEulerAngles.z);
        this.quaternionValueTo = Quaternion.Euler (this.valueTo, this.component.localEulerAngles.y, this.component.localEulerAngles.z);
        this.component.localRotation = Quaternion.Lerp (this.quaternionValueFrom, this.quaternionValueTo, easedTime);
      }
    }
  }
}