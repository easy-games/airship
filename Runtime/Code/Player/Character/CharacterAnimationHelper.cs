﻿using Code.Player.Character.API;
using UnityEngine;

namespace Code.Player.Character {
    [LuauAPI]
    public class CharacterAnimationHelper : MonoBehaviour {
        public enum CharacterAnimationLayer {
            OVERRIDE_1 = 1,
            OVERRIDE_2 = 2,
            OVERRIDE_3 = 3,
            OVERRIDE_4 = 4,
            UPPER_BODY_1 = 5,
        }

        public class CharacterAnimationSyncData{
            public CharacterState state = CharacterState.Idle;
            public bool grounded = true;
            public bool sprinting = false;
            public bool crouching = false;
        }

        [Header("References")]
        [SerializeField]
        public Animator animator;

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
        [Tooltip("How long in idle before triggering a random reaction animation. 0 = reactions off")]
        public float idleRectionLength = 3;

        private AnimatorOverrideController animatorOverride;
        private CharacterState currentState = CharacterState.Idle;
        private Vector2 currentVelNormalized = Vector2.zero;
        private Vector2 targetVelNormalized;
        private float verticalVel = 0;
        private float currentSpeed = 0;
        private bool firstPerson = false;
        private float lastStateTime = 0;

        private float lastGroundedTime = 0;
        private bool grounded = false;

        private void Awake() {
            if(sprintVfx){
                sprintVfx.Stop();
            }
            if(jumpPoofVfx){
                jumpPoofVfx.Stop();
            }
            if(slideVfx){
                slideVfx.Stop();
            }

            animatorOverride = animator.runtimeAnimatorController as AnimatorOverrideController;
            if(!animatorOverride){
                animatorOverride = new AnimatorOverrideController(animator.runtimeAnimatorController);
            }
            animator.runtimeAnimatorController = animatorOverride;
        }

        public void SetFirstPerson(bool firstPerson) {
            this.firstPerson = firstPerson;
            if (this.firstPerson) {
                animator.SetLayerWeight(0,0);
            } else {
                animator.SetLayerWeight(0,1);
                this.SetState(new (){state = this.currentState});
            }
        }
        
        private void LateUpdate() {
            UpdateAnimationState();
        }

        private void OnEnable() {
            this.animator.Rebind();

            //Enter default state
            SetState(new CharacterAnimationSyncData());
        }

        private void OnDisable() {
            if(sprintVfx){
                sprintVfx.Stop();
            }
            if(jumpPoofVfx){
                jumpPoofVfx.Stop();
            }
            if(slideVfx){
                slideVfx.Stop();
            }
        }

        public bool IsInParticleDistance() {
            return (this.transform.position - Camera.main.transform.position).magnitude <= particleMaxDistance;
        }

        private void UpdateAnimationState() {
            if(!enabled){
                return;
            }
            float moveDeltaMod = (currentState == CharacterState.Sprinting || currentState == CharacterState.Sliding) ? 2 : 1;
            float magnitude = targetVelNormalized.magnitude;
            float speed = magnitude * runAnimSpeedMod;
            
            //When idle lerp to a standstill
            if (currentState == CharacterState.Idle) {
                targetVelNormalized = Vector2.zero;
                speed = 1;
            }

            //RUNNING SPEED
            currentVelNormalized = Vector2.MoveTowards(currentVelNormalized, targetVelNormalized, this.blendSpeed * Time.deltaTime);
            animator.SetFloat("VelX", currentVelNormalized.x);
            animator.SetFloat("VelY", Mathf.Lerp(animator.GetFloat("VelY"), verticalVel, Time.deltaTime*1.5f));
            animator.SetFloat("VelZ", currentVelNormalized.y);
            animator.SetFloat("Speed", magnitude);
            //var newSpeed = Mathf.Lerp(currentSpeed, Mathf.Clamp(speed, 1, maxRunAnimSpeed), directionalLerpMod * Time.deltaTime);
            //anim.speed = currentState == CharacterState.Jumping ? 1 : newSpeed;
            
            if(grounded){
                lastGroundedTime = Time.time;
                animator.SetBool("Airborne", false);
            }else{
                animator.SetBool("Airborne", Time.time - lastGroundedTime > minAirborneTime);
            }

            if(idleRectionLength > 0 && currentState == CharacterState.Idle && Time.time - lastStateTime > idleRectionLength){
                //Idle reaction
                animator.SetFloat("ReactIndex", (float)UnityEngine.Random.Range(0,3));
                animator.SetTrigger("React");
                lastStateTime= Time.time+idleRectionLength;//Add time so it doesn't trigger a reaction while a reaction is still playing
            }
        }

        public void SetVelocity(Vector3 localVel) {
            targetVelNormalized = new Vector2(localVel.x, localVel.z).normalized;
            verticalVel = Mathf.Clamp(localVel.y, -10,10);
        }

        public void SetState(CharacterAnimationSyncData syncedState) {
            if (!enabled) {
                return;
            }

            var newState = syncedState.state;
            //this.SetVelocity(syncedState.velocity);
            this.grounded = syncedState.grounded;
            animator.SetBool("Grounded", grounded);
            animator.SetBool("Crouching", syncedState.crouching || syncedState.state == CharacterState.Crouching);
            animator.SetBool("Sprinting", !syncedState.crouching && (syncedState.sprinting|| syncedState.state == CharacterState.Sprinting));

            if (newState == CharacterState.Sliding) {
                StartSlide();
            } else if(currentState == CharacterState.Sliding) {
                StopSlide();
            }

            if(sprintVfx){
                if (newState == CharacterState.Sprinting) {
                    if (this.IsInParticleDistance()) {
                        sprintVfx.Play();
                    }
                } else {
                    sprintVfx.Stop();
                }
            }

            if (this.firstPerson) {
                animator.SetLayerWeight(0,0);
            }

            lastStateTime = Time.time;
            currentState = newState;
        }

        public void TriggerJump(){
            animator.SetTrigger("Jump");
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

        public void PlayAnimation(AnimationClip clip, CharacterAnimationLayer layer) {
            if (!enabled) {
                return;
            }
            // print("Setting override layer: " + (int)layerLayer);
            int index = (int)layer;

            if (index <= 4) {
                animatorOverride["Override" + index] = clip;
                animator.SetBool("Override" + index + "Looping", clip.isLooping);
                animator.SetTrigger("Override" + index);
                return;
            }

            // Upper body
            if (index <= 8) {
                index -= 4;
                animatorOverride["UpperBody" + index] = clip;
                animator.SetBool("UpperBody" + index + "Looping", clip.isLooping);
                animator.SetTrigger("UpperBody" + index);
                return;
            }
        }

        public void StopAnimation(CharacterAnimationLayer layer) {
            if(!enabled){
                return;
            }
            animator.SetBool("Override" + (int)layer + "Looping", false);
        }
    }
}
