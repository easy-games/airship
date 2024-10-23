using Mirror;
using Unity.Mathematics;
using UnityEngine;

public class AirshipPredictedRigidbody : AirshipPredictionController<AirshipPredictedRigidbodyState> {
    private const float replayGizmoDuration = 2;

#region INSPECTOR
    [Header("References")]
    public Rigidbody rigid;

    [Header("Rigidbody Variables")]
    [Tooltip("Correction threshold in degrees. For example, 5 means that if the client is off by more than 5 degrees, it gets corrected.")]
    public double rotationCorrectionThreshold = 5;
#endregion


    private void Awake() {
        if(!rigid){
            rigid = gameObject.GetComponent<Rigidbody>();
        }

        AirshipPredictionManager.instance.StartPrediction();
        
        base.Awake();
    }

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
        print("Snapping to state: " + newState.timestamp + " pos: " + newState.position);
        // apply the state to the Rigidbody instantly
        rigid.position = newState.position;

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
        }

        if(showGizmos){
            GizmoUtils.DrawSphere(newState.position, .2f, Color.red, 4, replayGizmoDuration);
            GizmoUtils.DrawLine(newState.position, newState.position+newState.velocity, Color.red, replayGizmoDuration);
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
        print("Rotation Angle: " + Quaternion.Angle(serverState.rotation, interpolatedState.rotation));
        return base.NeedsCorrection(serverState, interpolatedState) || 
            Quaternion.Angle(serverState.rotation, interpolatedState.rotation) > rotationCorrectionThreshold;
    }

    protected override void OnRecievedOlderState(AirshipPredictedRigidbodyState serverState) {
        ReplayStates(serverState, 5);
    }

    #region REPLAY

    protected override AirshipPredictedRigidbodyState ReplayStates(AirshipPredictedRigidbodyState serverState, int numberOfFutureStates) {
        print("Replaying to server state: " + serverState.timestamp);

        //Snap to the servers state
        SnapTo(serverState);

        double time = serverState.timestamp;
        double simulationDuration;

        //Simulate until the end of our history or however long we think we are ahead of the server whicher is longer
        double finalTime = lastRecorded.timestamp > NetworkTime.predictedTime ? lastRecorded.timestamp : NetworkTime.predictedTime;
        Log("Replaying for " + (finalTime - time) + " seconds");
        while(time <= finalTime){
            //Move the rigidbody based on the saved inputs (impulses)
            //If no inputs then just resimulate with its current velocity
            //TODO

            //Move all dynamic rigidbodies to their saved states at this time
            //TODO
            //TODO maybe make a bool so this is optional?

            //Simulate physics until the next input state
            //TODO
            simulationDuration = finalTime - time;
            time += simulationDuration;

            //Don't simulate the last tiny bit of time
            if(simulationDuration < Time.fixedDeltaTime){
                //continue;
            }
            Log("Simulating: " + simulationDuration);
            Physics.Simulate((float)simulationDuration);


            if(showGizmos){
                GizmoUtils.DrawSphere(currentPosition, .2f, Color.black, 4, replayGizmoDuration);
                GizmoUtils.DrawLine(currentPosition, currentPosition+currentVelocity, Color.black, replayGizmoDuration);
            }
            
            //Save the new history state
            RecordState(time);
        }

        //Return the most recent state
        return lastRecorded;
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
