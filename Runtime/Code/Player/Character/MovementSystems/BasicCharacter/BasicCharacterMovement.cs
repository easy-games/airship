using System;
using Assets.Luau;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    public enum BasicCharacterState
    {
        Idle = 0,
        Running = 1,
        Airborne = 2,
        Sprinting = 3,
        Crouching = 4,
    }

    [LuauAPI]
    public class BasicCharacterMovement : NetworkedMovement<BasicCharacterMovementState, BasicCharacterInputData>
    {
        public Rigidbody rigidbody;
        public Transform rootTransform;
        public Transform airshipTransform; //The visual transform controlled by this script
        public Transform graphicTransform; //A transform that games can animate
        public BasicCharacterAnimationHelper animationHelper;
        public BasicCharacterMovementSettings moveData;
        public BoxCollider mainCollider;
        public Transform slopeVisualizer;
        
        [Header("Debug")]
        public bool drawDebugGizmos_FORWARD = false;
        public bool drawDebugGizmos_GROUND = false;
        public bool drawDebugGizmos_STEPUP = false;
        public bool drawDebugGizmos_STATES= false;
        public bool useExtraLogging = false;

        [Header("Visual Variables")]
        public bool autoCalibrateSkiddingSpeed = true;
        public float observerRotationLerpMod = 1;
        [Tooltip("If true animations will be played on the server. This should be true if you care about character movement animations server-side (like for hit boxes).")]
        public bool playAnimationOnServer = true;

        #region PRIVATE REFS

        private BasicCharacterPhysics physics;
        private NetworkAnimator networkAnimator;
        private AirshipPredictedCharacterMovement predictedMovement;

        #endregion

        #region EVENTS
        public delegate void StateChanged(object state);
        public event StateChanged stateChanged;

        public delegate void DispatchCustomData(object tick, BinaryBlob customData);
        public event DispatchCustomData dispatchCustomData;

        /// <summary>
        /// Called before movement to sync up custom data from typescript
        /// </summary>
        public event Action OnSetCustomData;
	
        /// <summary>
        /// Called on the start of a Move function.
        /// Params: CharacterMovementState moveData, boolean isReplay, 
        /// </summary>
        public event Action<object, object> OnBeginMove;
        /// <summary>
        /// Called at the end of a Move function.
        /// Params: CharacterMovementState moveData, boolean isReplay
        /// </summary>
        public event Action<object, object> OnEndMove;

        /// <summary>
        /// Params: MoveModifier
        /// </summary>
        public event Action<object> OnAdjustMove;

        /// <summary>
        /// Params: Vector3 velocity, RaycastHit hitInfo
        /// </summary>
        public event Action<object, object> OnImpactWithGround;
        public event Action<object> OnMoveDirectionChanged;

        /// <summary>
        /// Called when movement processes a new jump
        /// Params: Vector3 velocity
        /// </summary>
        public event Action<object> OnJumped;

        /// <summary>
        /// Called when the look vector is externally set
        /// Params: Vector3 currentLookVector
        /// </summary>
        public event Action<object> OnNewLookVector;
        #endregion

        //Locally tracked variables
        private float currentSpeed;
        private Vector3 currentLocalVelocity;
        private Vector3 lastPos = Vector3.zero;
        private Vector3 impulseVelocity;
        private float forwardMargin = .05f;
        private BinaryBlob queuedCustomData = null;

        //Input Controls
        private bool jumpInput;
        private Vector3 moveDirInput;
        private bool sprintInput;
        private bool crouchInput;
        private Vector3 lookVector;
        private BinaryBlob customData;

        // State information
        public BasicCharacterMovementState currentMoveState = new BasicCharacterMovementState() {};
        public BasicCharacterAnimationSyncData currentAnimState = new BasicCharacterAnimationSyncData() { };

        #region PUBLIC GET
        public float currentCharacterHeight { get; private set; }
        public float standingCharacterHeight => moveData.characterHeight;
        public float characterRadius => moveData.characterRadius;
        public Vector3 characterHalfExtents { get; private set; }
        public RaycastHit groundedRaycastHit { get; private set; }
        public bool isGrounded { get; private set; }
        public bool isSprinting { get; private set; }
        public bool isCrouching { get; private set; }

        public bool disableInput
        {
            get { return this.currentMoveState.inputDisabled; }
            set { this.currentMoveState.inputDisabled = value; }
        }

        #endregion

        private void Awake()
        {
            networkAnimator = transform.GetComponent<NetworkAnimator>();
            predictedMovement = gameObject.GetComponent<AirshipPredictedCharacterMovement>();
            if(this.physics == null){
                this.physics = new BasicCharacterPhysics(this);
            }
        }

        public override void OnSetMode(MovementMode mode)
        {
            if (mode == MovementMode.Observer)
            {
                rigidbody.isKinematic = true;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (mode == MovementMode.Authority || mode == MovementMode.Input)
            {
                rigidbody.isKinematic = false;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public override void SetCurrentState(BasicCharacterMovementState state)
        {
            this.currentMoveState.CopyFrom(state);
            this.rigidbody.position = state.position;
        }

        public override BasicCharacterMovementState GetCurrentState(int commandNumber, double time)
        {
            // Custom data may be changed during the tick. We store the result of the tick
            // (and any custom data changes) in the snapshot.
            var snapshot = new BasicCharacterMovementState();
            snapshot.CopyFrom(this.currentMoveState);
            snapshot.time = time;
            snapshot.lastProcessedCommand = commandNumber;
            return snapshot;
        }

        public override BasicCharacterInputData GetCommand(int commandNumber, double time)
        {
            OnSetCustomData?.Invoke();
            return new BasicCharacterInputData()
            {
                commandNumber = commandNumber,
                time = time,
                moveDir = moveDirInput,
                jump = jumpInput,
                crouch = crouchInput,
                sprint = sprintInput,
                lookVector = lookVector,
                customData = customData,
            };
        }

        public override void Tick(BasicCharacterInputData md, bool replay)
        {
            if (md == null) return;
            // Use custom data from command
            this.currentMoveState.customData = md.customData;
            this.currentMoveState.lookVector = md.lookVector;
            
            OnBeginMove?.Invoke(this.currentMoveState, replay);

            var currentVelocity = this.rigidbody.velocity;
            var newVelocity = currentVelocity;
            var isIntersecting = false; // TODO: this was "IsIntersectingWithBlock" which just returned false
            var deltaTime = Time.fixedDeltaTime;
            var isImpulsing = impulseVelocity != Vector3.zero;
            var rootPosition = this.rigidbody.transform.position;

            //Ground checks
            var (grounded, groundHit, detectedGround) =
                physics.CheckIfGrounded(rootPosition, newVelocity * deltaTime, md.moveDir);
            if (isIntersecting)
            {
                grounded = true;
            }

            this.isGrounded = grounded;
            this.groundedRaycastHit = groundHit;

            if (grounded)
            {
                //Store this move dir
                currentMoveState.lastGroundedMoveDir = md.moveDir;

                //Snap to the ground if you are falling into the ground
                if (newVelocity.y < 1 &&
                    ((!currentMoveState.prevGrounded && this.moveData.colliderGroundOffset > 0) ||
                     //Snap if we always snap to ground
                     (moveData.alwaysSnapToGround && !currentMoveState.prevStepUp && !isImpulsing &&
                      !currentMoveState.airborneFromImpulse)))
                {
                    this.SnapToY(groundHit.point.y);
                    newVelocity.y = 0;
                }

                //Reset airborne impulse
                currentMoveState.airborneFromImpulse = false;
            }
            else
            {
                //While in the air how much control do we have over our direction?
                md.moveDir = Vector3.Lerp(currentMoveState.lastGroundedMoveDir, md.moveDir,
                    moveData.inAirDirectionalControl);
            }

            if (grounded && !currentMoveState.prevGrounded)
            {
                currentMoveState.jumpCount = 0;
                currentMoveState.timeSinceBecameGrounded = 0f;
                this.OnImpactWithGround?.Invoke(currentVelocity, groundHit);
            }
            else
            {
                currentMoveState.timeSinceBecameGrounded =
                    Math.Min(currentMoveState.timeSinceBecameGrounded + deltaTime, 100f);
            }

            var groundSlopeDir = detectedGround
                ? Vector3.Cross(Vector3.Cross(groundHit.normal, Vector3.down), groundHit.normal).normalized
                : transform.forward;
            var slopeDot = 1 - Mathf.Max(0, Vector3.Dot(groundHit.normal, Vector3.up));

            var canStand = physics.CanStand();

            var normalizedMoveDir = Vector3.ClampMagnitude(md.moveDir, 1);
            var characterMoveVelocity = normalizedMoveDir;

            #region GRAVITY

            if (moveData.useGravity)
            {
                if (!currentMoveState.isFlying && !currentMoveState.prevStepUp &&
                    (moveData.useGravityWhileGrounded ||
                     ((!grounded || newVelocity.y > .01f) && !currentMoveState.isFlying)))
                {
                    //print("Applying grav: " + newVelocity + " currentVel: " + currentVelocity);
                    //apply gravity
                    var verticalGravMod = !grounded && currentVelocity.y > .1f ? moveData.upwardsGravityMultiplier : 1;
                    newVelocity.y += Physics.gravity.y * moveData.gravityMultiplier * verticalGravMod * deltaTime;
                }
            }

            //print("gravity force: " + Physics.gravity.y + " vel: " + velocity.y);

            #endregion

            #region JUMPING

            var requestJump = md.jump;
            //Don't try to jump again until they stop requesting this jump
            if (!requestJump)
            {
                currentMoveState.alreadyJumped = false;
            }

            var didJump = false;
            var canJump = false;
            if (moveData.numberOfJumps > 0 && requestJump && !currentMoveState.alreadyJumped &&
                (!currentMoveState.prevCrouch || canStand))
            {
                //On the ground
                if (grounded || currentMoveState.prevStepUp)
                {
                    canJump = true;
                }
                else
                {
                    //In the air
                    // coyote jump
                    if (normalizedMoveDir.y <= 0.02f &&
                        currentMoveState.timeSinceWasGrounded <= moveData.jumpCoyoteTime && currentVelocity.y <= 0 &&
                        currentMoveState.timeSinceJump > moveData.jumpCoyoteTime)
                    {
                        canJump = true;
                    }
                    //the first jump requires grounded, so if in the air bump the currentMoveState.jumpCount up
                    else
                    {
                        if (currentMoveState.jumpCount == 0)
                        {
                            currentMoveState.jumpCount = 1;
                        }

                        //Multi Jump
                        if (currentMoveState.jumpCount < moveData.numberOfJumps)
                        {
                            canJump = true;
                        }
                    }
                }

                // extra cooldown if jumping up blocks
                // if (rootPosition.y - prevJumpStartPos.y > 0.01) {
                // 	if (currentMoveState.timeSinceJump < moveData.jumpUpBlockCooldown)
                // 	{
                // 		canJump = false;
                // 	}
                // }
                // dont allow jumping when travelling up
                // if (currentVelocity.y > 0f) {
                // 	canJump = false;
                // }

                // dont jump if we already processed the jump
                // if(currentMoveState.prevState == CharacterState.Jumping){
                // 	canJump = false;
                // }

                if (canJump)
                {
                    // Jump
                    didJump = true;
                    currentMoveState.alreadyJumped = true;
                    currentMoveState.jumpCount++;
                    newVelocity.y = moveData.jumpSpeed;
                    currentMoveState.airborneFromImpulse = false;
                    OnJumped?.Invoke(newVelocity);
                }
            }

            // print($"Tick={md.GetTick()} requestJump={md.jump} canJump={canJump} grounded={grounded} reconciling={replaying}");

            #endregion

            #region STATE

            /*
             * Determine entity state state.
             * md.State MUST be set in all cases below.
             * We CANNOT read md.State at this point. Only md.currentMoveState.prevState.
             */
            var isMoving = currentVelocity.sqrMagnitude > .1f;
            var inAir = didJump || (!detectedGround && !currentMoveState.prevStepUp);
            var tryingToSprint = moveData.onlySprintForward
                ? md.sprint && this.graphicTransform.InverseTransformVector(md.moveDir).z > 0.1f
                : //Only sprint if you are moving forward
                md.sprint && md.moveDir.magnitude > 0.1f; //Only sprint if you are moving

            BasicCharacterState
                groundedState =
                    BasicCharacterState.Idle; //So you can know the desired state even if we are technically in the air

            //Check to see if we can stand up from a crouch
            if ((moveData.autoCrouch || currentMoveState.prevState == BasicCharacterState.Crouching) && !canStand)
            {
                groundedState = BasicCharacterState.Crouching;
            }
            else if (md.crouch && grounded)
            {
                groundedState = BasicCharacterState.Crouching;
            }
            else if (isMoving)
            {
                if (tryingToSprint)
                {
                    groundedState = BasicCharacterState.Sprinting;
                    isSprinting = true;
                }
                else
                {
                    groundedState = BasicCharacterState.Running;
                }
            }
            else
            {
                groundedState = BasicCharacterState.Idle;
            }

            //If you are in the air override the state
            if (inAir)
            {
                currentMoveState.state = BasicCharacterState.Airborne;
            }
            else
            {
                //Otherwise use our found state
                currentMoveState.state = groundedState;
            }

            if (useExtraLogging && currentMoveState.prevState != currentMoveState.state)
            {
                print("New State: " + currentMoveState.state);
            }

            if (!tryingToSprint)
            {
                isSprinting = false;
            }

            /*
             * Update Time Since:
             */

            if (didJump)
            {
                currentMoveState.timeSinceJump = 0f;
            }
            else
            {
                currentMoveState.timeSinceJump = Math.Min(currentMoveState.timeSinceJump + deltaTime, 100f);
            }

            if (grounded)
            {
                currentMoveState.timeSinceWasGrounded = 0f;
            }
            else
            {
                currentMoveState.timeSinceWasGrounded =
                    Math.Min(currentMoveState.timeSinceWasGrounded + deltaTime, 100f);
            }

            #region CROUCH

            // Prevent falling off blocks while crouching
            this.isCrouching = groundedState == BasicCharacterState.Crouching;
            if (moveData.preventFallingWhileCrouching && !currentMoveState.prevStepUp && isCrouching && isMoving &&
                grounded)
            {
                var posInMoveDirection = rootPosition + normalizedMoveDir * 0.2f;
                var (groundedInMoveDirection, _, _) =
                    physics.CheckIfGrounded(posInMoveDirection, newVelocity, normalizedMoveDir);
                bool foundGroundedDir = false;
                if (!groundedInMoveDirection)
                {
                    // Determine which direction we're mainly moving toward
                    var xFirst = Math.Abs(md.moveDir.x) > Math.Abs(md.moveDir.z);
                    Vector3[] vecArr = { new(md.moveDir.x, 0, 0), new(0, 0, md.moveDir.z) };
                    for (int i = 0; i < 2; i++)
                    {
                        // We will try x dir first if x magnitude is greater
                        int index = (xFirst ? i : i + 1) % 2;
                        Vector3 safeDirection = vecArr[index];
                        var stepPosition = rootPosition + safeDirection.normalized * 0.2f;
                        (foundGroundedDir, _, _) =
                            physics.CheckIfGrounded(stepPosition, newVelocity, normalizedMoveDir);
                        if (foundGroundedDir)
                        {
                            characterMoveVelocity = safeDirection;
                            break;
                        }
                    }

                    // Only if we didn't find a safe direction set move to 0
                    if (!foundGroundedDir) characterMoveVelocity = Vector3.zero;
                }
            }

            #endregion

            // Modify colliders size based on movement state
            var offsetExtent = this.moveData.colliderGroundOffset / 2;
            this.currentCharacterHeight = isCrouching
                ? standingCharacterHeight * moveData.crouchHeightMultiplier
                : standingCharacterHeight;
            characterHalfExtents = new Vector3(moveData.characterRadius,
                this.currentCharacterHeight / 2f - offsetExtent, moveData.characterRadius);
            mainCollider.transform.localScale = characterHalfExtents * 2;
            mainCollider.transform.localPosition = new Vector3(0, this.currentCharacterHeight / 2f + offsetExtent, 0);

            #endregion

            #region FLYING

            //Flying movement
            if (currentMoveState.isFlying)
            {
                if (md.jump)
                {
                    newVelocity.y += moveData.verticalFlySpeed;
                }

                if (md.crouch)
                {
                    newVelocity.y -= moveData.verticalFlySpeed;
                }

                newVelocity.y *= Mathf.Clamp(.98f - deltaTime, 0, 1);
            }

            #endregion

            #region FRICTION_DRAG

            var flatMagnitude = new Vector3(newVelocity.x, 0, newVelocity.z).magnitude;
            // Calculate drag:
            var dragForce = physics.CalculateDrag(currentVelocity,
                currentMoveState.isFlying ? .5f : (moveData.drag * (inAir ? moveData.airDragMultiplier : 1)));
            if (!currentMoveState.isFlying)
            {
                //Ignore vertical drag so we have full control over jump and fall speeds
                dragForce.y = 0;
            }


            //Leaving friction out for now. Drag does the job and why complicate with two calculations and two variables to manage? 
            // Calculate friction:
            //var frictionForce = Vector3.zero;

            // if (grounded && !isImpulsing) {
            // 	frictionForce = CharacterPhysics.CalculateFriction(newVelocity, -Physics.gravity.y, predictionRigidbody.Rigidbody.mass, moveData.friction);
            // }

            //Slow down velocity based on drag
            newVelocity += Vector3.ClampMagnitude(dragForce, flatMagnitude);

            #endregion

            // if (OwnerId != -1) {
            //  print($"tick={md.GetTick()} state={_state}, velocity={_velocity}, pos={rootPosition}, name={gameObject.name}, ownerId={OwnerId}");
            // }


            #region IMPULSE

            //Apply any new impulses
            //Apply the impulse over multiple frames to push against drag in a more expected way
            ///_impulseForce *= .95f-deltaTime;
            //characterMoveVelocity *= .95f-deltaTime;
            //Stop the y impulse instantly since its not using air resistance atm
            // _impulseForce.y = 0; 
            // if(_impulseForce.sqrMagnitude < .5f){
            // 	_impulseForce = Vector3.zero;
            // }

            //Use the reconciled impulse velocity 
            if (isImpulsing)
            {
                //The velocity will create drag in X and Z but ignore Y. 
                //So we need to manually drag the impulses Y so it doesn't behave differently than the other axis
                //impulseVelocity.y += Mathf.Max(physics.CalculateDrag(impulseVelocity).y, -impulseVelocity.y);	

                //Apply the impulse to the velocity
                newVelocity += impulseVelocity;
                currentMoveState.airborneFromImpulse = !grounded || impulseVelocity.y > .01f;
                impulseVelocity = Vector3.zero;
                if (useExtraLogging)
                {
                    print(" isImpulsing: " + isImpulsing + " impulse force: " + impulseVelocity + "New Vel: " +
                          newVelocity);
                }
            }

            #endregion

            #region MOVEMENT

            // Find speed
            //Adding 1 to offset the drag force so actual movement aligns with the values people enter in moveData
            var currentAcc = 0f;
            if (tryingToSprint)
            {
                currentSpeed = moveData.sprintSpeed;
                currentAcc = moveData.sprintAccelerationForce;
            }
            else
            {
                currentSpeed = moveData.speed;
                currentAcc = moveData.accelerationForce;
            }

            if (currentMoveState.state == BasicCharacterState.Crouching)
            {
                currentSpeed *= moveData.crouchSpeedMultiplier;
                currentAcc *= moveData.crouchSpeedMultiplier;
            }

            if (currentMoveState.isFlying)
            {
                currentSpeed *= moveData.flySpeedMultiplier;
            }
            else if (inAir)
            {
                currentSpeed *= moveData.airSpeedMultiplier;
            }

            //Apply speed
            if (moveData.useAccelerationMovement)
            {
                characterMoveVelocity *= currentAcc;
            }
            else
            {
                characterMoveVelocity *= currentSpeed;
            }

            #region SLOPE

            if (moveData.detectSlopes && detectedGround)
            {
                //On Ground and detecting slopes
                if (slopeDot < 1 && slopeDot > moveData.minSlopeDelta)
                {
                    var slopeVel = groundSlopeDir.normalized * slopeDot * slopeDot * moveData.slopeForce;
                    if (slopeDot > moveData.maxSlopeDelta)
                    {
                        slopeVel.y = 0;
                    }

                    newVelocity += slopeVel;
                }


                //Project movement onto the slope
                if (characterMoveVelocity.sqrMagnitude > 0 && groundHit.normal.y > 0)
                {
                    //Adjust movement based on the slope of the ground you are on
                    var newMoveVector = Vector3.ProjectOnPlane(characterMoveVelocity, groundHit.normal);
                    newMoveVector.y = Mathf.Min(0, newMoveVector.y);
                    characterMoveVelocity = newMoveVector;
                    if (drawDebugGizmos_STEPUP)
                    {
                        GizmoUtils.DrawLine(rootPosition, rootPosition + characterMoveVelocity * 2, Color.red);
                    }
                    //characterMoveVector.y = Mathf.Clamp( characterMoveVector.y, 0, moveData.maxSlopeSpeed);
                }

                if (useExtraLogging && characterMoveVelocity.y < 0)
                {
                    //print("Move Vector After: " + characterMoveVelocity + " groundHit.normal: " + groundHit.normal + " hitGround: " + groundHit.collider.gameObject.name);
                }
            }

            if (slopeVisualizer)
            {
                slopeVisualizer.LookAt(slopeVisualizer.position +
                                       (groundSlopeDir.sqrMagnitude < .1f ? transform.forward : groundSlopeDir));
            }

            #endregion

            //Used by step ups and forward check
            var forwardDistance = (characterMoveVelocity.magnitude + newVelocity.magnitude) * deltaTime +
                                  (this.characterRadius + forwardMargin);
            var forwardVector = (characterMoveVelocity + newVelocity).normalized * forwardDistance;

            #region MOVE_FORCE

            //Clamp directional movement to not add forces if you are already moving in that direction
            var flatVelocity = new Vector3(newVelocity.x, 0, newVelocity.z);
            var tryingToMove = normalizedMoveDir.sqrMagnitude > .1f;
            var rawMoveDot = Vector3.Dot(flatVelocity.normalized, normalizedMoveDir);
            //print("Directional Influence: " + (characterMoveVector - newVelocity) + " mag: " + (characterMoveVector - currentVelocity).magnitude);

            //Don't drift if you are turning the character
            if (moveData.accelerationTurnFriction > 0 && moveData.useAccelerationMovement && !isImpulsing && grounded &&
                tryingToMove)
            {
                var parallelDot = 1 - Mathf.Abs(Mathf.Clamp01(rawMoveDot));
                //print("DOT: " + parallelDot);
                newVelocity += -Vector3.ClampMagnitude(flatVelocity, currentSpeed) * parallelDot *
                               moveData.accelerationTurnFriction;
            }

            //Stop character from moveing into colliders (Helps prevent axis aligned box colliders from colliding when they shoudln't like jumping in a voxel world)
            if (moveData.preventWallClipping)
            {
                //Do raycasting after we have claculated our move direction
                (bool didHitForward, RaycastHit forwardHit) =
                    physics.CheckForwardHit(rootPosition, forwardVector, true, true);

                if (!currentMoveState.prevStepUp && didHitForward)
                {
                    var isVerticalWall = 1 - Mathf.Max(0, Vector3.Dot(forwardHit.normal, Vector3.up)) >=
                                         moveData.maxSlopeDelta;
                    var isKinematic = forwardHit.collider?.attachedRigidbody == null ||
                                      forwardHit.collider.attachedRigidbody.isKinematic;

                    //print("Avoiding wall: " + forwardHit.collider.gameObject.name + " distance: " + forwardHit.distance + " isVerticalWall: " + isVerticalWall + " isKinematic: " + isKinematic);
                    //Stop character from walking into walls but Let character push into rigidbodies	
                    if (isVerticalWall && isKinematic)
                    {
                        //Stop movement into this surface
                        var colliderDot = 1 - Mathf.Max(0,
                            -Vector3.Dot(forwardHit.normal, characterMoveVelocity.normalized));
                        // var tempMagnitude = characterMoveVector.magnitude;
                        // characterMoveVector -= forwardHit.normal * tempMagnitude * colliderDot;
                        characterMoveVelocity = Vector3.ProjectOnPlane(characterMoveVelocity, forwardHit.normal);
                        characterMoveVelocity.y = 0;
                        characterMoveVelocity *= colliderDot;
                        //print("Collider Dot: " + colliderDot + " moveVector: " + characterMoveVelocity);
                    }

                    //Push the character out of any colliders
                    flatVelocity = Vector3.ClampMagnitude(newVelocity,
                        forwardHit.distance - characterRadius - forwardMargin);
                    newVelocity.x = flatVelocity.x;
                    newVelocity.z = flatVelocity.z;
                }

                if (!grounded && detectedGround)
                {
                    //Hit ground but its not valid ground, push away from it
                    //print("PUSHING AWAY FROM: " + groundHit.normal);
                    newVelocity += groundHit.normal * physics.GetFlatDistance(rootPosition, groundHit.point) * .25f /
                                   deltaTime;
                }
            }

            //Instantly move at the desired speed
            var moveMagnitude = characterMoveVelocity.magnitude;
            var velMagnitude = flatVelocity.magnitude;

            //Don't move character in direction its already moveing
            //Positive dot means we are already moving in this direction. Negative dot means we are moving opposite of velocity.
            //Multipy by 2 so perpendicular movement is still fully applied rather than half applied
            var dirDot = Mathf.Max(moveData.minAccelerationDelta, Mathf.Clamp01((1 - rawMoveDot) * 2));

            if (useExtraLogging)
            {
                //print("old vel: " + currentVelocity + " new vel: " + newVelocity + " move dir: " + characterMoveVelocity + " Dir dot: " + dirDot + " currentSpeed: " + currentSpeed + " grounded: " + grounded + " canJump: " + canJump + " didJump: " + didJump);
            }

            if (currentMoveState.isFlying)
            {
                newVelocity.x = md.moveDir.x * currentSpeed;
                newVelocity.z = md.moveDir.z * currentSpeed;
            }
            else if (!isImpulsing && !currentMoveState.airborneFromImpulse && //Not impulsing AND under our max speed
                     (velMagnitude < (moveData.useAccelerationMovement
                         ? currentSpeed
                         : Mathf.Max(moveData.sprintSpeed, currentSpeed) + 1)))
            {
                if (moveData.useAccelerationMovement)
                {
                    newVelocity += Vector3.ClampMagnitude(characterMoveVelocity, currentSpeed - velMagnitude);
                }
                else
                {
                    // if(Mathf.Abs(characterMoveVelocity.x) > Mathf.Abs(newVelocity.x)){
                    // 	newVelocity.x = characterMoveVelocity.x;
                    // }
                    // if(Mathf.Abs(characterMoveVelocity.z) > Mathf.Abs(newVelocity.z)){
                    // 	newVelocity.z = characterMoveVelocity.z;
                    // }
                    if (moveMagnitude + .5f >= velMagnitude)
                    {
                        newVelocity.x = characterMoveVelocity.x;
                        newVelocity.z = characterMoveVelocity.z;
                    }
                }
            }
            else
            {
                //Moving faster than max speed or using acceleration mode
                newVelocity += normalizedMoveDir * (dirDot * dirDot / 2) *
                               (groundedState == BasicCharacterState.Sprinting
                                   ? this.moveData.sprintAccelerationForce
                                   : moveData.accelerationForce);
            }

            //print("isreplay: " + replaying + " didHitForward: " + didHitForward + " moveVec: " + characterMoveVector + " colliderDot: " + colliderDot  + " for: " + forwardHit.collider?.gameObject.name + " point: " + forwardHit.point);

            #endregion

            #endregion

            #region STEP_UP

            //Step up as the last step so we have the most up to date velocity to work from
            var didStepUp = false;
            if (moveData.detectStepUps && //Want to check step ups
                (!md.crouch || !moveData.preventStepUpWhileCrouching) && //Not blocked by crouch
                (moveData.assistedLedgeJump || currentMoveState.timeSinceBecameGrounded > .05) && //Grounded
                (Mathf.Abs(newVelocity.x) + Mathf.Abs(newVelocity.z)) > .05f)
            {
                //Moveing
                (bool hitStepUp, bool onRamp, Vector3 pointOnRamp, Vector3 stepUpVel) = physics.StepUp(rootPosition,
                    newVelocity, deltaTime, detectedGround ? groundHit.normal : Vector3.up);
                if (hitStepUp)
                {
                    didStepUp = hitStepUp;
                    var oldPos = rootPosition;
                    if (pointOnRamp.y > oldPos.y)
                    {
                        SnapToY(pointOnRamp.y);
                        //airshipTransform.position = Vector3.MoveTowards(oldPos, transform.position, deltaTime);
                    }

                    //print("STEPPED UP. Vel before: " + newVelocity);
                    newVelocity = Vector3.ClampMagnitude(
                        new Vector3(stepUpVel.x, Mathf.Max(stepUpVel.y, newVelocity.y), stepUpVel.z),
                        newVelocity.magnitude);
                    //print("PointOnRamp: " + pointOnRamp + " position: " + rootPosition + " vel: " + newVelocity);

                    if (drawDebugGizmos_STEPUP)
                    {
                        GizmoUtils.DrawSphere(oldPos, .01f, Color.red, 4, 4);
                        GizmoUtils.DrawSphere(rootPosition + newVelocity, .03f, new Color(1, .5f, .5f), 4, 4);
                    }

                    currentMoveState.state =
                        groundedState; //Force grounded state since we are in the air for the step up
                    grounded = true;
                }
            }

            #endregion

            #region APPLY FORCES

            //Clamp the velocity
            newVelocity = Vector3.ClampMagnitude(newVelocity, moveData.terminalVelocity);
            var magnitude = new Vector3(newVelocity.x, 0, newVelocity.z).magnitude;
            var canStopVel = !currentMoveState.airborneFromImpulse && (!inAir || moveData.useMinimumVelocityInAir) &&
                             !isImpulsing;
            var underMin = magnitude <= moveData.minimumVelocity && magnitude > .01f;
            //print("currentMoveState.airborneFromImpulse: " + currentMoveState.airborneFromImpulse + " unerMin: " +underMin + " notTryingToMove: " + notTryingToMove);
            if (canStopVel && !tryingToMove && underMin)
            {
                //Not intending to move so snap to zero (Fake Dynamic Friction)
                //print("STOPPING VELOCITY. CanStop: " + canStopVel + " tryingtoMove: " + tryingToMove + " underMin: " + underMin);
                newVelocity.x = 0;
                newVelocity.z = 0;
            }


            //print($"<b>JUMP STATE</b> {md.GetTick()}. <b>isReplaying</b>: {replaying}    <b>mdJump </b>: {md.jump}    <b>canJump</b>: {canJump}    <b>didJump</b>: {didJump}    <b>currentPos</b>: {rootPosition}    <b>currentVel</b>: {currentVelocity}    <b>newVel</b>: {newVelocity}    <b>grounded</b>: {grounded}    <b>currentState</b>: {state}    <b>currentMoveState.prevState</b>: {currentMoveState.prevState}    <b>mdMove</b>: {md.moveDir}    <b>characterMoveVector</b>: {characterMoveVector}");

            //Execute the forces onto the rigidbody
            this.rigidbody.velocity = newVelocity;

            #endregion


            #region SAVE STATE

            // if(currentMoveState.timeSinceBecameGrounded < .1){
            // 	print("LANDED! prevVel: " + currentVelocity + " newVel: " + newVelocity);
            // }

            //Calculate the local velocity for animations ease
            currentLocalVelocity = graphicTransform.InverseTransformDirection(newVelocity);

            // only update animations if we are not in a replay
            if (!replay)
            {
                TrySetState(new BasicCharacterAnimationSyncData()
                {
                    state = currentMoveState.state,
                    grounded = !inAir || didStepUp,
                    sprinting = isSprinting,
                    crouching = isCrouching,
                    localVelocity = currentLocalVelocity,
                    lookVector = lookVector,
                    jumping = didJump,
                });
            }

            // Handle OnMoveDirectionChanged event
            if (currentMoveState.prevMoveDir != md.moveDir)
            {
                OnMoveDirectionChanged?.Invoke(md.moveDir);
            }

            currentMoveState.prevState = currentMoveState.state;
            currentMoveState.prevCrouch = md.crouch;
            currentMoveState.prevMoveDir = md.moveDir;
            currentMoveState.prevGrounded = grounded;
            currentMoveState.prevStepUp = didStepUp;
            currentMoveState.position = rootPosition;
            currentMoveState.velocity = newVelocity;

            #endregion

            //Track speed based on position
            if (useExtraLogging)
            {
                //print("Speed: " + currentSpeed + " Actual Movement Per Second: " + (physics.GetFlatDistance(rootPosition, lastPos) / deltaTime));
            }

            lastPos = rootPosition;
            
            OnEndMove?.Invoke(currentMoveState, replay);
        }

        public override void Interpolate(float delta, BasicCharacterMovementState stateOld,
            BasicCharacterMovementState stateNew)
        {
            throw new System.NotImplementedException();
            
            // var lookTarget = new Vector3(lookVector.x, 0, lookVector.z);
            // if(lookTarget == Vector3.zero){
            //     lookTarget = new Vector3(0,0,.01f);
            // }
            // airshipTransform.rotation = Quaternion.Lerp(
            //     airshipTransform.rotation,
            //     Quaternion.LookRotation(lookTarget),
            //     observerRotationLerpMod * Time.deltaTime);
        }

        public void LateUpdate()
        {
            // We only update rotation in late update if we are running on a client that is controlling
            // this system
            if (mode != MovementMode.Authority && mode != MovementMode.Input) return;
            
            var lookTarget = new Vector3(this.lookVector.x, 0, this.lookVector.z);
            if(lookTarget == Vector3.zero){
                lookTarget = new Vector3(0,0,.01f);
            }
            //Instantly rotate for owner
            airshipTransform.rotation = Quaternion.LookRotation(lookTarget).normalized;
        }

        #region Helpers

        private void SnapToY(float newY){
            var newPos = this.rigidbody.position;
            newPos.y = newY;
            this.rigidbody.position = newPos;
        }
        
        private void TrySetState(BasicCharacterAnimationSyncData newStateData)
        {
            if (newStateData.state == this.currentAnimState.state) return;
            
            stateChanged?.Invoke((int)newStateData.state);
            
            //Update our local visuals
            animationHelper.SetState(newStateData);
        }

        #endregion

        #region TypeScript Interaction

        public void SetMoveInput(Vector3 moveDir, bool jump, bool sprinting, bool crouch, bool moveDirWorldSpace)
        {
            if (moveDirWorldSpace)
            {
                moveDirInput = moveDir;
            }
            else
            {
                moveDirInput = this.graphicTransform.TransformDirection(moveDir);
            }

            crouchInput = crouch;
            sprintInput = sprinting;
            jumpInput = jump;
        }

        public void SetLookVector(Vector3 lookVector)
        {
            OnNewLookVector?.Invoke(lookVector);
            SetLookVectorRecurring(lookVector);
        }

        /// <summary>
        /// Manually force the look direction of the character without triggering the OnNewLookVector event. 
        /// Useful for something that is updating the lookVector frequently and needs to listen for other scripts modifying the lookVector. 
        /// </summary>
        /// <param name="lookVector"></param>
        public void SetLookVectorRecurring(Vector3 lookVector)
        {
            if (mode == MovementMode.Input)
            {
                this.lookVector = lookVector;
                return;
            }

            if (mode == MovementMode.Authority)
            {
                this.currentMoveState.lookVector = this.lookVector;
            }
        }

        public void SetCustomData(BinaryBlob data)
        {
            this.customData = data;
        }

        public void Teleport(Vector3 position)
        {
            TeleportAndLook(position, mode == MovementMode.Input ? this.lookVector : this.currentMoveState.lookVector);
        }

        public void TeleportAndLook(Vector3 position, Vector3 lookVector)
        {
            // TODO: why? Coppied from old movement
            currentMoveState.airborneFromImpulse = true;
            this.rigidbody.MovePosition(position);
            this.SetLookVector(lookVector);
        }
        
        public void SetMovementEnabled(bool isEnabled){
            this.disableInput = !isEnabled;
            if(this.predictedMovement) {
                this.predictedMovement.enabled = false;
            }
            this.netIdentity.enabled = isEnabled;
        }

        public void SetDebugFlying(bool flying){
            if (!this.moveData.allowDebugFlying) {
                // Debug.LogError("Unable to fly from console when allowFlying is false. Set this characters CharacterMovementData to allow flying if needed");
                return;
            }
            SetFlying(flying);
        }
        
        public void SetFlying(bool flyModeEnabled)
        {
            this.currentMoveState.isFlying = true;
        }
        
        public void AddImpulse(Vector3 impulse){
            if(useExtraLogging){
                print("Adding impulse: " + impulse);
            }
            SetImpulse(this.impulseVelocity + impulse);
        }

        public void SetImpulse(Vector3 impulse){
            if(useExtraLogging){
                print("setting impulse: " + impulse + " at tick: " + this.predictedMovement?.GetCurrentTick());
            }
            impulseVelocity = impulse;
        }
        
        public void IgnoreGroundCollider(Collider collider, bool ignore){
            if(ignore){
                physics.ignoredColliders.TryAdd(collider.GetInstanceID(), collider);
            }else{
                physics.ignoredColliders.Remove(collider.GetInstanceID());
            }
        }
        
        public bool IsIgnoringCollider(Collider collider){
            return physics.ignoredColliders.ContainsKey(collider.GetInstanceID());
        }
        
        public float GetTimeSinceWasGrounded(){
            return currentMoveState.timeSinceWasGrounded;
        }

        public float GetTimeSinceBecameGrounded(){
            return currentMoveState.timeSinceBecameGrounded;
        }
        
        public void SetVelocity(Vector3 velocity) {
            this.rigidbody.velocity = velocity;
        }
        
        public Vector3 GetVelocity() {
            return this.rigidbody.velocity;
        }
        
        public int GetState() {
            return (int)this.currentMoveState.state;
        }

        #endregion

        #region Typescript Data Access Functions

        public bool IsFlying()
        {
            return this.currentMoveState.isFlying;
        }

        public Vector3 GetLookVector()
        {
            // this.lookVector will only get populated when we are the one's creating the inputs
            return mode == MovementMode.Input ? this.lookVector : this.currentMoveState.lookVector;
        }

        #endregion
    }
}