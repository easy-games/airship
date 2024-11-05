using Mirror;
using UnityEngine;

public class AirshipPredictedRigidbody : AirshipPredictedController<AirshipPredictedRigidbodyState> {

#region INSPECTOR
    [Header("References")]
    public Rigidbody rigid;
    public Transform graphicsHolder;

    [Header("Rigidbody Variables")]
    [Tooltip("Correction threshold in degrees. For example, 5 means that if the client is off by more than 5 degrees, it gets corrected.")]
    public double rotationCorrectionThreshold = 5;
#endregion

#region PRIVATES

    private bool wasKinematic = false;
    private bool waitingForOthers = false;
    private Vector3 storedVelocity;
    private Vector3 storedAngularVelocity;

#endregion

    private void Awake() {
        if(!rigid){
            rigid = gameObject.GetComponent<Rigidbody>();
        }
        wasKinematic = rigid.isKinematic;

        AirshipPredictionManager.instance.StartPrediction();
        
        base.Awake();
    }

    protected new void OnEnable() {
        AirshipPredictionManager.instance.RegisterRigidbody(this.rigid, this.graphicsHolder);
        base.OnEnable();
    }

    protected new void OnDisable() {
        AirshipPredictionManager.instance.UnRegisterRigidbody(this.rigid);
        base.OnDisable();
    }

    public override Vector3 currentPosition {
        get{
            return rigid.position;
        } 
    }

    public override Vector3 currentVelocity {
        get{
            return rigid.velocity;
        }
    }

    public override AirshipPredictedRigidbodyState CreateCurrentState(double currentTime) {
        return new AirshipPredictedRigidbodyState(currentTime, rigid.position, rigid.rotation, rigid.velocity, rigid.angularVelocity);
    }

    public override void SnapTo(AirshipPredictedRigidbodyState newState){
        //print("Snapping to state: " + newState.timestamp + " pos: " + newState.position);
        // apply the state to the Rigidbody instantly
        rigid.position = newState.position;
        rigid.rotation = newState.rotation;

        // Set the velocities
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
            rigid.angularVelocity = newState.angularVelocity;
        }

        

        if(showGizmos){
            //Snapped position and velocity
            GizmoUtils.DrawSphere(newState.position, .2f, Color.red, 4, gizmoDuration);
            GizmoUtils.DrawLine(newState.position, newState.position+newState.velocity, Color.red, gizmoDuration);
        }
    }

    public override void MoveTo(AirshipPredictedRigidbodyState newState){
        //print("Moving To to state: " + newState.timestamp + " pos: " + newState.position);
        // apply the state to the Rigidbody
        // The only smoothing we get is from Rigidbody.MovePosition.
        rigid.MovePosition(newState.position);

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
            rigid.angularVelocity = newState.angularVelocity;
        }
    }

    protected override bool NeedsCorrection(AirshipPredictedRigidbodyState serverState, AirshipPredictedRigidbodyState interpolatedState) {
        print("Rotation Angle: " + Quaternion.Angle(serverState.rotation, interpolatedState.rotation));
        return base.NeedsCorrection(serverState, interpolatedState) || 
            Quaternion.Angle(serverState.rotation, interpolatedState.rotation) > rotationCorrectionThreshold;
    }

    #region REPLAY
    public override void OnReplayingOthersStarted() {
        if(waitingForOthers){
            return;
        }
        waitingForOthers = true;
        wasKinematic = rigid.isKinematic;
        if(!wasKinematic){
            storedVelocity = rigid.velocity;
            storedAngularVelocity = rigid.angularVelocity;
            print("rigid " + gameObject.GetInstanceID() + " is kinematic");
        }
        rigid.isKinematic = true;
    }


    public override void OnReplayingOthersFinished() {
        if(!waitingForOthers){
            return;
        }
        waitingForOthers = false;
        rigid.isKinematic = wasKinematic;
        if(!wasKinematic){
            rigid.velocity = storedVelocity;
            rigid.angularVelocity = storedAngularVelocity;
            print("rigid " + gameObject.GetInstanceID() + " WAS kinematic");
        }
    }

    public override void OnReplayStarted(AirshipPredictionState initialState, int historyIndex){
        // insert the correction and correct the history on top of it.
        // returns the final recomputed state after replaying.

        // var log = initialState.timestamp + ": History before replay: ";
        // foreach(var state in stateHistory){
        //     log += "\n State: " + state.Value.timestamp + " pos: " + state.Value.position;
        // }
        // print(log);

        var rigidState = (AirshipPredictedRigidbodyState)initialState;
        ClearHistoryAfterState(rigidState, historyIndex);

        //Snap to the servers state
        SnapTo(rigidState);
        if(showGizmos){
            //Replay Position and velocity
            GizmoUtils.DrawSphere(currentPosition, .4f, Color.blue, 4, gizmoDuration);
            GizmoUtils.DrawLine(currentPosition, currentPosition+currentVelocity, clientColor, gizmoDuration);
        }
    }

    public override void OnReplayTickStarted(double time){
        //TODO
        //run any input changes on this rigibodys
    }

    public override void OnReplayTickFinished(double time){
        if(showGizmos){
            //Replay Position and velocity
            GizmoUtils.DrawSphere(currentPosition, .2f, clientColor, 4, gizmoDuration);
            GizmoUtils.DrawLine(currentPosition, currentPosition+currentVelocity, clientColor, gizmoDuration);
        }

        //print("Replay tick: " + time);
        
        //Save the new history state
        RecordState(time);
    }

    public override void OnReplayFinished(AirshipPredictionState initialState){
        // var log = initialState.timestamp + "History after replay";
        // foreach(var state in stateHistory){
        //     log += "\n State: " + state.Value.timestamp + " pos: " + state.Value.position;
        // }
        // print(log);
        // print("new velocity: " + rigid.velocity);

        // log, draw & apply the final position.
        // always do this here, not when iterating above, in case we aren't iterating.
        // for example, on same machine with near zero latency.
        // int correctedAmount = stateHistory.Count - afterIndex;
        // Log($"Correcting {name}: {correctedAmount} / {stateHistory.Count} states to final position from: {rb.position} to: {lastState.position}");
        if(showGizmos){
            GizmoUtils.DrawLine(initialState.position, currentPosition, Color.green, gizmoDuration);
        }
    }

    #endregion

    #region SERIALIZE
    public override void SerializeState(NetworkWriter writer) {
        writer.WriteVector3(rigid.position);
        writer.WriteVector4(rigid.rotation.ConvertToVector4());
        writer.WriteVector3(rigid.velocity);
        writer.WriteVector3(rigid.angularVelocity);
    }

    public override AirshipPredictedRigidbodyState DeserializeState(NetworkReader reader, double timestamp) {
        return new AirshipPredictedRigidbodyState(timestamp, 
            reader.ReadVector3(), 
            reader.ReadVector4().ConvertToQuaternion(), 
            reader.ReadVector3(), 
            reader.ReadVector3());
    }
    #endregion
}
