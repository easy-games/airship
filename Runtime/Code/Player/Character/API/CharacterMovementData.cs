using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player.Character.API {
	public class CharacterMovementData : MonoBehaviour {
		[Header("Collision")]
		public float characterHeight = 1.8f;
		public float characterRadius = .5f;
		
		[Header("Movement")]
		[Tooltip("Default movement speed (units per second)")] [Min(0f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float speed = 4.666667f;

		[Tooltip("Sprint movement speed (units per second)")] [Min(0f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float sprintSpeed = 6.666667f;

		[Header("Crouch")]
		[Tooltip("Slide speed is determined by multiplying the speed against this number")] [Range(0.01f, 1f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float crouchSpeedMultiplier = 0.455f;

		[Tooltip("Character height multiplier when crouching (e.g. 0.75 would be 75% of normal character height)")] [Range(0.15f, 1f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float crouchHeightMultiplier = 0.75f;

		[Header("Slide")]
		[Tooltip("Slide speed is determined by multiplying the sprint speed against this number")] [Range(1f, 5f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float slideSpeedMultiplier = 3.28f;

		[Tooltip("Character height multiplier when sliding (e.g. 0.75 would be 75% of normal character height)")] [Range(0.15f, 1f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float slideHeightMultiplier = 0.5f;

		[Tooltip("Minimum interval between initiating slides (in seconds)")] [Min(0f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float slideCooldown = 0.8f;

		[Header("Jump")]
		[Tooltip("Upward velocity applied to character when player jumps")] [Min(0f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float jumpSpeed = 15f;

		[Tooltip("The time after falling that the player can still jump")] [Min(0f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float jumpCoyoteTime = 0.14f;

		[Tooltip("The elapsed time that jumps are buffered while in the air to automatically jump once grounded")] [Min(0f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float jumpBufferTime = 0.1f;

		[FormerlySerializedAs("jumpCooldown")]
		[Tooltip("Minimum interval (in seconds) between jumps, measured from the time the player became grounded")] [Min(0f)]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float jumpUpBlockCooldown = 0.4f;

		[Header("Physics")]
		[Tooltip("Maximum fall speed m/s")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float terminalVelocity = 100;
		//public float overlapMargin = .05f;

		[Tooltip("Multiplier of global gravity force. 1 = global gravity, 2 = twice the gravity")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float gravityMod = 1;
		[Tooltip("Use this to adjust gravity while moving in the +Y. So you can have floaty jumps upwards but still have hard drops downward")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float upwardsGravityMod = 1;

		[Tooltip("")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float maxSlopeSpeed = 2;
		public float slopeForce = 1;
		public float minSlopeDelta = .1f;
		public float maxStepUpHeight = .15f;

		[Tooltip("Air density")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float airDensity = 0.05f;

		[Tooltip("Drag coefficient")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float drag = 15f;

		[Tooltip("Friction coefficient")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float friction = 0.3f;

		[Tooltip("Elasticity coefficient")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float elasticity = 0.2f;

		[Tooltip("The time controls are disabled after being impulsed.")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float impulseMoveDisableTime = 0f;

		[Tooltip("The multiplier applied to movement during the impulseMoveDisableTime.")]
		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float impulseMoveDisabledScalar = 0.05f;

		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		public float mass = 1f;
	}
}
