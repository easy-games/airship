using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player.Character.API {
	[LuauAPI]
	public class CharacterMovementData : MonoBehaviour {
		[Header("Size")]
		[Tooltip("How tall is the character")] [Min(.01f)]
		public float characterHeight = 1.8f;

		[Tooltip("Radius of the character")] [Min(.01f)]
		public float characterRadius = .2f;

		/*[Tooltip("Default movement speed (units per second)")] [Min(0f)]
		public float colliderHeightOffset = .15f;*/

		[Header("Movement")]
		[Tooltip("Default movement speed (units per second)")] [Min(0f)]
		public float speed = 4.666667f;


		[Tooltip("Sprint movement speed (units per second)")] [Min(0f)]
		public float sprintSpeed = 6.666667f;

		[Tooltip("How much to multiply speed while you are in the air")] [Range(0,2f)]
		public float airSpeedMultiplier = 1;

		[Tooltip("Only allow sprinting forward.")]
		public bool onlySprintForward = false;

		[Header("Crouch")]
		[Tooltip("Auto crouch will make the character crouch if they walk into a small area")]
		public bool autoCrouch = true;

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
		[Tooltip("How many jumps you can make before hitting the ground again")] [Min(0f)]
		public int numberOfJumps = 1;

		[Tooltip("Upward velocity applied to character when player jumps")] [Min(0f)]
		public float jumpSpeed = 14f;

		[Tooltip("The time after falling that the player can still jump")] [Min(0f)]
		public float jumpCoyoteTime = 0.14f;

		//TODO: Was this purposfully removed? Should we add it back in
		// [Tooltip("The elapsed time that jumps are buffered while in the air to automatically jump once grounded")] [Min(0f)]
		// [SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		// public float jumpBufferTime = 0.1f;

		[FormerlySerializedAs("jumpCooldown")]
		[Tooltip("Minimum interval (in seconds) between jumps, measured from the time the player became grounded")] [Min(0f)]
		public float jumpUpBlockCooldown = 0.4f;

		[Header("Gravity")]
		[Tooltip("Apply Physics.gravity force every tick")]
		public bool useGravity = true;
		[Tooltip("Apply gravity even when on the ground for accurate physics")]
		public bool useGravityWhileGrounded = false;

		[Tooltip("Multiplier of global gravity force")]
		public float gravityMultiplier = 2;
		[Tooltip("Use this to adjust gravity while moving in the +Y. So you can have floaty jumps upwards but still have hard drops downward")]
		public float upwardsGravityMultiplier = 1;

		[Header("Physics")]
		[Tooltip("What layers will count as walkable ground")]
		public LayerMask groundCollisionLayerMask = 1 << 0 | 1 << 8 | 1 << 11; // Layers Default, VisuallyHidden and VoxelWorld

		[Tooltip("Maximum fall speed m/s")]
		public float terminalVelocity = 50;
		
		[Tooltip("The maximum force that pushes against the character when on a slope")] [Min(0f)]
		public float slopeForce = 45;

		[Tooltip("Slopes below this threshold will be ignored. O is flat ground, 1 is a vertical wall")]
		[Range(0,1)]
		public float minSlopeDelta = .1f;
		[Tooltip("Slopes above this threshold will be treated as walls")]
		[Range(0,1)]
		public float maxSlopeDelta = .3f;

		[Tooltip("How high in units can you auto step up")] [Min(.05f)]
		public float maxStepUpHeight = .5f;

		[Tooltip("Drag coefficient")]
		public float drag = 1f;


		[Header("Movement Modes")]

		[Tooltip("Auto detect slopes to create a downward drag. Disable as an optimization to skip raycast checks")]
		public bool detectSlopes = false;

		[Tooltip("Push the character up when they stop over a set threshold")]
		public bool detectStepUps = true;
		[Tooltip("Step the character up every frame if it theres nothing to push up to")]
		public bool alwaysStepUp = false;

		[Tooltip("While in the air, if you are near an edge it will push you up to the edge. Requries detectStepUps to be on")]
		public bool assistedLedgeJump = true;

		[Tooltip("Push the character away from walls to prevent rigibody friction")]
		public bool preventWallClipping = true;

		[Tooltip("While crouching, prevent falling of ledges")]
		public bool preventFallingWhileCrouching = true;
	}
}
