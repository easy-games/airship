using UnityEngine;

public abstract class AirshipPredictedState{
    public int tick;
    public Vector3 position;
    public Vector3 velocity;

    /// <summary>
    /// Interpolates from the current state to another state by delta amount
    /// </summary>
    /// <param name="other">The other state to lerp to</param>
    /// <param name="delta">the normalized percentage between the states</param>
    /// <returns>A new state object withe the interpolated result</returns>
    // public abstract AirshipPredictedState Interpolate(AirshipPredictedState other, float delta);
}