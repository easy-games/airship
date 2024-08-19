using System;
using System.Collections.Generic;
using Assets.Luau;
using Code.Player.Character.API;
using Code.Player.Human.Net;
using Mirror;
using UnityEngine;

namespace Code.Player.Character {
	[LuauAPI]
	[RequireComponent(typeof(Rigidbody))]
	public class CharacterMovement : NetworkBehaviour {
		[Header("References")]
		public Rigidbody rigidbody;
		public Transform rootTransform; //The true position transform
		public Transform networkTransform; //The visual transform controlled by this script
		public Transform graphicTransform; //A transform that games can animate
		public CharacterMovementData moveData;
		public CharacterAnimationHelper animationHelper;
		public BoxCollider mainCollider;
		public Transform slopeVisualizer;

		[Header("Debug")]
		public bool drawDebugGizmos = false;
		public bool useExtraLogging = false;

		[Header("Variables")]
		public float observerRotationLerpMod = 1;


		//Events
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
		/// Params: MoveInputData moveData, boolean isReplay, 
		/// </summary>
		public event Action<object> OnBeginMove;
		/// <summary>
		/// Called at the end of a Move function.
		/// Params: MoveInputData moveData, boolean isReplay
		/// </summary>
		public event Action<object> OnEndMove;

		/// <summary>
		/// Params: MoveModifier
		/// </summary>
		public event Action<object> OnAdjustMove;

		/// <summary>
		/// Params: Vector3 velocity, ushort blockId
		/// </summary>
		public event Action<object> OnImpactWithGround;
		public event Action<object> OnMoveDirectionChanged;
	
		/// <summary>
		/// Called when movement processes a new jump
		/// Params: Vector3 velocity
		/// </summary>
		public event Action<object> OnJumped;

		[NonSerialized] public RaycastHit groundedRaycastHit;


		public float standingCharacterHeight => moveData.characterHeight;
		public float characterRadius => moveData.characterRadius;
		public Vector3 characterHalfExtents {get; private set;}

		public float currentCharacterHeight {get; private set;}

		private bool disableInput = false;
		// Controls
		private bool _jump;
		private Vector3 _moveDir;
		private bool _sprint;
		private bool _crouch;
		private bool _flying;
		private bool _allowFlight;

		// State
		// private PredictionRigidbody predictionRigidbody = new PredictionRigidbody();
		private Vector3 lastWorldVel = Vector3.zero;//Literal last move of gameobject in scene
		private Vector3 trackedVelocity;
		private Vector3 impulseVelocity;
		private float voxelStepUp;
		private readonly Dictionary<int, CharacterMoveModifier> moveModifiers = new();
		public bool grounded {get; private set;}
		public bool sprinting {get; private set;}
		private MoveInputData currentMoveInputData;

		/// <summary>
		/// Key: tick
		/// Value: the MoveModifier received from <c>adjustMoveEvent</c>
		/// </summary>
		private readonly Dictionary<uint, CharacterMoveModifier> moveModifierFromEventHistory = new();

		// History
		private bool prevCrouch;
		private bool prevSprint;
		private int jumpCount = 0;
		private bool alreadyJumped = false;
		private bool prevStepUp;
		private Vector2 prevMoveVector;
		private Vector3 prevMoveDir;
		private Vector3 prevLookVector;
		private uint prevTick;
		private bool prevGrounded;
		private float timeSinceBecameGrounded;
		private float timeSinceWasGrounded;
		private float stepUpStartTime;
		private float timeSinceJump;
		private Vector3 prevJumpStartPos;
		private float lastServerUpdateTime = 0;
		private float serverUpdateRefreshDelay = .1f;
		private bool airborneFromImpulse = false;
		private float currentSpeed;

		private CharacterMoveModifier prevCharacterMoveModifier = new CharacterMoveModifier() {
			speedMultiplier = 1,
		};

		private Vector3 trackedPosition = Vector3.zero;

		/// <summary>
		/// This is replicated to observers.
		/// </summary>
		[NonSerialized]
		public CharacterStateData stateData = new CharacterStateData();

		[NonSerialized] [SyncVar] public Vector3 replicatedLookVector = Vector3.one;
		public Vector3 lookVector = Vector3.one;

		private CharacterState state = CharacterState.Idle;
		private CharacterState prevState = CharacterState.Idle;

		private BinaryBlob queuedCustomData = null;

