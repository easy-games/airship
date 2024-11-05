using System.Collections.Generic;
using Mirror;
using UnityEngine;

// PredictedCharacterMovement is based off of:
// PredictedRigidbody which stores & indidvidually rewinds history per Rigidbody.
// Instead of syncing position and velocity we sync the movement state and inputs for replays.
// This will be slower as we have to resimulate physics steps. Perhaps a future optimization
// would be to seperate the scene into multiple physics scenes and only resimulate the once the player is in

public class AirshipPredictedCharacterMovement : AirshipPredictedController<AirshipPredictedCharacterState> {

#region PUBLIC 
    [Header("References")]
    public CharacterMovement movement;

#endregion

#region PRIVATE
    //Cached Values
    private Transform tf; // this component is performance critical. cache .transform getter!
    private Rigidbody rigid;
    private AirshipPredictedCharacterState currentState;

    //For the client this stores inputs to use in the replay. For server this stores the recieved inputs from the client
    private List<AirshipPredictedCharacterState> predictionStates = new List<AirshipPredictedCharacterState>();

#endregion

#region GETTERS

    public override Vector3 currentPosition {
        get{
            return tf.position;
        } 
    }
    public override Vector3 currentVelocity {
        get{
            return rigid.velocity;
        } 
    }
#endregion 

#region INIT
    protected override void Awake() {
        rigid = movement.rigidbody;
        tf = transform;
        base.Awake();
    }

    protected override void OnEnable() {
        base.OnEnable();
        movement.OnEndMove += OnMovementUpdate;
    }

    protected override void OnDisable() {
        base.OnDisable();
        movement.OnEndMove -= OnMovementUpdate;
    }
#endregion

#region PREDICTION

    protected override bool NeedsCorrection(AirshipPredictedCharacterState serverState, AirshipPredictedCharacterState interpolatedState){
        return false;
    }

    public override void SnapTo(AirshipPredictedCharacterState newState){
        // apply the state to the Rigidbody instantly
        rigid.position = newState.position;

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
        }
    }

    public override void MoveTo(AirshipPredictedCharacterState newState){
        // apply the state to the Rigidbody
        // The only smoothing we get is from Rigidbody.MovePosition.
        rigid.MovePosition(newState.position);

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
        }
    }

    protected override void FixedUpdate() {
        //This disables the automatic calls to RecordState()
    }

    private void OnMovementUpdate(object data){
        currentState = (AirshipPredictedCharacterState)data;

        //Save the state in the history
        RecordState(NetworkTime.predictedTime);

        //Send the inputs to the server
        CmdMove(currentState.currentMoveInput);
    }

	[Command]
	//Sync the move input data to the server
	private void CmdMove(MoveInputData moveData){
		//Move(moveData);
	}

    public override AirshipPredictedCharacterState CreateCurrentState(double currentTime){
        // create state to insert
        return currentState;
    }
#endregion 

#region REPLAY

    public override void OnReplayStarted(AirshipPredictionState initialState, int historyIndex){
        //Save the future inputs
        predictionStates.Clear();
        for(int i=historyIndex; i < stateHistory.Count; i++){
            //Store the states into a new array
            predictionStates.Add(stateHistory[historyIndex]);
            //Clear the official history since we will be re writing it
            stateHistory.Remove(historyIndex);
        }

        //Snap to the servers state
        SnapTo((AirshipPredictedCharacterState)initialState);
        if(showGizmos){
            //Replay Position and velocity
            GizmoUtils.DrawSphere(currentPosition, .4f, Color.blue, 4, gizmoDuration);
            GizmoUtils.DrawLine(currentPosition, currentPosition+currentVelocity, clientColor, gizmoDuration);
        }
    }

    public override void OnReplayTickStarted(double time) {
        //Before physics sim 

        //If needed apply the inputs the player issued
        if(predictionStates.Count > 0) {
            var futureState = predictionStates[0];
            if(time >= futureState.timestamp) {
                movement.SetMoveInputData(futureState.currentMoveInput);
                predictionStates.RemoveAt(0);
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

    public override void OnReplayFinished(AirshipPredictionState initialState) {
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
        writer.WriteVector3(rigid.position);
        writer.WriteVector3(rigid.velocity);
    }

    public override AirshipPredictedCharacterState DeserializeState(NetworkReader reader, double timestamp) {
        var state = new AirshipPredictedCharacterState(timestamp, reader.ReadVector3(), reader.ReadVector3());
        return state;
    }
    #endregion

}