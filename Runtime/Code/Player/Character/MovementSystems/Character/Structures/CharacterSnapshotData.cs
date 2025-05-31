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
            
            if (this.lastProcessedCommand != other.lastProcessedCommand)
                message += $"lastProcessedCommand: {this.lastProcessedCommand} != {other.lastProcessedCommand}\n";
            if (this.position != other.position) message += $"position: {this.position} != {other.position}\n";
            if (this.velocity != other.velocity) message += $"velocity: {this.velocity} != {other.velocity}\n";
            if (this.currentSpeed != other.currentSpeed) message += $"currentSpeed: {this.currentSpeed} != {other.currentSpeed}\n";
            if (this.speedModifier != other.speedModifier)
                message += $"speedModifier: {this.speedModifier} != {other.speedModifier}";
            if (inputDisabled != other.inputDisabled)
                message += $"inputDisabled: {inputDisabled} != {other.inputDisabled}\n";
            if (isFlying != other.isFlying) message += $"isFlying: {isFlying} != {other.isFlying}\n";
            if (isSprinting != other.isSprinting) message += $"isSprinting: {isSprinting} != {other.isSprinting}\n";
            if (jumpCount != other.jumpCount) message += $"jumpCount: {jumpCount} != {other.jumpCount}\n";
            if (airborneFromImpulse != other.airborneFromImpulse)
                message += $"airborneFromImpulse: {airborneFromImpulse} != {other.airborneFromImpulse}\n";
            if (alreadyJumped != other.alreadyJumped)
                message += $"alreadyJumped: {alreadyJumped} != {other.alreadyJumped}\n";
            if (isCrouching != other.isCrouching) message += $"prevCrouch: {isCrouching} != {other.isCrouching}\n";
            if (prevStepUp != other.prevStepUp) message += $"prevStepUp: {prevStepUp} != {other.prevStepUp}\n";
            if (isGrounded != other.isGrounded)
                message += $"prevGrounded: {isGrounded} != {other.isGrounded}\n";
            if (state != other.state) message += $"state: {state} != {other.state}\n";
            
            var same =  this.lastProcessedCommand == other.lastProcessedCommand && this.position == other.position &&
                        this.velocity == other.velocity && this.currentSpeed == other.currentSpeed && this.speedModifier == other.speedModifier &&
                        inputDisabled == other.inputDisabled
                        && isFlying == other.isFlying && isSprinting == other.isSprinting && jumpCount == other.jumpCount &&
                        airborneFromImpulse == other.airborneFromImpulse &&
                        alreadyJumped == other.alreadyJumped
                        && isCrouching == other.isCrouching && prevStepUp == other.prevStepUp &&
                        isGrounded == other.isGrounded && state == other.state;

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
            
            writer.WriteInt(value.customData.dataSize);
            writer.WriteBytes(value.customData.data, 0, value.customData.data.Length);
            
            writer.Write(value.time);
            writer.Write(value.lastProcessedCommand);
            writer.Write(value.position);
            writer.Write(value.velocity);
            writer.Write((short)(value.lookVector.x * 1000f));
            writer.Write((short)(value.lookVector.y * 1000f));
            writer.Write((short)(value.lookVector.z * 1000f));
            writer.Write(value.currentSpeed);
            // This makes our max speed modifier 65.535 with a 0.001 precision.
            writer.Write((ushort) Math.Clamp(value.speedModifier * 1000f, 0, ushort.MaxValue));
            writer.Write((byte) Math.Min(Math.Floor(value.timeSinceJump / Time.fixedDeltaTime), 255));
            writer.Write((byte) Math.Min(Math.Floor(value.timeSinceWasGrounded / Time.fixedDeltaTime), 255));
            writer.Write((byte) Math.Min(Math.Floor(value.timeSinceBecameGrounded / Time.fixedDeltaTime), 255));
            writer.Write((byte) value.state);
            writer.Write(value.jumpCount);
        }

        public static CharacterSnapshotData ReadCharacterSnapshotData(this NetworkReader reader) {
            var bools = reader.Read<byte>();
            var customDataSize = reader.ReadInt();
            var customDataArray = reader.ReadBytes(customDataSize);
            var customData = new BinaryBlob(customDataArray);
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
                lookVector = new Vector3(reader.Read<short>() / 1000f, reader.Read<short>() / 1000f, reader.Read<short>() / 1000f),
                currentSpeed = reader.Read<float>(),
                speedModifier = reader.Read<ushort>() / 1000f,
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