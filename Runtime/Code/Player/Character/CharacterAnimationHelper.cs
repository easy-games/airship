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
        public Animator animator;
        public OneOffAnimation oneOffAnimation;

        public EntityAnimationEvents events;
        public ParticleSystem sprintVfx;
        public ParticleSystem jumpPoofVfx;
        public ParticleSystem slideVfx;

        [Header("Variables")] 
        public float minAirborneTime = .4f;
        public float runAnimSpeedMod = 1;
        public float maxRunAnimSpeed = 3f;
        public float directionalLerpMod = 5;
        public float particleMaxDistance = 25f;
        public float blendSpeed = 8f;
        public float idleRectionLength = 3;

        private CharacterState currentState = CharacterState.Idle;
        private Vector2 currentVelNormalized = Vector2.zero;
        private Vector2 targetVelNormalized;
        private float verticalVel = 0;
        private float currentSpeed = 0;
        private bool movementIsDirty = false;
        private bool firstPerson = false;
        private float lastStateTime = 0;

        private void Awake() {
            oneOffAnimation = gameObject.GetComponentInChildren<OneOffAnimation>();
            sprintVfx.Stop();
            jumpPoofVfx.Stop();
            slideVfx.Stop();

            //Initialize move state
            SetVelocity(Vector3.zero);
            SetState(CharacterState.Idle);
        }

        public void SetFirstPerson(bool firstPerson) {
            this.firstPerson = firstPerson;
            if (this.firstPerson) {
                animator.SetLayerWeight(0,0);
            } else {
                animator.SetLayerWeight(0,1);
                this.SetState(this.currentState, true, true);
            }
        }
        
        private void LateUpdate() {
            UpdateAnimationState();
        }

        private void OnEnable() {
            this.animator.Rebind();

            this.SetState(CharacterState.Idle, true);
        }

        private void Start() {
            this.SetState(CharacterState.Idle, true);
        }

        private void OnDisable() {
            this.sprintVfx.Stop();
            this.jumpPoofVfx.Stop();
            this.slideVfx.Stop();
        }

        public bool IsInParticleDistance() {
            return (this.transform.position - Camera.main.transform.position).magnitude <= particleMaxDistance;
        }

        private void UpdateAnimationState() {
            // if (!movementIsDirty) {
            //     return;
            // }
            float moveDeltaMod = (currentState == CharacterState.Sprinting || currentState == CharacterState.Sliding) ? 2 : 1;
            float magnitude = targetVelNormalized.magnitude;
            float speed = magnitude * runAnimSpeedMod;
            
            //When idle lerp to a standstill
            if (currentState == CharacterState.Idle) {
                targetVelNormalized = Vector2.zero;
                speed = 1;
            }
            
            //Smoothly adjust animation values

            // if (currentMoveDir == newMoveDir && Math.Abs(currentSpeed - newSpeed) < .01) {
            //     movementIsDirty = false;
            //     return;
            // }

            //RUNNING SPEED
            currentVelNormalized = Vector2.MoveTowards(currentVelNormalized, targetVelNormalized, this.blendSpeed * Time.deltaTime);
            animator.SetFloat("VelX", currentVelNormalized.x);
            animator.SetFloat("VelY", Mathf.Lerp(animator.GetFloat("VelY"), verticalVel, Time.deltaTime*1.5f));
            animator.SetFloat("VelZ", currentVelNormalized.y);
            animator.SetFloat("Speed", magnitude);
            var newSpeed = Mathf.Lerp(currentSpeed, Mathf.Clamp(speed, 1, maxRunAnimSpeed), directionalLerpMod * Time.deltaTime);
            //anim.speed = currentState == CharacterState.Jumping ? 1 : newSpeed;
            
            if(grounded){
                lastGroundedTime = Time.time;
                animator.SetBool("Airborne", false);
            }else{
                animator.SetBool("Airborne", Time.time - lastGroundedTime > minAirborneTime);
            }

            if(currentState == CharacterState.Idle && Time.time - lastStateTime > idleRectionLength){
                //Idle reaction
                animator.SetFloat("ReactIndex", (float)UnityEngine.Random.Range(0,3));
                animator.SetTrigger("React");
                lastStateTime= Time.time+idleRectionLength;//Add time so it doesn't trigger a reaction while a reaction is still playing
            }
        }

        public void SetVelocity(Vector3 localVel) {
            movementIsDirty = true;
            targetVelNormalized = new Vector2(localVel.x, localVel.z).normalized;
            verticalVel = Mathf.Clamp(localVel.y, -10,10);
        }

        private float lastGroundedTime = 0;
        private bool grounded = false;
        public void SetGrounded(bool grounded){
            this.grounded = grounded;
            animator.SetBool("Grounded", grounded);
        }

        public void SetState(CharacterState newState, bool force = false, bool noRootLayerFade = false) {
            if (newState == currentState && !force) {
                return;
            }

            movementIsDirty = true;
            if (currentState == CharacterState.Jumping && newState != CharacterState.Jumping) {
                TriggerLand(verticalVel <= -.75f && Time.time-lastStateTime > .5f);
            }
            if (newState == CharacterState.Sliding)
            {
                StartSlide();
            } else if(currentState == CharacterState.Sliding)
            {
                StopSlide();
            }

            if (newState == CharacterState.Sprinting) {
                if (this.IsInParticleDistance()) {
                    sprintVfx.Play();
                }
            } else {
                sprintVfx.Stop();
            }

            if (this.firstPerson) {
                animator.SetLayerWeight(0,0);
            }


            animator.SetBool("Crouching", newState == CharacterState.Crouching);
            animator.SetBool("Sprinting", newState == CharacterState.Sprinting);

            lastStateTime = Time.time;
            currentState = newState;
        }

        private void StartSlide() {
            //layer1World.Play(SlideAnimation, quickFadeDuration);
            if (IsInParticleDistance()) {
                slideVfx.Play();
            }
            events.TriggerBasicEvent(EntityAnimationEventKey.SLIDE_START);
        }

        private void StopSlide() {
            //layer1World.StartFade(0, defaultFadeDuration);
            slideVfx.Stop();
            events.TriggerBasicEvent(EntityAnimationEventKey.SLIDE_END);
        }

        public void TriggerJump() {
            animator.SetTrigger("Jump");
            events.TriggerBasicEvent(EntityAnimationEventKey.JUMP);
        }

        public void TriggerLand(bool impact) {
            if(impact){
                
            }
            events.TriggerBasicEvent(EntityAnimationEventKey.LAND);
        }

        /*public AnimancerState PlayRoot(AnimationClip clip, AnimationClipOptions options){
            return Play(clip, 0, options);
        }

        public AnimancerState PlayRootOneShot(AnimationClip clip){
            return Play(clip, 0, new AnimationClipOptions());
        }

        public AnimancerState PlayOneShot(AnimationClip clip, int layerIndex){
            return Play(clip, layerIndex, new AnimationClipOptions());
        }

        public AnimancerState Play(AnimationClip clip, int layerIndex, AnimationClipOptions options) {
            AnimancerLayer layer = GetLayer(layerIndex);
            
            var previousState = layer.CurrentState;
            var state = layer.Play(clip, options.fadeDuration, options.fadeMode);
            state.Speed = options.playSpeed;
            if(options.autoFadeOut && clip.isLooping == false){
                state.Events.OnEnd = ()=>{
                    if(options.fadeOutToClip != null){
                        layer.Play(options.fadeOutToClip, options.fadeDuration, options.fadeMode);
                    }else if(previousState != null){
                        layer.Play(previousState, options.fadeDuration, options.fadeMode);
                    }
                };
            }
            return state;
        }*/
    }
}
