using Mirror;
using UnityEngine;

public class AirshipPredictedRigidbody : AirshipPredictionController<AirshipPredictedRigidbodyState> {

#region INSPECTOR
    [Header("References")]
    public Rigidbody rigid;

    [Header("Rigidbody Variables")]
    [Tooltip("Correction threshold in degrees. For example, 5 means that if the client is off by more than 5 degrees, it gets corrected.")]
    public double rotationCorrectionThreshold = 5;
#endregion

    protected override Vector3 currentPosition {
        get{
            return rigid.position;
        } 
    }

    protected override Vector3 currentVelocity {
        get{
            return rigid.velocity;
        }
    }

    protected override AirshipPredictedRigidbodyState CreateCurrentState(double currentTime) {
        return new AirshipPredictedRigidbodyState(currentTime, rigid.position, rigid.rotation, rigid.velocity, rigid.angularVelocity);
    }

    protected override void SnapTo(AirshipPredictedRigidbodyState newState){
        // apply the state to the Rigidbody instantly
        rigid.position = newState.position;

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
        }
    }

    protected override void MoveTo(AirshipPredictedRigidbodyState newState){
        // apply the state to the Rigidbody
        // The only smoothing we get is from Rigidbody.MovePosition.
        rigid.MovePosition(newState.position);

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
        }
    }

    protected override bool NeedsCorrection(AirshipPredictedRigidbodyState serverState, AirshipPredictedRigidbodyState interpolatedState) {
        return Quaternion.Angle(serverState.rotation, interpolatedState.rotation) > rotationCorrectionThreshold;
    }

#region REPLAY

    protected override AirshipPredictedRigidbodyState ReplayStates(AirshipPredictedRigidbodyState serverState, int numberOfFutureStates) {
        SnapTo(serverState);
        for(int i=0; i<numberOfFutureStates;i++){
            //TODO tick from the server state to the next history state
            //Will need to save the delta changes between ticks? 
        }
        return CreateCurrentState(NetworkTime.predictedTime);
    }

#endregion

#region SERIALIZE
    protected override void SerializeState(NetworkWriter writer) {
        writer.WriteVector3(rigid.position);
        writer.WriteVector4(rigid.rotation.ConvertToVector4());
        writer.WriteVector3(rigid.velocity);
        writer.WriteVector3(rigid.angularVelocity);
    }

    protected override AirshipPredictedRigidbodyState DeserializeState(NetworkReader reader, double timestamp) {
        return new AirshipPredictedRigidbodyState(timestamp, 
            reader.ReadVector3(), 
            reader.ReadVector4().ConvertToQuaternion(), 
            reader.ReadVector3(), 
            reader.ReadVector3());
    }
#endregion
}
