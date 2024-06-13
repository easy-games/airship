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
        public Animator anim;

        public EntityAnimationEvents events;
        public ParticleSystem sprintVfx;
        public ParticleSystem jumpPoofVfx;
        public ParticleSystem slideVfx;

        [Header("Variables")] 
        public float runAnimSpeedMod = 1;
        public float maxRunAnimSpeed = 3f;
        public float directionalLerpMod = 5;
        public float spineClampAngle = 15;
        public float neckClampAngle = 35;
        public float particleMaxDistance = 25f;
        public float blendSpeed = 8f;

        private CharacterState currentState = CharacterState.Idle;
        private Vector2 currentMoveDir = Vector2.zero;
        private Vector2 targetMoveDir;
        private float currentSpeed = 0;
        private bool movementIsDirty = false;
        private bool firstPerson = false;
        private float verticalVel = 0;
        private float lastStateTime = 0;

        private void Awake() {
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
                anim.SetLayerWeight(0,0);
            } else {
                anim.SetLayerWeight(0,1);
                this.SetState(this.currentState, true, true);
            }
        }
        
        private void LateUpdate() {
            UpdateAnimationState();
        }

        private void OnEnable() {
            this.anim.Rebind();

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
            float magnitude = targetMoveDir.magnitude;
            float speed = magnitude * runAnimSpeedMod;
            
            //When idle lerp to a standstill
            if (currentState == CharacterState.Idle) {
                targetMoveDir = Vector2.zero;
                speed = 1;
            }
            
            //Smoothly adjust animation values

            // if (currentMoveDir == newMoveDir && Math.Abs(currentSpeed - newSpeed) < .01) {
            //     movementIsDirty = false;
            //     return;
            // }

            //RUNNING SPEED
            currentMoveDir = Vector2.MoveTowards(currentMoveDir, targetMoveDir, this.blendSpeed * Time.deltaTime);
            anim.SetFloat("SpeedX", currentMoveDir.x);
            anim.SetFloat("SpeedZ", currentMoveDir.y);
            var newSpeed = Mathf.Lerp(currentSpeed, Mathf.Clamp(speed, 1, maxRunAnimSpeed), directionalLerpMod * Time.deltaTime);
            //anim.speed = currentState == CharacterState.Jumping ? 1 : newSpeed;

            //AIR SPEED
            anim.SetFloat("SpeedY", Mathf.Lerp(anim.GetFloat("SpeedY"), verticalVel, Time.deltaTime));
        }

        public void SetVelocity(Vector3 localVel) {
            movementIsDirty = true;
            targetMoveDir = new Vector2(localVel.x, localVel.z).normalized;
            verticalVel = Mathf.Clamp(localVel.y, -10,10);
        }

        public void SetGrounded(bool grounded){
            anim.SetBool("Grounded", grounded);
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
                anim.SetLayerWeight(0,0);
            }


            anim.SetBool("Crouching", newState == CharacterState.Crouching);
            anim.SetBool("Sprinting", newState == CharacterState.Sprinting);

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
            anim.SetLayerWeight(1,1);
            anim.SetTrigger("Jump");
            events.TriggerBasicEvent(EntityAnimationEventKey.JUMP);
        }

        public void TriggerLand(bool impact) {
            if(impact){
                anim.SetLayerWeight(1,0);
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
