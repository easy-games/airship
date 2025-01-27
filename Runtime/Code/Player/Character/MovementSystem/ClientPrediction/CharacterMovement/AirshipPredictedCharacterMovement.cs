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
    public bool pauseOnReplay = false;
	public float observerLerpDuration = .1f;

    [Header("Character References")]
    public CharacterMovement movement;

#endregion

#region PRIVATE
    //Cached Values
    private Transform tf; // this component is performance critical. cache .transform getter!
    private CharacterMovementState currentState => movement.currentMoveState;

    //For the client this stores inputs to use in the replay.
    private List<CharacterMovementState> replayPredictionStates = new List<CharacterMovementState>();
    private SortedList<int, MoveInputData> recievedInputs = new SortedList<int, MoveInputData>();

    private MoveInputData lastSentInput;
    
	//Prediction Observers
	private Vector3 prevLocalObserverPosition;
	private Vector3 prevObserverPosition;
	private Vector3 nextObserverPosition;
	private float lastObserverTime;
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
        AirshipPredictionManager.instance.StartPrediction();

        tf = transform;
        recordInterval = Time.fixedDeltaTime;

        base.Awake();
        
    }

    protected override void OnEnable() {
        if(IsObserver()){
            base.OnEnable();
            return;
        }

        if(this.smoothRigidbody){
            AirshipPredictionManager.instance.RegisterRigidbody(this.movement.rigidbody, this.movement.airshipTransform);
        }
        base.OnEnable();
        movement.OnSetCustomData += OnSetMovementData;
        movement.OnEndMove += OnMovementEnd;
    }

    protected override void OnDisable() {
        if(IsObserver()){
            base.OnDisable();
            return;
        }

        if(this.smoothRigidbody){
            AirshipPredictionManager.instance.UnRegisterRigidbody(this.movement.rigidbody);
        }
        base.OnDisable();
        movement.OnSetCustomData -= OnSetMovementData;
        movement.OnEndMove -= OnMovementEnd;
    }
    #endregion

    #region OBSERVERS

    protected override void OnRecievedObserverState(CharacterMovementState serverState) {
		//In server auth we don't sync position with the NetworkTransform so I am doing it here
        
        //Update observers movement with all the state information we have (For animations only)
        movement.ForceToNewMoveState(serverState);

        //Store prev and next positions so we can lerp between the two to account for network lag
        //prevObserverPosition = movement.transform.position; //Start lerping from your current position
        prevLocalObserverPosition = movement.transform.position;
        prevObserverPosition = nextObserverPosition; //Start lerping from this states position
        //TODO: Should prev position be the last state position? Would get snapping but would be more accurate to their current pos
        nextObserverPosition = serverState.position; //End at the latest server position

        //Track how long we should interpolate
        lastObserverTime = Time.time;

    }

	protected override void Update(){
        base.Update();
        if(IsObserver()){
            //Find where we are in time since the last observer update
            var delta = Mathf.Clamp((Time.time - lastObserverTime) / observerLerpDuration, 0, 10); //Let it extrapolate but not too crazy (10 is an arbitrary guess at what is too crazy)
            //Interpolate between prev position and the lateset server position
            movement.transform.position = Vector3.Lerp(
                //Vector3.Lerp(prevLocalObserverPosition, prevObserverPosition, delta), //To avoid hard snap if we didn't make it to the observer position before the next update
                prevObserverPosition,
                nextObserverPosition, delta);
        }
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

    protected override void OnPhysicsTick() {
        // This disables the automatic calls to RecordState()
        //base.OnPhysicsTick();
        serverTick++;
        predictedTick++;
    }
    
    private void OnSetMovementData(){
        if(isServerOnly){
            // The server applys inputs it recieves from the client
            // Only if the time is past the inputs state time with a margin of the record interval 
            // Minus 1 because the tick describes the time the inputs were already used
            if(recievedInputs.Count > 0 && serverTick >= recievedInputs.Keys[0]-1){
                if(showLogs){
                    print("using input: " + recievedInputs.Keys[0] + " at: " + serverTick + " moveDir: " + recievedInputs.Values[0].moveDir);
                }
                //This input should be used
                movement.SetMoveInputData(recievedInputs.Values[0]);
                recievedInputs.RemoveAt(0);
            }
        }
    }
    
    //Record the state of the object after the physics sim and the inputs that brought it to that point
    private void OnMovementEnd(object data, object isReplay){
        if((bool)isReplay){
            return;
        }

        if(isClientOnly){
            var newInput = currentState.currentMoveInput;
            var isInputChanged =  lastRecorded == null || !lastRecorded.currentMoveInput.Equals(newInput);

            if (onlyRecordChanges && !isInputChanged && lastRecorded != null &&
                Vector3.SqrMagnitude(lastRecorded.position - currentState.position) >= positionCorrectionThresholdSqr &&
                Vector3.SqrMagnitude(lastRecorded.velocity - currentState.velocity) >= velocityCorrectionThresholdSqr) {
                    return;
            }

            // Save the state in the history
            //print("Movement Tick: " + serverTick + " predictedTime: " + predictedTick);
            RecordState(predictedTick);

            if(isInputChanged){
                // Update state on server if its a new input state
                this.lastSentInput = newInput;
                
                // Send the inputs to the server
                if(showLogs){
                    print("Sending input at: " + predictedTick + " moveDir: " + this.lastSentInput.moveDir);
                }
                SetServerInput(predictedTick, this.lastSentInput);
            }
        }
    }

	[Command]
	//Sync the move input data to the server
	private void SetServerInput(int tick, MoveInputData moveData){
        if(showLogs){
            print("recieved inputs. From: " + tick + " current time: " + serverTick);
        }
        if(tick < serverTick){
            Debug.LogWarning("Recieved inputs from client that are in the past by " + (tick - serverTick) + " ticks");
            return;
        }

        //If there is already a value here, overwrite it
        if(recievedInputs.ContainsKey(tick)){
            Debug.LogWarning("Overwriting input from client at tick: " + tick);
            recievedInputs.Remove(tick);
        }

        //Store this input sorted by time
        recievedInputs.Add(tick, moveData);

        // keep state history within limit
        if(recievedInputs.Count > this.stateHistoryLimit){
            recievedInputs.Remove(0);
        }
	}

    public override CharacterMovementState CreateCurrentState(int currentTick){
        // create state to insert
        return new CharacterMovementState(currentState){tick = currentTick};
    }

#endregion 

#region REPLAY    
    protected override bool NeedsCorrection(CharacterMovementState serverState, CharacterMovementState interpolatedState){
        return base.NeedsCorrection(serverState, interpolatedState);
    }

    public override void SnapTo(CharacterMovementState newState){
        if(showLogs){
            print("Snapping Movement To: " + newState.tick);
        }    

        if(IsObserver()){
            //Observers visual states are already synced by CharacterMovement
            // movement.rigidbody.position = newState.position;
            // movement.currentMoveState.position = newState.position;
            // movement.currentMoveState.velocity = newState.velocity;
            return;
        }else{
            //Sync this state into the character movement
            movement.ForceToNewMoveState(newState);
        }
    }

    public override void OnReplayStarted(AirshipPredictedState initialState, int historyIndex){
        if(showLogs){
            print("Replay start: " + initialState.tick);
        }
        //Save the future inputs
        replayPredictionStates.Clear();
        for(int i=historyIndex ; i < stateHistory.Count; i++){
            var nextState = stateHistory.Values[i];
           //print("Replaying state: " + nextState.timestamp + " jump: " + nextState.currentMoveInput.jump);

            //Store the states into a new array
            replayPredictionStates.Add(nextState);
        }
        //Clear the official history since we will be re writing it
        stateHistory.Clear();


        //Snap to the initial state
        var movementState = (CharacterMovementState)initialState;
        SnapTo(movementState);
        movement.transform.position = movementState.position;
        stateHistory.Add(movementState.tick, movementState);
    }

    public override void OnReplayTickStarted(int tick) {
        //Before physics sim

        //If needed apply the inputs the player issued
        if(replayPredictionStates.Count > 0) {
            var futureState = replayPredictionStates[0];
            if(tick >= futureState.tick) {
                // if(futureState.currentMoveInput.jump){
                //     print("JUMP Replaying inputs: " + futureState.timestamp + " at: " + time);
                // }
                movement.SetMoveInputData(futureState.currentMoveInput);
                replayPredictionStates.RemoveAt(0);
            }
        }

        //Run the movement logic based on the saved input history
        movement.RunMovementTick(true);
    }

    public override void OnReplayTickFinished(int tick) {
        // After the physics sim
        if(showGizmos){
            //Replay Position and velocity
            GizmoUtils.DrawSphere(currentPosition, .05f, clientColor, 4, gizmoDuration);
            GizmoUtils.DrawLine(currentPosition, currentPosition+currentVelocity * .1f, clientColor, gizmoDuration);
        }
        if(lastRecorded == null || !lastRecorded.Equals(currentState)){
            // Save the new history state
            RecordState(tick);
        }
        if(showLogs){
            print("tick finished: " + tick);
        }
        predictedTick = tick;
    }

    public override void OnReplayFinished(AirshipPredictedState initialState) {
        if(showLogs){
         print("Replay ended. initial tick: " + initialState.tick);
        }
        predictedTick++;
        serverTick = initialState.tick;
        //PrintHistory("REPLAY FINISHED");
        if(showGizmos){
            GizmoUtils.DrawSphere(currentPosition, .1f, Color.green, 4, gizmoDuration);
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

    public override CharacterMovementState DeserializeState(NetworkReader reader, int tick) {
        var state = new CharacterMovementState(tick, reader.ReadVector3(), reader.ReadVector3());
        return state;
    }
    #endregion

}