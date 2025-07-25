using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player.Character.MovementSystems.Character {
    [LuauAPI]
    public class CharacterMovementSettings : MonoBehaviour {
        [Header("Collider Size")] [Tooltip("Height of the character hit box")] [Min(.01f)]
        public float characterHeight = 1.8f;

        [Tooltip("Half size of the character hit box")] [Min(.01f)]
        public float characterRadius = .2f;

        [Tooltip("How high off the ground should the character hit box be")] [Min(0)]
        public float colliderGroundOffset = .015f;


        [Header("Movement")] [Tooltip("Only allow sprinting forward.")]
        public bool onlySprintForward = false;

        [Tooltip("Should movement be applied over time as a force? Or a constant speed.")]
        public bool useAccelerationMovement = false;

        [Tooltip("Default movement speed (units per second) when not using accesleration movement")] [Min(0f)]
        public float speed = 4.666667f;

        [Tooltip("Sprint movement speed (units per second) when not using accesleration movement")] [Min(0f)]
        public float sprintSpeed = 6.666667f;

        [Tooltip(
            "How much to accelerate (units per second) when using acceleration movement or when going faster than the target speed")]
        [Min(0f)]
        public float accelerationForce = 1;

        [Tooltip(
            "How much to accelerate sprinting (units per second) when using acceleration movement or when going faster than the target speed")]
        [Min(0f)]
        public float sprintAccelerationForce = 1.4f;

        [Tooltip("If accelerating in a direction you are already moving, how much force can you still apply?")]
        [Range(0, 1)]
        public float minAccelerationDelta = 0;

        [Tooltip(
            "How much control do you have over movement while in the air? 0 is no control, 1 is full control just like on the ground")]
        [Range(0, 1)]
        public float inAirDirectionalControl = 1;

        [Tooltip(
            "An experimental force that makes changing directions stop your forward momentum as if the character has to plant their feet to turn.")]
        [Range(0, 1)]
        public float accelerationTurnFriction = 0;


        [Header("Crouch")] [Tooltip("Auto crouch will make the character crouch if they walk into a small area")]
        public bool autoCrouch = true;

        [Tooltip("While crouching, prevent falling of ledges")]
        public CrouchEdgeDetection preventFallingWhileCrouching = CrouchEdgeDetection.UseAxisAlignedNormals;

        [Tooltip("While crouching dont step up onto ledges")]
        public bool preventStepUpWhileCrouching = true;

        [Tooltip("Crouching speed is determined by multiplying the speed against this number")] [Range(0f, 1f)]
        public float crouchSpeedMultiplier = 0.455f;

        [Tooltip("Character height multiplier when crouching (e.g. 0.75 would be 75% of normal character height)")]
        [Range(0.15f, 1f)]
        public float crouchHeightMultiplier = 0.75f;


        [Header("Jump")] [Tooltip("How many jumps you can make before hitting the ground again")] [Min(0f)]
        public int numberOfJumps = 1;

        [Tooltip("Upward velocity applied to character when player jumps")] [Min(0f)]
        public float jumpSpeed = 14f;

        [Tooltip("The time after falling that the player can still jump")] [Min(0f)]
        public float jumpCoyoteTime = 0.14f;


        [Header("Fly")] [Tooltip("Let console commands toggle flying (/fly from chat)")]
        public bool allowDebugFlying = true;

        [Tooltip("Flying speed is determined by multiplying the speed against this number")]
        public float flySpeedMultiplier = 3f;

        [Tooltip("How fast to move up and down")]
        public float verticalFlySpeed = 14;

        //TODO: Was this purposfully removed? Should we add it back in
        // [Tooltip("The elapsed time that jumps are buffered while in the air to automatically jump once grounded")] [Min(0f)]
        // [SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
        // public float jumpBufferTime = 0.1f;

        [FormerlySerializedAs("jumpCooldown")]
        [Tooltip("Minimum interval (in seconds) between jumps, measured from the time the player became grounded")]
        [Min(0f)]
        public float jumpUpBlockCooldown = 0.4f;


        [Header("Gravity")] [Tooltip("Apply Physics.gravity force every tick")]
        public bool useGravity = true;

        [Tooltip("Apply gravity even when on the ground for accurate physics")]
        public bool useGravityWhileGrounded = false;

        [Tooltip("When grounded force the Y position of the character to the found ground plane")]
        public bool alwaysSnapToGround = false;

        [Tooltip("Multiplier of global gravity force")]
        public float gravityMultiplier = 2;

        [Tooltip(
            "Use this to adjust gravity while moving in the +Y. So you can have floaty jumps upwards but still have hard drops downward")]
        public float upwardsGravityMultiplier = 1;


        [Header("Physics")] [Tooltip("What layers will count as walkable ground")]
        public LayerMask
            groundCollisionLayerMask = (1 << 0) | (1 << 8) | (1 << 11); // Layers Default, VisuallyHidden and VoxelWorld

        [Tooltip("Maximum fall speed m/s")] public float terminalVelocity = 50;

        [Tooltip("Velocity will be set to zero when below this threshold on the ground")]
        public float minimumVelocity = 1;

        [Tooltip("Also stop momentum when in the air")]
        public bool useMinimumVelocityInAir = false;

        [Tooltip("Push the character away from walls to prevent rigibody friction and unwanted collision overlaps")]
        public bool preventWallClipping = false;

        [Tooltip("Drag coefficient")] [Range(0, 1)]
        public float drag = .1f;

        [Tooltip("How much to multiply drag resistance while you are in the air")] [Range(0, 2f)]
        public float airDragMultiplier = 1;

        [Tooltip("How much to multiply speed while you are in the air")] [Range(0, 2f)]
        public float airSpeedMultiplier = 1;

        [Tooltip("How much to decelerate when no input is given in the air at a per second rate")]
        public float additionalNoInputDrag = 3f;

        [Tooltip("How fast your player will accelerate in the air from player input at a per second rate")]
        public float airInputAcceleration = 100f;

        [Header("Step Ups")] [Tooltip("Push the character up when they stop over a set threshold")]
        public bool detectStepUps = true;

        [Tooltip("Step the character up every frame if it theres nothing to push up to")]
        public bool alwaysStepUp = false;

        [Tooltip(
            "While in the air, if you are near an edge it will push you up to the edge. Requries detectStepUps to be on")]
        public bool assistedLedgeJump = true;

        [Tooltip("How high in units can you auto step up")] [Range(.05f, 1)]
        public float maxStepUpHeight = .5f;

        [Tooltip("How far away to check for a step up")] [Range(0.01f, 5)]
        public float stepUpRampDistance = .75f;


        [Header("Slopes")]
        [Tooltip("Auto detect slopes to create a downward drag. Disable as an optimization to skip raycast checks")]
        public bool detectSlopes = false;

        [Tooltip("The maximum force that pushes against the character when on a slope")] [Min(0f)]
        public float slopeForce = 45;

        [Tooltip("Slopes below this threshold will be ignored. O is flat ground, 1 is a vertical wall")] [Range(0, 1)]
        public float minSlopeDelta = .1f;

        [Tooltip("Slopes above this threshold will be treated as walls")] [Range(0, 1)]
        public float maxSlopeDelta = .3f;
    }

    public enum CrouchEdgeDetection {
        None,
        UseMeshNormals,
        UseAxisAlignedNormals
    }
}