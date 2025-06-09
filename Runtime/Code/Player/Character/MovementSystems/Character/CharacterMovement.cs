using System;
using Assets.Luau;
using Code.Network.Simulation;
using Code.Network.StateSystem;
using Code.Player.Character.NetworkedMovement;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Player.Character.MovementSystems.Character {
    public enum CharacterState {
        Idle = 0,
        Running = 1,
        Airborne = 2,
        Sprinting = 3,
        Crouching = 4
    }

    internal enum MoveDirectionMode {
        World,
        Character,
        Camera
    }

    [LuauAPI]
    public class
        CharacterMovement : NetworkedStateSystem<CharacterMovement, CharacterSnapshotData, CharacterInputData> {
        [FormerlySerializedAs("rigidbody")] public Rigidbody rb;
        public Transform rootTransform;
        public Transform airshipTransform; //The visual transform controlled by this script
        public Transform graphicTransform; //A transform that games can animate
        public CharacterMovementSettings movementSettings;
        public BoxCollider mainCollider;

        [Header("Optional Refs")] public CharacterAnimationHelper animationHelper;

        public Transform slopeVisualizer;

        [Header("Debug")] public bool drawDebugGizmos_FORWARD = false;

        public bool drawDebugGizmos_WALLCLIPPING = false;
        public bool drawDebugGizmos_CROUCH = false;

        public bool drawDebugGizmos_GROUND = false;
        public bool drawDebugGizmos_STEPUP = false;
        public bool drawDebugGizmos_STATES = false;
        public bool useExtraLogging = false;

        [Header("Prediction Correction Interpolation (server auth only)")]
        [Tooltip(
            "Controls the speed of correction when non-authoritative clients mis-predict their location. Unit is seconds.")]
        [Range(0, 1)]
        public float correctionInterpTime = 0.1f;

        [Tooltip(
            "Controls the maximum magnitude of the correction interp. Mis-predictions larger that this magnitude will instantly teleport the character to the correct location.")]
        public float correctionMaxMagnitude = 10;

        [Header("Visual Variables")] public bool autoCalibrateSkiddingSpeed = true;

        [Tooltip("Controls the speed in which local character rotates to face look direction.")]
        public float ownerRotationLerpMod = 6;

        [Tooltip(
            "If true animations will be played on the server. This should be true if you care about character movement animations server-side (like for hit boxes).")]
        public bool playAnimationOnServer = true;

#region PRIVATE REFS

        private CharacterPhysics physics;
        private Transform _cameraTransform;
        private bool _smoothLookVector = false;

        /**
         * Used for calculating interp for non-authoritative clients. Set to the previous airshipTransform location
         * OnPause (before resim). Used to calculate difference in simulated and actual position which is then
         * applied to the airshipTransform.
         */
        private Vector3 correctionLastSimulatedPosition = Vector3.zero;

        /**
         * The offset applied to the airshipTransform due to correction.
         */
        private Vector3 correctionOffset = Vector3.zero;

        /**
         * How long since the last correction
         */
        private float correctionTime = 0;

#endregion

#region EVENTS

        public delegate void StateChanged(object state);

        public event StateChanged stateChanged;

        public event Action<object> OnSetMode;

        /// <summary>
        /// Called when a new command is being created. This can be used to set custom data for new
        /// command by calling the SetCustomInputData function during this event.
        /// </summary>
        public event Action<object> OnCreateCommand;

        /// <summary>
        /// Called on the start of processing for a tick. The state passed in is the last state of the
        /// movement system.
        /// Params: CharacterMovementInput input, CharacterMovementState state, boolean isReplay
        /// </summary>
        public event Action<object, object, object> OnProcessCommand;

        /// <summary>
        /// Called at the end of processing for a tick. This is after the command has been processed by
        /// the move function. Use this event if you want to modify the result of the tick. Remember:
        /// the resulting state from the provided input must be deterministic. Failure to have deterministic
        /// state will cause unwanted resimulations.
        /// Params: CharacterMovementInput input, CharacterMovementState finalState, boolean isReplay
        /// </summary>
        public event Action<object, object, object> OnProcessedCommand;

        /// <summary>
        /// Called when the movement system needs to reset to a specific snapshot
        /// state. The passed object is the state that we need to reset to.
        /// </summary>
        public event Action<object> OnSetSnapshot;

        /// <summary>
        /// Called when we need to capture a given snapshot.
        /// </summary>
        public event Action<object, object> OnCaptureSnapshot;

        /// <summary>
        /// Fired every frame. Provides lastState, nextState, and delta between the two.
        /// Internally this is used to position the character in the correct location for rendering
        /// based upon the received network snapshots.
        /// </summary>
        public event Action<object, object, object> OnInterpolateState;

        /// <summary>
        /// Fired on fixed update, but only when a new state has been reached. We use this internally
        /// to set things like animation state where interpolation between two states is not possible.
        /// Provides the new state that has been reached.
        /// </summary>
        public event Action<object> OnInterpolateReachedState;

        public event Action<object, object> OnCompareSnapshots;

        public event Action<object> OnMoveDirectionChanged;

        /// <summary>
        /// Called when the look vector is externally set
        /// Params: Vector3 currentLookVector
        /// </summary>
        public event Action<object> OnNewLookVector;

        /// <summary>
        /// Called when movement processes a new jump
        /// Params: Vector3 velocity
        /// </summary>
        public event Action<object> OnJumped;

        /// <summary>
        /// Params: Vector3 velocity, RaycastHit hitInfo
        /// </summary>
        public event Action<object, object> OnImpactWithGround;

#endregion

        // step up + forward constant
        private float forwardMargin = .05f;

        //Input Controls (also updated to match input command on server)
        private bool jumpInput;
        private Vector3 moveDirInput;
        private bool sprintInput;
        private bool crouchInput;
        private Vector3 lookVector;
        private BinaryBlob customInputData;

        [SyncVar] public Vector3 startingLookVector;

        // State information
        public CharacterSnapshotData currentMoveSnapshot = new() { };
        public CharacterAnimationSyncData currentAnimState = new() { };
        private BinaryBlob customSnapshotData;
        private Vector3 pendingImpulse = new(); // Impulse that will be applied on the next tick.

#region PUBLIC GET

        public float currentCharacterHeight { get; private set; }
        public float standingCharacterHeight => movementSettings.characterHeight;
        public float characterRadius => movementSettings.characterRadius;
        public Vector3 characterHalfExtents { get; private set; }
        public RaycastHit groundedRaycastHit { get; private set; }

        public bool disableInput {
            get => currentMoveSnapshot.inputDisabled;
            set => currentMoveSnapshot.inputDisabled = value;
        }

#endregion

        private void Awake() {
            if (physics == null) {
                physics = new CharacterPhysics(this);
            }

            _cameraTransform = Camera.main.transform;
        }

        public override void OnStartClient() {
            base.OnStartClient();
            lookVector = startingLookVector;
        }

        public override void OnStartServer() {
            base.OnStartServer();
            lookVector = startingLookVector;
        }

        public override void SetMode(NetworkedStateSystemMode mode) {
            // Debug.Log("Running movement in " + mode + " mode for " + this.name + ".");
            if (mode == NetworkedStateSystemMode.Observer) {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (mode == NetworkedStateSystemMode.Authority || mode == NetworkedStateSystemMode.Input) {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // non-authoritative client functions for interpolating mispredicts
                if (isClient && mode == NetworkedStateSystemMode.Input) {
                    AirshipSimulationManager.Instance.OnSetPaused += OnPaused;
                }
            }

            OnSetMode?.Invoke(mode);
        }

        public void OnDestroy() {
            // non-authoritative client
            if (isClient && mode == NetworkedStateSystemMode.Input) {
                AirshipSimulationManager.Instance.OnSetPaused -= OnPaused;
            }
        }

        private void OnPaused(bool paused) {
            if (paused) {
                correctionLastSimulatedPosition
                    = airshipTransform
                        .position; // save the last transform position so that we calculate the difference from where the player sees themselves
            } else {
                correctionTime = 0;
                var goalPosition = rb.position;
                var difference
                    = correctionLastSimulatedPosition -
                      goalPosition; // inverted so that when we apply the difference, we move the airshipTransform back to the original pos
                correctionOffset = difference.magnitude > correctionMaxMagnitude ? Vector3.zero : difference;
            }
        }

        public override void SetCurrentState(CharacterSnapshotData snapshot) {
            currentMoveSnapshot.CopyFrom(snapshot);
            rb.position = snapshot.position;
            if (!rb.isKinematic) {
                rb.linearVelocity = snapshot.velocity;
            }

            var lookTarget = new Vector3(snapshot.lookVector.x, 0, snapshot.lookVector.z);
            if (lookTarget == Vector3.zero) {
                lookTarget = new Vector3(0, 0, .01f);
            }

            airshipTransform.rotation = Quaternion.LookRotation(lookTarget);

            OnSetSnapshot?.Invoke(snapshot);
        }

        public override CharacterSnapshotData GetCurrentState(int commandNumber, double time) {
            // We reset the custom data to make sure earlier calls outside of our
            // specific state capture function don't find their way into our state record.
            customSnapshotData = null;
            OnCaptureSnapshot?.Invoke(commandNumber, time);
            currentMoveSnapshot.customData = customSnapshotData;
            currentMoveSnapshot.time = time;
            currentMoveSnapshot.lastProcessedCommand = commandNumber;
            currentMoveSnapshot.position = rb.position;
            currentMoveSnapshot.velocity = rb.linearVelocity;
            var snapshot = new CharacterSnapshotData();
            snapshot.CopyFrom(currentMoveSnapshot);
            // Reset the custom data again
            customSnapshotData = null;
            return snapshot;
        }

        public override CharacterInputData GetCommand(int commandNumber, double time) {
            // We reset the custom data to make sure earlier calls outside of our
            // specific command generation function don't find their way into our command.
            customInputData = null;
            OnCreateCommand?.Invoke(commandNumber);
            var data = new CharacterInputData() {
                commandNumber = commandNumber,
                moveDir = moveDirInput,
                jump = jumpInput,
                crouch = crouchInput,
                sprint = sprintInput,
                lookVector = lookVector,
                customData = customInputData,
                time = time
            };
            // Reset the custom data again
            customInputData = null;
            return data;
        }

        public override void Tick(CharacterInputData command, double time, bool replay) {
            if (command == null) {
                // If there is no command, we use a "no input" command. This command uses the same command number as our lastProcessedCommand state data
                // so that we treat this input essentially as a ghost input that doesn't effect our stored command information, but allows us to
                // properly tick physics. TS custom command data is not copied. TS has to keep active commands running and tick them with null input
                // We replace the base inputs with the last known state input so that players do not feel that their inputs were lost from dropped packets.
                command = new CharacterInputData() {
                    commandNumber = currentMoveSnapshot.lastProcessedCommand,
                    time = time,
                    jump = currentMoveSnapshot.alreadyJumped,
                    crouch = currentMoveSnapshot.isCrouching,
                    sprint = currentMoveSnapshot.isSprinting,
                    lookVector = currentMoveSnapshot.lookVector
                };
            }

            // If input is disabled, we use default inputs, but we keep customData since we don't know how TS will want to handle that data.
            // TODO: in the future we might not want to actually overwrite the data passed in by the client. It might be nice for TS to be able
            // to still read what direction the character wants to move, even if processing that input is disabled.
            if (disableInput) {
                var replacementCmd = command.Clone() as CharacterInputData;
                replacementCmd.moveDir = new Vector3();
                replacementCmd.lookVector = currentMoveSnapshot.lookVector;
                replacementCmd.jump = false;
                replacementCmd.crouch = false;
                replacementCmd.sprint = false;
                command = replacementCmd;
            }

            OnProcessCommand?.Invoke(command, currentMoveSnapshot, replay);

            var currentVelocity = rb.linearVelocity;
            var newVelocity = currentVelocity;
            var isIntersecting = false; // TODO: this was "IsIntersectingWithBlock" which just returned false
            var deltaTime = Time.fixedDeltaTime;
            var isImpulsing = pendingImpulse != Vector3.zero;
            var rootPosition = rb.position;

            // Apply rotation when ticking on the server. This rotation is automatically applied on the owning client in LateUpdate.
            if (isServer && !isClient) {
                var lookTarget = new Vector3(command.lookVector.x, 0, command.lookVector.z);
                if (lookTarget == Vector3.zero) {
                    lookTarget = new Vector3(0, 0, .01f);
                }

                airshipTransform.rotation = Quaternion.LookRotation(lookTarget);
            }

            //Ground checks
            var (grounded, groundHit, detectedGround) =
                physics.CheckIfGrounded(rootPosition, newVelocity * deltaTime, command.moveDir);
            if (isIntersecting) {
                grounded = true;
            }

            groundedRaycastHit = groundHit;

            if (grounded) {
                //Store this move dir
                // currentMoveSnapshot.lastGroundedMoveDir = command.moveDir;

                //Snap to the ground if you are falling into the ground
                if (newVelocity.y < 1 &&
                    ((!currentMoveSnapshot.isGrounded && movementSettings.colliderGroundOffset > 0) ||
                     //Snap if we always snap to ground
                     (movementSettings.alwaysSnapToGround && !currentMoveSnapshot.prevStepUp && !isImpulsing &&
                      !currentMoveSnapshot.airborneFromImpulse))) {
                    SnapToY(groundHit.point.y);
                    newVelocity.y = 0;
                }

                //Reset airborne impulse
                currentMoveSnapshot.airborneFromImpulse = false;
            } else {
                //While in the air how much control do we have over our direction?
                // TODO: was lastGroundedMoveDir
                command.moveDir = Vector3.Lerp(Vector3.zero, command.moveDir,
                    movementSettings.inAirDirectionalControl);
            }

            if (grounded && !currentMoveSnapshot.isGrounded) {
                currentMoveSnapshot.jumpCount = 0;
                currentMoveSnapshot.canJump = 255;
                OnImpactWithGround?.Invoke(currentVelocity, groundHit);
                if (mode == NetworkedStateSystemMode.Authority && isServer) {
                    SAuthImpactEvent(currentVelocity, groundHit);
                } else if (mode == NetworkedStateSystemMode.Authority && isClient) {
                    CAuthImpactEvent(currentVelocity, groundHit);
                }
            }

            // If we have transitioned to airborne
            if (!grounded && currentMoveSnapshot.isGrounded) {
                // Set canJump to the number of ticks of coyote time we have
                currentMoveSnapshot.canJump
                    = (byte)Math.Min(Math.Floor(movementSettings.jumpCoyoteTime / Time.fixedDeltaTime), 255);
            }

            if (!grounded && !currentMoveSnapshot.isGrounded) {
                // If we've now ticked once in the air, remove a tick of canJump time.
                currentMoveSnapshot.canJump = (byte)Math.Max(currentMoveSnapshot.canJump - 1, 0);
            }

            var groundSlopeDir = detectedGround
                ? Vector3.Cross(Vector3.Cross(groundHit.normal, Vector3.down), groundHit.normal).normalized
                : transform.forward;
            var slopeDot = 1 - Mathf.Max(0, Vector3.Dot(groundHit.normal, Vector3.up));

            var canStand = physics.CanStand();

            var normalizedMoveDir = Vector3.ClampMagnitude(command.moveDir, 1);
            var characterMoveVelocity = normalizedMoveDir;
            //Save the crouching var
            currentMoveSnapshot.isCrouching = command.crouch;

#region GRAVITY

            if (movementSettings.useGravity) {
                if (!currentMoveSnapshot.isFlying && !currentMoveSnapshot.prevStepUp &&
                    (movementSettings.useGravityWhileGrounded ||
                     ((!grounded || newVelocity.y > .01f) && !currentMoveSnapshot.isFlying))) {
                    //print("Applying grav: " + newVelocity + " currentVel: " + currentVelocity);
                    //apply gravity
                    var verticalGravMod = !grounded && currentVelocity.y > .1f
                        ? movementSettings.upwardsGravityMultiplier
                        : 1;
                    newVelocity.y += Physics.gravity.y * movementSettings.gravityMultiplier * verticalGravMod *
                                     deltaTime;
                }
            }

            //print("gravity force: " + Physics.gravity.y + " vel: " + velocity.y);

#endregion

#region JUMPING

            var requestJump = command.jump;
            //Don't try to jump again until they stop requesting this jump
            if (!requestJump) {
                currentMoveSnapshot.alreadyJumped = false;
            }

            var didJump = false;
            var canJump = false;
            if (movementSettings.numberOfJumps > 0 && requestJump && !currentMoveSnapshot.alreadyJumped &&
                (!currentMoveSnapshot.isCrouching || canStand)) {
                //On the ground
                if (grounded || currentMoveSnapshot.prevStepUp) {
                    canJump = true;
                } else {
                    //In the air
                    // coyote jump
                    if (currentVelocity.y < 0f &&
                        // currentMoveSnapshot.timeSinceWasGrounded <= movementSettings.jumpCoyoteTime &&
                        // currentMoveSnapshot.timeSinceJump > movementSettings.jumpCoyoteTime
                        currentMoveSnapshot.canJump > 0
                       ) {
                        canJump = true;
                    }
                    //the first jump requires grounded, so if in the air bump the currentMoveState.jumpCount up
                    else {
                        if (currentMoveSnapshot.jumpCount == 0) {
                            currentMoveSnapshot.jumpCount = 1;
                        }

                        //Multi Jump
                        if (currentMoveSnapshot.jumpCount < movementSettings.numberOfJumps) {
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

                if (canJump) {
                    // Jump
                    didJump = true;
                    currentMoveSnapshot.alreadyJumped = true;
                    currentMoveSnapshot.jumpCount++;
                    newVelocity.y = movementSettings.jumpSpeed;
                    currentMoveSnapshot.airborneFromImpulse = false;
                    OnJumped?.Invoke(newVelocity);
                    if (mode == NetworkedStateSystemMode.Authority && isServer) {
                        SAuthJumpedEvent(newVelocity);
                    } else if (mode == NetworkedStateSystemMode.Authority && isClient) {
                        CAuthJumpedEvent(newVelocity);
                    }
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
            var inAir = didJump || (!detectedGround && !currentMoveSnapshot.prevStepUp);
            var tryingToSprint = movementSettings.onlySprintForward
                ? command.sprint && graphicTransform.InverseTransformVector(command.moveDir).z > 0.1f
                : //Only sprint if you are moving forward
                command.sprint && command.moveDir.magnitude > 0.1f; //Only sprint if you are moving

            var
                groundedState =
                    CharacterState.Idle; //So you can know the desired state even if we are technically in the air

            //Check to see if we can stand up from a crouch
            if ((movementSettings.autoCrouch || currentMoveSnapshot.state == CharacterState.Crouching) &&
                !canStand) {
                groundedState = CharacterState.Crouching;
            } else if (command.crouch && grounded) {
                groundedState = CharacterState.Crouching;
            } else if (isMoving) {
                if (tryingToSprint) {
                    groundedState = CharacterState.Sprinting;
                    currentMoveSnapshot.isSprinting = true;
                } else {
                    groundedState = CharacterState.Running;
                }
            } else {
                groundedState = CharacterState.Idle;
            }

            //If you are in the air override the state
            if (inAir) {
                currentMoveSnapshot.state = CharacterState.Airborne;
            } else {
                //Otherwise use our found state
                currentMoveSnapshot.state = groundedState;
            }

            if (!tryingToSprint) {
                currentMoveSnapshot.isSprinting = false;
            }

#region CROUCH

            // Prevent falling off blocks while crouching
            currentMoveSnapshot.isCrouching = groundedState == CharacterState.Crouching;
            if (movementSettings.preventFallingWhileCrouching && !currentMoveSnapshot.prevStepUp &&
                currentMoveSnapshot.isCrouching && isMoving &&
                grounded) {
                var posInMoveDirection = rootPosition + normalizedMoveDir * 0.2f;
                var (groundedInMoveDirection, _, _) =
                    physics.CheckIfGrounded(posInMoveDirection, newVelocity, normalizedMoveDir);
                var foundGroundedDir = false;
                if (!groundedInMoveDirection) {
                    // Determine which direction we're mainly moving toward
                    var xFirst = Math.Abs(command.moveDir.x) > Math.Abs(command.moveDir.z);
                    Vector3[] vecArr = { new(command.moveDir.x, 0, 0), new(0, 0, command.moveDir.z) };
                    for (var i = 0; i < 2; i++) {
                        // We will try x dir first if x magnitude is greater
                        var index = (xFirst ? i : i + 1) % 2;
                        var safeDirection = vecArr[index];
                        var stepPosition = rootPosition + safeDirection.normalized * 0.2f;
                        (foundGroundedDir, _, _) =
                            physics.CheckIfGrounded(stepPosition, newVelocity, normalizedMoveDir);
                        if (foundGroundedDir) {
                            characterMoveVelocity = safeDirection;
                            break;
                        }
                    }

                    // Only if we didn't find a safe direction set move to 0
                    if (!foundGroundedDir) {
                        characterMoveVelocity = Vector3.zero;
                    }
                }
            }

#endregion

            // Modify colliders size based on movement state
            var offsetExtent = movementSettings.colliderGroundOffset / 2;
            currentCharacterHeight = currentMoveSnapshot.isCrouching
                ? standingCharacterHeight * movementSettings.crouchHeightMultiplier
                : standingCharacterHeight;
            characterHalfExtents = new Vector3(movementSettings.characterRadius,
                currentCharacterHeight / 2f - offsetExtent, movementSettings.characterRadius);
            var mainColliderTransform = mainCollider.transform;
            mainColliderTransform.localScale = characterHalfExtents * 2;
            mainColliderTransform.localPosition = new Vector3(0, currentCharacterHeight / 2f + offsetExtent, 0);

#endregion

#region FLYING

            //Flying movement
            if (currentMoveSnapshot.isFlying) {
                if (command.jump) {
                    newVelocity.y += movementSettings.verticalFlySpeed;
                }

                if (command.crouch) {
                    newVelocity.y -= movementSettings.verticalFlySpeed;
                }

                newVelocity.y *= Mathf.Clamp(.98f - deltaTime, 0, 1);
            }

#endregion

#region FRICTION_DRAG

            var flatMagnitude = new Vector3(newVelocity.x, 0, newVelocity.z).magnitude;
            // Calculate drag:
            var dragForce = physics.CalculateDrag(currentVelocity,
                currentMoveSnapshot.isFlying
                    ? .5f
                    : movementSettings.drag * (inAir ? movementSettings.airDragMultiplier : 1));
            if (!currentMoveSnapshot.isFlying) {
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
            if (isImpulsing) {
                //The velocity will create drag in X and Z but ignore Y. 
                //So we need to manually drag the impulses Y so it doesn't behave differently than the other axis
                //impulseVelocity.y += Mathf.Max(physics.CalculateDrag(impulseVelocity).y, -impulseVelocity.y);	

                //Apply the impulse to the velocity
                newVelocity += pendingImpulse;
                currentMoveSnapshot.airborneFromImpulse = !grounded || pendingImpulse.y > .01f;
                pendingImpulse = Vector3.zero;
                if (isImpulsing) {
                    // print(" isImpulsing: " + isImpulsing + " impulse force: " + this.pendingImpulse + "New Vel: " +
                    //       newVelocity);
                }
            }

#endregion

#region MOVEMENT

            // Find speed
            //Adding 1 to offset the drag force so actual movement aligns with the values people enter in moveData
            var currentAcc = 0f;
            if (tryingToSprint) {
                currentMoveSnapshot.currentSpeed = movementSettings.sprintSpeed;
                currentAcc = movementSettings.sprintAccelerationForce;
            } else {
                currentMoveSnapshot.currentSpeed = movementSettings.speed;
                currentAcc = movementSettings.accelerationForce;
            }

            if (currentMoveSnapshot.state == CharacterState.Crouching) {
                currentMoveSnapshot.currentSpeed *= movementSettings.crouchSpeedMultiplier;
                currentAcc *= movementSettings.crouchSpeedMultiplier;
            }

            if (currentMoveSnapshot.isFlying) {
                currentMoveSnapshot.currentSpeed *= movementSettings.flySpeedMultiplier;
            } else if (inAir) {
                currentMoveSnapshot.currentSpeed *= movementSettings.airSpeedMultiplier;
            }

            //Apply speed
            if (movementSettings.useAccelerationMovement) {
                characterMoveVelocity *= currentAcc;
            } else {
                characterMoveVelocity *= currentMoveSnapshot.currentSpeed;
            }

#region SLOPE

            if (movementSettings.detectSlopes && detectedGround) {
                //On Ground and detecting slopes
                if (slopeDot < 1 && slopeDot > movementSettings.minSlopeDelta) {
                    var slopeVel = groundSlopeDir.normalized * slopeDot * slopeDot * movementSettings.slopeForce;
                    if (slopeDot > movementSettings.maxSlopeDelta) {
                        slopeVel.y = 0;
                    }

                    newVelocity += slopeVel;
                }


                //Project movement onto the slope
                if (characterMoveVelocity.sqrMagnitude > 0 && groundHit.normal.y > 0) {
                    //Adjust movement based on the slope of the ground you are on
                    var newMoveVector = Vector3.ProjectOnPlane(characterMoveVelocity, groundHit.normal);
                    newMoveVector.y = Mathf.Min(0, newMoveVector.y);
                    characterMoveVelocity = newMoveVector;
                    if (drawDebugGizmos_STEPUP) {
                        Debug.DrawLine(rootPosition, rootPosition + characterMoveVelocity * 2, Color.red);
                    }
                    //characterMoveVector.y = Mathf.Clamp( characterMoveVector.y, 0, moveData.maxSlopeSpeed);
                }

                if (useExtraLogging && characterMoveVelocity.y < 0) {
                    //print("Move Vector After: " + characterMoveVelocity + " groundHit.normal: " + groundHit.normal + " hitGround: " + groundHit.collider.gameObject.name);
                }
            }

            if (slopeVisualizer) {
                slopeVisualizer.LookAt(slopeVisualizer.position +
                                       (groundSlopeDir.sqrMagnitude < .1f ? transform.forward : groundSlopeDir));
            }

#endregion


#region MOVE_FORCE

            //Clamp directional movement to not add forces if you are already moving in that direction
            var flatVelocity = new Vector3(newVelocity.x, 0, newVelocity.z);
            var tryingToMove = normalizedMoveDir.sqrMagnitude > .1f;
            var rawMoveDot = Vector3.Dot(flatVelocity.normalized, normalizedMoveDir);
            //print("Directional Influence: " + (characterMoveVector - newVelocity) + " mag: " + (characterMoveVector - currentVelocity).magnitude);
            var bumpSize = characterRadius + .15f;

            //Don't drift if you are turning the character
            if (movementSettings.accelerationTurnFriction > 0 && movementSettings.useAccelerationMovement &&
                !isImpulsing && grounded &&
                tryingToMove) {
                var parallelDot = 1 - Mathf.Abs(Mathf.Clamp01(rawMoveDot));
                //print("DOT: " + parallelDot);
                newVelocity += -Vector3.ClampMagnitude(flatVelocity, currentMoveSnapshot.currentSpeed) * parallelDot *
                               movementSettings.accelerationTurnFriction;
            }

            //Stop character from moveing into colliders (Helps prevent axis aligned box colliders from colliding when they shouldn't like jumping in a voxel world)
            if (movementSettings.preventWallClipping && !currentMoveSnapshot.prevStepUp) {
                var forwardDistance = (characterMoveVelocity.magnitude + newVelocity.magnitude) * deltaTime +
                                      (characterRadius + forwardMargin);
                var forwardVector = (characterMoveVelocity + newVelocity).normalized *
                                    Mathf.Max(forwardDistance, bumpSize);
                //print("Forward vec: " + forwardVector);

                //Do raycasting after we have claculated our move direction
                var forwardHits =
                    physics.CheckAllForwardHits(rootPosition - flatVelocity.normalized * -.01f, forwardVector, true,
                        true);

                float i = 0;
                foreach (var forwardHitResult in forwardHits) {
                    //Check if this is a valid wall and not something behind a surface
                    var forwardHit = forwardHitResult;
                    var checkPoint = transform.position + new Vector3(0, characterHalfExtents.y, 0);

                    //Valid result from BoxCastAll but not a hit we want to use (happens on corners of voxels sometimes)
                    if (forwardHitResult.distance == 0) {
                        forwardHit.point = checkPoint + forwardVector;
                    }

                    if (Physics.Raycast(checkPoint, forwardHit.point - checkPoint,
                            out var rayTestHit, forwardMargin + forwardHit.distance,
                            movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                        //This is more accurate and may be a complete different wall than the box cast found
                        forwardHit = rayTestHit;
                        if (drawDebugGizmos_WALLCLIPPING) {
                            Debug.DrawLine(checkPoint, rayTestHit.point, Color.magenta);
                            GizmoUtils.DrawSphere(rayTestHit.point, .15f, Color.magenta);
                        }
                    } else if (drawDebugGizmos_WALLCLIPPING) {
                        GizmoUtils.DrawSphere(checkPoint, .05f, Color.white);
                        Debug.DrawLine(checkPoint, checkPoint + (forwardHit.point - checkPoint), Color.white);
                    }

                    if (drawDebugGizmos_WALLCLIPPING) {
                        var color = Color.Lerp(Color.green, Color.cyan, i / (forwardHits.Length - 1f));
                        GizmoUtils.DrawSphere(forwardHit.point, .1f, color);
                        Debug.DrawLine(forwardHit.point, forwardHit.point + forwardHit.normal, color);
                    }

                    i++;
                    if (forwardHit.distance == 0) {
                        //still invalid so skip
                        continue;
                    }

                    var isVerticalWall = 1 - Mathf.Max(0, Vector3.Dot(forwardHit.normal, Vector3.up)) >=
                                         movementSettings.maxSlopeDelta;
                    var isKinematic = forwardHit.collider?.attachedRigidbody == null ||
                                      forwardHit.collider.attachedRigidbody.isKinematic;

                    //print("Avoiding wall: " + forwardHit.collider.gameObject.name + " distance: " + forwardHit.distance + " isVerticalWall: " + isVerticalWall + " isKinematic: " + isKinematic);
                    //Stop character from walking into walls but Let character push into rigidbodies	
                    if (isVerticalWall && isKinematic) {
                        //Stop movement into this surface
                        var colliderDot = 1 - Mathf.Max(0,
                            -Vector3.Dot(forwardHit.normal, forwardVector));
                        //var colliderDot = 1 - -Vector3.Dot(forwardHit.normal, forwardVector);
                        if (Mathf.Abs(colliderDot) < .01) {
                            //|| forwardHit.distance < bumpSize) {
                            colliderDot = 0;
                        }

                        //limit movement dir based on how straight you are walking into the wall
                        characterMoveVelocity = Vector3.ProjectOnPlane(characterMoveVelocity, forwardHit.normal);
                        characterMoveVelocity.y = 0;
                        characterMoveVelocity *= colliderDot;

                        if (forwardHit.distance < characterRadius + .15f) {
                            // newVelocity.x = 0;
                            // newVelocity.z = 0;
                            //newVelocity -= flatVelocity * (1 - colliderDot);
                            //transform.position = forwardHit.point - forwardHit.normal * bumpSize;
                        }
                        //print("Collider Dot: " + colliderDot.ToString("R") + " moveVector: " + characterMoveVelocity.magnitude.ToString("R"));
                    }

                    //Push the character out of any colliders
                    // if (forwardHit.distance < characterRadius + .15f) {
                    //     newVelocity.x = 0;
                    //     newVelocity.z = 0;
                    // }
                    flatVelocity = Vector3.ClampMagnitude(newVelocity,
                        forwardHit.distance - characterRadius - forwardMargin);
                    //print("FLAT VEL: " + flatVelocity);
                    newVelocity.x -= flatVelocity.x;
                    newVelocity.z -= flatVelocity.z;
                }

                // if (forwardHits.Length == 0) {
                //     //Not hitting anything forwad, but make sure we aren't already overlapping something
                //     var boundHits = Physics.OverlapBox(transform.position + new Vector3(0, characterHalfExtents.y, 0),
                //         characterHalfExtents + new Vector3(forwardMargin, forwardMargin, forwardMargin),
                //         Quaternion.identity);
                // }

                if (!grounded && detectedGround) {
                    //Hit ground but its not valid ground, push away from it
                    print("PUSHING AWAY FROM: " + groundHit.normal);
                    newVelocity += groundHit.normal * physics.GetFlatDistance(rootPosition, groundHit.point) * .25f /
                                   deltaTime;
                }
            }

            //Instantly move at the desired speed
            var moveMagnitude = characterMoveVelocity.magnitude;
            var flatVelMagnitude = flatVelocity.magnitude;

            //Don't move character in direction its already moveing
            //Positive dot means we are already moving in this direction. Negative dot means we are moving opposite of velocity.
            //Multipy by 2 so perpendicular movement is still fully applied rather than half applied
            var dirDot = Mathf.Max(movementSettings.minAccelerationDelta, Mathf.Clamp01((1 - rawMoveDot) * 2));

            if (useExtraLogging) {
                //print("old vel: " + currentVelocity + " new vel: " + newVelocity + " move dir: " + characterMoveVelocity + " Dir dot: " + dirDot + " currentSpeed: " + currentSpeed + " grounded: " + grounded + " canJump: " + canJump + " didJump: " + didJump);
            }

            if (currentMoveSnapshot.isFlying) {
                newVelocity.x = command.moveDir.x * currentMoveSnapshot.currentSpeed;
                newVelocity.z = command.moveDir.z * currentMoveSnapshot.currentSpeed;
            } else if (!isImpulsing &&
                       !currentMoveSnapshot.airborneFromImpulse && //Not impulsing AND under our max speed
                       flatVelMagnitude < (movementSettings.useAccelerationMovement
                           ? currentMoveSnapshot.currentSpeed
                           : Mathf.Max(movementSettings.sprintSpeed, currentMoveSnapshot.currentSpeed) + 1)) {
                if (movementSettings.useAccelerationMovement) {
                    newVelocity += Vector3.ClampMagnitude(characterMoveVelocity,
                        currentMoveSnapshot.currentSpeed - flatVelMagnitude);
                } else {
                    // if(Mathf.Abs(characterMoveVelocity.x) > Mathf.Abs(newVelocity.x)){
                    // 	newVelocity.x = characterMoveVelocity.x;
                    // }
                    // if(Mathf.Abs(characterMoveVelocity.z) > Mathf.Abs(newVelocity.z)){
                    // 	newVelocity.z = characterMoveVelocity.z;
                    // }
                    if (moveMagnitude + .5f >= flatVelMagnitude) {
                        newVelocity.x = characterMoveVelocity.x;
                        newVelocity.z = characterMoveVelocity.z;
                    }
                }
            } else {
                if (movementSettings.useAccelerationMovement) {
                    //Using acceleration movement
                    newVelocity += normalizedMoveDir * (dirDot * dirDot / 2) *
                                   (groundedState == CharacterState.Sprinting
                                       ? movementSettings.sprintAccelerationForce
                                       : movementSettings.accelerationForce);
                } else {
                    //Impulsing
                    var forwardMod = Mathf.Max(0, dirDot);
                    var addedForce = groundedState == CharacterState.Sprinting
                        ? movementSettings.sprintAccelerationForce
                        : movementSettings.accelerationForce;
                    if (flatVelMagnitude + addedForce < currentMoveSnapshot.currentSpeed) {
                        forwardMod = 1;
                    }

                    //Apply the force
                    newVelocity += normalizedMoveDir * forwardMod * addedForce;

                    //Never get faster than you've been impulsed
                    var flatVel = Vector3.ClampMagnitude(new Vector3(newVelocity.x, 0, newVelocity.z),
                        Mathf.Max(addedForce, flatVelMagnitude));
                    newVelocity.x = flatVel.x;
                    newVelocity.z = flatVel.z;
                }
            }

            //print("isreplay: " + replaying + " didHitForward: " + didHitForward + " moveVec: " + characterMoveVector + " colliderDot: " + colliderDot  + " for: " + forwardHit.collider?.gameObject.name + " point: " + forwardHit.point);

#endregion

#endregion

#region STEP_UP

            //Step up as the last step so we have the most up to date velocity to work from
            var didStepUp = false;
            if (movementSettings.detectStepUps && //Want to check step ups
                (!command.crouch || !movementSettings.preventStepUpWhileCrouching) && //Not blocked by crouch
                (movementSettings.assistedLedgeJump ||
                 currentMoveSnapshot.canJump >
                 0) && //Grounded // Used to be currentMoveSnapshot.timeSinceBecameGrounded > .05
                Mathf.Abs(newVelocity.x) + Mathf.Abs(newVelocity.z) > .05f) {
                //Moveing
                var (hitStepUp, onRamp, pointOnRamp, stepUpVel) = physics.StepUp(rootPosition,
                    newVelocity, deltaTime, detectedGround ? groundHit.normal : Vector3.up);
                if (hitStepUp) {
                    didStepUp = hitStepUp;
                    var oldPos = rootPosition;
                    if (pointOnRamp.y > oldPos.y) {
                        SnapToY(pointOnRamp.y);
                        //airshipTransform.position = Vector3.MoveTowards(oldPos, transform.position, deltaTime);
                    }

                    //print("STEPPED UP. Vel before: " + newVelocity);
                    newVelocity = Vector3.ClampMagnitude(
                        new Vector3(stepUpVel.x, Mathf.Max(stepUpVel.y, newVelocity.y), stepUpVel.z),
                        newVelocity.magnitude);
                    //print("PointOnRamp: " + pointOnRamp + " position: " + rootPosition + " vel: " + newVelocity);

                    if (drawDebugGizmos_STEPUP) {
                        GizmoUtils.DrawSphere(oldPos, .01f, Color.red, 4, 4);
                        GizmoUtils.DrawSphere(rootPosition + newVelocity, .03f, new Color(1, .5f, .5f), 4, 4);
                    }

                    currentMoveSnapshot.state =
                        groundedState; //Force grounded state since we are in the air for the step up
                    grounded = true;
                }
            }

#endregion

#region CROUCH

            // Prevent falling off blocks while crouching
            if (movementSettings.preventFallingWhileCrouching && !currentMoveSnapshot.prevStepUp &&
                currentMoveSnapshot.isCrouching && !didJump && grounded) {
                var distanceCheck = movementSettings.characterRadius * 3 + newVelocity.magnitude * deltaTime;
                var normalizedVel = newVelocity.normalized;
                var projectedPosition = rootPosition + normalizedVel * distanceCheck;
                if (drawDebugGizmos_CROUCH) {
                    GizmoUtils.DrawSphere(projectedPosition, .1f, Color.blue, 4, .1f);
                }

                var (groundedInMoveDirection, _, _) =
                    physics.CheckIfGrounded(projectedPosition, newVelocity, normalizedVel);
                var foundGroundedDir = false;
                if (!groundedInMoveDirection) {
                    var wallStartPos = projectedPosition + new Vector3(0, -.5f, 0);
                    if (drawDebugGizmos_CROUCH) {
                        GizmoUtils.DrawSphere(wallStartPos, .1f, Color.white, 4, .1f);
                        Debug.DrawLine(wallStartPos, wallStartPos + -distanceCheck * normalizedVel, Color.white,
                            .1f);
                    }

                    if (Physics.Raycast(projectedPosition + new Vector3(0, -.25f, 0), -normalizedVel,
                            out var cliffHit, distanceCheck,
                            movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                        if (drawDebugGizmos_CROUCH) {
                            GizmoUtils.DrawSphere(cliffHit.point, .1f, Color.red, 4, .1f);
                        }

                        //Stop movement into this surface
                        // var colliderDot = 1 - Mathf.Max(0,
                        //     -Vector3.Dot(-cliffHit.normal, newVelocity));
                        var colliderDot = Vector3.Dot(newVelocity, -cliffHit.normal);
                        //colliderDot *= .9f;
                        //var colliderDot = 1 - -Vector3.Dot(forwardHit.normal, forwardVector);
                        // if (Mathf.Abs(colliderDot) < .01f || normalizedVel.sqrMagnitude < 1f) {
                        //     colliderDot = 0;
                        // }
                        var flatPoint = new Vector3(cliffHit.point.x, transform.position.y, cliffHit.point.z);
                        //If we are too close to the edge or if there is an obstruction in the way
                        if (Vector3.Distance(flatPoint, transform.position) < bumpSize - forwardMargin
                            || Physics.Raycast(transform.position + new Vector3(0, .25f, 0), newVelocity, distanceCheck,
                                movementSettings.groundCollisionLayerMask)) {
                            //Snap back to the bump distance so you never inch your way to the edge 
                            //newVelocity = new Vector3(0, newVelocity.y, 0);
                            //var newPos = cliffHit.point - normalizedVel * (bumpSize-forwardMargin);
                            //transform.position = new Vector3(newPos.x, transform.position.y, newPos.z);

                            newVelocity = -cliffHit.normal;
                        } else {
                            //limit movement dir based on how straight you are walking into the edge
                            // characterMoveVelocity = Vector3.ProjectOnPlane(characterMoveVelocity, -cliffHit.normal);
                            // characterMoveVelocity.y = 0;
                            // characterMoveVelocity *= colliderDot;
                            // normalizedMoveDir = characterMoveVelocity.normalized;

                            newVelocity -= colliderDot * -cliffHit.normal;
                            //newVelocity *= Mathf.Max(0, -colliderDot);
                            if (newVelocity.sqrMagnitude < 2f) {
                                newVelocity = Vector3.zero;
                            }

                            //With this new velocity are we going to fall off a different ledge? 
                            if (!Physics.Raycast(
                                    new Vector3(0, 1.25f, 0) + transform.position +
                                    newVelocity.normalized * distanceCheck, Vector3.down, 1.5f)) {
                                //Nothing in the direction of the new velocity
                                newVelocity = Vector3.zero;
                            }
                            //newVelocity *= colliderDot;
                        }
                    }
                }
            }

#endregion

#region APPLY FORCES

            //Stop character from moveing into colliders (Helps prevent axis aligned box colliders from colliding when they shouldn't like jumping in a voxel world)
            if (movementSettings.preventWallClipping && !currentMoveSnapshot.prevStepUp) {
                var velY = newVelocity.y;
                flatVelocity = new Vector3(newVelocity.x, 0, newVelocity.z);
                var minDistance = characterRadius + forwardMargin;
                var forwardDistance = Mathf.Max(flatVelocity.magnitude * deltaTime, minDistance);
                var forwardVector = flatVelocity.normalized * Mathf.Max(forwardDistance, bumpSize);
                //print("Forward vec: " + forwardVector);

                //Do raycasting after we have claculated our move direction
                var forwardHits =
                    physics.CheckAllForwardHits(rootPosition - flatVelocity.normalized * -forwardMargin, forwardVector,
                        true,
                        true);

                float i = 0;
                var label = "ForwardHitCounts: " + forwardHits.Length + "\n";
                var forcedCount = 0;
                foreach (var forwardHitResult in forwardHits) {
                    label += "Hit " + i + " Point: " + forwardHitResult.point + " Normal: " + forwardHitResult.normal;
                    //Check if this is a valid wall and not something behind a surface
                    var forwardHit = forwardHitResult;
                    var checkPoint = transform.position + new Vector3(0, characterHalfExtents.y, 0);

                    if (drawDebugGizmos_WALLCLIPPING) {
                        var color = Color.Lerp(Color.green, Color.cyan, i / (forwardHits.Length - 1f));
                        GizmoUtils.DrawSphere(forwardHit.point, .05f, color, 4, .2f);
                        Debug.DrawLine(forwardHit.point, forwardHit.point + forwardHit.normal, color, .2f);
                    }

                    //Valid result from BoxCastAll but not a hit we want to use (happens on corners of voxels sometimes)
                    if (forwardHitResult.distance == 0) {
                        forwardHit.point = checkPoint + forwardVector;
                        label += " ZEROED HIT POINT";
                    }

                    var checkDir = (forwardHit.point - checkPoint).normalized;
                    var checkDistance = forwardMargin +
                                        Mathf.Max(forwardHit.distance, movementSettings.characterRadius * 2);
                    if (Physics.Raycast(checkPoint, checkDir,
                            out var rayTestHit, checkDistance,
                            movementSettings.groundCollisionLayerMask, QueryTriggerInteraction.Ignore)) {
                        //This is more accurate and may be a complete different wall than the box cast found
                        forwardHit = rayTestHit;
                        if (drawDebugGizmos_WALLCLIPPING) {
                            Debug.DrawLine(checkPoint, checkPoint + checkDir * checkDistance, Color.magenta, .2f);
                            GizmoUtils.DrawSphere(rayTestHit.point, .04f, Color.magenta, 4, .2f);
                            Debug.DrawLine(rayTestHit.point, rayTestHit.point + rayTestHit.normal, Color.magenta, .2f);
                        }
                    } else if (drawDebugGizmos_WALLCLIPPING) {
                        GizmoUtils.DrawSphere(checkPoint, .03f, Color.white, 4, .2f);
                        Debug.DrawLine(checkPoint, checkPoint + checkDir * checkDistance, Color.white, .2f);
                    }

                    i++;
                    if (forwardHit.distance == 0) {
                        //still invalid so skip
                        continue;
                    }

                    var isVerticalWall = 1 - Mathf.Max(0, Vector3.Dot(forwardHit.normal, Vector3.up)) >=
                                         movementSettings.maxSlopeDelta;
                    var isKinematic = forwardHit.collider?.attachedRigidbody == null ||
                                      forwardHit.collider.attachedRigidbody.isKinematic;

                    //print("Avoiding wall: " + forwardHit.collider.gameObject.name + " distance: " + forwardHit.distance + " isVerticalWall: " + isVerticalWall + " isKinematic: " + isKinematic);
                    //Stop character from walking into walls but Let character push into rigidbodies	
                    if (isVerticalWall && isKinematic) {
                        //Stop movement into this surface
                        var colliderDot = 1 - Mathf.Max(0,
                            -Vector3.Dot(forwardHit.normal, forwardVector));
                        //var colliderDot = 1 - -Vector3.Dot(forwardHit.normal, forwardVector);
                        if (Mathf.Abs(colliderDot) < .01) {
                            //|| forwardHit.distance < bumpSize) {
                            colliderDot = 0;
                        }

                        // flatVelocity = Vector3.ClampMagnitude(newVelocity,
                        //     forwardHit.distance - characterRadius - forwardMargin);
                        // //print("FLAT VEL: " + flatVelocity);
                        // newVelocity.x -= flatVelocity.x;
                        // newVelocity.z -= flatVelocity.z;

                        var flatPoint = new Vector3(forwardHit.point.x, transform.position.y, forwardHit.point.z);
                        if (Vector3.Distance(flatPoint, transform.position) < minDistance) {
                            //Snap back to the bump distance so you never inch your way to the edge 
                            var newPos = forwardHit.point + forwardHit.normal * (bumpSize + forwardMargin);
                            if (forcedCount == 0) {
                                transform.position = new Vector3(newPos.x, transform.position.y, newPos.z);
                            } else {
                                transform.position = new Vector3(
                                    (transform.position.x + newPos.x) / 2f,
                                    transform.position.y,
                                    (transform.position.z + newPos.z) / 2f);
                            }

                            forcedCount++;
                            //newVelocity = new Vector3(0, velY, 0);

                            // var normalVel = forwardHit.normal * (Math.Abs(newVelocity.x) + Math.Abs(newVelocity.z)); 
                            // newVelocity = new Vector3(normalVel.x, velY , normalVel.z);
                        } else { }

                        newVelocity = Vector3.ProjectOnPlane(flatVelocity, forwardHit.normal);
                        //newVelocity.y = 0;
                        newVelocity *= colliderDot * .9f;
                        newVelocity.y = velY;

                        //limit movement dir based on how straight you are walking into the wall
                        // characterMoveVelocity = Vector3.ProjectOnPlane(characterMoveVelocity, forwardHit.normal);
                        // characterMoveVelocity.y = 0;
                        // characterMoveVelocity *= colliderDot;

                        // if (forwardHit.distance < characterRadius + .15f)
                        // {
                        // newVelocity.x = 0;
                        // newVelocity.z = 0;
                        //newVelocity -= flatVelocity * (1 - colliderDot);
                        //transform.position = forwardHit.point - forwardHit.normal * bumpSize;
                        // }
                        //print("Collider Dot: " + colliderDot.ToString("R") + " moveVector: " + characterMoveVelocity.magnitude.ToString("R"));
                    }

                    //Push the character out of any colliders
                    // if (forwardHit.distance < characterRadius + .15f) {
                    //     newVelocity.x = 0;
                    //     newVelocity.z = 0;
                    // }
                    label += "\n";
                }

                if (forwardHits.Length > 0) {
                    //Debug.Log(label);
                }

                // if (forwardHits.Length == 0) {
                //     //Not hitting anything forwad, but make sure we aren't already overlapping something
                //     var boundHits = Physics.OverlapBox(transform.position + new Vector3(0, characterHalfExtents.y, 0),
                //         characterHalfExtents + new Vector3(forwardMargin, forwardMargin, forwardMargin),
                //         Quaternion.identity);
                // }

                if (!grounded && detectedGround) {
                    //Hit ground but its not valid ground, push away from it
                    //print("PUSHING AWAY FROM: " + groundHit.normal);
                    newVelocity += groundHit.normal * physics.GetFlatDistance(rootPosition, groundHit.point) * .25f /
                                   deltaTime;
                }
            }

            //Clamp the velocity
            newVelocity = Vector3.ClampMagnitude(newVelocity, movementSettings.terminalVelocity);
            var magnitude = new Vector3(newVelocity.x, 0, newVelocity.z).magnitude;
            var canStopVel = !currentMoveSnapshot.airborneFromImpulse &&
                             (!inAir || movementSettings.useMinimumVelocityInAir) &&
                             !isImpulsing;
            var underMin = magnitude <= movementSettings.minimumVelocity && magnitude > .01f;
            //print("currentMoveState.airborneFromImpulse: " + currentMoveState.airborneFromImpulse + " unerMin: " +underMin + " notTryingToMove: " + notTryingToMove);
            if (canStopVel && !tryingToMove && underMin) {
                //Not intending to move so snap to zero (Fake Dynamic Friction)
                //print("STOPPING VELOCITY. CanStop: " + canStopVel + " tryingtoMove: " + tryingToMove + " underMin: " + underMin);
                newVelocity.x = 0;
                newVelocity.z = 0;
            }


            //print($"<b>JUMP STATE</b> {md.GetTick()}. <b>isReplaying</b>: {replaying}    <b>mdJump </b>: {md.jump}    <b>canJump</b>: {canJump}    <b>didJump</b>: {didJump}    <b>currentPos</b>: {rootPosition}    <b>currentVel</b>: {currentVelocity}    <b>newVel</b>: {newVelocity}    <b>grounded</b>: {grounded}    <b>currentState</b>: {state}    <b>currentMoveState.prevState</b>: {currentMoveState.prevState}    <b>mdMove</b>: {md.moveDir}    <b>characterMoveVector</b>: {characterMoveVector}");

            //Execute the forces onto the rigidbody
            // if (isImpulsing) print("Impulsed velocity resulted in " + newVelocity);
            rb.linearVelocity = newVelocity;

#endregion

#region SAVE STATE

            // if(currentMoveState.timeSinceBecameGrounded < .1){
            // 	print("LANDED! prevVel: " + currentVelocity + " newVel: " + newVelocity);
            // }

            // only update animations if we are not in a replay
            if (!replay) {
                var newState = new CharacterAnimationSyncData() {
                    state = currentMoveSnapshot.state,
                    grounded = !inAir || didStepUp,
                    sprinting = currentMoveSnapshot.isSprinting,
                    crouching = currentMoveSnapshot.isCrouching,
                    localVelocity = graphicTransform.InverseTransformDirection(newVelocity),
                    lookVector = lookVector,
                    jumping = didJump
                };
                if (newState.state != currentAnimState.state) {
                    stateChanged?.Invoke((int)newState.state);
                    if (animationHelper) {
                        animationHelper.SetState(newState);
                    }
                } else {
                    if (animationHelper) {
                        animationHelper.SetVelocity(graphicTransform.InverseTransformDirection(newVelocity));
                    }
                }

                currentAnimState = newState;
            }

            // Handle OnMoveDirectionChanged event
            if (moveDirInput != command.moveDir) {
                OnMoveDirectionChanged?.Invoke(command.moveDir);
            }

            moveDirInput = command.moveDir;

            // Record variables that will not change due to physics tick. Variables affected by physics tick will need to be
            // recorded as part of OnCaptureSnapshot so that they record the value post physics tick.
            currentMoveSnapshot.lookVector = command.lookVector;
            currentMoveSnapshot.isCrouching = command.crouch;
            currentMoveSnapshot.isGrounded = grounded;
            currentMoveSnapshot.prevStepUp = didStepUp;

#endregion

            //Track speed based on position
            if (useExtraLogging) {
                //print("Speed: " + currentSpeed + " Actual Movement Per Second: " + (physics.GetFlatDistance(rootPosition, lastPos) / deltaTime));
            }

            OnProcessedCommand?.Invoke(command, currentMoveSnapshot, replay);
        }

        public override void Interpolate(
            float delta,
            CharacterSnapshotData snapshotOld,
            CharacterSnapshotData snapshotNew) {
            rb.position = Vector3.Lerp(snapshotOld.position, snapshotNew.position, delta);
            var oldLook = new Vector3(snapshotOld.lookVector.x, 0, snapshotOld.lookVector.z);
            var newLook = new Vector3(snapshotNew.lookVector.x, 0, snapshotNew.lookVector.z);
            if (oldLook == Vector3.zero) {
                oldLook.z = 0.01f;
            }

            if (newLook == Vector3.zero) {
                newLook.z = 0.01f;
            }

            airshipTransform.rotation = Quaternion.Lerp(
                Quaternion.LookRotation(oldLook),
                Quaternion.LookRotation(newLook),
                delta);
            OnInterpolateState?.Invoke(snapshotOld, snapshotNew, delta);
        }

        public override void InterpolateReachedState(CharacterSnapshotData snapshot) {
            var newState = new CharacterAnimationSyncData() {
                state = snapshot.state,
                grounded = snapshot.isGrounded,
                sprinting = snapshot.isSprinting,
                crouching = snapshot.isCrouching,
                localVelocity = graphicTransform.InverseTransformDirection(snapshot.velocity),
                lookVector = snapshot.lookVector,
                jumping = snapshot.jumpCount > currentMoveSnapshot.jumpCount
            };
            var changed = newState.state != currentAnimState.state;

            if (animationHelper) {
                animationHelper.SetState(newState);
            }

            currentMoveSnapshot = snapshot;
            currentAnimState = newState;

            if (changed) {
                stateChanged?.Invoke((int)newState.state);
            }

            OnInterpolateReachedState?.Invoke(snapshot);
        }

        public void LateUpdate() {
            // We only update rotation in late update if we are running on a client that is controlling
            // this system
            if (isServer && !isClient) {
                return;
            }

            if (mode == NetworkedStateSystemMode.Observer) {
                return;
            }

            if (!_smoothLookVector) {
                var lookTarget = new Vector3(lookVector.x, 0, lookVector.z);
                if (lookTarget == Vector3.zero) {
                    lookTarget = new Vector3(0, 0, .01f);
                }

                //Instantly rotate for owner
                airshipTransform.rotation = Quaternion.LookRotation(lookTarget).normalized;
            } else {
                //Tween to rotation
                var lookTarget = new Vector3(lookVector.x, 0, lookVector.z);
                if (lookTarget == Vector3.zero) {
                    lookTarget = new Vector3(0, 0, .01f);
                }

                airshipTransform.rotation = Quaternion.Lerp(
                    airshipTransform.rotation,
                    Quaternion.LookRotation(lookTarget),
                    ownerRotationLerpMod * Time.deltaTime);
            }
        }

        public void Update() {
            if (correctionTime < 1) {
                correctionTime += correctionInterpTime == 0 ? 1 : Time.deltaTime / correctionInterpTime;
                airshipTransform.localPosition = Vector3.Lerp(correctionOffset, Vector3.zero,
                    correctionTime);
            }
        }

#region Helpers

        private void SnapToY(float newY) {
            var newPos = rb.position;
            newPos.y = newY;
            rb.position = newPos;
        }

#endregion

#region TypeScript Interaction

        public double GetLocalSimulationTimeFromCommandNumber(int commandNumber) {
            CharacterSnapshotData localState = null;
            foreach (var state in manager.stateHistory.Values) {
                if (state.lastProcessedCommand >= commandNumber) {
                    localState = state;
                    break;
                }
            }

            if (localState == null) {
                Debug.LogWarning(
                    $"Unable to find predicted state for command number {commandNumber}. Returning 0 as simulation time.");
                return 0;
            }

            return localState.time;
        }

        public bool RequestResimulation(int commandNumber) {
            CharacterSnapshotData clientPredictedState = null;
            foreach (var predictedState in manager.stateHistory.Values) {
                if (predictedState.lastProcessedCommand == commandNumber) {
                    clientPredictedState = predictedState;
                    break;
                }
            }

            if (clientPredictedState == null) {
                Debug.LogWarning($"Unable to find predicted state for command number {commandNumber} on " + name +
                                 ". Resimulation will not be performed.");
                return false;
            }

            AirshipSimulationManager.Instance.ScheduleResimulation((resimulate) => {
                Debug.LogWarning("Resimulating for TS");
                resimulate(clientPredictedState.time);
            });

            return true;
        }

        public void SetMoveInput(Vector3 moveDir, bool jump, bool sprinting, bool crouch, int moveDirModeInt) {
            moveDir = moveDir.normalized;
            var moveDirMode = (MoveDirectionMode)moveDirModeInt;
            switch (moveDirMode) {
                case MoveDirectionMode.World:
                    moveDirInput = moveDir;
                    break;
                case MoveDirectionMode.Character:
                    moveDirInput = graphicTransform.TransformDirection(moveDir);
                    break;
                case MoveDirectionMode.Camera:
                    var forwardZeroY = _cameraTransform.forward;
                    forwardZeroY = new Vector3(forwardZeroY.x, 0, forwardZeroY.z).normalized;
                    var angle = Mathf.Atan2(forwardZeroY.x, forwardZeroY.z) * Mathf.Rad2Deg;
                    moveDirInput = Quaternion.AngleAxis(angle, Vector3.up) * moveDir;
                    break;
                default:
                    Debug.LogWarning($"Unknown move direction input: {moveDirModeInt}");
                    break;
            }

            crouchInput = crouch;
            sprintInput = sprinting;
            jumpInput = jump;

            _smoothLookVector = moveDirMode == MoveDirectionMode.Camera;
        }

        public void SetLookVector(Vector3 lookVector) {
            OnNewLookVector?.Invoke(lookVector);
            SetLookVectorRecurring(lookVector);
        }

        /// <summary>
        /// Manually force the look direction of the character without triggering the OnNewLookVector event. 
        /// Useful for something that is updating the lookVector frequently and needs to listen for other scripts modifying the lookVector. 
        /// </summary>
        /// <param name="lookVector"></param>
        public void SetLookVectorRecurring(Vector3 lookVector) {
            // Don't set look vectors on observed characters
            if (mode == NetworkedStateSystemMode.Observer) {
                return;
            }

            // If we are the client creating input, we want to set the actual local look vector.
            // It will be moved into the state and sent to the server in the next snapshot.
            if (mode == NetworkedStateSystemMode.Input || (mode == NetworkedStateSystemMode.Authority && isClient)) {
                this.lookVector = lookVector;
                return;
            }

            // If we are an authoritative server, we set the current move state to use this new look vector.
            // This will get sent to the client as the authoritative truth and reconciled on the next snapshot.
            // Keep in mind that the client overwrites this on each tick, so the timing of this set is important (needs to be after move tick).
            // It's generally better to just force a look vector on the client because reconciled camera
            // rotation makes people nauseous.
            if (mode == NetworkedStateSystemMode.Authority) {
                this.lookVector = lookVector; // we set the input look vector for server generated commands
                currentMoveSnapshot.lookVector
                    = lookVector; // we set the snapshot vector for predicted client reconcile
            }
        }

        public void SetLookVectorRecurringToMoveDir() {
            // Don't set look vectors on observed characters
            if (mode == NetworkedStateSystemMode.Observer) {
                return;
            }

            if (moveDirInput == Vector3.zero) {
                return;
            }

            // If we are the client creating input, we want to set the actual local look vector.
            // It will be moved into the state and sent to the server in the next snapshot.
            if (mode == NetworkedStateSystemMode.Input || (mode == NetworkedStateSystemMode.Authority && isClient)) {
                lookVector = moveDirInput;
                return;
            }

            // If we are an authoritative server, we set the current move state to use this new look vector.
            // This will get sent to the client as the authoritative truth and reconciled on the next snapshot.
            // Keep in mind that the client overwrites this on each tick, so the timing of this set is important.
            // It's generally better to just force a look vector on the client because reconciled camera
            // rotation makes people nauseous.
            if (mode == NetworkedStateSystemMode.Authority) {
                lookVector = moveDirInput; // for server generated commands. Ignored in any other case
                currentMoveSnapshot.lookVector = moveDirInput;
            }
        }

        public void SetCustomInputData(BinaryBlob data) {
            customInputData = data;
            // print("Custom input bytes: " + data.dataSize);
        }

        public void SetCustomSnapshotData(BinaryBlob data) {
            customSnapshotData = data;
            // print("Custom snapshot bytes: " + data.dataSize);
        }

        public void Teleport(Vector3 position) {
            if (mode == NetworkedStateSystemMode.Observer && isServer) {
                RpcTeleport(position);
                return;
            }

            TeleportAndLook(position,
                isClient && mode != NetworkedStateSystemMode.Observer ? lookVector : currentMoveSnapshot.lookVector);
        }

        public void TeleportAndLook(Vector3 position, Vector3 lookVector) {
            if (mode == NetworkedStateSystemMode.Observer && isServer) {
                RpcTeleportAndLook(position, lookVector);
                return;
            }

            // TS listens to this to update the local camera.
            // Position will update from reconcile, but we handle look direction manually.
            if (mode == NetworkedStateSystemMode.Authority && isServer && !manager.serverGeneratesCommands) {
                RpcSetLookVector(lookVector);
            }

            // TODO: why? Copied from old movement
            currentMoveSnapshot.airborneFromImpulse = true;
            rb.MovePosition(position);
            SetLookVector(lookVector);
        }

        public void SetMovementEnabled(bool isEnabled) {
            disableInput = !isEnabled;
        }

        public void SetDebugFlying(bool flying) {
            if (!movementSettings.allowDebugFlying) {
                // Debug.LogError("Unable to fly from console when allowFlying is false. Set this characters CharacterMovementData to allow flying if needed");
                return;
            }

            SetFlying(flying);
        }

        public void SetFlying(bool flyModeEnabled) {
            if (mode == NetworkedStateSystemMode.Observer && isServer) {
                RpcSetFlying(flyModeEnabled);
                return;
            }

            currentMoveSnapshot.isFlying = flyModeEnabled;
        }

        public void AddImpulse(Vector3 impulse) {
            // print("Adding impulse: " + impulse);
            if (mode == NetworkedStateSystemMode.Observer && isServer) {
                RpcAddImpulse(impulse);
                return;
            }

            SetImpulse(pendingImpulse + impulse);
        }

        public void SetImpulse(Vector3 impulse) {
            if (mode == NetworkedStateSystemMode.Observer && isServer) {
                RpcSetImpulse(impulse);
                return;
            }

            pendingImpulse = impulse;
        }

        public void IgnoreGroundCollider(Collider collider, bool ignore) {
            if (ignore) {
                physics.ignoredColliders.TryAdd(collider.GetInstanceID(), collider);
            } else {
                physics.ignoredColliders.Remove(collider.GetInstanceID());
            }
        }

        public bool IsIgnoringCollider(Collider collider) {
            return physics.ignoredColliders.ContainsKey(collider.GetInstanceID());
        }

        public void SetVelocity(Vector3 velocity) {
            if (mode == NetworkedStateSystemMode.Observer && isServer) {
                RpcVelocity(velocity);
                return;
            }

            if (mode == NetworkedStateSystemMode.Observer) {
                Debug.LogWarning("Attempted to set velocity on an observed player. This will not work.");
                return;
            }

            rb.linearVelocity = velocity;
        }

        // TODO: check if we should have this or make people use movement.currentMoveState.velocity
        public Vector3 GetVelocity() {
            return rb.linearVelocity;
        }

        public int GetState() {
            return (int)currentMoveSnapshot.state;
        }

#endregion

#region Typescript Data Access Functions

        public bool IsAuthority() {
            return mode == NetworkedStateSystemMode.Authority;
        }

        public bool IsFlying() {
            return currentMoveSnapshot.isFlying;
        }

        public Vector3 GetLookVector() {
            // this.lookVector will only get populated when we are the one creating the inputs
            if (mode == NetworkedStateSystemMode.Input) {
                return lookVector;
            }

            if (mode == NetworkedStateSystemMode.Authority && isClient) {
                return lookVector;
            }

            return currentMoveSnapshot.lookVector;
        }

        public Vector3 GetMoveDir() {
            // this.moveDirInput will only get populated when we are the one creating the inputs
            if (mode == NetworkedStateSystemMode.Input) {
                return moveDirInput;
            }

            if (mode == NetworkedStateSystemMode.Authority && isClient) {
                return moveDirInput;
            }

            return moveDirInput;
        }

        public Vector3 GetPosition() {
            return rb.position;
        }

        // This is used by typescript to allow custom data to be compared in TS and the result used
        // in C#. See the CharacterSnapshotData file for how compareResult is used.
        public bool compareResult = false;

        public void SetComparisonResult(bool result) {
            compareResult = result;
        }

        public void FireTsCompare(CharacterSnapshotData a, CharacterSnapshotData b) {
            OnCompareSnapshots?.Invoke(a, b);
        }

#endregion

#region RPCs

        [Command]
        public void CAuthJumpedEvent(Vector3 velocity) {
            // Only used in the client authoritative networking mode.
            if (mode != NetworkedStateSystemMode.Observer) {
                return;
            }

            OnJumped?.Invoke(velocity);
            SAuthJumpedEvent(velocity);
        }

        [Command]
        public void CAuthImpactEvent(Vector3 velocity, RaycastHit hitInfo) {
            // Only used in the client authoritative networking mode.
            if (mode != NetworkedStateSystemMode.Observer) {
                return;
            }

            OnImpactWithGround?.Invoke(velocity, hitInfo);
            SAuthImpactEvent(velocity, hitInfo);
        }

        [ClientRpc(includeOwner = false)]
        public void SAuthJumpedEvent(Vector3 velocity) {
            OnJumped?.Invoke(velocity);
        }

        [ClientRpc(includeOwner = false)]
        public void SAuthImpactEvent(Vector3 velocity, RaycastHit hitInfo) {
            OnImpactWithGround?.Invoke(velocity, hitInfo);
        }

        /**
         * RPCs are used in client authoritative networking to allow server side code to move clients. These
         * RPCs are not used in server authoritative mode.
         */
        [TargetRpc]
        public void RpcSetImpulse(Vector3 impulse) {
            SetImpulse(impulse);
        }

        [TargetRpc]
        public void RpcAddImpulse(Vector3 impulse) {
            AddImpulse(impulse);
        }

        [TargetRpc]
        public void RpcVelocity(Vector3 velocity) {
            SetVelocity(velocity);
        }

        [TargetRpc]
        public void RpcTeleport(Vector3 position) {
            Teleport(position);
        }

        [TargetRpc]
        public void RpcSetLookVector(Vector3 lookVector) {
            SetLookVector(lookVector);
        }

        [TargetRpc]
        public void RpcTeleportAndLook(Vector3 position, Vector3 look) {
            TeleportAndLook(position, look);
        }

        [TargetRpc]
        public void RpcSetFlying(bool flying) {
            SetFlying(flying);
        }

#endregion
    }
}