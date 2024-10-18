using UnityEngine;

public class AirshipPredictedCharacterState : AirshipPredictionState{
    public AirshipPredictedCharacterState(double time, Vector3 pos, Vector3 vel){
        this.position = pos;
        this.velocity = vel;
        this.timestamp = time;
    }

    public override AirshipPredictionState Interpolate(AirshipPredictionState other, float delta) {
        //TODO: make actual interpolation here
        return other;
    }
}
