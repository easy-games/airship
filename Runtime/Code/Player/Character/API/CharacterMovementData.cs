using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player.Character.API {
	public class CharacterMovementData : MonoBehaviour {
		[Header("Collision")]
		[Tooltip("How tall is the character")] [Min(.01f)]
		public float characterHeight = 1.8f;

		[Tooltip("Radius of the character")] [Min(.01f)]
		public float characterRadius = .5f;

		/*[Tooltip("Default movement speed (units per second)")] [Min(0f)]
		public float colliderHeightOffset = .15f;*/

		[Header("Movement")]
		[Tooltip("Default movement speed (units per second)")] [Min(0f)]
		public float speed = 4.666667f;

		[Tooltip("Sprint movement speed (units per second)")] [Min(0f)]
		public float sprintSpeed = 6.666667f;

		[Header("Crouch")]
		[Tooltip("Slide speed is determined by multiplying the speed against this number")] [Range(0.01f, 1f)]
		public float crouchSpeedMultiplier = 0.455f;

		[Tooltip("Character height multiplier when crouching (e.g. 0.75 would be 75% of normal character height)")] [Range(0.15f, 1f)]
		public float crouchHeightMultiplier = 0.75f;

		[Header("Slide")]
		[Tooltip("Slide speed is determined by multiplying the sprint speed against this number")] [Range(1f, 5f)]
		public float slideSpeedMultiplier = 3.28f;

		[Tooltip("Character height multiplier when sliding (e.g. 0.75 would be 75% of normal character height)")] [Range(0.15f, 1f)]
		public float slideHeightMultiplier = 0.5f;

		[Tooltip("Minimum interval between initiating slides (in seconds)")] [Min(0f)]
		public float slideCooldown = 0.8f;

		[Header("Jump")]
		[Tooltip("Upward velocity applied to character when player jumps")] [Min(0f)]
		public float jumpSpeed = 15f;

		[Tooltip("The time after falling that the player can still jump")] [Min(0f)]
		public float jumpCoyoteTime = 0.14f;

		//TODO: Was this purposfully removed? Should we add it back in
		// [Tooltip("The elapsed time that jumps are buffered while in the air to automatically jump once grounded")] [Min(0f)]
		// [SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		// public float jumpBufferTime = 0.1f;

		[FormerlySerializedAs("jumpCooldown")]
		[Tooltip("Minimum interval (in seconds) between jumps, measured from the time the player became grounded")] [Min(0f)]
		public float jumpUpBlockCooldown = 0.4f;

		[Header("Physics")]
		[Tooltip("Maximum fall speed m/s")]
		public float terminalVelocity = 50;

		[Tooltip("Multiplier of global gravity force. 1 = global gravity, 2 = twice the gravity")]
		public float gravityMod = 1;
		[Tooltip("Use this to adjust gravity while moving in the +Y. So you can have floaty jumps upwards but still have hard drops downward")]
		public float upwardsGravityMod = 1;
		
		[Tooltip("The maximum force that pushes against the character when on a slope")] [Min(0f)]
		public float slopeForce = 20;

		[Tooltip("Slopes below this threshold will be ignored. O is flat ground, 1 is a vertical wall")]
		[Range(0,1)]
		public float minSlopeDelta = .1f;
		[Tooltip("Slopes above this threshold will be treated as walls")]
		[Range(0,1)]
		public float maxSlopeDelta = .65f;

		[Tooltip("How high in units can you auto step up")] [Min(.05f)]
		public float maxStepUpHeight = .15f;

		[Tooltip("Air density")]
		public float airDensity = 0.05f;

		[Tooltip("Drag coefficient")]
		public float drag = 15f;
	}
}
