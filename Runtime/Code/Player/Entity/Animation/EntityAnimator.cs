using Animancer;
using FishNet;
using UnityEngine;

namespace Player.Entity {
    public class EntityAnimator : MonoBehaviour {
        [Header("References")] [SerializeField]
        private AnimancerComponent anim;

        public AnimancerLayer rootLayer;
        public AnimancerLayer overrideLayer;
        public AnimancerLayer handsLayer;

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

        private MixerState<Vector2> moveState;
        private MixerState<Vector2> crouchState;
        private EntityState currentState = EntityState.Idle;
        private Vector3 currentVel = Vector3.zero;

        private void Awake() {
            anim.Playable.ApplyAnimatorIK = true;
        
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
            overrideLayer = anim.Layers[1];
            overrideLayer.SetDebugName("Override");
            overrideLayer.SetMask(rootMask);

            //Hands - Upper body animations for things like holding items or IK hands
            handsLayer = anim.Layers[2];
            handsLayer.SetMask(handsMask);
            handsLayer.SetDebugName("Hands");
            handsLayer.DestroyStates();
            
            //TopMost - Plays over all animations
            handsLayer = anim.Layers[3];
            handsLayer.SetDebugName("TopMost");
            handsLayer.DestroyStates();

            //Initialize move state
            SetVelocity(Vector3.zero);
            SetState(EntityState.Idle);
            
        }

        private Vector2 currentMoveDelta = Vector2.zero;
        private float currentSpeed = 0;
        public void SetVelocity(Vector3 vel) {
            //Gather needed properties
            currentVel = transform.InverseTransformVector(vel).normalized;
            Vector2 moveDir = new Vector2(currentVel.x, currentVel.z).normalized;
            float moveDeltaMod = (currentState == EntityState.Sprinting || currentState == EntityState.Sliding) ? 2 : 1;
            float timeDelta = (float)InstanceFinder.TimeManager.TickDelta * directionalLerpMod;
            float magnitude = moveDir.magnitude;
            float speed = magnitude * runAnimSpeedMod;
            
            //When idle lerp to a standstill
            if (currentState == EntityState.Idle) {
                moveDir = Vector2.zero;
                speed = 1;
            }
            
            //Smoothly adjust animation values
            currentMoveDelta =  Vector2.Lerp(currentMoveDelta, moveDir * moveDeltaMod, timeDelta);
            currentSpeed = Mathf.Lerp(currentSpeed, Mathf.Clamp(speed, 1, maxRunAnimSpeed),
                timeDelta);
            // currentMoveDelta = moveDir * moveDeltaMod;
            // currentSpeed = Mathf.Clamp(speed, 1, maxRunAnimSpeed);

            // print("currentMoveDelta=" + currentMoveDelta + ", vel=" + vel + ", moveDir=" + moveDir + ", speed=" + speed + ", currentSpeed=" + currentSpeed + ", currentState=" + currentState + ", maxRunAnimSpeed=" + maxRunAnimSpeed + ", timeDelta=" + timeDelta + ", go=" + gameObject.name);
            
            //Apply values to animator
            moveState.Parameter =  currentMoveDelta;
            moveState.Speed = Mathf.Clamp(currentSpeed, 1, maxRunAnimSpeed);
            crouchState.Parameter =  moveState.Parameter;
            crouchState.Speed = moveState.Speed;
        }

        public void SetState(EntityState newState) {
            currentState = newState;

            if (newState == EntityState.Sliding)
            {
                StartSlide();
            } else
            {
                StopSlide();
            }

            if (newState == EntityState.Idle || newState == EntityState.Running || newState == EntityState.Sprinting) {
                rootLayer.Play(moveState, defaultFadeDuration);
            } else if (newState == EntityState.Jumping) {
                rootLayer.Play(FallAnimation, defaultFadeDuration);
            } else if (newState == EntityState.Crouching)
            {
                rootLayer.Play(crouchState, defaultFadeDuration);
            }

            if (newState == EntityState.Sprinting) {
                sprintVfx.Play();
            } else {
                sprintVfx.Stop();
            }
        }

        private void StartSlide() {
            overrideLayer.Play(SlideAnimation, quickFadeDuration);
            slideVfx.Play();
        }

        private void StopSlide() {
            overrideLayer.StartFade(0, quickFadeDuration);
            slideVfx.Stop();
        }

        public void StartJump() {
            overrideLayer.Play(JumpAnimation, jumpFadeDuration).Events.OnEnd += () => {
                overrideLayer.StartFade(0, jumpFadeDuration);
            };
        }
    }
}
