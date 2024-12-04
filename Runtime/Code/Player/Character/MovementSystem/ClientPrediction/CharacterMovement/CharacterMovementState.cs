using System;
using Assets.Luau;
using UnityEngine;

public enum CharacterState {
    Idle = 0,
    Running = 1,
    Airborne = 2,
    Sprinting = 3,
    Crouching = 4,
}

public class CharacterMovementState : AirshipPredictedState, IEquatable<CharacterMovementState>{
	public MoveInputData currentMoveInput = new MoveInputData();
    public bool inputDisabled = false;
    public bool isFlying = false;
    public int jumpCount = 0;
    public bool airborneFromImpulse = false;
    public bool alreadyJumped = false;//Only lets the character jump once until the jump key is released
    public Vector3 prevMoveDir;//Only used for fireing an event
    public Vector3 lastGroundedMoveDir = Vector3.zero;//What direction were we moving on the ground, so we can float that dir in the air
    public bool prevCrouch;
    public bool prevStepUp;
    public bool prevGrounded;
    public CharacterState state = CharacterState.Idle;
    public CharacterState prevState = CharacterState.Idle;
    public float timeSinceBecameGrounded;
    public float timeSinceWasGrounded;
    public float timeSinceJump;

    public CharacterMovementState(){
    }
    
    public CharacterMovementState(int tick, Vector3 pos, Vector3 vel){
        this.position = pos;
        this.velocity = vel;
        this.tick = tick;
    }

    public CharacterMovementState(CharacterMovementState copyState){
        this.CopyFrom(copyState);
    }

    public void CopyFrom(CharacterMovementState copyState){
        this.tick = copyState.tick;
        this.position = copyState.position;
        this.velocity = copyState.velocity;
        this.timeSinceJump = copyState.timeSinceJump;
        this.timeSinceWasGrounded = copyState.timeSinceWasGrounded;
        this.timeSinceBecameGrounded = copyState.timeSinceBecameGrounded;
        this.prevState = copyState.prevState;
        this.state = copyState.state;
        this.prevGrounded = copyState.prevGrounded;
        this.prevStepUp = copyState.prevStepUp;
        this.prevCrouch = copyState.prevCrouch;
        this.lastGroundedMoveDir = copyState.lastGroundedMoveDir;
        this.prevMoveDir = copyState.prevMoveDir;
        this.alreadyJumped = copyState.alreadyJumped;
        this.airborneFromImpulse = copyState.airborneFromImpulse;
        this.jumpCount = copyState.jumpCount;
        this.isFlying = copyState.isFlying;
        this.inputDisabled = copyState.inputDisabled;
        this.currentMoveInput = copyState.currentMoveInput;
    }

    // public override AirshipPredictedState Interpolate(AirshipPredictedState other, float delta) {
    //     return new CharacterMovementState(delta <=.5f ? this : (CharacterMovementState)other) { 
    //         position = Vector3.Lerp(this.position, other.position, delta), 
    //         velocity = Vector3.Lerp(this.velocity, other.velocity, delta)
    //     };
    // }

    public bool Equals(CharacterMovementState other) {
        return currentMoveInput.Equals(other.currentMoveInput) &&
            velocity == other.velocity &&
            position == other.position &&
            state == other.state &&
            inputDisabled == other.inputDisabled &&
            isFlying == other.isFlying &&
            jumpCount == other.jumpCount &&
            airborneFromImpulse == other.airborneFromImpulse &&
            alreadyJumped == other.alreadyJumped &&
            prevMoveDir == other.prevMoveDir;
    }
}