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

#endregion

#region PRIVATE
    //Cached Values
    private Transform tf; // this component is performance critical. cache .transform getter!
    private CharacterMovementState currentState;

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

    protected override bool NeedsCorrection(CharacterMovementState serverState, CharacterMovementState interpolatedState){
        return base.NeedsCorrection(serverState, interpolatedState);
    }

    public override void SnapTo(CharacterMovementState newState){
        print("Snapping Movement To: " + newState.timestamp);
        movement.ForceToNewMoveState(newState);
    }

    protected override void FixedUpdate() {
        //This disables the automatic calls to RecordState()
    }
    
    private void OnSetMovementData(){
        if(!isServerOnly){
            return;
        }
        
        //The server applys inputs it recieves from the client
        int lastIndex = recievedInputs.Count-1;
        if(lastIndex >= 0 && NetworkTime.time >= recievedInputs.Keys[lastIndex]){
            if(recievedInputs.Values[lastIndex].jump){
                print("JUMP Applying inputs from client: " + recievedInputs.Keys[lastIndex] + " servertime: " + NetworkTime.time);
            }
            //This input should be used
            movement.SetMoveInputData(recievedInputs.Values[lastIndex]);
            recievedInputs.RemoveAt(lastIndex);
        }
    }

    private void OnMovementEnd(object data, object isReplay){
        currentState = (CharacterMovementState)data;

        if(!isClientOnly){
            return;
        }

        //Save the state in the history
        RecordState(NetworkTime.predictedTime);

        //Send the inputs to the server
        if(currentState.currentMoveInput.jump){
            print("JUMP sending inputs to server: " + NetworkTime.predictedTime);
        }
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
        return currentState;
    }
#endregion 

#region REPLAY

    public override void OnReplayStarted(AirshipPredictedState initialState, int historyIndex){
        //Save the future inputs
        replayPredictionStates.Clear();
        for(int i=historyIndex ; i < stateHistory.Count; i++){
            var nextState = stateHistory[historyIndex];
            //print("Replaying state: " + nextState.timestamp + " jump: " + nextState.currentMoveInput.jump);
            //Store the states into a new array
            replayPredictionStates.Add(nextState);
        }
        //Clear the official history since we will be re writing it
        stateHistory.Clear();

        //Snap to the servers state
        SnapTo((CharacterMovementState)initialState);
        if(showGizmos){
            //Replay Position and velocity
            GizmoUtils.DrawSphere(currentPosition, .4f, Color.blue, 4, gizmoDuration);
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
        movement.RunMovementTick();
    }

    public override void OnReplayTickFinished(double time) {
        //After the physics sim
        
        //Save the new history state
        RecordState(time);
    }

    public override void OnReplayFinished(AirshipPredictedState initialState) {
        if(showGizmos){
            GizmoUtils.DrawLine(initialState.position, currentPosition, Color.green, gizmoDuration);
        }
    }

    public override void OnReplayingOthersStarted() {
        //TODO
    }

    public override void OnReplayingOthersFinished() {
        //TODO
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