using System.Collections.Generic;
using Code.Player.Character.MovementSystems.Character;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement {
    [LuauAPI]
    public class CharacterAnimationHelper : MonoBehaviour {
        public enum CharacterAnimationLayer {
            OVERRIDE_1 = 1,
            OVERRIDE_2 = 2,
            OVERRIDE_3 = 3,
            OVERRIDE_4 = 4,
            UPPER_BODY_1 = 5,
            UPPER_BODY_2 = 6
        }

        [Header("References")]
        [SerializeField]
        public Animator animator;

        [SerializeField] public NetworkAnimator? networkAnimator;
        public AnimationEventListener? animationEvents;

        [Header("Variables")] public float minAirborneTime = .25f;
        public float particleMaxDistance = 25f;
        public float directionalBlendLerpMod = 15f;

        [Tooltip("At what speed should we be considered skidding")]
        public float skiddingSpeed = 7.5f;

        [Tooltip("How long in idle before triggering a random reaction animation. 0 = reactions off")]
        public float idleRectionLength = 15;

        public bool isSkidding { get; private set; } = false;

        [Header("Animation Calibration")]
        public float animWalkSpeed = 4.4444445f;

        public float animRunSpeed = 6.6666667f;
        public float animCrouchSpeed = 2.1233335f;

        private float nextIdleReactionLength = 0;
        private AnimatorOverrideController animatorOverride;
        private CharacterState currentState = CharacterState.Idle;
        private Vector2 currentVelNormalized = Vector2.zero;
        private Vector2 targetVelNormalized;
        private float verticalVel = 0;
        private float currentPlaybackSpeed = 0;
        private float currentSpeed = 0;
        private bool firstPerson = false;
        private float lastStateTime = 0;
        private float targetPlaybackSpeed = 0;

        private float lastGroundedTime = 0;
        private bool grounded = false;

        private void Awake() {
            // Make a new instance of the animator override controller
            if (!animatorOverride) {
                if (animator.runtimeAnimatorController is AnimatorOverrideController over) {
                    // Copy all the overrides if we already have an override controller in use
                    var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(over.overridesCount);
                    over.GetOverrides(overrides);
                    animatorOverride = new AnimatorOverrideController(animator.runtimeAnimatorController);
                    animator.runtimeAnimatorController = animatorOverride;
                    animatorOverride.ApplyOverrides(overrides);
                } else if (animator.runtimeAnimatorController) {
                    animatorOverride = new AnimatorOverrideController(animator.runtimeAnimatorController);
                    animator.runtimeAnimatorController = animatorOverride;
                }
            }
        }

        private void Start() {
            // AnimatorOverrideController animatorOverrideController =
            //     new AnimatorOverrideController(this.animator.runtimeAnimatorController);
            // this.animator.runtimeAnimatorController = animatorOverrideController;
            // this.animatorOverride = animatorOverrideController;

            var offset = Random.Range(0f, 1f);
            animator.SetFloat("AnimationOffset", offset);
        }

        public void SetFirstPerson(bool firstPerson) {
            this.firstPerson = firstPerson;
            if (this.firstPerson) {
                animator.SetLayerWeight(0, 0);
            } else {
                animator.SetLayerWeight(0, 1);
                SetState(new CharacterAnimationSyncData { state = currentState });
            }
        }

        private void LateUpdate() {
            UpdateAnimationState();
        }

        private void OnEnable() {
            animator.Rebind();
            GetRandomReactionLength();

            //Enter default state
            SetState(new CharacterAnimationSyncData());
        }

        public bool IsInParticleDistance() {
            return true;
        }

        private void UpdateAnimationState() {
            if (!enabled || !gameObject.activeInHierarchy) {
                return;
            }

            //Don't vary animation speeds if we are in the air or not moving
            if (currentState == CharacterState.Airborne) {
                targetPlaybackSpeed = 1;
            } else if ((currentState == CharacterState.Crouching && targetPlaybackSpeed < .1) ||
                       targetPlaybackSpeed < .03) {
                targetVelNormalized = Vector2.zero;
                targetPlaybackSpeed = 1;
            }

            var currentMagnitude = currentVelNormalized.magnitude;
            var targetMagnitude = targetVelNormalized.magnitude;
            var blendMod = targetMagnitude > currentMagnitude
                ? directionalBlendLerpMod
                : directionalBlendLerpMod / 2f;

            //RUNNING SPEED
            //Speed up animations based on actual speed vs target speed
            currentPlaybackSpeed = Mathf.Lerp(currentPlaybackSpeed, targetPlaybackSpeed, Time.deltaTime * blendMod);
            animator.SetFloat("MovementPlaybackSpeed", currentPlaybackSpeed);

            //Blend directional influence
            var smoothXVelocity = 0f;
            var smoothYVelocity = 0f;
            var smoothTime = 0.025f;

            currentVelNormalized.x = Mathf.SmoothDamp(currentVelNormalized.x, targetVelNormalized.x,
                ref smoothXVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            currentVelNormalized.y = Mathf.SmoothDamp(currentVelNormalized.y, targetVelNormalized.y,
                ref smoothYVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
            var velX = Mathf.Abs(currentVelNormalized.x) < 0.01f ? 0 : currentVelNormalized.x;
            var velZ = Mathf.Abs(currentVelNormalized.y) < 0.01f ? 0 : currentVelNormalized.y;
            animator.SetFloat("VelX", velX);
            animator.SetFloat("VelY", Mathf.Lerp(animator.GetFloat("VelY"), verticalVel, Time.deltaTime * 1.5f));
            animator.SetFloat("VelZ", velZ);
            animator.SetFloat("Speed", targetMagnitude);


            if (grounded) {
                lastGroundedTime = Time.time;
                isSkidding = currentSpeed >= skiddingSpeed;
                animator.SetBool("Skidding", isSkidding);
                animator.SetBool("Airborne", false);
            } else {
                animator.SetBool("Skidding", false);
                animator.SetBool("Airborne", Time.time - lastGroundedTime > minAirborneTime);
            }

            // if (idleRectionLength > 0 && currentState == CharacterState.Idle &&
            //     Time.time - lastStateTime > nextIdleReactionLength)
            // {
            //     //Idle reaction
            //     GetRandomReactionLength();
            //     animator.SetFloat("ReactIndex", (float)UnityEngine.Random.Range(0, 3));
            //     SetTrigger("React");
            //
            //     lastStateTime =
            //         Time.time +
            //         idleRectionLength; //Add time so it doesn't trigger a reaction while a reaction is still playing
            // }
        }

        private void GetRandomReactionLength() {
            nextIdleReactionLength = idleRectionLength +
                                     Random.Range(-idleRectionLength / 2, idleRectionLength / 2);
        }

        public void SetVelocity(Vector3 localVel) {
            currentSpeed = new Vector2(localVel.x, localVel.z).magnitude;
            //The target speed is the movement speed the animations were built for
            var targetSpeed = animWalkSpeed;
            if (currentState == CharacterState.Sprinting) {
                if (currentSpeed < animWalkSpeed) {
                    //We will walk instead of run even though we are trying to run
                } else {
                    targetSpeed = animRunSpeed;
                }
            } else if (currentState == CharacterState.Crouching) {
                targetSpeed = animCrouchSpeed;
            }

            //Have to update sprinting state dynamically because low speeds can disable it
            animator.SetBool("Sprinting",
                currentState == CharacterState.Sprinting && currentSpeed >= animWalkSpeed);

            targetPlaybackSpeed = currentSpeed / targetSpeed;
            targetVelNormalized = new Vector2(localVel.x, localVel.z).normalized;
            verticalVel = Mathf.Clamp(localVel.y, -10, 10);
            //print("currentSpeed: " + currentSpeed + " targetSpeed: " + targetSpeed + " playbackSpeed: " + targetPlaybackSpeed + " velNormalized: " + targetVelNormalized);
        }

        public void SetState(CharacterAnimationSyncData syncedState) {
            if (!enabled || !gameObject.activeInHierarchy) {
                return;
            }

            var newState = syncedState.state;
            grounded = syncedState.grounded;
            animator.SetBool("Grounded", grounded);
            // print("New State. Grounded: " + grounded + " state: " + syncedState.state + " Jumping: " +
            //       syncedState.jumping);
            animator.SetBool("Crouching", syncedState.crouching || syncedState.state == CharacterState.Crouching);

            if (syncedState.jumping) {
                SetTrigger("Jump");
            }

            if (firstPerson) {
                animator.SetLayerWeight(0, 0);
            }

            lastStateTime = Time.time;
            currentState = newState;

            SetVelocity(syncedState.localVelocity);
            //print("Set state: " + currentState);
        }

        private void SetTrigger(string trigger) {
            if (networkAnimator != null) {
                networkAnimator.SetTrigger(trigger);
                return;
            }

            animator.SetTrigger(trigger);
        }

        public void SetLayerWeight(CharacterAnimationLayer layer, float weight) {
            var layerName = "Override" + (int)layer;
            animator.SetLayerWeight(animator.GetLayerIndex(layerName), weight);
        }

        public void PlayAnimation(AnimationClip clip, CharacterAnimationLayer layer, float fixedTransitionDuration) {
            if (!enabled || !gameObject.activeInHierarchy) {
                return;
            }

            animatorOverride = animator.runtimeAnimatorController as AnimatorOverrideController;
            // Debug.Log($"Inst id: {animator.runtimeAnimatorController.GetInstanceID()}");

            var stateName = "Override" + (int)layer;

            animatorOverride[stateName] = clip;
            animator.SetBool(stateName + "Looping", clip.isLooping);
            animator.CrossFadeInFixedTime(stateName + "Anim", fixedTransitionDuration,
                animator.GetLayerIndex(stateName));
        }

        public void StopAnimation(CharacterAnimationLayer layer, float fixedTransitionDuration) {
            if (!enabled || !gameObject.activeInHierarchy) {
                return;
            }

            animator.CrossFadeInFixedTime("EarlyExit", fixedTransitionDuration, 4 + (int)layer);
            animator.SetBool("Override" + (int)layer + "Looping", false);
        }

        public float GetPlaybackSpeed() {
            return targetPlaybackSpeed;
        }
    }
}