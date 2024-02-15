using System;
using Animancer;
using Code.Player.Character.API;
using FishNet;
using UnityEngine;

namespace Code.Player.Character {
    [LuauAPI]
    public class CharacterAnimationHelper : MonoBehaviour {
        [Header("References")]
        [SerializeField]
        public AnimancerComponent worldmodelAnimancer;

        public EntityAnimationEvents events;

        [NonSerialized] public AnimancerLayer rootLayerWorld;
        [NonSerialized] public AnimancerLayer layer1World;
        [NonSerialized] public AnimancerLayer layer2World;
        [NonSerialized] public AnimancerLayer layer3World;
        [NonSerialized] public AnimancerLayer layer4World;

        // public AnimationClip JumpAnimation;
        // public AnimationClip FallAnimation;
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
        public float particleMaxDistance = 25f;

        private MixerState<Vector2> moveStateWorld;
        private MixerState<Vector2> crouchStateWorld;
        private CharacterState currentState = CharacterState.Idle;
        private Vector2 currentMoveDir = Vector2.zero;
        private Vector2 targetMoveDir;
        private float currentSpeed = 0;
        private bool movementIsDirty = false;
        private bool firstPerson = false;

        private void Awake() {
            worldmodelAnimancer.Playable.ApplyAnimatorIK = true;

            sprintVfx.Stop();
            jumpPoofVfx.Stop();
            slideVfx.Stop();

            // Worldmodel layers
            rootLayerWorld = worldmodelAnimancer.Layers[0];
            rootLayerWorld.SetDebugName("Layer0 (Root)");

            layer1World = worldmodelAnimancer.Layers[1];
            layer1World.DestroyStates();
            layer1World.SetDebugName("Layer1");

            layer2World = worldmodelAnimancer.Layers[2];
            layer2World.SetDebugName("Layer2");
            layer2World.DestroyStates();

            layer3World = worldmodelAnimancer.Layers[3];
            layer3World.SetDebugName("Layer3");
            layer3World.DestroyStates();

            layer4World = worldmodelAnimancer.Layers[4];
            layer4World.SetDebugName("Layer4");
            layer4World.DestroyStates();

            moveStateWorld = (MixerState<Vector2>)worldmodelAnimancer.States.GetOrCreate(moveTransition);
            crouchStateWorld = (MixerState<Vector2>)worldmodelAnimancer.States.GetOrCreate(crouchTransition);

            //Initialize move state
            SetVelocity(Vector3.zero);
            SetState(CharacterState.Idle);
        }

        public void SetFirstPerson(bool firstPerson) {
            this.firstPerson = firstPerson;
            if (this.firstPerson) {
                rootLayerWorld.Weight = 0f;
            } else {
                rootLayerWorld.Weight = 1f;
                this.SetState(this.currentState, true, true);
            }
        }
        
        private void LateUpdate() {
            UpdateAnimationState();
        }

        private void OnEnable() {
            this.worldmodelAnimancer.Animator.Rebind();

            this.SetState(CharacterState.Idle, true);
        }

        private void Start() {
            this.SetState(CharacterState.Idle, true);
        }

        private void OnDisable() {
            this.sprintVfx.Stop();
            this.jumpPoofVfx.Stop();
            this.slideVfx.Stop();
            this.currentState = CharacterState.Idle;
        }

        public bool IsInParticleDistance() {
            return (this.transform.position - Camera.main.transform.position).magnitude <= particleMaxDistance;
        }

        private void UpdateAnimationState() {
            if (!movementIsDirty) {
                return;
            }
            float moveDeltaMod = (currentState == CharacterState.Sprinting || currentState == CharacterState.Sliding) ? 2 : 1;
            float timeDelta = Time.deltaTime * directionalLerpMod;
            if (InstanceFinder.TimeManager != null) {
                timeDelta = (float)InstanceFinder.TimeManager.TickDelta * directionalLerpMod;
            }
            float magnitude = targetMoveDir.magnitude;
            float speed = magnitude * runAnimSpeedMod;
            
            //When idle lerp to a standstill
            if (currentState == CharacterState.Idle) {
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
            moveStateWorld.Parameter =  currentMoveDir;
            moveStateWorld.Speed = Mathf.Clamp(currentSpeed, 1, maxRunAnimSpeed);
            if (currentState == CharacterState.Jumping) {
                moveStateWorld.Speed *= 0.45f;
            }

            crouchStateWorld.Parameter =  moveStateWorld.Parameter;
            crouchStateWorld.Speed = moveStateWorld.Speed;
        }

        public void SetVelocity(Vector3 vel) {
            movementIsDirty = true;
            var localVel = transform.InverseTransformVector(vel).normalized;
            targetMoveDir = new Vector2(localVel.x, localVel.z).normalized;
        }

        public void SetState(CharacterState newState, bool force = false, bool noRootLayerFade = false) {
            // if (!worldmodelAnimancer.gameObject.activeInHierarchy) return;

            if (newState == currentState && !force) {
                return;
            }

            movementIsDirty = true;
            if (currentState == CharacterState.Jumping && newState != CharacterState.Jumping) {
                TriggerLand();
            }
            if (newState == CharacterState.Sliding)
            {
                StartSlide();
            } else if(currentState == CharacterState.Sliding)
            {
                StopSlide();
            }
            currentState = newState;

            if (newState == CharacterState.Idle || newState == CharacterState.Running || newState == CharacterState.Sprinting || newState == CharacterState.Jumping) {
                rootLayerWorld.Play(moveStateWorld, noRootLayerFade ? 0f : defaultFadeDuration);
            } else if (newState == CharacterState.Jumping) {
                // rootLayer.Play(FallAnimation, defaultFadeDuration);
            } else if (newState == CharacterState.Crouching) {
                rootLayerWorld.Play(crouchStateWorld, noRootLayerFade ? 0f : defaultFadeDuration);
            }

            if (newState == CharacterState.Sprinting) {
                if (this.IsInParticleDistance()) {
                    sprintVfx.Play();
                }
            } else {
                sprintVfx.Stop();
            }

            if (this.firstPerson) {
                rootLayerWorld.Weight = 0f;
            }
        }

        private void StartSlide() {
            layer1World.Play(SlideAnimation, quickFadeDuration);
            if (IsInParticleDistance()) {
                slideVfx.Play();
            }
            events.TriggerBasicEvent(EntityAnimationEventKey.SLIDE_START);
        }

        private void StopSlide() {
            layer1World.StartFade(0, defaultFadeDuration);
            slideVfx.Stop();
            events.TriggerBasicEvent(EntityAnimationEventKey.SLIDE_END);
        }

        public void TriggerJump() {
            // rootOverrideLayer.Play(JumpAnimation, jumpFadeDuration).Events.OnEnd += () => {
            //     rootOverrideLayer.StartFade(0, jumpFadeDuration);
            // };
            events.TriggerBasicEvent(EntityAnimationEventKey.JUMP);
        }

        public void TriggerLand() {
            events.TriggerBasicEvent(EntityAnimationEventKey.LAND);
        }
    }
}
