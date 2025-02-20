using System;
using Assets.Luau;
using Code.Player.Character.Net;
using Mirror.BouncyCastle.Asn1.X9;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    [LuauAPI]
    public class BasicCharacterMovementState : StateSnapshot
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 impulseVelocity = Vector3.zero;
        public float currentSpeed;
        public bool inputDisabled;
        public bool isFlying;
        public bool isSprinting;
        public int jumpCount;
        public bool airborneFromImpulse;
        public bool alreadyJumped; //Only lets the character jump once until the jump key is released
        public Vector3 prevMoveDir; //Only used for firing an event

        public Vector3
            lastGroundedMoveDir; //What direction were we moving on the ground, so we can float that dir in the air

        public bool isCrouching;
        public bool prevStepUp;
        public bool isGrounded;
        public bool animGrounded;
        public BasicCharacterState state = BasicCharacterState.Idle;
        public BasicCharacterState prevState = BasicCharacterState.Idle;
        public float timeSinceBecameGrounded;
        public float timeSinceWasGrounded;
        public float timeSinceJump;
        public Vector3 lookVector;
        public BinaryBlob customData;

        public override bool CompareWithMargin(float margin, StateSnapshot snapshot)
        {
            // TODO: could probably be optimized?
            if (snapshot is not BasicCharacterMovementState other) return false;

            string message = "";

            if (this.lastProcessedCommand != other.lastProcessedCommand)
                message += $"lastProcessedCommand: {this.lastProcessedCommand} != {other.lastProcessedCommand}\n";
            if (this.position != other.position) message += $"position: {this.position} != {other.position}\n";
            if (this.velocity != other.velocity) message += $"velocity: {this.velocity} != {other.velocity}\n";
            if (this.currentSpeed != other.currentSpeed) message += $"currentSpeed: {this.currentSpeed} != {other.currentSpeed}\n";
            if (this.impulseVelocity != other.impulseVelocity) message += $"impulseVelocity: {this.impulseVelocity} != {other.impulseVelocity}\n";
            if (inputDisabled != other.inputDisabled)
                message += $"inputDisabled: {inputDisabled} != {other.inputDisabled}\n";
            if (isFlying != other.isFlying) message += $"isFlying: {isFlying} != {other.isFlying}\n";
            if (isSprinting != other.isSprinting) message += $"isSprinting: {isSprinting} != {other.isSprinting}\n";
            if (jumpCount != other.jumpCount) message += $"jumpCount: {jumpCount} != {other.jumpCount}\n";
            if (airborneFromImpulse != other.airborneFromImpulse)
                message += $"airborneFromImpulse: {airborneFromImpulse} != {other.airborneFromImpulse}\n";
            if (alreadyJumped != other.alreadyJumped)
                message += $"alreadyJumped: {alreadyJumped} != {other.alreadyJumped}\n";
            if (prevMoveDir != other.prevMoveDir) message += $"prevMoveDir: {prevMoveDir} != {other.prevMoveDir}\n";
            if (lastGroundedMoveDir != other.lastGroundedMoveDir)
                message += $"lastGroundedMoveDir: {lastGroundedMoveDir} != {other.lastGroundedMoveDir}\n";
            if (isCrouching != other.isCrouching) message += $"prevCrouch: {isCrouching} != {other.isCrouching}\n";
            if (prevStepUp != other.prevStepUp) message += $"prevStepUp: {prevStepUp} != {other.prevStepUp}\n";
            if (isGrounded != other.isGrounded)
                message += $"prevGrounded: {isGrounded} != {other.isGrounded}\n";
            if (state != other.state) message += $"state: {state} != {other.state}\n";
            if (prevState != other.prevState) message += $"prevState: {prevState} != {other.prevState}\n";

            if (message.Length != 0) Debug.Log(message.TrimEnd());

            return this.lastProcessedCommand == other.lastProcessedCommand && this.position == other.position &&
                   this.velocity == other.velocity && this.currentSpeed == other.currentSpeed &&
                   inputDisabled == other.inputDisabled
                   && isFlying == other.isFlying && isSprinting == other.isSprinting && jumpCount == other.jumpCount &&
                   airborneFromImpulse == other.airborneFromImpulse &&
                   alreadyJumped == other.alreadyJumped && prevMoveDir == other.prevMoveDir &&
                   lastGroundedMoveDir == other.lastGroundedMoveDir
                   && isCrouching == other.isCrouching && animGrounded == other.animGrounded && prevStepUp == other.prevStepUp &&
                   isGrounded == other.isGrounded && state == other.state
                   && prevState == other.prevState;
            //&& timeSinceBecameGrounded == other.timeSinceBecameGrounded && timeSinceWasGrounded == other.timeSinceWasGrounded
            //&& timeSinceJump == other.timeSinceJump && customData.Equals(other.customData);
        }

        public void CopyFrom(BasicCharacterMovementState copyState)
        {
            this.time = copyState.time;
            this.lastProcessedCommand = copyState.lastProcessedCommand;
            this.position = copyState.position;
            this.velocity = copyState.velocity;
            this.currentSpeed = copyState.currentSpeed;
            this.impulseVelocity = copyState.impulseVelocity;
            this.timeSinceJump = copyState.timeSinceJump;
            this.timeSinceWasGrounded = copyState.timeSinceWasGrounded;
            this.timeSinceBecameGrounded = copyState.timeSinceBecameGrounded;
            this.prevState = copyState.prevState;
            this.state = copyState.state;
            this.isGrounded = copyState.isGrounded;
            this.animGrounded = copyState.animGrounded;
            this.prevStepUp = copyState.prevStepUp;
            this.isCrouching = copyState.isCrouching;
            this.isSprinting = copyState.isSprinting;
            this.lastGroundedMoveDir = copyState.lastGroundedMoveDir;
            this.prevMoveDir = copyState.prevMoveDir;
            this.alreadyJumped = copyState.alreadyJumped;
            this.airborneFromImpulse = copyState.airborneFromImpulse;
            this.jumpCount = copyState.jumpCount;
            this.isFlying = copyState.isFlying;
            this.inputDisabled = copyState.inputDisabled;
            this.lookVector = copyState.lookVector;
            this.customData = copyState.customData;
        }

        public override string ToString()
        {
            return "Pos: " + this.position + " Vel: " + velocity;
        }
    }
}