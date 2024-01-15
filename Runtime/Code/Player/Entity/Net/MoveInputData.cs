using Assets.Luau;
using FishNet;
using FishNet.Object.Prediction;
using FishNet.Serializing.Helping;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

namespace Player.Entity {
	/// <summary>
	/// MoveInputData is the movement command stream that keeps track of what the
	/// player wants to do. For example, move in a specific direction, or jump.
	///
	/// TS/Luau can use the CustomData interface to write arbitrary data to this stream.
	/// </summary>
	public struct MoveInputData : IReplicateData
	{
		public Vector3 MoveDir;
		public bool Jump;
		public bool CrouchOrSlide;
		public bool Sprint;
		public Vector3 LookVector;
		public uint GroundedTick;
		public uint PrevGroundedTick;
		public uint SyncTick;

		public bool DebugStartedSliding;

		public BinaryBlob CustomData;

		private uint _tick;

		// public MoveInputData(Vector2 moveVector, bool jump, bool crouchOrSlide, bool sprint, float lookAngle, float bumpToY, uint groundedTick, BinaryBlob customData, uint syncTick) {
		// 	MoveVector = moveVector;
		// 	Jump = jump;
		// 	CrouchOrSlide = crouchOrSlide;
		// 	Sprint = sprint;
		// 	LookAngle = lookAngle;
		// 	BumpToY = bumpToY;
		// 	GroundedTick = groundedTick;
		// 	CreatedTime = Time.time;
		// 	CustomData = customData;
		// 	_tick = 0;
		// 	SyncTick = syncTick;
		// }
		public void Dispose() { }
		public uint GetTick() => _tick;
		public void SetTick(uint value) => _tick = value;

		/// <summary>
		/// Compare BinaryBlobs. FishNet internally uses this.
		/// </summary>
		[CustomComparer]
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
