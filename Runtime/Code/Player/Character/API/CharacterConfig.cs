using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player.Character.API {
	[CreateAssetMenu(fileName = "CharacterConfig", menuName = "Airship/Character/CharacterConfig", order = 1)]
	public class CharacterConfig : ScriptableObject {
		[Header("Movement")]
		[Tooltip("Default movement speed (units per second)")] [Min(0f)]
		public float speed = 8f;

		[Tooltip("Sprint movement speed (units per second)")] [Min(0f)]
		public float sprintSpeed = 10f;

		[Header("Crouch")]
		[Tooltip("Slide speed is determined by multiplying the speed against this number")] [Range(0.01f, 1f)]
		public float crouchSpeedMultiplier = 0.5f;

		[Tooltip("Character height multiplier when crouching (e.g. 0.75 would be 75% of normal character height)")] [Range(0.15f, 1f)]
		public float crouchHeightMultiplier = 0.5f;

		[Header("Slide")]
		[Tooltip("Slide speed is determined by multiplying the sprint speed against this number")] [Range(1f, 5f)]
		public float slideSpeedMultiplier = 1.2f;

		[Tooltip("Character height multiplier when sliding (e.g. 0.75 would be 75% of normal character height)")] [Range(0.15f, 1f)]
		public float slideHeightMultiplier = 0.5f;

		[Tooltip("Minimum interval between initiating slides (in seconds)")] [Min(0f)]
		public float slideCooldown = 0.5f;

		[Header("Jump")]
		[Tooltip("Upward velocity applied to character when player jumps")] [Min(0f)]
		public float jumpSpeed = 6f;

		[Tooltip("The time after falling that the player can still jump")] [Min(0f)]
		public float jumpCoyoteTime = 0.1f;

		[Tooltip("The elapsed time that jumps are buffered while in the air to automatically jump once grounded")] [Min(0f)]
		public float jumpBufferTime = 0.1f;

		[FormerlySerializedAs("jumpCooldown")]
		[Tooltip("Minimum interval (in seconds) between jumps, measured from the time the player became grounded")] [Min(0f)]
		public float jumpUpBlockCooldown = 0.35f;

		[Header("Physics")]
		[Tooltip("Air density")]
		public float airDensity = 0.1f;

		[Tooltip("Drag coefficient")]
		public float drag = 0.1f;

		[Tooltip("Friction coefficient")]
		public float friction = 0.3f;

		[Tooltip("Elasticity coefficient")]
		public float elasticity = 0.2f;

		[Tooltip("The time controls are disabled after being impulsed.")]
		public float impulseMoveDisableTime = 0.1f;

		[Tooltip("The multiplier applied to movement during the impulseMoveDisableTime.")]
		public float impulseMoveDisabledScalar = 0.05f;

		public float mass = 1f;
	}
}
