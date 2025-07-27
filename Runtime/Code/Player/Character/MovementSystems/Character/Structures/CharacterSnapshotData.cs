using System;
using Assets.Luau;
using Code.Network.StateSystem;
using Code.Network.StateSystem.Structures;
using Code.Util;
using Force.Crc32;
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
        // > 0 means it is possible to jump. Value is the number of ticks they have left until they can no longer jump. byte.MaxValue when grounded.
        public byte canJump;
    
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
            
            // We do not compare time since it does not affect local client simulation
            var vectorTolerance = 0.01 * 0.01; // sqr since we use sqr magnitude
            var lastProcessedCommandEqual = this.lastProcessedCommand == other.lastProcessedCommand;
            var positionEqual = (this.position - other.position).sqrMagnitude < vectorTolerance;
            var velocityEqual =  (this.velocity - other.velocity).sqrMagnitude < vectorTolerance;
            var currentSpeedEqual = Math.Round(this.currentSpeed, 2) == Math.Round(other.currentSpeed, 2);
            var speedModifierEqual = NetworkSerializationUtil.CompressToUshort(this.speedModifier) == NetworkSerializationUtil.CompressToUshort(other.speedModifier);
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

            // if (!lastProcessedCommandEqual)
            //     message += $"lastProcessedCommand: {this.lastProcessedCommand} != {other.lastProcessedCommand}\n";
            // if (!positionEqual)
            //     message += $"position: {this.position} != {other.position}\n";
            // if (!velocityEqual)
            //     message += $"velocity: {this.velocity} != {other.velocity}\n";
            // if (!currentSpeedEqual)
            //     message += $"currentSpeed: {this.currentSpeed} != {other.currentSpeed}\n";
            // if (!speedModifierEqual)
            //     message += $"speedModifier: {this.speedModifier} != {other.speedModifier}\n";
            // if (!inputDisabledEqual)
            //     message += $"inputDisabled: {inputDisabled} != {other.inputDisabled}\n";
            // if (!isFlyingEqual)
            //     message += $"isFlying: {isFlying} != {other.isFlying}\n";
            // if (!isSprintingEqual)
            //     message += $"isSprinting: {isSprinting} != {other.isSprinting}\n";
            // if (!jumpCountEqual)
            //     message += $"jumpCount: {jumpCount} != {other.jumpCount}\n";
            // if (!airborneFromImpulseEqual)
            //     message += $"airborneFromImpulse: {airborneFromImpulse} != {other.airborneFromImpulse}\n";
            // if (!alreadyJumpedEqual)
            //     message += $"alreadyJumped: {alreadyJumped} != {other.alreadyJumped}\n";
            // if (!isCrouchingEqual)
            //     message += $"prevCrouch: {isCrouching} != {other.isCrouching}\n";
            // if (!prevStepUpEqual)
            //     message += $"prevStepUp: {prevStepUp} != {other.prevStepUp}\n";
            // if (!isGroundedEqual)
            //     message += $"prevGrounded: {isGrounded} != {other.isGrounded}\n";
            // if (!stateEqual)
            //     message += $"state: {state} != {other.state}\n";

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
                // if (same == false) message += $"customData: a != b";
            }
            
            // if (message.Length != 0) Debug.Log(message.TrimEnd());
            return same;
        }

        public void CopyFrom(CharacterSnapshotData copySnapshot) {
            this.time = copySnapshot.time;
            this.tick = copySnapshot.tick;
            this.lastProcessedCommand = copySnapshot.lastProcessedCommand;
            this.position = copySnapshot.position;
            this.velocity = copySnapshot.velocity;
            this.currentSpeed = copySnapshot.currentSpeed;
            this.speedModifier = copySnapshot.speedModifier;
            this.canJump = copySnapshot.canJump;
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
                $"Tick: {tick}\n" +
                $"LastProcessedCommand: {lastProcessedCommand}\n" +
                $"Position: {position}\n" +
                $"Velocity: {velocity}\n" +
                $"CurrentSpeed: {currentSpeed}\n" +
                $"SpeedModifier: {speedModifier} ({NetworkSerializationUtil.CompressToUshort(speedModifier)})\n" +
                $"CanJump: {canJump}\n" +
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
                $"LookVector: {lookVector} ({NetworkSerializationUtil.CompressToShort(lookVector.x)}, {NetworkSerializationUtil.CompressToShort(lookVector.y)}, {NetworkSerializationUtil.CompressToShort(lookVector.z)})\n" +
                $"CustomData: {(customData != null ? $"Size: {customData.dataSize}" : "null")}";
        }

        public override object Clone()
        {
            return new CharacterSnapshotData()
            {
                time = time,
                tick = tick,
                lastProcessedCommand = lastProcessedCommand,
                position = position,
                velocity = velocity,
                currentSpeed = currentSpeed,
                speedModifier = speedModifier,
                canJump = canJump,
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
            bool canJumpChanged = this.canJump != other.canJump;
            
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
            if (canJumpChanged) BitUtil.SetBit(ref changedMask, 8, true);

            // Write only changed fields
            var writer = NetworkWriterPool.Get();
            writer.Write(NetworkSerializationUtil.CompressToUshort(other.time - time));
            writer.Write((ushort)(other.tick - tick)); // We should send diffs far before 65,535 ticks have passed. 255 is a little too low if messing with time scale (ticks will skip in slow timescales)
            writer.Write((ushort)(other.lastProcessedCommand - lastProcessedCommand)); // same with commands (~1 processed per tick)
            writer.Write(changedMask);
            if (boolsChanged) writer.Write(newBools);
            if (positionChanged) writer.Write(other.position);
            if (velocityChanged) writer.Write(other.velocity);
            if (lookVectorChanged) {
                writer.Write(NetworkSerializationUtil.CompressToShort(other.lookVector.x));
                writer.Write(NetworkSerializationUtil.CompressToShort(other.lookVector.y));
                writer.Write(NetworkSerializationUtil.CompressToShort(other.lookVector.z));
            }
            if (speedChanged) writer.Write(other.currentSpeed);
            if (modifierChanged) writer.Write(NetworkSerializationUtil.CompressToUshort(other.speedModifier));
            if (jumpCountChanged) writer.Write(other.jumpCount);
            if (stateChanged) writer.Write((byte)other.state);
            if (canJumpChanged) writer.Write(other.canJump);

            // We are cheating here by only writing bytes at the end if we have custom data. We can do this because we know the expected size
            // of the above bytes and we know that a diff packet will only contain one diff. If we were to pass multiple diffs in a single packet,
            // we could not do this optimization since there would be no way to know where the next packet starts.
            if (customData != null) {
                var customDataDiff = customData.CreateDiff(other.customData);
                writer.WriteBytes(customDataDiff, 0, customDataDiff.Length);
            }

            var dataArray = writer.ToArray();
            NetworkWriterPool.Return(writer);
            
            return new CharacterStateDiff {
                baseTick = tick, // The base is the instance CreateDiff is being called on, so use our instance time value as the base time.
                crc32 = other.ComputeCrc32(),
                data = dataArray
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

            if (tick != diff.baseTick) {
                // We return null here since we are essentially unable to construct a correct snapshot from the provided diff
                // using this snapshot as the base.
                Debug.LogWarning($"Snapshot diff was applied to the wrong base snapshot. Report this. Diff base {diff.baseTick} was applied to {tick}");
                return null;
            }

            var reader = NetworkReaderPool.Get(stateDiff.data);
            var snapshot = (CharacterSnapshotData) this.Clone();

            snapshot.time = time + NetworkSerializationUtil.DecompressUShort(reader.Read<ushort>());
            snapshot.tick = tick + reader.Read<ushort>();
            snapshot.lastProcessedCommand = lastProcessedCommand + reader.Read<ushort>();
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
                    NetworkSerializationUtil.DecompressShort(reader.Read<short>()),
                    NetworkSerializationUtil.DecompressShort(reader.Read<short>()),
                    NetworkSerializationUtil.DecompressShort(reader.Read<short>())
                );
            }
            if (BitUtil.GetBit(changedMask, 4)) snapshot.currentSpeed = reader.Read<float>();
            if (BitUtil.GetBit(changedMask, 5)) snapshot.speedModifier = NetworkSerializationUtil.DecompressUShort(reader.Read<ushort>());
            if (BitUtil.GetBit(changedMask, 6)) snapshot.jumpCount = reader.Read<byte>();
            if (BitUtil.GetBit(changedMask, 7)) snapshot.state = (CharacterState)reader.Read<byte>();
            if (BitUtil.GetBit(changedMask, 8)) snapshot.canJump = reader.Read<byte>();
            
            if (reader.Remaining != 0) {
                var cDataDiff = reader.ReadBytes(reader.Remaining);
                snapshot.customData = customData.ApplyDiff(cDataDiff);
            }
            else {
                snapshot.customData = null;
            }
            
            NetworkReaderPool.Return(reader);

            var crc32 = snapshot.ComputeCrc32();
            if (crc32 != diff.crc32) {
                // We return null here since we are essentially unable to construct a correct snapshot from the provided diff
                // using this snapshot as the base.
                // Debug.LogWarning($"Applying diff failed CRC check. This may happen due to poor network conditions.");
                return null;
            }

            return snapshot;
        }

        public uint ComputeCrc32() {
            if (_crc32 != 0) return _crc32;
            
            // We serialize to a byte array for calculating the CRC32. We use slightly more lenient compression
            // on things like the vectors so that floating point errors don't cause the crc checks to fail.
            var writer = NetworkWriterPool.Get();
            byte bools = 0;
            CharacterSnapshotDataSerializer.EncodeBools(ref bools, this);
            writer.Write(bools);
            writer.Write(this.tick);
            writer.Write(this.lastProcessedCommand);
            
            // We don't include position and velocity because we send the full value for those instead of a diff. The 
            // floating point representation of those numbers also makes it challenging to get a consistent CRC.

            writer.Write(NetworkSerializationUtil.CompressToShort(this.lookVector.x));
            writer.Write(NetworkSerializationUtil.CompressToShort(this.lookVector.y));
            writer.Write(NetworkSerializationUtil.CompressToShort(this.lookVector.z));
            writer.Write(Math.Round(this.currentSpeed, 2));
            // This makes our max speed modifier 65.535 with a 0.001 precision.
            writer.Write(NetworkSerializationUtil.CompressToUshort(this.speedModifier));
            writer.Write(this.canJump);
            writer.Write((byte) this.state);
            writer.Write(this.jumpCount);
            if (this.customData != null) writer.Write(this.customData.data);
            var bytes = writer.ToArray();
            
            NetworkWriterPool.Return(writer);
            
            _crc32 = Crc32Algorithm.Compute(bytes);
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
            writer.Write(value.tick);
            writer.Write(value.lastProcessedCommand);
            writer.Write(value.position);
            writer.Write(value.velocity);
            writer.Write(NetworkSerializationUtil.CompressToShort(value.lookVector.x));
            writer.Write(NetworkSerializationUtil.CompressToShort(value.lookVector.y));
            writer.Write(NetworkSerializationUtil.CompressToShort(value.lookVector.z));
            writer.Write(value.currentSpeed);
            // This makes our max speed modifier 65.535 with a 0.001 precision.
            writer.Write(NetworkSerializationUtil.CompressToUshort(value.speedModifier));
            writer.Write(value.canJump);
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
                tick = reader.Read<int>(),
                lastProcessedCommand = reader.Read<int>(),
                position = reader.Read<Vector3>(),
                velocity = reader.Read<Vector3>(),
                lookVector = new Vector3(
                    NetworkSerializationUtil.DecompressShort(reader.Read<short>()), 
                    NetworkSerializationUtil.DecompressShort(reader.Read<short>()), 
                    NetworkSerializationUtil.DecompressShort(reader.Read<short>())),
                currentSpeed = reader.Read<float>(),
                speedModifier = NetworkSerializationUtil.DecompressUShort(reader.Read<ushort>()),
                canJump = reader.Read<byte>(),
                state = (CharacterState) reader.Read<byte>(),
                jumpCount = reader.Read<byte>(),
                customData = customData,
            };
        }
    }
}