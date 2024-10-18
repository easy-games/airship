using UnityEngine;

public class PredictedCharacterState : PredictionState{

    public override PredictionState Interpolate(PredictionState other, float delta) {
        //TODO: make actual interpolation here
        return other;
    }
}
