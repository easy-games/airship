using System;
using Assets.Luau;
using Code.Network.StateSystem.Structures;
using Code.Util;
using Code.Player.Character.Net;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.MovementSystems.Character
{
	/// <summary>
	/// MoveInputData is the movement command stream that keeps track of what the
	/// player wants to do. For example, move in a specific direction, or jump.
	///
	/// TS/Luau can use the CustomData interface to write arbitrary data to this stream.
	/// </summary>
	[LuauAPI]
	public class CharacterInputData : InputCommand
	{
		public Vector3 moveDir;
		public bool jump;
		public bool crouch;
		public bool sprint;
		public Vector3 lookVector;
		public BinaryBlob customData;

		public override string ToString()
		{
			return "command: " + this.commandNumber;
		}

		public override object Clone()
		{
			return new CharacterInputData()
			{
				commandNumber = commandNumber,
				moveDir = moveDir,
				jump = jump,
				crouch = crouch,
				sprint = sprint,
				lookVector = lookVector,
				customData = customData?.Clone(),
			};
		}

		public override InputCommandDiff CreateDiff<TInput>(TInput input) {
			if (input is not CharacterInputData other) {
				throw new Exception("Invalid input for diff generation");
			}

			byte oldBools = 0;
			byte newBools = 0;
			CharacterInputDataSerializer.EncodeBools(ref oldBools, this);
			CharacterInputDataSerializer.EncodeBools(ref newBools, other);
			bool boolsChanged = oldBools != newBools;
			bool lookChanged = this.lookVector != other.lookVector;
			bool moveChanged = this.moveDir != other.moveDir;
			bool customChanged = this.customData == null ? other.customData != null : !this.customData.Equals(other.customData);
			
			// Flag for if we are sending a full snapshot or just a diff. Sometimes a diff is bigger if
			// all bytes have changed.
			bool fullCustomData = customChanged && customData == null;  // Always send full custom data if base data is null
			byte[] customDataDiff = null;
			if (customChanged && !fullCustomData && other.customData != null) { // If base data is !null and our new data is !null, generate the diff and see if we should use it.
				customDataDiff = customData.CreateDiff(other.customData);
				fullCustomData = customDataDiff.Length > other.customData.Data.Length;
			}
			
			byte changedMask = 0;
			if (boolsChanged) BitUtil.SetBit(ref changedMask, 0, true);	
			if (lookChanged) BitUtil.SetBit(ref changedMask, 1, true);
			if (moveChanged) BitUtil.SetBit(ref changedMask, 2, true);
			if (customChanged) BitUtil.SetBit(ref changedMask, 3, true);
			if (fullCustomData) BitUtil.SetBit(ref changedMask, 4, true);
			
			var writer = NetworkWriterPool.Get();
			writer.Write(changedMask);
			if (boolsChanged) writer.Write(newBools);
			if (lookChanged) {
				writer.Write(NetworkSerializationUtil.CompressToShort(other.lookVector.x));
				writer.Write(NetworkSerializationUtil.CompressToShort(other.lookVector.y));
				writer.Write(NetworkSerializationUtil.CompressToShort(other.lookVector.z));
			}
			if (moveChanged) {
				writer.Write(other.moveDir);
			}

			if (customChanged) {
				if (fullCustomData && other.customData != null) {
					writer.WriteBytes(other.customData.Data, 0, other.customData.Data.Length);
				} else if (customDataDiff != null) {
					writer.WriteBytes(customDataDiff, 0, customDataDiff.Length);
				}
			}

			var dataArray = writer.ToArray();
			NetworkWriterPool.Return(writer);
			
			return new CharacterInputDiff() {
				data = dataArray
			};
		}

		public override InputCommand ApplyDiff(InputCommandDiff diff) {
			if (diff is not CharacterInputDiff inputDiff) {
				throw new Exception("Invalid input for applying diff");
			}
			
			// Normally we would do a base check here, but since the input diffs are grouped, we won't do that here.

			var reader = NetworkReaderPool.Get(inputDiff.data);
			var input = (CharacterInputData) this.Clone();
			input.commandNumber += 1; // Diffs are always applied to the previous input command.

			var changeMask = reader.Read<byte>();

			if (BitUtil.GetBit(changeMask, 0)) {
				byte bools = reader.Read<byte>();
				input.crouch = BitUtil.GetBit(bools, 0);
				input.jump = BitUtil.GetBit(bools, 1);
				input.sprint = BitUtil.GetBit(bools, 2);
			}
			if (BitUtil.GetBit(changeMask, 1)) {
				input.lookVector = new Vector3(
					NetworkSerializationUtil.DecompressShort(reader.Read<short>()),
					NetworkSerializationUtil.DecompressShort(reader.Read<short>()),
					NetworkSerializationUtil.DecompressShort(reader.Read<short>()));
			}
			if (BitUtil.GetBit(changeMask, 2)) {
				input.moveDir = reader.Read<Vector3>();
			}
			if (BitUtil.GetBit(changeMask, 3)) { // customData changed
				if (reader.Remaining == 0) { // No data written means null
					input.customData = null;
				} else { // Something exists
					var fullCustomData = BitUtil.GetBit(changeMask, 4);
					var cData = reader.ReadBytes(reader.Remaining);
					if (fullCustomData) { // It's full custom data if this bit is set
						input.customData = new BinaryBlob(cData);
					} else { // Otherwise it's a custom data diff we need to process
						input.customData = customData.ApplyDiff(cData);
					}
				}
			}
			
			NetworkReaderPool.Return(reader);

			return input;
		}
	}

	public static class CharacterInputDataSerializer {

		public static void EncodeBools(ref byte bools, CharacterInputData value) {
			BitUtil.SetBit(ref bools, 0, value.crouch);
			BitUtil.SetBit(ref bools, 1, value.jump);
			BitUtil.SetBit(ref bools, 2, value.sprint);
		}
		
		public static void WriteCharacterInputData(this NetworkWriter writer, CharacterInputData value) {
			byte bools = 0;
			EncodeBools(ref bools, value);
			writer.Write(bools);
			writer.Write(value.commandNumber);
			writer.Write(NetworkSerializationUtil.CompressToShort(value.lookVector.x));
			writer.Write(NetworkSerializationUtil.CompressToShort(value.lookVector.y));
			writer.Write(NetworkSerializationUtil.CompressToShort(value.lookVector.z));
			writer.Write(value.moveDir);
			
			// We are cheating here by only writing bytes at the end if we have custom data. We can do this because we know the expected size
			// of the above bytes and we know that we send each cmd packet individually. If we were to pass multiple cmds as an array in a single packet,
			// we could not do this optimization since there would be no way to know where the next cmd starts.
			if (value.customData != null) {
				writer.WriteBytes(value.customData.Data, 0, value.customData.Data.Length);
			}
		}

		public static CharacterInputData ReadCharacterInputData(this NetworkReader reader) {
			var bools = reader.Read<byte>();
			var commandNumber = reader.Read<int>();
			var lookVector = new Vector3(
				NetworkSerializationUtil.DecompressShort(reader.Read<short>()),
				NetworkSerializationUtil.DecompressShort(reader.Read<short>()),
				NetworkSerializationUtil.DecompressShort(reader.Read<short>()));
			var moveDir = reader.Read<Vector3>();
			
			BinaryBlob customData = default;
			if (reader.Remaining != 0) {
				var customDataArray = reader.ReadBytes(reader.Remaining); 
				customData = new BinaryBlob(customDataArray);
			}
			
			return new CharacterInputData() {
				crouch = BitUtil.GetBit(bools, 0),
				jump = BitUtil.GetBit(bools, 1),
				sprint = BitUtil.GetBit(bools, 2),
				customData = customData,
				commandNumber = commandNumber,
				lookVector = lookVector,
				moveDir = moveDir,
			};
		}
	}
}