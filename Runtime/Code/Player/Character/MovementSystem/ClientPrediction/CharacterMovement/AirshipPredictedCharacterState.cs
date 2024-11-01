using UnityEngine;

public enum CharacterState {
    Idle = 0,
    Running = 1,
    Airborne = 2,
    Sprinting = 3,
    Crouching = 4,
}

public class AirshipPredictedCharacterState : AirshipPredictionState{
	public MoveInputData currentMoveInput;
    public bool disableInput = false;
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

    public AirshipPredictedCharacterState(){
    }
    
    public AirshipPredictedCharacterState(double time, Vector3 pos, Vector3 vel){
        this.position = pos;
        this.velocity = vel;
        this.timestamp = time;
    }

    public override AirshipPredictionState Interpolate(AirshipPredictionState other, float delta) {
        //TODO: make actual interpolation here
        return other;
    }
}