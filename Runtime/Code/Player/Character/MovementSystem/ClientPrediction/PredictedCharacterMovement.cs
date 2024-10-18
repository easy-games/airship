
using System.Runtime.CompilerServices;
using Code.Player.Character;
using UnityEngine;

public class PredictedCharacterMovement : PredictionController<PredictedCharacterState> {

#region PUBLIC 
    [Header("References")]
    public CharacterMovement movement;

    [Header("Debugging")]

    [Tooltip("Draw gizmos. Shows server position and client position")]
    public bool showGizmos = false;

    [Tooltip("Physics components are moved onto a ghost object beyond this threshold. Main object visually interpolates to it.")]
    public float gizmoVelocityThreshold = 0.1f;

    [Tooltip("Performance optimization: only draw gizmos at an interval.")]
    public int drawGizmosEveryNthFrame = 4;
    public Color serverColor = Color.red;
    public Color clientColor = Color.red;

#endregion

#region PRIVATE
    //Cached Values
    private Transform tf; // this component is performance critical. cache .transform getter!
    private Rigidbody rigid;

#endregion

#region GETTERS;

    protected override Vector3 currentPosition {
        get{
            return tf.position;
        } 
        set{
            rigid.position = value;
        }
    }
    protected override Vector3 currentVelocity {
        get{
            return rigid.velocity;
        } 
        set{
            // projects may keep Rigidbodies as kinematic sometimes. in that case, setting velocity would log an error
            if(!rigid.isKinematic){
                rigid.velocity = value;
            }
        }
    }

    protected override PredictedCharacterState CreateCurrentState(){
            // grab current position/rotation/velocity only once.
            // this is performance critical, avoid calling .transform multiple times.
            Vector3 currentPosition = tf.position;//GetPos(out Vector3 currentPosition, out Quaternion currentRotation); // faster than accessing .position + .rotation manually
            Vector3 currentVelocity = movement.rigidbody.velocity;

            // create state to insert
            return movement.GetState();
            // PredictedCharacterState state = new PredictedCharacterState(
            //     predictedTime,
            //     positionDelta,
            //     currentPosition,
            //     rotationDelta,
            //     currentRotation,
            //     velocityDelta,
            //     currentVelocity,
            //     angularVelocityDelta,
            //     currentAngularVelocity
            // );
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

    protected override bool CanReceiveServerState(PredictedCharacterState serverState){
        //Render server state
        if(this.showGizmos){
            GizmoUtils.DrawBox(serverState.position + new Vector3(0,movement.currentCharacterHeight/2f, 0), Quaternion.identity, 
                new Vector3(movement.characterRadius, movement.currentCharacterHeight, movement.characterRadius), serverColor);
        }
        return true;
    }

    protected override bool NeedsCorrection(PredictedCharacterState serverState, PredictedCharacterState interpolatedState){
        return false;
    }

    protected override void MoveTo(Vector3 newPosition, Vector3 newVelocity){
        // apply the state to the Rigidbody
        // The only smoothing we get is from Rigidbody.MovePosition.
        rigid.MovePosition(newPosition);

        // Set the velocity
        if (!rigid.isKinematic) {
            rigid.velocity = newVelocity;
        }
    }

#region 

#region UTIL
    
        // simple and slow version with MoveTowards, which recalculates delta and delta.sqrMagnitude:
        //   Vector3 newPosition = Vector3.MoveTowards(currentPosition, physicsPosition, positionStep * deltaTime);
        // faster version copied from MoveTowards:
        // this increases Prediction Benchmark Client's FPS from 615 -> 640.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector3 MoveTowardsCustom(
            Vector3 current,
            Vector3 target,
            Vector3 _delta,     // pass this in since we already calculated it
            float _sqrDistance, // pass this in since we already calculated it
            float _distance,    // pass this in since we already calculated it
            float maxDistanceDelta) {
            if (_sqrDistance == 0.0 || maxDistanceDelta >= 0.0 && _sqrDistance <= maxDistanceDelta * maxDistanceDelta)
                return target;

            float distFactor = maxDistanceDelta / _distance; // unlike Vector3.MoveTowards, we only calculate this once
            return new Vector3(
                // current.x + (_delta.x / _distance) * maxDistanceDelta,
                // current.y + (_delta.y / _distance) * maxDistanceDelta,
                // current.z + (_delta.z / _distance) * maxDistanceDelta);
                current.x + _delta.x * distFactor,
                current.y + _delta.y * distFactor,
                current.z + _delta.z * distFactor);
        }

#endregion
}
