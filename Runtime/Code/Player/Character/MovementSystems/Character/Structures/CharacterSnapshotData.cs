using System;
using System.IO;
using Assets.Luau;
using Code.Misc;
using Code.Network.StateSystem;
using Code.Network.StateSystem.Structures;
using Force.Crc32;
using JetBrains.Annotations;
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

        // Cached for reuse. This assumes the fields on the snapshot will never change. (Which should be the case)
        // Cloning will not clone this field, so you can safely clone then modify and existing snapshot and regenerate the crc32
        private uint _crc32;

        public override bool Compare<TSystem, TState, TDiff, TInput>(NetworkedStateSystem<TSystem, TState, TDiff, TInput> system, TState snapshot)
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
            return
                $"Time: {time}\n" +
                $"LastProcessedCommand: {lastProcessedCommand}\n" +
                $"Position: {position}\n" +
                $"Velocity: {velocity}\n" +
                $"CurrentSpeed: {currentSpeed}\n" +
                $"SpeedModifier: {speedModifier} ({CharacterSnapshotDataSerializer.CompressToUshort(speedModifier)})\n" +
                $"TimeSinceJump: {timeSinceJump} ({(byte)Math.Min(Math.Floor(timeSinceJump / Time.fixedDeltaTime), 255)})\n" +
                $"TimeSinceWasGrounded: {timeSinceWasGrounded} ({(byte)Math.Min(Math.Floor(timeSinceWasGrounded / Time.fixedDeltaTime), 255)})\n" +
                $"TimeSinceBecameGrounded: {timeSinceBecameGrounded} ({(byte)Math.Min(Math.Floor(timeSinceBecameGrounded / Time.fixedDeltaTime), 255)})\n" +
                $"State: {state}\n" +
                $"IsGrounded: {isGrounded}\n" +
                $"PrevStepUp: {prevStepUp}\n" +
                $"IsCrouching: {isCrouching}\n" +
                $"IsSprinting: {isSprinting}\n" +
                $"AlreadyJumped: {alreadyJumped}\n" +
                $"AirborneFromImpulse: {airborneFromImpulse}\n" +
                $"JumpCount: {jumpCount}\n" +
                $"IsFlying: {isFlying}\n" +
                $"InputDisabled: {inputDisabled}\n" +
                $"LookVector: {lookVector} ({CharacterSnapshotDataSerializer.CompressToShort(lookVector.x)}, {CharacterSnapshotDataSerializer.CompressToShort(lookVector.y)}, {CharacterSnapshotDataSerializer.CompressToShort(lookVector.z)})\n" +
                $"CustomData: {(customData != null ? $"Size: {customData.dataSize}" : "null")}";
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
        
        public override StateDiff CreateDiff<TState>(TState snapshot) {
            if (snapshot is not CharacterSnapshotData other) {
                throw new Exception("Invalid snapshot for diff generation.");
            }

            // Determine which fields changed
            byte oldBools = 0;
            byte newBools = 0;
            CharacterSnapshotDataSerializer.EncodeBools(ref oldBools, this);
            CharacterSnapshotDataSerializer.EncodeBools(ref newBools, other);
            bool boolsChanged = oldBools != newBools;
            bool positionChanged = this.position != other.position;
            bool velocityChanged = this.velocity != other.velocity;
            bool lookVectorChanged = this.lookVector != other.lookVector;
            bool speedChanged = this.currentSpeed != other.currentSpeed;
            bool modifierChanged = this.speedModifier != other.speedModifier;
            bool jumpCountChanged = this.jumpCount != other.jumpCount;
            bool stateChanged = this.state != other.state;
            bool becameGroundedChanged = this.timeSinceBecameGrounded != other.timeSinceBecameGrounded;
            bool wasGroundedChanged = this.timeSinceWasGrounded != other.timeSinceWasGrounded;
            bool timeSinceJumpChanged = this.timeSinceJump != other.timeSinceJump;
            
            // Set the changed mask to reflect changed fields
            short changedMask = 0;
            if (boolsChanged) BitUtil.SetBit(ref changedMask, 0, true);
            if (positionChanged) BitUtil.SetBit(ref changedMask, 1, true);
            if (velocityChanged) BitUtil.SetBit(ref changedMask, 2, true);
            if (lookVectorChanged) BitUtil.SetBit(ref changedMask, 3, true);
            if (speedChanged) BitUtil.SetBit(ref changedMask, 4, true);
            if (modifierChanged) BitUtil.SetBit(ref changedMask, 5, true);
            if (jumpCountChanged) BitUtil.SetBit(ref changedMask, 6, true);
            if (stateChanged) BitUtil.SetBit(ref changedMask, 7, true);
            if (becameGroundedChanged) BitUtil.SetBit(ref changedMask, 8, true);
            if (wasGroundedChanged) BitUtil.SetBit(ref changedMask, 9, true);
            if (timeSinceJumpChanged) BitUtil.SetBit(ref changedMask, 10, true);

            // Write only changed fields
            var writer = new NetworkWriter();
            writer.Write(other.time);
            writer.Write(other.lastProcessedCommand);
            writer.Write(changedMask);
            if (boolsChanged) writer.Write(newBools);
            if (positionChanged) writer.Write(other.position);
            if (velocityChanged) writer.Write(other.velocity);
            if (lookVectorChanged) {
                writer.Write(CharacterSnapshotDataSerializer.CompressToShort(other.lookVector.x));
                writer.Write(CharacterSnapshotDataSerializer.CompressToShort(other.lookVector.y));
                writer.Write(CharacterSnapshotDataSerializer.CompressToShort(other.lookVector.z));
            }
            if (speedChanged) writer.Write(other.currentSpeed);
            if (modifierChanged) writer.Write(CharacterSnapshotDataSerializer.CompressToUshort(other.speedModifier));
            if (jumpCountChanged) writer.Write(other.jumpCount);
            if (stateChanged) writer.Write((byte)other.state);
            if (becameGroundedChanged) writer.Write((byte)Math.Min(Math.Floor(other.timeSinceBecameGrounded / Time.fixedDeltaTime), 255));
            if (wasGroundedChanged) writer.Write((byte)Math.Min(Math.Floor(other.timeSinceWasGrounded / Time.fixedDeltaTime), 255));
            if (timeSinceJumpChanged) writer.Write((byte)Math.Min(Math.Floor(other.timeSinceJump / Time.fixedDeltaTime), 255));

            // Always write custom data. TODO: we will want to apply a diffing algorithm to the byte array
            if (other.customData != null) {
                writer.WriteInt(other.customData.dataSize);
                writer.WriteBytes(other.customData.data, 0, other.customData.data.Length);
            }
            else {
                writer.WriteInt(0);
            }

            return new CharacterStateDiff {
                baseTime = time, // The base is the instance CreateDiff is being called on, so use our instance time value as the base time.
                crc32 = other.ComputeCrc32(),
                data = writer.ToArray()
            };
        }

        /// <summary>
        /// Attempts to apply a diff to this snapshot to generate a new snapshot. Returns null if
        /// the diff cannot be correctly applied to this snapshot
        /// </summary>
        /// <param name="diff"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public override StateSnapshot ApplyDiff(StateDiff diff) {
            if (diff is not CharacterStateDiff stateDiff) {
                throw new Exception("Invalid snapshot for applying diff.");
            }

            if (time != diff.baseTime) {
                // We return null here since we are essentially unable to construct a correct snapshot from the provided diff
                // using this snapshot as the base.
                Debug.LogWarning("Snapshot diff was applied to the wrong base snapshot. Report this.");
                return null;
            }

            var reader = new NetworkReader(stateDiff.data);
            var snapshot = (CharacterSnapshotData) this.Clone();

            snapshot.time = reader.Read<double>();
            snapshot.lastProcessedCommand = reader.Read<int>();
            var changedMask = reader.Read<short>();

            if (BitUtil.GetBit(changedMask, 0)) {
                byte bools = reader.Read<byte>();
                snapshot.inputDisabled = BitUtil.GetBit(bools, 0);
                snapshot.isFlying = BitUtil.GetBit(bools, 1);
                snapshot.isSprinting = BitUtil.GetBit(bools, 2);
                snapshot.airborneFromImpulse = BitUtil.GetBit(bools, 3);
                snapshot.alreadyJumped = BitUtil.GetBit(bools, 4);
                snapshot.isCrouching = BitUtil.GetBit(bools, 5);
                snapshot.prevStepUp = BitUtil.GetBit(bools, 6);
                snapshot.isGrounded = BitUtil.GetBit(bools, 7);
            }
            if (BitUtil.GetBit(changedMask, 1)) snapshot.position = reader.Read<Vector3>();
            if (BitUtil.GetBit(changedMask, 2)) snapshot.velocity = reader.Read<Vector3>();
            if (BitUtil.GetBit(changedMask, 3)) {
                snapshot.lookVector = new Vector3(
                    CharacterSnapshotDataSerializer.DecompressShort(reader.Read<short>()),
                    CharacterSnapshotDataSerializer.DecompressShort(reader.Read<short>()),
                    CharacterSnapshotDataSerializer.DecompressShort(reader.Read<short>())
                );
            }
            if (BitUtil.GetBit(changedMask, 4)) snapshot.currentSpeed = reader.Read<float>();
            if (BitUtil.GetBit(changedMask, 5)) snapshot.speedModifier = CharacterSnapshotDataSerializer.DecompressUShort(reader.Read<ushort>());
            if (BitUtil.GetBit(changedMask, 6)) snapshot.jumpCount = reader.Read<byte>();
            if (BitUtil.GetBit(changedMask, 7)) snapshot.state = (CharacterState)reader.Read<byte>();
            if (BitUtil.GetBit(changedMask, 8)) snapshot.timeSinceBecameGrounded = reader.Read<byte>() * Time.fixedDeltaTime;
            if (BitUtil.GetBit(changedMask, 9)) snapshot.timeSinceWasGrounded = reader.Read<byte>() * Time.fixedDeltaTime;
            if (BitUtil.GetBit(changedMask, 10)) snapshot.timeSinceJump = reader.Read<byte>() * Time.fixedDeltaTime;
                
            int size = reader.ReadInt();
            if (size != 0) {
                snapshot.customData = new BinaryBlob(reader.ReadBytes(size));
            }
            else {
                snapshot.customData = default;
            }

            var crc32 = snapshot.ComputeCrc32();
            if (crc32 != diff.crc32) {
                // We return null here since we are essentially unable to construct a correct snapshot from the provided diff
                // using this snapshot as the base.
                Debug.LogWarning("Applying diff failed CRC check. This may happen due to poor network connection. Expected " + diff.crc32 + ", got " + crc32);
                return null;
            }

            return snapshot;
        }

        public uint ComputeCrc32() {
            if (_crc32 != 0) return _crc32;
            var writer = new NetworkWriter();
            CharacterSnapshotDataSerializer.WriteCharacterSnapshotData(writer, this);
            var bytes = writer.ToArray();
            _crc32 = Crc32Algorithm.Compute(bytes);
            Debug.Log("Computed CRC32 for time " + time + ": " + _crc32);
            Debug.Log(this);
            return _crc32;
        }
    }

    public static class CharacterSnapshotDataSerializer {

        public static void EncodeBools(ref byte bools, CharacterSnapshotData value) {
            BitUtil.SetBit(ref bools, 0, value.inputDisabled);
            BitUtil.SetBit(ref bools, 1, value.isFlying);
            BitUtil.SetBit(ref bools, 2, value.isSprinting);
            BitUtil.SetBit(ref bools, 3, value.airborneFromImpulse);
            BitUtil.SetBit(ref bools, 4, value.alreadyJumped);
            BitUtil.SetBit(ref bools, 5, value.isCrouching);
            BitUtil.SetBit(ref bools, 6, value.prevStepUp);
            BitUtil.SetBit(ref bools, 7, value.isGrounded);
        }
        
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
            EncodeBools(ref bools, value);
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
                inputDisabled = BitUtil.GetBit(bools, 0),
                isFlying = BitUtil.GetBit(bools, 1),
                isSprinting = BitUtil.GetBit(bools, 2),
                airborneFromImpulse = BitUtil.GetBit(bools, 3),
                alreadyJumped = BitUtil.GetBit(bools, 4),
                isCrouching = BitUtil.GetBit(bools, 5),
                prevStepUp = BitUtil.GetBit(bools, 6),
                isGrounded = BitUtil.GetBit(bools, 7),
                
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
    }
}