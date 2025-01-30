using UnityEngine;

public class AirshipPredictedState{
    public int tick;
    public Vector3 position;
    public Vector3 velocity;

    public AirshipPredictedState Copy(AirshipPredictedState otherState){
        this.tick = otherState.tick;
        this.position = otherState.position;
        this.velocity = otherState.velocity;
        return this;
    }

    /// <summary>
    /// Interpolates from the current state to another state by delta amount
    /// </summary>
    /// <param name="other">The other state to lerp to</param>
    /// <param name="delta">the normalized percentage between the states</param>
    /// <returns>A new state object withe the interpolated result</returns>
    // public abstract AirshipPredictedState Interpolate(AirshipPredictedState other, float delta);
}