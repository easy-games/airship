using System.Runtime.CompilerServices;
using Code.Player.Character;
using Mirror;
using UnityEngine;

// PredictedCharacterMovement is based off of:
// PredictedRigidbody which stores & indidvidually rewinds history per Rigidbody.
// Instead of syncing position and velocity we sync the movement state and inputs for replays.
// This will be slower as we have to resimulate physics steps. Perhaps a future optimization
// would be to seperate the scene into multiple physics scenes and only resimulate the once the player is in

public class AirshipPredictedCharacterMovement : AirshipPredictionController<AirshipPredictedCharacterState> {

#region PUBLIC 
    [Header("References")]
    public CharacterMovement movement;

#endregion

#region PRIVATE
    //Cached Values
    private Transform tf; // this component is performance critical. cache .transform getter!
    private Rigidbody rigid;

#endregion

#region GETTERS

    protected override Vector3 currentPosition {
        get{
            return tf.position;
        } 
    }
    protected override Vector3 currentVelocity {
        get{
            return rigid.velocity;
        } 
    }

    protected override AirshipPredictedCharacterState CreateCurrentState(double currentTime){
        // grab current position/rotation/velocity only once.
        // this is performance critical, avoid calling .transform multiple times.
        Vector3 currentPosition = tf.position;//GetPos(out Vector3 currentPosition, out Quaternion currentRotation); // faster than accessing .position + .rotation manually
        Vector3 currentVelocity = movement.rigidbody.velocity;

        // create state to insert
        return new AirshipPredictedCharacterState(currentTime, currentPosition, currentVelocity);
    }
#endregion 


#region INIT
    protected override void Awake() {
        rigid = movement.rigidbody;
        tf = transform;
        base.Awake();
    }
#endregion

#region PREDICTION

    protected override bool NeedsCorrection(AirshipPredictedCharacterState serverState, AirshipPredictedCharacterState interpolatedState){
        return false;
    }

    protected override void SnapTo(AirshipPredictedCharacterState newState){
        // apply the state to the Rigidbody instantly
        rigid.position = newState.position;

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
        }
    }

    protected override void MoveTo(AirshipPredictedCharacterState newState){
        // apply the state to the Rigidbody
        // The only smoothing we get is from Rigidbody.MovePosition.
        rigid.MovePosition(newState.position);

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newState.velocity;
        }
    }
#endregion 

#region REPLAY

#endregion

#region SERIALIZE
    protected override void SerializeState(NetworkWriter writer) {
        writer.WriteVector3(rigid.position);
        writer.WriteVector3(rigid.velocity);
    }

    protected override AirshipPredictedCharacterState DeserializeState(NetworkReader reader, double timestamp) {
        return new AirshipPredictedCharacterState(timestamp, 
            reader.ReadVector3(), 
            reader.ReadVector3());
    }

    public override void OnReplayStart(AirshipPredictionState initialState)
    {
        throw new System.NotImplementedException();
    }

    public override void OnReplayTickStarted(double time)
    {
        throw new System.NotImplementedException();
    }

    public override void OnReplayTickFinished(double time)
    {
        throw new System.NotImplementedException();
    }

    public override void OnReplayFinished(AirshipPredictionState initialState)
    {
        throw new System.NotImplementedException();
    }
    #endregion
}