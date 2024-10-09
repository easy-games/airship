using Assets.Luau;
using UnityEngine;
using UnityEngine.Profiling;

namespace Code.Player.Human.Net {
	/// <summary>
	/// MoveInputData is the movement command stream that keeps track of what the
	/// player wants to do. For example, move in a specific direction, or jump.
	///
	/// TS/Luau can use the CustomData interface to write arbitrary data to this stream.
	/// </summary>
	public struct MoveInputData {
		public Vector3 moveDir;
		public bool jump;
		public bool crouch;
		public bool sprint;
		public Vector3 lookVector;
		public BinaryBlob customData;

		public MoveInputData(Vector3 moveDir, bool jump, bool crouch, bool sprint, Vector3 lookVector, BinaryBlob customData) {
			this.moveDir = moveDir;
			this.jump = jump;
			this.crouch = crouch;
			this.sprint = sprint;
			this.lookVector = lookVector;
			this.customData = customData;
		}
	}
}
