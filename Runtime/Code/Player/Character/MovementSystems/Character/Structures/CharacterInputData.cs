using System;
using Assets.Luau;
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
				customData = customData != null ?  new BinaryBlob()
				{
					dataSize = customData.dataSize,
					data = (byte[])customData.data.Clone(),
				} : default,
			};
		}
	}

	public static class CharacterInputDataSerializer {
		public static void WriteCharacterInputData(this NetworkWriter writer, CharacterInputData value) {
			byte bools = 0;
			BitUtil.SetBit(ref bools, 0, value.crouch);
			BitUtil.SetBit(ref bools, 1, value.jump);
			BitUtil.SetBit(ref bools, 2, value.sprint);
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
				writer.WriteBytes(value.customData.data, 0, value.customData.data.Length);
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