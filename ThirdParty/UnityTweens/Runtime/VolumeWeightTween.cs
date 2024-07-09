#if TWEENS_DEFINED_COM_UNITY_RENDERPIPELINES_CORE

using ElRaccoone.Tweens.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElRaccoone.Tweens {
  public static partial class NativeTween {
    public static Tween<float> VolumeWeight (this Component self, float to, float duration) =>
      Tween<float>.Add<VolumeWeightDriver> (self).Finalize (to, duration);

    public static Tween<float> VolumeWeight (this GameObject self, float to, float duration) =>
      Tween<float>.Add<VolumeWeightDriver> (self).Finalize (to, duration);

    /// <summary>
    /// The driver is responsible for updating the tween's state.
    /// </summary>
    private class VolumeWeightDriver : TweenComponent<float, Volume> {
      
      /// <summary>
      /// Overriden method which is called when the tween starts and should
      /// return the tween's initial value.
      /// </summary>
      public override float OnGetFrom () {
        return this.component.weight;
      }

      /// <summary>
      /// Overriden method which is called every tween update and should be used
      /// to update the tween's value.
      /// </summary>
      /// <param name="easedTime">The current eased time of the tween's step.</param>
      public override void OnUpdate (float easedTime) {
        this.valueCurrent = this.InterpolateValue (this.valueFrom, this.valueTo, easedTime);
        this.component.weight = this.valueCurrent;
      }
    }
  }
}

#endif
