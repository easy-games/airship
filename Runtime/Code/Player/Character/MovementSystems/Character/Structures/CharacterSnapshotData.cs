using System;
using Assets.Luau;
using Code.Network.StateSystem;
using Code.Network.StateSystem.Structures;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character
{
    [LuauAPI]
    public class CharacterSnapshotData : StateSnapshot
    {
        public bool inputDisabled;
        public bool isFlying;
        public bool isSprinting;
        public bool airborneFromImpulse;
        public bool alreadyJumped;
        public bool isCrouching;
        public bool prevStepUp;
        public bool isGrounded;
        
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 lookVector;
        
        public float currentSpeed;
        public float speedModifier = 1; // Not used yet
        public byte jumpCount;
        public CharacterState state = CharacterState.Idle;
        public float timeSinceBecameGrounded;
        public float timeSinceWasGrounded;
        public float timeSinceJump;
    
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
            
            var lastProcessedCommandEqual = this.lastProcessedCommand == other.lastProcessedCommand;
            var positionEqual = this.position == other.position;
            var velocityEqual = this.velocity == other.velocity;
            var currentSpeedEqual = CharacterSnapshotDataSerializer.CompressToShort(this.currentSpeed) == CharacterSnapshotDataSerializer.CompressToShort(other.currentSpeed);
            var speedModifierEqual = CharacterSnapshotDataSerializer.CompressToShort(this.speedModifier) == CharacterSnapshotDataSerializer.CompressToShort(other.speedModifier);
            var inputDisabledEqual = inputDisabled == other.inputDisabled;
            var isFlyingEqual = isFlying == other.isFlying;
            var isSprintingEqual = isSprinting == other.isSprinting;
            var jumpCountEqual = jumpCount == other.jumpCount;
            var airborneFromImpulseEqual = airborneFromImpulse == other.airborneFromImpulse;
            var alreadyJumpedEqual = alreadyJumped == other.alreadyJumped;
            var isCrouchingEqual = isCrouching == other.isCrouching;
            var prevStepUpEqual = prevStepUp == other.prevStepUp;
            var isGroundedEqual = isGrounded == other.isGrounded;
            var stateEqual = state == other.state;

            if (!lastProcessedCommandEqual)
                message += $"lastProcessedCommand: {this.lastProcessedCommand} != {other.lastProcessedCommand}\n";
            if (!positionEqual)
                message += $"position: {this.position} != {other.position}\n";
            if (!velocityEqual)
                message += $"velocity: {this.velocity} != {other.velocity}\n";
            if (!currentSpeedEqual)
                message += $"currentSpeed: {this.currentSpeed} != {other.currentSpeed}\n";
            if (!speedModifierEqual)
                message += $"speedModifier: {this.speedModifier} != {other.speedModifier}\n";
            if (!inputDisabledEqual)
                message += $"inputDisabled: {inputDisabled} != {other.inputDisabled}\n";
            if (!isFlyingEqual)
                message += $"isFlying: {isFlying} != {other.isFlying}\n";
            if (!isSprintingEqual)
                message += $"isSprinting: {isSprinting} != {other.isSprinting}\n";
            if (!jumpCountEqual)
                message += $"jumpCount: {jumpCount} != {other.jumpCount}\n";
            if (!airborneFromImpulseEqual)
                message += $"airborneFromImpulse: {airborneFromImpulse} != {other.airborneFromImpulse}\n";
            if (!alreadyJumpedEqual)
                message += $"alreadyJumped: {alreadyJumped} != {other.alreadyJumped}\n";
            if (!isCrouchingEqual)
                message += $"prevCrouch: {isCrouching} != {other.isCrouching}\n";
            if (!prevStepUpEqual)
                message += $"prevStepUp: {prevStepUp} != {other.prevStepUp}\n";
            if (!isGroundedEqual)
                message += $"prevGrounded: {isGrounded} != {other.isGrounded}\n";
            if (!stateEqual)
                message += $"state: {state} != {other.state}\n";

            var same =
                lastProcessedCommandEqual &&
                positionEqual &&
                velocityEqual &&
                currentSpeedEqual &&
                speedModifierEqual &&
                inputDisabledEqual &&
                isFlyingEqual &&
                isSprintingEqual &&
                jumpCountEqual &&
                airborneFromImpulseEqual &&
                alreadyJumpedEqual &&
                isCrouchingEqual &&
                prevStepUpEqual &&
                isGroundedEqual &&
                stateEqual;

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
            this.timeSinceJump = copySnapshot.timeSinceJump;
            this.timeSinceWasGrounded = copySnapshot.timeSinceWasGrounded;
            this.timeSinceBecameGrounded = copySnapshot.timeSinceBecameGrounded;
            this.state = copySnapshot.state;
            this.isGrounded = copySnapshot.isGrounded;
            this.prevStepUp = copySnapshot.prevStepUp;
            this.isCrouching = copySnapshot.isCrouching;
            this.isSprinting = copySnapshot.isSprinting;
            this.alreadyJumped = copySnapshot.alreadyJumped;
            this.airborneFromImpulse = copySnapshot.airborneFromImpulse;
            this.jumpCount = copySnapshot.jumpCount;
            this.isFlying = copySnapshot.isFlying;
            this.inputDisabled = copySnapshot.inputDisabled;
            this.lookVector = copySnapshot.lookVector;
            this.customData = copySnapshot.customData != null
                ? new BinaryBlob()
                {
                    dataSize = copySnapshot.customData.dataSize,
                    data = (byte[])copySnapshot.customData.data.Clone(),
                }
                : default;
        }

        public override string ToString()
        {
            return "Last cmd#" + this.lastProcessedCommand + " time: " + this.time + "Pos: " + this.position + " Vel: " + velocity;
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
                timeSinceJump = timeSinceJump,
                timeSinceWasGrounded = timeSinceWasGrounded,
                timeSinceBecameGrounded = timeSinceBecameGrounded,
                state = state, 
                isGrounded = isGrounded,
                prevStepUp = prevStepUp,
                isCrouching = isCrouching,
                isSprinting = isSprinting,
                alreadyJumped = alreadyJumped,
                airborneFromImpulse = airborneFromImpulse,
                jumpCount = jumpCount,
                isFlying = isFlying,
                inputDisabled = inputDisabled,
                lookVector = lookVector,
                customData = customData != null ? new BinaryBlob()
                {
                    dataSize = customData.dataSize,
                    data = (byte[]) customData.data.Clone(),
                } : default,
            };
        }
    }

    public static class CharacterSnapshotDataSerializer {

        public static ushort CompressToUshort(float value) {
            return (ushort)Math.Clamp(value * 1000f, 0, ushort.MaxValue);
        }
        
        public static short CompressToShort(float value) {
            return (short)Math.Clamp(value * 1000f, short.MinValue, short.MaxValue);
        }

        public static float DecompressUShort(ushort value) {
            return value / 1000f;
        }

        public static float DecompressShort(short value) {
            return value / 1000f;
        }
        
        public static void WriteCharacterSnapshotData(this NetworkWriter writer, CharacterSnapshotData value) {
            byte bools = 0;
            SetBit(ref bools, 0, value.inputDisabled);
            SetBit(ref bools, 1, value.isFlying);
            SetBit(ref bools, 2, value.isSprinting);
            SetBit(ref bools, 3, value.airborneFromImpulse);
            SetBit(ref bools, 4, value.alreadyJumped);
            SetBit(ref bools, 5, value.isCrouching);
            SetBit(ref bools, 6, value.prevStepUp);
            SetBit(ref bools, 7, value.isGrounded);
            writer.Write(bools);

            if (value.customData != null) {
                writer.WriteInt(value.customData.dataSize);
                writer.WriteBytes(value.customData.data, 0, value.customData.data.Length);
            }
            else {
                writer.WriteInt(0);
            }
            
            writer.Write(value.time);
            writer.Write(value.lastProcessedCommand);
            writer.Write(value.position);
            writer.Write(value.velocity);
            writer.Write(CompressToShort(value.lookVector.x));
            writer.Write(CompressToShort(value.lookVector.y));
            writer.Write(CompressToShort(value.lookVector.z));
            writer.Write(value.currentSpeed);
            // This makes our max speed modifier 65.535 with a 0.001 precision.
            writer.Write(CompressToUshort(value.speedModifier));
            writer.Write((byte) Math.Min(Math.Floor(value.timeSinceJump / Time.fixedDeltaTime), 255));
            writer.Write((byte) Math.Min(Math.Floor(value.timeSinceWasGrounded / Time.fixedDeltaTime), 255));
            writer.Write((byte) Math.Min(Math.Floor(value.timeSinceBecameGrounded / Time.fixedDeltaTime), 255));
            writer.Write((byte) value.state);
            writer.Write(value.jumpCount);
        }

        public static CharacterSnapshotData ReadCharacterSnapshotData(this NetworkReader reader) {
            var bools = reader.Read<byte>();
            var customDataSize = reader.ReadInt();
            BinaryBlob customData = default;
            if (customDataSize != 0) {
                var customDataArray = reader.ReadBytes(customDataSize);
                customData = new BinaryBlob(customDataArray);
            }
            return new CharacterSnapshotData() {
                inputDisabled = GetBit(bools, 0),
                isFlying = GetBit(bools, 1),
                isSprinting = GetBit(bools, 2),
                airborneFromImpulse = GetBit(bools, 3),
                alreadyJumped = GetBit(bools, 4),
                isCrouching = GetBit(bools, 5),
                prevStepUp = GetBit(bools, 6),
                isGrounded = GetBit(bools, 7),
                time = reader.Read<double>(),
                lastProcessedCommand = reader.Read<int>(),
                position = reader.Read<Vector3>(),
                velocity = reader.Read<Vector3>(),
                lookVector = new Vector3(
                    DecompressShort(reader.Read<short>()), 
                    DecompressShort(reader.Read<short>()), 
                    DecompressShort(reader.Read<short>())),
                currentSpeed = reader.Read<float>(),
                speedModifier = DecompressUShort(reader.Read<ushort>()),
                timeSinceJump = reader.Read<byte>() * Time.fixedDeltaTime,
                timeSinceWasGrounded = reader.Read<byte>() * Time.fixedDeltaTime,
                timeSinceBecameGrounded = reader.Read<byte>() * Time.fixedDeltaTime,
                state = (CharacterState) reader.Read<byte>(),
                jumpCount = reader.Read<byte>(),
                customData = customData,
            };
        }
        
        private static bool GetBit(byte bools, int bit) => (bools & (1 << bit)) != 0;

        private static void SetBit(ref byte bools, int bit, bool value)
        {
            if (value)
                bools |= (byte)(1 << bit);
            else
                bools &= (byte)~(1 << bit);
        }
    }
}