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
		private uint tick;

		public void Dispose() { }
		public uint GetTick() => tick;
		public void SetTick(uint value) => tick = value;

		public MoveInputData(Vector3 moveDir, bool jump, bool crouch, bool sprint, Vector3 lookVector, BinaryBlob customData) {
			this.moveDir = moveDir;
			this.jump = jump;
			this.crouch = crouch;
			this.sprint = sprint;
			this.lookVector = lookVector;
			this.customData = customData;
			this.tick = 0;
		}

		/// <summary>
		/// Compare BinaryBlobs. FishNet internally uses this.
		/// </summary>
		public static bool CompareBinaryBlobs(BinaryBlob a, BinaryBlob b) {
			var aNull = a is null;
			var bNull = b is null;
			
			if ((aNull && bNull) || (aNull != bNull) || (a.m_dataSize != b.m_dataSize)) {
				Profiler.EndSample();
				return false;
			}
			
			// Full compare:
			var len = a.m_dataSize;
			for (long i = 0; i < len; i++) {
				if (a.m_data[i] != b.m_data[i]) {
					Profiler.EndSample();
					return false;
				}
			}
			return true;
		}
	}
}
