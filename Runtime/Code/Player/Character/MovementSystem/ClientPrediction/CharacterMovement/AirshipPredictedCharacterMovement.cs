using System.Collections.Generic;
using Mirror;
using UnityEngine;

// PredictedCharacterMovement is based off of:
// PredictedRigidbody which stores & indidvidually rewinds history per Rigidbody.
// Instead of syncing position and velocity we sync the movement state and inputs for replays.
// This will be slower as we have to resimulate physics steps. Perhaps a future optimization
// would be to seperate the scene into multiple physics scenes and only resimulate the once the player is in

public class AirshipPredictedCharacterMovement : AirshipPredictedController<CharacterMovementState> {

#region PUBLIC 
    [Header("References")]
    public CharacterMovement movement;

    [Header("Variables")]
    public bool pauseOnReplay = false;

#endregion

#region PRIVATE
    //Cached Values
    private Transform tf; // this component is performance critical. cache .transform getter!
    private CharacterMovementState currentState => movement.currentMoveState;

    //For the client this stores inputs to use in the replay.
    private List<CharacterMovementState> replayPredictionStates = new List<CharacterMovementState>();
    private SortedList<double, MoveInputData> recievedInputs = new SortedList<double, MoveInputData>();
#endregion

#region GETTERS
    public override Vector3 currentPosition {
        get{
            return tf.position;
        } 
    }
    public override Vector3 currentVelocity {
        get{
            return movement.GetVelocity();
        } 
    }

    public override string friendlyName => "PredictedCharacter_"+movement.gameObject.name;
    #endregion

    #region INIT
    protected override void Awake() {
        tf = transform;
        recordInterval =  Time.fixedDeltaTime;
        base.Awake();
    }

    protected override void OnEnable() {
        base.OnEnable();
        movement.OnSetCustomData += OnSetMovementData;
        movement.OnEndMove += OnMovementEnd;
    }

    protected override void OnDisable() {
        base.OnDisable();
        movement.OnSetCustomData -= OnSetMovementData;
        movement.OnEndMove -= OnMovementEnd;
    }
#endregion

#region PREDICTION

    //For history
    protected override bool ShouldRecordState(){
        if(lastRecorded != null && lastRecorded.Equals(currentState)){
            return true;
        }
        return base.ShouldRecordState();
    }

    protected override void FixedUpdate() {
        // This disables the automatic calls to RecordState()
    }
    
    private void OnSetMovementData(){
        if(!isServerOnly){
            return;
        }
        
        // The server applys inputs it recieves from the client
        // int lastIndex = recievedInputs.Count-1;
        // if(lastIndex >= 0 && NetworkTime.time >= recievedInputs.Keys[lastIndex]){
        //     if(recievedInputs.Values[lastIndex].jump){
        //         print("JUMP Applying inputs from client: " + recievedInputs.Keys[lastIndex] + " servertime: " + NetworkTime.time);
        //     }
        //     //This input should be used
        //     movement.SetMoveInputData(recievedInputs.Values[lastIndex]);
        //     recievedInputs.RemoveAt(lastIndex);
        // }

        if(recievedInputs.Count > 0 && NetworkTime.time >= recievedInputs.Keys[0] - recordInterval){
            //print("using input: " + recievedInputs.Keys[0] + " at: " + NetworkTime.time + " remaining: " + (recievedInputs.Count-1));
             //This input should be used
            movement.SetMoveInputData(recievedInputs.Values[0]);
            recievedInputs.RemoveAt(0);
        }
    }

    private MoveInputData lastSentInput;
    private void OnMovementEnd(object data, object isReplay){
        if((bool)isReplay || !isClientOnly){
            return;
        }

        if (onlyRecordChanges && lastRecorded != null &&
            lastRecorded.currentMoveInput.Equals(currentState.currentMoveInput) &&
            lastRecorded.position.Equals(currentState.position) &&
            lastRecorded.velocity.Equals(currentState.velocity)) {
            //NetworkTime.time - lastRecordTime < recordInterval) {
                // Log($"FixedUpdate for {name}: taking optimized early return instead of recording state.");
                return;
        }

        // Save the state in the history
        RecordState(NetworkTime.predictedTime);

        if(lastSentInput.Equals(currentState.currentMoveInput)){
            // Don't need to send reduntant data
            return;
        }
        this.lastSentInput = currentState.currentMoveInput;
        // Send the inputs to the server
        // print("Sending input at: " + NetworkTime.predictedTime);
        SetServerInput(NetworkTime.predictedTime, currentState.currentMoveInput);
    }

