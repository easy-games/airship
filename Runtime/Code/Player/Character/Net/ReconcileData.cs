using System.Collections.Generic;
using Code.Player.Character.API;
using FishNet.Object.Prediction;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Entity {
	public struct ReconcileData : IReconcileData {
		
		//As of 4.1.3 you can use RigidbodyState to send
		//the transform and rigidbody information easily.
		public FishNet.Component.Prediction.RigidbodyState RigidbodyState;
		//As of 4.1.3 PredictionRigidbody was introduced.
		//It primarily exists to create reliable simulations
		//when interacting with triggers and collider callbacks.
		public PredictionRigidbody PredictionRigidbody;
		public Vector3 SlideVelocity;
		public Vector3 PrevMoveFinalizedDir;
		public CharacterState characterState;
		public CharacterState prevCharacterState;
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
		// public float TimeSinceStepUp;
		public CharacterMoveModifier prevCharacterMoveModifier;
		// public Dictionary<int, MoveModifier> MoveModifiers;
		// public Dictionary<uint, MoveModifier> MoveModifierFromEventHistory;

		public void Initialize(PredictionRigidbody predictionRigidbody){
			this.PredictionRigidbody = predictionRigidbody;
			RigidbodyState = new FishNet.Component.Prediction.RigidbodyState(predictionRigidbody.Rigidbody);
			_tick = 0;
		}

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
