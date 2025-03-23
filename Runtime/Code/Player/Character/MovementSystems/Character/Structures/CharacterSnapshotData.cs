using System;
using Assets.Luau;
using Code.Network.StateSystem;
using Code.Network.StateSystem.Structures;
using Code.Player.Character.Net;
using Mirror.BouncyCastle.Asn1.X9;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character
{
    [LuauAPI]
    public class CharacterSnapshotData : StateSnapshot
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 impulseVelocity = Vector3.zero;
        public float currentSpeed;
        public float speedModifier = 1; // Not used yet
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
        public CharacterState state = CharacterState.Idle;
        public CharacterState prevState = CharacterState.Idle;
        public float timeSinceBecameGrounded;
        public float timeSinceWasGrounded;
        public float timeSinceJump;
        public Vector3 lookVector;
        public BinaryBlob customData;

        public override bool Compare<TSystem, TState, TInput>(NetworkedStateSystem<TSystem, TState, TInput> system, TState snapshot)
        {
            // TODO: could probably be optimized?
            if (system.GetType() != typeof(CharacterMovement))
            {
                Debug.LogWarning("Character snapshot comparison did not receive the correct state system. Report this.");
                return false;
            }

            if (snapshot is not CharacterSnapshotData other)
            {
                Debug.LogWarning("Character snapshot comparison did not receive the correct snapshot type. Report this.");
                return false;
            }

            string message = "";
            
            if (this.lastProcessedCommand != other.lastProcessedCommand)
                message += $"lastProcessedCommand: {this.lastProcessedCommand} != {other.lastProcessedCommand}\n";
            if (this.position != other.position) message += $"position: {this.position} != {other.position}\n";
            if (this.velocity != other.velocity) message += $"velocity: {this.velocity} != {other.velocity}\n";
            if (this.currentSpeed != other.currentSpeed) message += $"currentSpeed: {this.currentSpeed} != {other.currentSpeed}\n";
            if (this.speedModifier != other.speedModifier)
                message += $"speedModifier: {this.speedModifier} != {other.speedModifier}";
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
            
            var same =  this.lastProcessedCommand == other.lastProcessedCommand && this.position == other.position &&
                        this.velocity == other.velocity && this.currentSpeed == other.currentSpeed && this.speedModifier == other.speedModifier &&
                        inputDisabled == other.inputDisabled
                        && isFlying == other.isFlying && isSprinting == other.isSprinting && jumpCount == other.jumpCount &&
                        airborneFromImpulse == other.airborneFromImpulse &&
                        alreadyJumped == other.alreadyJumped && prevMoveDir == other.prevMoveDir &&
                        lastGroundedMoveDir == other.lastGroundedMoveDir
                        && isCrouching == other.isCrouching && animGrounded == other.animGrounded && prevStepUp == other.prevStepUp &&
                        isGrounded == other.isGrounded && state == other.state
                        && prevState == other.prevState;

            if (same)
            {
                var movement = system as CharacterMovement;
                movement.compareResult = true;
                movement.FireTsCompare(this, other);
                same = movement.compareResult;
                if (same == false) message += $"customData: a != b";
            }
            
            if (message.Length != 0) Debug.Log(message.TrimEnd());

            return same;
        }

        public void CopyFrom(CharacterSnapshotData copySnapshot)
        {
            this.time = copySnapshot.time;
            this.lastProcessedCommand = copySnapshot.lastProcessedCommand;
            this.position = copySnapshot.position;
            this.velocity = copySnapshot.velocity;
            this.currentSpeed = copySnapshot.currentSpeed;
            this.speedModifier = copySnapshot.speedModifier;
            this.impulseVelocity = copySnapshot.impulseVelocity;
            this.timeSinceJump = copySnapshot.timeSinceJump;
            this.timeSinceWasGrounded = copySnapshot.timeSinceWasGrounded;
            this.timeSinceBecameGrounded = copySnapshot.timeSinceBecameGrounded;
            this.prevState = copySnapshot.prevState;
            this.state = copySnapshot.state;
            this.isGrounded = copySnapshot.isGrounded;
            this.animGrounded = copySnapshot.animGrounded;
            this.prevStepUp = copySnapshot.prevStepUp;
            this.isCrouching = copySnapshot.isCrouching;
            this.isSprinting = copySnapshot.isSprinting;
            this.lastGroundedMoveDir = copySnapshot.lastGroundedMoveDir;
            this.prevMoveDir = copySnapshot.prevMoveDir;
            this.alreadyJumped = copySnapshot.alreadyJumped;
            this.airborneFromImpulse = copySnapshot.airborneFromImpulse;
            this.jumpCount = copySnapshot.jumpCount;
            this.isFlying = copySnapshot.isFlying;
            this.inputDisabled = copySnapshot.inputDisabled;
            this.lookVector = copySnapshot.lookVector;
            this.customData = copySnapshot.customData != null
                ? new BinaryBlob()
                {
                    m_dataSize = copySnapshot.customData.m_dataSize,
                    m_data = (byte[])copySnapshot.customData.m_data.Clone(),
                }
                : default;
        }

        public override string ToString()
        {
            return "Pos: " + this.position + " Vel: " + velocity;
        }

        public override object Clone()
        {
            return new CharacterSnapshotData()
            {
                time = time,
                lastProcessedCommand = lastProcessedCommand,
                position = position,
                velocity = velocity,
                currentSpeed = currentSpeed,
                speedModifier = speedModifier,
                impulseVelocity =  impulseVelocity,
                timeSinceJump = timeSinceJump,
                timeSinceWasGrounded = timeSinceWasGrounded,
                timeSinceBecameGrounded = timeSinceBecameGrounded,
                prevState = prevState,
                state = state,
                isGrounded = isGrounded,
                animGrounded = animGrounded,
                prevStepUp = prevStepUp,
                isCrouching = isCrouching,
                isSprinting = isSprinting,
                lastGroundedMoveDir = lastGroundedMoveDir,
                prevMoveDir = prevMoveDir,
                alreadyJumped = alreadyJumped,
                airborneFromImpulse = airborneFromImpulse,
                jumpCount = jumpCount,
                isFlying = isFlying,
                inputDisabled = inputDisabled,
                lookVector = lookVector,
                customData = customData != null ? new BinaryBlob()
                {
                    m_dataSize = customData.m_dataSize,
                    m_data = (byte[]) customData.m_data.Clone(),
                } : default,
            };
        }
    }
}