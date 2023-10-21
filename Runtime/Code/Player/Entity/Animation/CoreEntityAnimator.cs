using System;
using Animancer;
using FishNet;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Entity {
    public class CoreEntityAnimator : MonoBehaviour {
        public const string boneKey = "Bones";
        
        [Header("References")] [SerializeField]
        private AnimancerComponent anim;
        public EntityAnimationEvents events;

        public AnimancerLayer rootLayer;
        public AnimancerLayer rootOverrideLayer;
        public AnimancerLayer handsLayer;
        [FormerlySerializedAs("topMoseLayer")]
        public AnimancerLayer topMostLayer;

        public AvatarMask rootMask;
        public AvatarMask handsMask;
        
        public AnimationClip JumpAnimation;
        public AnimationClip FallAnimation;
        public AnimationClip SlideAnimation;

        public MixerTransition2D moveTransition;
        public MixerTransition2D crouchTransition;

        public ParticleSystem sprintVfx;
        public ParticleSystem jumpPoofVfx;
        public ParticleSystem slideVfx;

        [Header("Variables")] 
        public float defaultFadeDuration = .25f;
        public float quickFadeDuration = .1f;
        public float jumpFadeDuration = .2f;
        public float runAnimSpeedMod = 1;
        public float maxRunAnimSpeed = 3f;
        public float directionalLerpMod = 5;
        public float spineClampAngle = 15;
        public float neckClampAngle = 35;

        private MixerState<Vector2> moveState;
        private MixerState<Vector2> crouchState;
        private EntityState currentState = EntityState.NONE;
        private Vector2 currentMoveDir = Vector2.zero;
        private Vector2 targetMoveDir;
        private float currentSpeed = 0;
        private Transform[] spineBones;
        private Transform neckBone;
        private Transform rootBone;
        private bool forceLookForward = true;
        private bool movementIsDirty = false;
        private bool firstPerson = false;

        private void Awake() {
            anim.Playable.ApplyAnimatorIK = true;
            
            //Grab Bones
            GameObjectReferences refs = gameObject.GetComponent<GameObjectReferences>();
            rootBone = refs.GetValueTyped<Transform>(boneKey, "GraphicsRoot");
            spineBones = new Transform[2];
            spineBones[0] = refs.GetValueTyped<Transform>(boneKey, "Spine1");
            spineBones[1] = refs.GetValueTyped<Transform>(boneKey, "Spine2");
            neckBone = refs.GetValueTyped<Transform>(boneKey, "Neck");
        
            sprintVfx.Stop(); 
            jumpPoofVfx.Stop();
            slideVfx.Stop();

            //Create the layers
            //Root - Main layer for whole body animations
            rootLayer = anim.Layers[0];
            rootLayer.SetDebugName("Root");
            rootLayer.SetMask(rootMask);
            
            //Create the movement state
            moveState = (MixerState<Vector2>) anim.States.GetOrCreate(moveTransition);
            crouchState = (MixerState<Vector2>) anim.States.GetOrCreate(crouchTransition);

            //Override - Animations that override the root
            rootOverrideLayer = anim.Layers[1];
            rootOverrideLayer.SetDebugName("RootOverride");
            rootOverrideLayer.SetMask(rootMask);

            //Hands - Upper body animations for things like holding items or IK hands
            handsLayer = anim.Layers[2];
            handsLayer.SetMask(handsMask);
            handsLayer.SetDebugName("Hands");
            handsLayer.DestroyStates();
            
            //TopMost - Plays over all animations
            topMostLayer = anim.Layers[3];
            topMostLayer.SetDebugName("TopMost");
            topMostLayer.DestroyStates();

            //Initialize move state
            SetVelocity(Vector3.zero);
            SetState(EntityState.Idle);
        }

        public void SetFirstPerson(bool firstPerson) {
            this.firstPerson = firstPerson;
            if (this.firstPerson) {
                rootLayer.Weight = 0f;
            } else {
                rootLayer.Weight = 1f;
            }
        }
        
        private void LateUpdate() {
            UpdateAnimationState();
            
            //Disabling this for now until we have a finalized rig and know how we want to procedurally clamp it
            return;

            //Procedural Animations
            if (forceLookForward) {
                ForceLookForward();
            }
        }

        private void OnDisable() {
            this.sprintVfx.Stop();
            this.jumpPoofVfx.Stop();
            this.slideVfx.Stop();
            this.currentState = EntityState.Idle;
        }

        private void OnEnable() {
            this.SetState(EntityState.Idle, true);
        }

        private void Start() {
            this.SetState(EntityState.Idle, true);
            if (RunCore.IsServer()) {
                this.SetForceLookForward(false);
            }
        }

        private void UpdateAnimationState() {
            if (!movementIsDirty) {
                return;
            }
            float moveDeltaMod = (currentState == EntityState.Sprinting || currentState == EntityState.Sliding) ? 2 : 1;
            float timeDelta = (float)InstanceFinder.TimeManager.TickDelta * directionalLerpMod;
            float magnitude = targetMoveDir.magnitude;
            float speed = magnitude * runAnimSpeedMod;
            
            //When idle lerp to a standstill
            if (currentState == EntityState.Idle) {
                targetMoveDir = Vector2.zero;
                speed = 1;
            }
            
            //Smoothly adjust animation values
            var newMoveDir = Vector2.Lerp(currentMoveDir, targetMoveDir * moveDeltaMod, timeDelta);
            var newSpeed = Mathf.Lerp(currentSpeed, Mathf.Clamp(speed, 1, maxRunAnimSpeed),
                timeDelta);

            if (currentMoveDir == newMoveDir && Math.Abs(currentSpeed - newSpeed) < .01) {
                movementIsDirty = false;
                return;
            }

            currentMoveDir = newMoveDir;
            currentSpeed = newSpeed;
            
            //Apply values to animator
            moveState.Parameter =  currentMoveDir;
            moveState.Speed = Mathf.Clamp(currentSpeed, 1, maxRunAnimSpeed);
            if (currentState == EntityState.Jumping) {
                moveState.Speed *= 0.45f;
            }

            crouchState.Parameter =  moveState.Parameter;
            crouchState.Speed = moveState.Speed;
        }

        public void SetForceLookForward(bool forceLookForward) {
            this.forceLookForward = forceLookForward;
        }
        
        //Always keep the character looking where the player is looking
        private void ForceLookForward() {
            for (var i = 0; i < spineBones.Length; i++) {
                ClampRotation(spineBones[i], spineClampAngle);
            }
            ClampRotation(neckBone, neckClampAngle);
        }

        private void ClampRotation(Transform spine, float maxAngle) {
            //Take the world look and convert to this spines local space
            var targetLocalRot = Quaternion.Inverse(spine.rotation) * rootBone.rotation;
            var newEulerAngles = spine.localEulerAngles;
            newEulerAngles.y = MathUtil.ClampAngle(targetLocalRot.eulerAngles.y, -maxAngle, maxAngle);
            spine.localEulerAngles = newEulerAngles;
        }

        public void SetVelocity(Vector3 vel) {
            movementIsDirty = true;
            var localVel = transform.InverseTransformVector(vel).normalized;
            targetMoveDir = new Vector2(localVel.x, localVel.z).normalized;
        }

        public void SetState(EntityState newState, bool force = false) {
            if (newState == currentState && !force) {
                return;
            }

            movementIsDirty = true;
            if (currentState == EntityState.Jumping && newState != EntityState.Jumping) {
                Land();
            }
            if (newState == EntityState.Sliding)
            {
                StartSlide();
            } else if(currentState == EntityState.Sliding)
            {
                StopSlide();
            }
            currentState = newState;

            if (newState == EntityState.Idle || newState == EntityState.Running || newState == EntityState.Sprinting || newState == EntityState.Jumping) {
                rootLayer.Play(moveState, defaultFadeDuration);
            } else if (newState == EntityState.Jumping) {
                // rootLayer.Play(FallAnimation, defaultFadeDuration);
            } else if (newState == EntityState.Crouching) {
                rootLayer.Play(crouchState, defaultFadeDuration);
            }

            if (newState == EntityState.Sprinting) {
                sprintVfx.Play();
            } else {
                sprintVfx.Stop();
            }

            if (this.firstPerson) {
                rootLayer.Weight = 0f;
            }
        }

        private void StartSlide() {
            rootOverrideLayer.Play(SlideAnimation, quickFadeDuration);
            slideVfx.Play();
            events.TriggerBasicEvent(EntityAnimationEventKey.SLIDE_START);
            
        }

        private void StopSlide() {
            rootOverrideLayer.StartFade(0, defaultFadeDuration);
            slideVfx.Stop();
            events.TriggerBasicEvent(EntityAnimationEventKey.SLIDE_END);
        }

        public void StartJump() {
            // rootOverrideLayer.Play(JumpAnimation, jumpFadeDuration).Events.OnEnd += () => {
            //     rootOverrideLayer.StartFade(0, jumpFadeDuration);
            // };
            events.TriggerBasicEvent(EntityAnimationEventKey.JUMP);
        }

        private void Land() {
            events.TriggerBasicEvent(EntityAnimationEventKey.LAND);
        }
    }
}
