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
        public bool inputDisabled;
        public bool isFlying;
        public int jumpCount;
        public bool airborneFromImpulse;
        public bool alreadyJumped; //Only lets the character jump once until the jump key is released
        public Vector3 prevMoveDir; //Only used for firing an event

        public Vector3
            lastGroundedMoveDir; //What direction were we moving on the ground, so we can float that dir in the air

        public bool prevCrouch;
        public bool prevStepUp;
        public bool prevGrounded;
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

            string message = "Mismatched fields:\n";

            if (this.lastProcessedCommand != other.lastProcessedCommand)
                message += $"lastProcessedCommand: {this.lastProcessedCommand} != {other.lastProcessedCommand}\n";
            if (this.position != other.position) message += $"position: {this.position} != {other.position}\n";
            if (this.velocity != other.velocity) message += $"velocity: {this.velocity} != {other.velocity}\n";
            if (inputDisabled != other.inputDisabled)
                message += $"inputDisabled: {inputDisabled} != {other.inputDisabled}\n";
            if (isFlying != other.isFlying) message += $"isFlying: {isFlying} != {other.isFlying}\n";
            if (jumpCount != other.jumpCount) message += $"jumpCount: {jumpCount} != {other.jumpCount}\n";
            if (airborneFromImpulse != other.airborneFromImpulse)
                message += $"airborneFromImpulse: {airborneFromImpulse} != {other.airborneFromImpulse}\n";
            if (alreadyJumped != other.alreadyJumped)
                message += $"alreadyJumped: {alreadyJumped} != {other.alreadyJumped}\n";
            if (prevMoveDir != other.prevMoveDir) message += $"prevMoveDir: {prevMoveDir} != {other.prevMoveDir}\n";
            if (lastGroundedMoveDir != other.lastGroundedMoveDir)
                message += $"lastGroundedMoveDir: {lastGroundedMoveDir} != {other.lastGroundedMoveDir}\n";
            if (prevCrouch != other.prevCrouch) message += $"prevCrouch: {prevCrouch} != {other.prevCrouch}\n";
            if (prevStepUp != other.prevStepUp) message += $"prevStepUp: {prevStepUp} != {other.prevStepUp}\n";
            if (prevGrounded != other.prevGrounded)
                message += $"prevGrounded: {prevGrounded} != {other.prevGrounded}\n";
            if (state != other.state) message += $"state: {state} != {other.state}\n";
            if (prevState != other.prevState) message += $"prevState: {prevState} != {other.prevState}\n";

            Debug.Log(message.TrimEnd());

            return this.lastProcessedCommand == other.lastProcessedCommand && this.position == other.position &&
                   this.velocity == other.velocity &&
                   inputDisabled == other.inputDisabled
                   && isFlying == other.isFlying && jumpCount == other.jumpCount &&
                   airborneFromImpulse == other.airborneFromImpulse &&
                   alreadyJumped == other.alreadyJumped && prevMoveDir == other.prevMoveDir &&
                   lastGroundedMoveDir == other.lastGroundedMoveDir
                   && prevCrouch == other.prevCrouch && prevStepUp == other.prevStepUp &&
                   prevGrounded == other.prevGrounded && state == other.state
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
            this.lookVector = copyState.lookVector;
            this.customData = copyState.customData;
        }
    }
}