		private bool _forceReconcile;
		private int moveModifierIdCounter = 0;
		private Vector3 lastPos = Vector3.zero;
		private CharacterPhysics physics;

#region INIT

		private void OnEnable() {
			this.physics = new CharacterPhysics(this);
			this.disableInput = false;
			this._allowFlight = false;
			this._flying = false;
			this.mainCollider.enabled = true;
		}

		private void OnDisable() {
			// EntityManager.Instance.RemoveEntity(this);
			mainCollider.enabled = false;
		}
#endregion

		public void SetReplicatedState(CharacterStateData oldData, CharacterStateData newData) {
			animationHelper.SetState(newData);
			if(oldData.state != newData.state){
				this.stateChanged?.Invoke((int)newData.state);
			}
		}

		private void LateUpdate(){
			if (isClient && isOwned) {
				var lookTarget = new Vector3(this.lookVector.x, 0, this.lookVector.z);
				//Instantly rotate for owner
				networkTransform.rotation = Quaternion.LookRotation(lookTarget);
				//Notify the server of the new rotation periodically
				if (Time.time - lastServerUpdateTime > serverUpdateRefreshDelay) {
					lastServerUpdateTime = Time.time;
					SetServerLookVector(this.lookVector);
				}
			} else {
				//Tween to rotation
				var lookTarget = new Vector3(replicatedLookVector.x, 0, replicatedLookVector.z);
				networkTransform.rotation = Quaternion.Lerp(
					graphicTransform.rotation,
					Quaternion.LookRotation(lookTarget),
					observerRotationLerpMod * Time.deltaTime);
			}
		}

		public Vector3 GetLookVector() {
			if (isOwned) {
				return this.lookVector;
			}
			return this.replicatedLookVector;
		}

		private void FixedUpdate() {
			//Update the movement state of the character		
			StartMove(BuildMoveData());
		}

		private void Update(){
			if (isClient) {
				//Update visual state of client character
				var currentPos = rootTransform.position;
				var worldVel = (currentPos - trackedPosition) * (1 / (float)Time.deltaTime);
				trackedPosition = currentPos;
				if (worldVel != lastWorldVel) {
					lastWorldVel = worldVel;
					//Debug.Log("VEL: " + worldVel);
					animationHelper.SetVelocity(graphicTransform.InverseTransformDirection(worldVel));
				}
			}
		}

		private void StartMove(MoveInputData md) {
			// Observers don't calculate moves
			if (!isOwned){
				return;
			}

			this.currentMoveInputData = md;
			OnBeginMove?.Invoke(md);
			Move(md);
			OnEndMove?.Invoke(md);
		}

		[ClientRpc]
		private void ObserverOnImpactWithGround(Vector3 velocity) {
			this.OnImpactWithGround?.Invoke(velocity);
		}

		private bool CheckIfSprinting(MoveInputData md) {
			//Only sprint if you are moving forward
			// return md.Sprint && md.MoveInput.y > sprintForwardThreshold;
			return md.sprint && md.moveDir.magnitude > 0.1f;
		}
		public bool IsGrounded() {
			return grounded;
		}

