using System;
using Assets.Luau;
using Code.Player.Character.Net;
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
				time = time,
				moveDir = moveDir,
				jump = jump,
				crouch = crouch,
				sprint = sprint,
				lookVector = lookVector,
				customData = customData != null ?  new BinaryBlob()
				{
					m_dataSize = customData.m_dataSize,
					m_data = (byte[])customData.m_data.Clone(),
				} : default,
			};
		}
	}
}