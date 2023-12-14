using System;
using Animancer;
using FishNet;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player.Entity {
    [LuauAPI]
    public class CharacterAnimationHelper : MonoBehaviour {
        [FormerlySerializedAs("thirdPersonAnimancer")]
        [Header("References")]
        [SerializeField]
        public AnimancerComponent worldmodelAnimancer;

        [FormerlySerializedAs("firstPersonAnimancer")] [SerializeField]
        public AnimancerComponent viewmodelAnimancer;

        public EntityAnimationEvents events;

        [DoNotSerialize] public AnimancerLayer rootLayerWorld;
        [DoNotSerialize] public AnimancerLayer layer1World;
        [DoNotSerialize] public AnimancerLayer layer2World;
        [DoNotSerialize] public AnimancerLayer layer3World;
        [DoNotSerialize] public AnimancerLayer layer4World;

        [DoNotSerialize] public AnimancerLayer rootLayerView;
        [DoNotSerialize] public AnimancerLayer layer1View;
        [DoNotSerialize] public AnimancerLayer layer2View;
        [DoNotSerialize] public AnimancerLayer layer3View;
        [DoNotSerialize] public AnimancerLayer layer4View;


        // public AnimationClip JumpAnimation;
        // public AnimationClip FallAnimation;
        public AnimationClip SlideAnimation;

        public MixerTransition2D moveTransition;
        public MixerTransition2D crouchTransition;

        public ParticleSystem sprintVfx;
        public ParticleSystem jumpPoofVfx;
        public ParticleSystem slideVfx;

        public float particleMaxDistance = 25f;

        [Header("Variables")] 
        public float defaultFadeDuration = .25f;
        public float quickFadeDuration = .1f;
        public float jumpFadeDuration = .2f;
        public float runAnimSpeedMod = 1;
        public float maxRunAnimSpeed = 3f;
        public float directionalLerpMod = 5;
        public float spineClampAngle = 15;
        public float neckClampAngle = 35;

        private MixerState<Vector2> moveStateWorld;
        private MixerState<Vector2> crouchStateWorld;
        private EntityState currentState = EntityState.NONE;
        private Vector2 currentMoveDir = Vector2.zero;
        private Vector2 targetMoveDir;
        private float currentSpeed = 0;
        private bool forceLookForward = true;
        private bool movementIsDirty = false;
        private bool firstPerson = false;

        private void Awake() {
            worldmodelAnimancer.Playable.ApplyAnimatorIK = true;
            viewmodelAnimancer.Playable.ApplyAnimatorIK = true;

            sprintVfx.Stop();
            jumpPoofVfx.Stop();
            slideVfx.Stop();

            // Worldmodel layers
            rootLayerWorld = worldmodelAnimancer.Layers[0];
            rootLayerWorld.SetDebugName("Root (Worldmodel)");

            moveStateWorld = (MixerState<Vector2>)worldmodelAnimancer.States.GetOrCreate(moveTransition);
            crouchStateWorld = (MixerState<Vector2>)worldmodelAnimancer.States.GetOrCreate(crouchTransition);

            layer1World = worldmodelAnimancer.Layers[1];
            layer1World.SetDebugName("Layer1 (Worldmodel)");

            layer2World = worldmodelAnimancer.Layers[2];
            layer2World.SetDebugName("Layer2 (Worldmodel)");
            layer2World.DestroyStates();

            layer3World = worldmodelAnimancer.Layers[3];
            layer3World.SetDebugName("Layer3 (Worldmodel)");
            layer3World.DestroyStates();

            layer4World = worldmodelAnimancer.Layers[4];
            layer4World.SetDebugName("Layer4 (Worldmodel)");
            layer4World.DestroyStates();

            // Viewmodel layers
            rootLayerView = viewmodelAnimancer.Layers[0];
            rootLayerView.SetDebugName("Root (Viewmodel)");

            layer1View = viewmodelAnimancer.Layers[1];
            layer1View.SetDebugName("Layer 1 (Viewmodel)");

            layer2View = viewmodelAnimancer.Layers[2];
            layer2View.SetDebugName("Layer 2 (Viewmodel)");

            layer3View = viewmodelAnimancer.Layers[3];
            layer3View.SetDebugName("Layer 3 (Viewmodel)");

            layer4View = viewmodelAnimancer.Layers[4];
            layer4View.SetDebugName("Layer 4 (Viewmodel)");


            //Initialize move state
            SetVelocity(Vector3.zero);
            SetState(EntityState.Idle);
        }

        private void SetupLayers(AnimancerComponent animancer) {

        }

        public void SetFirstPerson(bool firstPerson) {
            this.firstPerson = firstPerson;
            if (this.firstPerson) {
                rootLayerWorld.Weight = 0f;
            } else {
                rootLayerWorld.Weight = 1f;
            }
        }
        
        private void LateUpdate() {
            if (!enabled) {
                return;
            }
            UpdateAnimationState();

            //Procedural Animations
            // if (forceLookForward) {
            //     ForceLookForward();
            // }
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
        }

        public bool IsInParticleDistance() {
            return (this.transform.position - Camera.main.transform.position).magnitude <= particleMaxDistance;
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
            moveStateWorld.Parameter =  currentMoveDir;
            moveStateWorld.Speed = Mathf.Clamp(currentSpeed, 1, maxRunAnimSpeed);
            if (currentState == EntityState.Jumping) {
                moveStateWorld.Speed *= 0.45f;
            }

            crouchStateWorld.Parameter =  moveStateWorld.Parameter;
            crouchStateWorld.Speed = moveStateWorld.Speed;

            //Debug.Log("MOVE DIR: " + currentMoveDir + " SPEED: " + currentSpeed);
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
                rootLayerWorld.Play(moveStateWorld, defaultFadeDuration);
            } else if (newState == EntityState.Jumping) {
                // rootLayer.Play(FallAnimation, defaultFadeDuration);
            } else if (newState == EntityState.Crouching) {
                rootLayerWorld.Play(crouchStateWorld, defaultFadeDuration);
            }

            if (newState == EntityState.Sprinting) {
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
