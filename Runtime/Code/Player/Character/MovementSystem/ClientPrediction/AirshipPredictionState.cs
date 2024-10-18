

// inline everything because this is performance critical!
using System.Runtime.CompilerServices;
using UnityEngine;

public abstract class AirshipPredictionState{
    public double timestamp { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] protected set; }
    public Vector3 position { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] protected set; }
    public Vector3 velocity { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] protected set; }


    /// <summary>
    /// Interpolates from the current state to another state by delta amount
    /// </summary>
    /// <param name="other">The other state to lerp to</param>
    /// <param name="delta">the normalized percentage between the states</param>
    /// <returns>A new state object withe the interpolated result</returns>
    public abstract AirshipPredictionState Interpolate(AirshipPredictionState other, float delta);
}