		public bool IsSprinting() {
			return sprinting;
		}

#region MOVE START
		private void Move(MoveInputData md) {
			 #region INIT VARIABLES
			var characterMoveVelocity = Vector3.zero;
			var currentVelocity = this.rigidbody.velocity;// trackedVelocity;
			var newVelocity = currentVelocity;
			var isIntersecting = IsIntersectingWithBlock();
			var deltaTime = Time.deltaTime;

#region GROUNDED
			//Ground checks
			var (grounded, groundHit, detectedGround) = physics.CheckIfGrounded(transform.position, newVelocity * deltaTime, md.moveDir);
			if (isIntersecting) {
				grounded = true;
			}
			this.grounded = grounded;
			this.groundedRaycastHit = groundHit;

			if (grounded && !prevGrounded) {
				jumpCount = 0;
				timeSinceBecameGrounded = 0f;
				airborneFromImpulse = false;
			} else {
				timeSinceBecameGrounded = Math.Min(timeSinceBecameGrounded + deltaTime, 100f);
			}

			var groundSlopeDir = detectedGround ? Vector3.Cross(Vector3.Cross(groundHit.normal, Vector3.down), groundHit.normal).normalized : transform.forward;
			var slopeDot = 1-Mathf.Max(0, Vector3.Dot(groundHit.normal, Vector3.up));

			var canStand = physics.CanStand();
#endregion

			if (this.disableInput) {
				md.moveDir = Vector3.zero;
				md.crouch = false;
				md.jump = false;
				md.lookVector = prevLookVector;
				md.sprint = false;
			}
#endregion

			// Fall impact
			if (grounded && !prevGrounded) {
				this.OnImpactWithGround?.Invoke(currentVelocity);
			}

			#region GRAVITY
			if(moveData.useGravity){
				if(!_flying && !prevStepUp &&
					(moveData.useGravityWhileGrounded || ((!grounded || newVelocity.y > .01f) && !_flying))){
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
			if(!requestJump){
				alreadyJumped = false;
			}
			var didJump = false;
			var canJump = false;
			if (requestJump && !alreadyJumped && (!prevCrouch || canStand)) {
				//On the ground
				if (grounded || prevStepUp) {
					canJump = true;
				}else{
					//In the air
					// coyote jump
					if (prevMoveVector.y <= 0.02f && timeSinceWasGrounded <= moveData.jumpCoyoteTime && currentVelocity.y <= 0 && timeSinceJump > moveData.jumpCoyoteTime) {
						canJump = true;
					}
					//the first jump requires grounded, so if in the air bump the jumpCount up
					else {
						if(jumpCount == 0){
							jumpCount = 1;
						}
						
						//Multi Jump
						if (jumpCount < moveData.numberOfJumps){
							canJump = true;
						}
					}
				}

				// extra cooldown if jumping up blocks
				// if (transform.position.y - prevJumpStartPos.y > 0.01) {
				// 	if (timeSinceJump < moveData.jumpUpBlockCooldown)
				// 	{
				// 		canJump = false;
				// 	}
				// }
				// dont allow jumping when travelling up
				// if (currentVelocity.y > 0f) {
				// 	canJump = false;
				// }

				// dont jump if we already processed the jump
				// if(prevState == CharacterState.Jumping){
				// 	canJump = false;
				// }

				if (canJump) {
					// Jump
					didJump = true;
					alreadyJumped = true;
					jumpCount++;
					newVelocity.y = moveData.jumpSpeed;
					prevJumpStartPos = transform.position;
					airborneFromImpulse = false;
					OnJumped?.Invoke(newVelocity);
				}
			}

			// print($"Tick={md.GetTick()} requestJump={md.jump} canJump={canJump} grounded={grounded} reconciling={replaying}");

#endregion

#region STATE
			/*
         * Determine entity state state.
         * md.State MUST be set in all cases below.
         * We CANNOT read md.State at this point. Only md.PrevState.
         */
			var isMoving = md.moveDir.sqrMagnitude > 0.1f;
			var inAir = didJump || (!detectedGround && !prevStepUp);
			CharacterState groundedState = CharacterState.Idle; //So you can know the desired state even if we are technically in the air

			//Check to see if we can stand up from a crouch
			if((moveData.autoCrouch || prevState == CharacterState.Crouching) && !canStand){
				groundedState = CharacterState.Crouching;
			}else if (md.crouch && grounded) {
				groundedState = CharacterState.Crouching;
			} else if (isMoving) {
				if (CheckIfSprinting(md)) {
					groundedState = CharacterState.Sprinting;
					sprinting = true;
				} else {
					groundedState = CharacterState.Running;
				}
			} else {
				groundedState = CharacterState.Idle;
			}

			//If you are in the air override the state
			if (inAir) {
				state = CharacterState.Jumping;
			}else{
				//Otherwise use our found state
				state = groundedState;
			}

			if(useExtraLogging && prevState != state){
				print("New State: " + state);
			}

			if (!CheckIfSprinting(md)) {
				sprinting = false;
			}

			/*
	         * Update Time Since:
	         */

			if (didJump) {
				timeSinceJump = 0f;
			} else
			{
				timeSinceJump = Math.Min(timeSinceJump + deltaTime, 100f);
			}

			if (grounded) {
				timeSinceWasGrounded = 0f;
			} else {
				timeSinceWasGrounded = Math.Min(timeSinceWasGrounded + deltaTime, 100f);
			}

			/*
	         * md.State has been set. We can use it now.
	         */
			var normalizedMoveDir = md.moveDir.normalized;
			characterMoveVelocity.x = normalizedMoveDir.x;
			characterMoveVelocity.z = normalizedMoveDir.z;
	#region CROUCH
			// Prevent falling off blocks while crouching
			var isCrouching = groundedState == CharacterState.Crouching;
			if (moveData.preventFallingWhileCrouching && !prevStepUp && isCrouching && isMoving && grounded ) {
				var posInMoveDirection = transform.position + normalizedMoveDir * 0.2f;
				var (groundedInMoveDirection, _, _) = physics.CheckIfGrounded(posInMoveDirection, newVelocity, normalizedMoveDir);
				bool foundGroundedDir = false;
				if (!groundedInMoveDirection) {
					// Determine which direction we're mainly moving toward
					var xFirst = Math.Abs(md.moveDir.x) > Math.Abs(md.moveDir.z);
					Vector3[] vecArr = { new(md.moveDir.x, 0, 0), new (0, 0, md.moveDir.z) };
					for (int i = 0; i < 2; i++)
					{
						// We will try x dir first if x magnitude is greater
						int index = (xFirst ? i : i + 1) % 2;
						Vector3 safeDirection = vecArr[index];
						var stepPosition = transform.position + safeDirection.normalized * 0.2f;
						(foundGroundedDir, _, _) = physics.CheckIfGrounded(stepPosition, newVelocity, normalizedMoveDir);
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
			this.currentCharacterHeight = isCrouching ? standingCharacterHeight * moveData.crouchHeightMultiplier : standingCharacterHeight;
			characterHalfExtents = new Vector3(moveData.characterRadius,  this.currentCharacterHeight/2f,moveData.characterRadius);
			mainCollider.transform.localScale = characterHalfExtents*2;
			mainCollider.transform.localPosition = new Vector3(0,this.currentCharacterHeight/2f,0);
#endregion

#region FRICTION_DRAG
			var flatMagnitude = new Vector3(newVelocity.x, 0, newVelocity.z).magnitude;
			// Calculate drag:
			var dragForce = physics.CalculateDrag(currentVelocity);
			if(!_flying){
				//Ignore vertical drag so we have full control over jump and fall speeds
				dragForce.y = 0;
			}
			if(inAir){
				dragForce *= moveData.airDragMultiplier;
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
			//  print($"tick={md.GetTick()} state={_state}, velocity={_velocity}, pos={transform.position}, name={gameObject.name}, ownerId={OwnerId}");
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
		var isImpulsing = impulseVelocity != Vector3.zero;
		if (isImpulsing) {
			if(useExtraLogging){
				print("Tick: " + md.GetTick() + " isImpulsing: " + isImpulsing + " impulse force: " +impulseVelocity);
			}
			//The velocity will create drag in X and Z but ignore Y. 
			//So we need to manually drag the impulses Y so it doesn't behave differently than the other axis
			//impulseVelocity.y += Mathf.Max(physics.CalculateDrag(impulseVelocity).y, -impulseVelocity.y);	

			//Apply the impulse to the velocity
			newVelocity += impulseVelocity;
			impulseVelocity = Vector3.zero;
			airborneFromImpulse = true;
		}
#endregion

#region MOVEMENT
			// Find speed
			//Adding 1 to offset the drag force so actual movement aligns with the values people enter in moveData
			if (CheckIfSprinting(md)) {
				currentSpeed = moveData.sprintSpeed;
			} else {
				currentSpeed = moveData.speed;
			}

			if (state == CharacterState.Crouching) {
				currentSpeed *= moveData.crouchSpeedMultiplier;
			}

			if (_flying) {
				currentSpeed *= moveData.flySpeedMultiplier;
			}

			//Apply speed
			characterMoveVelocity *= currentSpeed;

			//Flying movement
			if (_flying) {
				if (md.jump) {
					newVelocity.y += moveData.verticalFlySpeed;
				}

				if (md.crouch) {
					newVelocity.y -= moveData.verticalFlySpeed;
				}
			}

#region SLOPE			
			if (moveData.detectSlopes && detectedGround){
				//On Ground and detecting slopes
				if(slopeDot < 1 && slopeDot > moveData.minSlopeDelta){
					var slopeVel = groundSlopeDir.normalized * slopeDot * slopeDot * moveData.slopeForce;
					if(slopeDot > moveData.maxSlopeDelta){
						slopeVel.y = 0;
					}
					newVelocity += slopeVel;
				}


				//Project movement onto the slope
				if(characterMoveVelocity.sqrMagnitude > 0 &&  groundHit.normal.y > 0){
					//Adjust movement based on the slope of the ground you are on
					var newMoveVector = Vector3.ProjectOnPlane(characterMoveVelocity, groundHit.normal);
					newMoveVector.y = Mathf.Min(0, newMoveVector.y);
					characterMoveVelocity = newMoveVector;
					if(drawDebugGizmos){
						GizmoUtils.DrawLine(transform.position, transform.position + characterMoveVelocity * 2, Color.red);
					}
					//characterMoveVector.y = Mathf.Clamp( characterMoveVector.y, 0, moveData.maxSlopeSpeed);
				}
				if(useExtraLogging && characterMoveVelocity.y < 0){
					print("Move Vector After: " + characterMoveVelocity + " groundHit.normal: " + groundHit.normal + " hitGround: " + groundHit.collider.gameObject.name);
				}

			}
			
			if(slopeVisualizer){
				slopeVisualizer.LookAt(slopeVisualizer.position + (groundSlopeDir.sqrMagnitude < .1f ? transform.forward : groundSlopeDir));
			}
#endregion

			//Used by step ups and forward check
			var forwardDistance = (characterMoveVelocity.magnitude + newVelocity.magnitude) * deltaTime + (this.characterRadius+.01f);
			var forwardVector = (characterMoveVelocity + newVelocity).normalized * forwardDistance;
	
#region CLAMP_MOVE
			//Clamp directional movement to not add forces if you are already moving in that direction
			var flatVelocity = new Vector3(newVelocity.x, 0, newVelocity.z);
			//print("Directional Influence: " + (characterMoveVector - newVelocity) + " mag: " + (characterMoveVector - currentVelocity).magnitude);
			
			if(moveData.preventWallClipping){
				//Do raycasting after we have claculated our move direction
				(bool didHitForward, RaycastHit forwardHit)  = physics.CheckForwardHit(rootTransform.position, forwardVector, true);

				if(!prevStepUp && didHitForward){
					var isVerticalWall = 1-Mathf.Max(0, Vector3.Dot(forwardHit.normal, Vector3.up)) >= moveData.maxSlopeDelta;

					//Stop character from walking into walls		
					if(isVerticalWall &&
						//Let character push into rigidbodies
						(forwardHit.collider?.attachedRigidbody == null ||
						forwardHit.collider.attachedRigidbody.isKinematic)){
						//Stop movement into this surface
						var colliderDot = 1-Mathf.Max(0,-Vector3.Dot(forwardHit.normal, characterMoveVelocity.normalized));
						// var tempMagnitude = characterMoveVector.magnitude;
						// characterMoveVector -= forwardHit.normal * tempMagnitude * colliderDot;
						characterMoveVelocity = Vector3.ProjectOnPlane(characterMoveVelocity, forwardHit.normal);
						characterMoveVelocity.y = 0;
						characterMoveVelocity *= colliderDot;
						//print("Collider Dot: " + colliderDot + " moveVector: " + characterMoveVector);
					}

					//Push the character out of any colliders
					flatVelocity = Vector3.ClampMagnitude(newVelocity, forwardHit.distance-characterRadius);
					newVelocity.x = flatVelocity.x;
					newVelocity.z = flatVelocity.z;
				}

				/*if(!grounded && detectedGround){
					//Hit ground but its not valid ground, push away from it
					//print("PUSHING AWAY FROM: " + groundHit.normal);
					newVelocity += groundHit.normal * physics.GetFlatDistance(transform.position, groundHit.point) * .25f / deltaTime;
				}*/
			}
			

			if(!moveData.useAccelerationMovement){
				//Instantly move at the desired speed
				var moveMagnitude = characterMoveVelocity.magnitude;
				var velMagnitude = flatVelocity.magnitude;
				var clampedIncrease = characterMoveVelocity.normalized * Mathf.Min(moveMagnitude, Mathf.Max(0, currentSpeed - velMagnitude));

				
				//Don't move character in direction its already moveing
				//Positive dot means we are already moving in this direction. Negative dot means we are moving opposite of velocity.
				var dirDot = Vector3.Dot(flatVelocity/ currentSpeed, clampedIncrease/ currentSpeed);// / currentSpeed;
				
				if(useExtraLogging){
					print("old vel: " + currentVelocity + " new vel: " + newVelocity + " move dir: " + characterMoveVelocity + " Dir dot: " + dirDot + " currentSpeed: " + currentSpeed + " grounded: " + grounded + " canJump: " + canJump + " didJump: " + didJump);
				}
		
				if (inAir){
					clampedIncrease *= moveData.airSpeedMultiplier;
				}

				if(velMagnitude < currentSpeed){
					// if(clampedIncrease.x < 0){
					// 	clampedIncrease.x = Mathf.Max(clampedIncrease.x, newVelocity.x + clampedIncrease.x);
					// }else{
					// 	clampedIncrease.x = Mathf.Min(clampedIncrease.x, newVelocity.x + clampedIncrease.x);
					// }
					// if(clampedIncrease.z < 0){
					// 	clampedIncrease.z = Mathf.Max(clampedIncrease.z, newVelocity.z + clampedIncrease.z);
					// }else{
					// 	clampedIncrease.z = Mathf.Min(clampedIncrease.z, newVelocity.z + clampedIncrease.z);
					// }
					newVelocity.x = characterMoveVelocity.x;
					newVelocity.z = characterMoveVelocity.z;
				}else{
					//dirDot = dirDot - 1 / 2;
					clampedIncrease *= -Mathf.Min(0, dirDot-1);
					newVelocity += clampedIncrease;
				}
				characterMoveVelocity = clampedIncrease;
				// if(Mathf.Abs(newVelocity.x) < Mathf.Abs(characterMoveVelocity.x)){
				// 	newVelocity.x = characterMoveVelocity.x;
				// }
				// if(Mathf.Abs(newVelocity.y) < Mathf.Abs(characterMoveVelocity.y)){
				// 	newVelocity.y = characterMoveVelocity.y;
				// }
				// if(Mathf.Abs(newVelocity.z) < Mathf.Abs(characterMoveVelocity.z)){
				// 	newVelocity.z = characterMoveVelocity.z;
				// }
			}
			//print("isreplay: " + replaying + " didHitForward: " + didHitForward + " moveVec: " + characterMoveVector + " colliderDot: " + colliderDot  + " for: " + forwardHit.collider?.gameObject.name + " point: " + forwardHit.point);
#endregion
#endregion

#region STEP_UP
		//Step up as the last step so we have the most up to date velocity to work from
		var didStepUp = false;
		if(moveData.detectStepUps && !md.crouch){
			(bool hitStepUp, bool onRamp, Vector3 pointOnRamp, Vector3 stepUpVel) = physics.StepUp(rootTransform.position, newVelocity + characterMoveVelocity, deltaTime, detectedGround ? groundHit.normal: Vector3.up);
			if(hitStepUp){
				didStepUp = hitStepUp;
				var oldPos = transform.position;
				if(pointOnRamp.y > oldPos.y){
					SnapToY(pointOnRamp.y, true);
					//networkTransform.position = Vector3.MoveTowards(oldPos, transform.position, deltaTime);
				}
				newVelocity = Vector3.ClampMagnitude(new Vector3(stepUpVel.x, Mathf.Max(stepUpVel.y, newVelocity.y), stepUpVel.z), newVelocity.magnitude);
				var debugPoint = transform.position;
				debugPoint.y = pointOnRamp.y;
				debugPoint += newVelocity * deltaTime;
				//print("PointOnRamp: " + pointOnRamp + " position: " + transform.position + " velY: " + newVelocity.y);
				
				if(drawDebugGizmos){
					GizmoUtils.DrawSphere(debugPoint, .03f, Color.red, 4, 4);
				}
				state = groundedState;//Force grounded state since we are in the air for the step up
			}
			// if(!didStepUp){
			// 	networkTransform.localPosition = Vector3.zero;
			// }
		}
#endregion
			
#region APPLY FORCES
			if(moveData.useAccelerationMovement){
				newVelocity += characterMoveVelocity;
			}

			//Clamp the velocity
			newVelocity = Vector3.ClampMagnitude(newVelocity, moveData.terminalVelocity);
			if(!airborneFromImpulse && (!inAir || moveData.useMinimumVelocityInAir) && !isImpulsing
				&& normalizedMoveDir.sqrMagnitude < .1f 
				&& Mathf.Abs(newVelocity.x + newVelocity.z) < moveData.minimumVelocity
				){
				//Not intending to move so snap to zero (Fake Dynamic Friction)
				newVelocity.x = 0;
				newVelocity.z = 0;
			}
			
			//print($"<b>JUMP STATE</b> {md.GetTick()}. <b>isReplaying</b>: {replaying}    <b>mdJump </b>: {md.jump}    <b>canJump</b>: {canJump}    <b>didJump</b>: {didJump}    <b>currentPos</b>: {transform.position}    <b>currentVel</b>: {currentVelocity}    <b>newVel</b>: {newVelocity}    <b>grounded</b>: {grounded}    <b>currentState</b>: {state}    <b>prevState</b>: {prevState}    <b>mdMove</b>: {md.moveDir}    <b>characterMoveVector</b>: {characterMoveVector}");
			
			//Execute the forces onto the rigidbody
			this.rigidbody.velocity = newVelocity;
			trackedVelocity = newVelocity;
#endregion

			
#region SAVE STATE
			//Replicate the look vector
			SetLookVector(md.lookVector);

			//Fire state change event
			TrySetState(new CharacterStateData() {
				state = state,
				grounded = !inAir || didStepUp,
				sprinting = sprinting,
				crouching = isCrouching,
			});

			if(didJump){
				CommandTriggerJump();
				//Fire locally immediately
				this.animationHelper.TriggerJump();
			}

			// Handle OnMoveDirectionChanged event
			if (prevMoveDir != md.moveDir) {
				OnMoveDirectionChanged?.Invoke(md.moveDir);
			}
			prevState = state;
			prevSprint = md.sprint;
			prevCrouch = md.crouch;
			prevMoveVector = characterMoveVelocity;
			prevMoveDir = md.moveDir;
			prevGrounded = grounded;
			prevTick = md.GetTick();
			prevLookVector = md.lookVector;
			prevStepUp = didStepUp;
#endregion

			if(useExtraLogging){
				print("Speed: " + currentSpeed + " Actual Movement Per Second: " + (physics.GetFlatDistance(rootTransform.position, lastPos) / deltaTime));
			}
			lastPos = transform.position;
		}
#endregion
#region MOVE END
#endregion

		private MoveInputData BuildMoveData() {
			//Let TS apply custom data
			OnSetCustomData?.Invoke();

			var customData = queuedCustomData;
			queuedCustomData = null;

			MoveInputData moveData = new MoveInputData(_moveDir, _jump, _crouch, _sprint, this.lookVector, customData);

			return moveData;
		}

		private void SnapToY(float newY, bool forceSnap){
			if(useExtraLogging){
				print("Snapping to Y: " + newY);
			}
			var newPos = this.rigidbody.transform.position;
			newPos.y = newY;
			rigidbody.position = newPos;
		}

		public void Teleport(Vector3 position) {
			TeleportAndLook(position, isOwned ? lookVector : replicatedLookVector);
		}

		public void TeleportAndLook(Vector3 position, Vector3 lookVector) {
			if (useExtraLogging) {
				print("Teleporting to: " + position);
			}
			if (!this.isServer) {
				//Teleport Locally
				TeleportInternal(position, lookVector);
			} else {
				//Tell client to teleport
				RpcTeleport(base.connectionToClient, position, lookVector);
			}
		}

		[TargetRpc]
		private void RpcTeleport(NetworkConnection conn, Vector3 pos, Vector3 lookVector) {
			this.TeleportInternal(pos, lookVector);
		}

		private void TeleportInternal(Vector3 pos, Vector3 lookVector){
			this.rigidbody.position = pos;
			this.lookVector = lookVector;
			this.replicatedLookVector = lookVector;
		}

		[Server]
		public void SetVelocity(Vector3 velocity) {
			SetVelocityInternal(velocity);
			// if (Owner.ClientId != -1) {
			// 	RpcSetVelocity(Owner, velocity);
			// }
		}

		public void DisableMovement() {
			SetVelocity(Vector3.zero);
			disableInput = true;
		}

		public void EnableMovement() {
			disableInput = false;
		}

		private void SetVelocityInternal(Vector3 velocity) {
			if(useExtraLogging){
				print("Setting velocity: " + velocity);
			}

			this.rigidbody.velocity = velocity;
			_forceReconcile = true;
		}

		[TargetRpc]
		private void RpcSetVelocity(NetworkConnection conn, Vector3 velocity) {
			SetVelocityInternal(velocity);
		}

#region TS_ACCESS
		public void SetMoveInput(Vector3 moveDir, bool jump, bool sprinting, bool crouch, bool moveDirWorldSpace) {
			if (moveDirWorldSpace) {
				_moveDir = moveDir;
			} else {
				_moveDir = this.graphicTransform.TransformDirection(moveDir);
			}
			_crouch = crouch;
			_sprint = sprinting;
			_jump = jump;
		}

		public void AddImpulse(Vector3 impulse){
			if(useExtraLogging){
				print("Adding impulse: " + impulse);
			}
			impulseVelocity += impulse;
		}

		public void SetImpulse(Vector3 impulse){
			if(useExtraLogging){
				print("Setting impulse: " + impulse);
			}
			impulseVelocity = impulse;
		}

		public void SetLookVector(Vector3 lookVector) {
			this.lookVector = lookVector;
			this.replicatedLookVector = lookVector;
		}

		public void SetCustomData(BinaryBlob customData) {
			queuedCustomData = customData;
		}

		public int GetState() {
			if (isOwned) {
				return (int)this.state;
			}
			return (int)stateData.state;
		}

		public bool IsFlying() {
			return this._flying;
		}

		public bool IsAllowFlight() {
			return this._allowFlight;
		}

		public Vector3 GetVelocity() {
			return trackedVelocity;
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

		public float GetNextTick(){
			return prevTick+2;
		}

		public float GetPrevTick(){
			return prevTick+1;
		}

		public float GetTimeSinceWasGrounded(){
			return timeSinceWasGrounded;
		}

		public float GetTimeSinceBecameGrounded(){
			return timeSinceBecameGrounded;
		}

		public MoveInputData GetCurrentMoveInputData(){
			return currentMoveInputData;
		}

#endregion

		[Command]
		private void RpcSetFlying(bool flyModeEnabled) {
			this._flying = flyModeEnabled;
		}

		public void SetFlying(bool flying) {
			// if (flying && !this._allowFlight) {
			// 	Debug.LogError("Unable to fly when allow flight is false. Call entity.SetAllowFlight(true) first.");
			// 	return;
			// }
			// this._flying = flying;
			// RpcSetFlying(flying);
		}

		[Server]
		public void SetAllowFlight(bool allowFlight) {
			// TargetAllowFlight(base.Owner, allowFlight);
		}

		[TargetRpc]
		private void TargetAllowFlight(NetworkConnectionToClient conn, bool allowFlight) {
			// this._allowFlight = allowFlight;
			// if (this._flying && !this._allowFlight) {
			// 	this._flying = false;
			// }
		}

		private void TrySetState(CharacterStateData newStateData) {
			bool isNewState = newStateData.state != this.stateData.state;
			bool isNewData = !newStateData.Equals(this.stateData);

			// If new value in the state
			if (isNewData) {
				this.stateData = newStateData;
				CommandSetStateData(newStateData);
			}

			// If the character state is different
			if (isNewState) {
				if(drawDebugGizmos && newStateData.state == CharacterState.Jumping){
					GizmoUtils.DrawSphere(transform.position, .05f, Color.green,4,1);
				}
				stateChanged?.Invoke((int)newStateData.state);
			}

			animationHelper.SetState(newStateData);
		}
		
		// Called by owner to update the state data. This is then sent to all observers
		[Command] private void CommandSetStateData(CharacterStateData data){
			this.stateData = data;
			this.RpcSetStateData(data);
		}

		[ClientRpc(includeOwner = false)]
		private void RpcSetStateData(CharacterStateData data) {
			var oldState = this.stateData;
			this.stateData = data;

			if (oldState.state != data.state) {
				stateChanged?.Invoke((int)data.state);
			}
			animationHelper.SetState(data);
		}
		
		//Create a ServerRpc to allow owner to update the value on the server in the ClientAuthoritative mode
		[Command] private void SetServerLookVector(Vector3 value) {
			this.replicatedLookVector = value;
		}

		[Command]
		private void CommandTriggerJump(){
			RpcTriggerJump();
		}
		
		[ClientRpc(includeOwner = false)]
		private void RpcTriggerJump() {
			this.animationHelper.TriggerJump();
		}

		/**
		 * Checks for colliders that intersect with the character.
		 * Returns true if character is colliding with any colliders.
		 */
		public bool IsIntersectingWithBlock() {
			return false;
		}

	}
}
