using System.Collections.Generic;
using FishNet.Object.Prediction;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Entity {
	public struct ReconcileData : IReconcileData {
		public Vector3 Position;
		public Quaternion Rotation;
		public Vector3 Velocity;
		public Vector3 SlideVelocity;
		public Vector3 ImpulseVelocity;
		public Vector3 ImpulseStartVelocity;
		public Vector3 PrevMoveFinalizedDir;
		public EntityState EntityState;
		public EntityState PrevEntityState;
		public Vector3 PrevMoveVector;
		public Vector3 PrevJumpStartPos;
		public Vector3 PrevLookVector;
		public bool PrevSprint;
		public bool PrevJump;
		public Vector3 PrevMoveDir;
		public bool PrevGrounded;
		public float TimeSinceSlideStart;
		public float TimeSinceBecameGrounded;
		public float TimeSinceWasGrounded;
		public float TimeSinceJump;
		public float TimeSinceImpulse;
		public float ImpulseDuration;
		public MoveModifier PrevMoveModifier;
		// public Dictionary<int, MoveModifier> MoveModifiers;
		// public Dictionary<uint, MoveModifier> MoveModifierFromEventHistory;

		/* Everything below this is required for
	    * the interface. You do not need to implement
	    * Dispose, it is there if you want to clean up anything
	    * that may allocate when this structure is discarded. */
		private uint _tick;
		public void Dispose() { }
		public uint GetTick() => _tick;
		public void SetTick(uint value) => _tick = value;
	}
}
