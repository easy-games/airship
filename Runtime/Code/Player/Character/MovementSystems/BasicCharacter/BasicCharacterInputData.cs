using System;
using Assets.Luau;
using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
	/// <summary>
	/// MoveInputData is the movement command stream that keeps track of what the
	/// player wants to do. For example, move in a specific direction, or jump.
	///
	/// TS/Luau can use the CustomData interface to write arbitrary data to this stream.
	/// </summary>
	[LuauAPI]
	public class BasicCharacterInputData : InputCommand
	{
		public Vector3 moveDir;
		public bool jump;
		public bool crouch;
		public bool sprint;
		public Vector3 lookVector;
		public BinaryBlob customData;
	}
}