	[Command]
	//Sync the move input data to the server
	private void SetServerInput(double timeStamp, MoveInputData moveData){
        //print("recieved inputs from: " + timeStamp + " current time: " + NetworkTime.time);
        if(timeStamp > NetworkTime.time){
            var diff = Mathf.Abs((float)(NetworkTime.time - timeStamp));
            if(diff > .1f){
                Debug.LogWarning("Recieved inputs from client that are in the past by " + diff + " seconds");
            }
        }

        //Store this input sorted by time
        recievedInputs.Add(timeStamp, moveData);

        // keep state history within limit
        if(recievedInputs.Count > this.stateHistoryLimit){
            recievedInputs.Remove(0);
        }
	}

    public override CharacterMovementState CreateCurrentState(double currentTime){
        // create state to insert
        return new CharacterMovementState(currentState){timestamp = currentTime};
    }
#endregion 

#region REPLAY    
    protected override bool NeedsCorrection(CharacterMovementState serverState, CharacterMovementState interpolatedState){
        return base.NeedsCorrection(serverState, interpolatedState);
    }

    public override void SnapTo(CharacterMovementState newState){
        print("Snapping Movement To: " + newState.timestamp);
        movement.ForceToNewMoveState(newState);
    }

    public override void OnReplayStarted(AirshipPredictedState initialState, int historyIndex){
        PrintHistory("REPLAY STARTED");
        //Save the future inputs
        replayPredictionStates.Clear();
        for(int i=historyIndex ; i < stateHistory.Count; i++){
            var nextState = stateHistory.Values[historyIndex];
            //print("Replaying state: " + nextState.timestamp + " jump: " + nextState.currentMoveInput.jump);
            //Store the states into a new array
            replayPredictionStates.Add(nextState);
        }
        //Clear the official history since we will be re writing it
        stateHistory.Clear();


        //Snap to the servers state
        var movementState = (CharacterMovementState)initialState;
        SnapTo(movementState);
        movement.transform.position = movementState.position;
        stateHistory.Add(movementState.timestamp, movementState);
        PrintHistory("SNAPPED TO INITIAL STATE CLEARED");
        if(showGizmos){
            //Replay Position and velocity
            GizmoUtils.DrawSphere(currentPosition, .4f, clientColor, 4, gizmoDuration);
            GizmoUtils.DrawLine(currentPosition, currentPosition+currentVelocity, clientColor, gizmoDuration);
        }
    }

    public override void OnReplayTickStarted(double time) {
        //Before physics sim

        //If needed apply the inputs the player issued
        if(replayPredictionStates.Count > 0) {
            var futureState = replayPredictionStates[0];
            if(time >= futureState.timestamp) {
                if(futureState.currentMoveInput.jump){
                    print("JUMP Replaying inputs: " + futureState.timestamp + " at: " + time);
                }
                movement.SetMoveInputData(futureState.currentMoveInput);
                replayPredictionStates.RemoveAt(0);
            }
        }

        //Run the movement logic based on the saved input history
        movement.RunMovementTick(true);
    }

    public override void OnReplayTickFinished(double time) {
        // After the physics sim
        if(showGizmos){
            //Replay Position and velocity
            GizmoUtils.DrawSphere(currentPosition, .1f, clientColor, 4, gizmoDuration);
            GizmoUtils.DrawLine(currentPosition, currentPosition+currentVelocity, clientColor, gizmoDuration);
        }
        if(lastRecorded == null || !lastRecorded.Equals(currentState)){
            // Save the new history state
            RecordState(time);
        }
    }

    public override void OnReplayFinished(AirshipPredictedState initialState) {
        PrintHistory("REPLAY FINISHED");
        if(showGizmos){
            GizmoUtils.DrawSphere(currentPosition, .4f, Color.green, 4, gizmoDuration);
            GizmoUtils.DrawLine(initialState.position, currentPosition, Color.green, gizmoDuration);
        }

        if(pauseOnReplay){
            Debug.Break();
        }
    }

    public override void OnReplayingOthersStarted() {
        // TODO
    }

    public override void OnReplayingOthersFinished() {
        // TODO
    }
#endregion

#region SERIALIZE
    public override void SerializeState(NetworkWriter writer) {
        writer.WriteVector3(tf.position);
        writer.WriteVector3(movement.GetVelocity());
    }

    public override CharacterMovementState DeserializeState(NetworkReader reader, double timestamp) {
        var state = new CharacterMovementState(timestamp, reader.ReadVector3(), reader.ReadVector3());
        return state;
    }
    #endregion

}