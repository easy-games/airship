using System;
using Assets.Luau;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

[LuauAPI]
public class CharacterMovement : NetworkBehaviour {
#region  INSPECTOR
	[Header("References")]
	public Rigidbody rigidbody;
	public Transform rootTransform; //The true position transform
	[FormerlySerializedAs("networkTransform")]
	public Transform airshipTransform; //The visual transform controlled by this script
	public Transform graphicTransform; //A transform that games can animate
	public CharacterMovementData moveData;
	public CharacterAnimationHelper animationHelper;
	public BoxCollider mainCollider;
	public Transform slopeVisualizer;

	[Header("Debug")]
	public bool drawDebugGizmos_FORWARD = false;
	public bool drawDebugGizmos_GROUND = false;
	public bool drawDebugGizmos_STEPUP = false;
	public bool drawDebugGizmos_STATES= false;
	public bool useExtraLogging = false;

	[Header("Visual Variables")]
	public float observerRotationLerpMod = 1;
	[Tooltip("If true animations will be played on the server. This should be true if you care about character movement animations server-side (like for hit boxes).")]
	public bool playAnimationOnServer = true;
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

#region PUBLIC GET
	public float standingCharacterHeight => moveData.characterHeight;
	public float characterRadius => moveData.characterRadius;
	public Vector3 characterHalfExtents {get; private set;}

	public float currentCharacterHeight {get; private set;}
	public RaycastHit groundedRaycastHit {get; private set;}
	public bool grounded {get; private set;}
	public bool sprinting {get; private set;}
#endregion

#region PRIVATE REFS
	private CharacterPhysics physics;
	private NetworkAnimator networkAnimator;
	private NetworkTransformUnreliable networkTransform;
	private AirshipPredictedRigidbody predictedRigid;
#endregion

#region INTERNAL
	//Calculated on start
	private bool hasMovementAuth = false;
	private bool isServerAuth = false;

	//Locally tracked variables
	private float currentSpeed;
	private Vector3 lastPos = Vector3.zero;
	private Vector3 lastWorldVel = Vector3.zero;//Literal last move of gameobject in scene
	private Vector3 trackedPosition = Vector3.zero;
	private Vector3 impulseVelocity;
	private float lastServerUpdateTime = 0;
	private float serverUpdateRefreshDelay = .1f;
	private float trackedDeltaTime = 0;
	private float forwardMargin = .05f;
	private BinaryBlob queuedCustomData = null;

	//Input Controls
	private bool jumpInput;
	private Vector3 moveDirInput;
	private bool sprintInput;
	private bool crouchInput;
#endregion

#region STATE
	AirshipPredictedCharacterState currentMoveState = new AirshipPredictedCharacterState();
#endregion

#region SYNC VARS
	/// <summary>
	/// This is replicated to observers.
	/// </summary>
	[NonSerialized]
	public CharacterStateSyncData stateSyncData = new CharacterStateSyncData();
	[NonSerialized] [SyncVar] public Vector3 lookVector = Vector3.one;		
#endregion

#region INIT
	private void Awake() {
		//Gather references and constant variables
		networkAnimator = transform.GetComponent<NetworkAnimator>();
		networkTransform = transform.GetComponent<NetworkTransformUnreliable>();
		predictedRigid = gameObject.GetComponent<AirshipPredictedRigidbody>();
		isServerAuth = predictedRigid != null;
		if(this.physics == null){
			this.physics = new CharacterPhysics(this);
		}
		if(this.animationHelper){
			this.animationHelper.skiddingSpeed = this.moveData.sprintSpeed + .5f;
		}
	}

	public override void OnStartClient() {
		RefreshAuthority();
	}

	public override void OnStartServer() {
		RefreshAuthority();
	}

	public override void OnStartAuthority() {
		RefreshAuthority();
	}

	public override void OnStopAuthority() {
		RefreshAuthority();
	}

	private void RefreshAuthority(){
		Debug.Log("ServerOnly: " + isServerOnly + " is client: " + isClient + " is owned: " + isOwned +  " auth: " + authority + " CONNECTION: " + netIdentity?.connectionToClient?.address);
		//Only the owner can control
		hasMovementAuth = isOwned || (isServer && (netIdentity.connectionToClient == null || isServerAuth));

		//Have to manualy control the flow of data
		if(isServerOnly){
			networkTransform.syncDirection = hasMovementAuth ? SyncDirection.ServerToClient : SyncDirection.ClientToServer;
		}else {
			networkTransform.syncDirection = hasMovementAuth ? SyncDirection.ClientToServer : SyncDirection.ServerToClient;
		}
		//print("Refreshed auth: " + hasAuth);
	}
private void OnEnable() {
		this.physics = new CharacterPhysics(this);
		this.currentMoveState.disableInput = false;
		this.currentMoveState.isFlying = false;
		this.GetCollider().enabled = true;
	}

	private void OnDisable() {
		// EntityManager.Instance.RemoveEntity(this);
		this.GetCollider().enabled = false;
	}
#endregion

#region HELPERS
	//Mirror prediction can move the colliders during rollback so you have to access the collider dynamically
	public Collider GetCollider(){
		return mainCollider;
	}

	//Mirror prediction can move the rigidbody during rollback so you have to access the collider dynamically
	public Rigidbody GetRigidbody(){
		return rigidbody;
	}
	
	private void SnapToY(float newY, bool forceSnap){
		if(useExtraLogging){
			print("Snapping to Y: " + newY);
		}
		var newPos = this.GetRigidbody().transform.position;
		newPos.y = newY;
		this.GetRigidbody().position = newPos;
	}
#endregion

#region LATEUPDATE
	//Every frame update the calculated look vector and the visual state of the movement
	private void LateUpdate(){
		if (isClient && isOwned) {
			var lookTarget = new Vector3(this.lookVector.x, 0, this.lookVector.z);
			if(lookTarget == Vector3.zero){
				lookTarget = new Vector3(0,0,.01f);
			}
			//Instantly rotate for owner
			airshipTransform.rotation = Quaternion.LookRotation(lookTarget);
			//Notify the server of the new rotation periodically
			//Not doing now that look vector is sync var
			// if (Time.time - lastServerUpdateTime > serverUpdateRefreshDelay) {
			// 	lastServerUpdateTime = Time.time;
			// 	SetServerLookVector(this.lookVector);
			// }
		} else {
			//Tween to rotation
			var lookTarget = new Vector3(lookVector.x, 0, lookVector.z);
			if(lookTarget == Vector3.zero){
				lookTarget = new Vector3(0,0,.01f);
			}
			airshipTransform.rotation = Quaternion.Lerp(
				graphicTransform.rotation,
				Quaternion.LookRotation(lookTarget),
				observerRotationLerpMod * Time.deltaTime);
		}
		if (isLocalPlayer || networkAnimator == null) {
			//Track movement to visually sync animator
			UpdateAnimationVelocity();
		}
	}

	//Update the visual state of the characters velocity
	private void UpdateAnimationVelocity() {
		//Update visual state of client character
		var currentPos = rootTransform.position;
		trackedDeltaTime += Time.deltaTime;
		var worldVel = (currentPos - trackedPosition) * (1 / trackedDeltaTime);
		if (currentPos != trackedPosition || worldVel != lastWorldVel) {
			lastWorldVel = worldVel;
			trackedPosition = currentPos;
			// if(!this.isOwned){
			// 	Debug.Log("Pos: " + currentPos + " ve: " + lastWorldVel + " time: " + trackedDeltaTime);
			// }
			trackedDeltaTime = 0;
			animationHelper.SetVelocity(graphicTransform.InverseTransformDirection(worldVel));
		}
	}
#endregion

#region FIXEDUPDATE
	//Every Physics tick we process the move data
	private void FixedUpdate() {	
		// Observers don't calculate moves
		if(!hasMovementAuth || (isServerOnly && !isServerAuth)) {
			return;
		}

		//Update the movement state of the character	
		currentMoveState.currentMoveInput = BuildMoveData();
		OnBeginMove?.Invoke(currentMoveState.currentMoveInput);
		Move(currentMoveState.currentMoveInput);
		if(isServerAuth && isClient){
			CmdMove(currentMoveState.currentMoveInput);
		}
		OnEndMove?.Invoke(currentMoveState.currentMoveInput);
	}

	//Compile the inputs and custom data into one struct
	private MoveInputData BuildMoveData() {
		//Let TS apply custom data
		OnSetCustomData?.Invoke();

		var customData = queuedCustomData;
		queuedCustomData = null;

		return new MoveInputData(moveDirInput, jumpInput, crouchInput, sprintInput, this.lookVector, customData);
	}

	[Command]
	//Sync the move input data to the server
	private void CmdMove(MoveInputData moveData){
		//Move(moveData);
	}
#endregion

#region MOVE START
	private void Move(MoveInputData md) {
		var currentVelocity = this.GetRigidbody().velocity;
		var newVelocity = currentVelocity;
		var isIntersecting = IsIntersectingWithBlock();
		var deltaTime = Time.fixedDeltaTime;
		var isImpulsing = impulseVelocity != Vector3.zero;
		var rootPosition = this.GetRigidbody().transform.position;
		var normalizedMoveDir = md.moveDir.normalized;
		var characterMoveVelocity = Vector3.zero;
		characterMoveVelocity.x = normalizedMoveDir.x;
		characterMoveVelocity.z = normalizedMoveDir.z;

		//Ground checks
		var (grounded, groundHit, detectedGround) = physics.CheckIfGrounded(rootPosition, newVelocity * deltaTime, md.moveDir);
		if (isIntersecting) {
			grounded = true;
		}
		this.grounded = grounded;
		this.groundedRaycastHit = groundHit;

		if(grounded){
			//Reset airborne impulse
			currentMoveState.airborneFromImpulse = false;

			//Store this move dir
			currentMoveState.lastGroundedMoveDir = md.moveDir;
			
			//Snap to the ground if you are falling into the ground
			if(newVelocity.y < 1  && 
				((!currentMoveState.prevGrounded && this.moveData.colliderGroundOffset > 0) || 
					//Snap if we always snap to ground
					(moveData.alwaysSnapToGround && !currentMoveState.prevStepUp && !isImpulsing))){
				this.SnapToY(groundHit.point.y, true);
				newVelocity.y = 0;
			}
		} else{
			//While in the air how much control do we have over our direction?
			md.moveDir = Vector3.Lerp(currentMoveState.lastGroundedMoveDir, md.moveDir, moveData.inAirDirectionalControl);
		}
		
		if (grounded && !currentMoveState.prevGrounded) {
			currentMoveState.jumpCount = 0;
			currentMoveState.timeSinceBecameGrounded = 0f;
			this.OnImpactWithGround?.Invoke(currentVelocity, groundHit);
		} else {
			currentMoveState.timeSinceBecameGrounded = Math.Min(currentMoveState.timeSinceBecameGrounded + deltaTime, 100f);
		}
		var groundSlopeDir = detectedGround ? Vector3.Cross(Vector3.Cross(groundHit.normal, Vector3.down), groundHit.normal).normalized : transform.forward;
		var slopeDot = 1-Mathf.Max(0, Vector3.Dot(groundHit.normal, Vector3.up));

		var canStand = physics.CanStand();
#endregion

		if (this.currentMoveState.disableInput) {
			md.moveDir = Vector3.zero;
			md.crouch = false;
			md.jump = false;
			md.lookVector = lookVector;
			md.sprint = false;
		}

#region GRAVITY
		if(moveData.useGravity){
			if(!currentMoveState.isFlying && !currentMoveState.prevStepUp &&
				(moveData.useGravityWhileGrounded || ((!grounded || newVelocity.y > .01f) && !currentMoveState.isFlying))){
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
			currentMoveState.alreadyJumped = false;
		}
		var didJump = false;
		var canJump = false;
		if (moveData.numberOfJumps > 0 && requestJump && !currentMoveState.alreadyJumped && (!currentMoveState.prevCrouch || canStand)) {
			//On the ground
			if (grounded || currentMoveState.prevStepUp) {
				canJump = true;
			}else{
				//In the air
				// coyote jump
				if (normalizedMoveDir.y <= 0.02f && currentMoveState.timeSinceWasGrounded <= moveData.jumpCoyoteTime && currentVelocity.y <= 0 && currentMoveState.timeSinceJump > moveData.jumpCoyoteTime) {
					canJump = true;
				}
				//the first jump requires grounded, so if in the air bump the currentMoveState.jumpCount up
				else {
					if(currentMoveState.jumpCount == 0){
						currentMoveState.jumpCount = 1;
					}
					
					//Multi Jump
					if (currentMoveState.jumpCount < moveData.numberOfJumps){
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
		var tryingToSprint = moveData.onlySprintForward ? 
			md.sprint && this.graphicTransform.InverseTransformVector(md.moveDir).z > 0.1f : //Only sprint if you are moving forward
			md.sprint && md.moveDir.magnitude > 0.1f; //Only sprint if you are moving
		
		CharacterState groundedState = CharacterState.Idle; //So you can know the desired state even if we are technically in the air

		//Check to see if we can stand up from a crouch
		if((moveData.autoCrouch || currentMoveState.prevState == CharacterState.Crouching) && !canStand){
			groundedState = CharacterState.Crouching;
		}else if (md.crouch && grounded) {
			groundedState = CharacterState.Crouching;
		} else if (isMoving) {
			if (tryingToSprint) {
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
			currentMoveState.state = CharacterState.Airborne;
		}else{
			//Otherwise use our found state
			currentMoveState.state = groundedState;
		}

		if(useExtraLogging && currentMoveState.prevState != currentMoveState.state){
			print("New State: " + currentMoveState.state);
		}

		if (!tryingToSprint) {
			sprinting = false;
		}

		/*
			* Update Time Since:
			*/

		if (didJump) {
			currentMoveState.timeSinceJump = 0f;
		} else
		{
			currentMoveState.timeSinceJump = Math.Min(currentMoveState.timeSinceJump + deltaTime, 100f);
		}

		if (grounded) {
			currentMoveState.timeSinceWasGrounded = 0f;
		} else {
			currentMoveState.timeSinceWasGrounded = Math.Min(currentMoveState.timeSinceWasGrounded + deltaTime, 100f);
		}

#region CROUCH
		// Prevent falling off blocks while crouching
		var isCrouching = groundedState == CharacterState.Crouching;
		if (moveData.preventFallingWhileCrouching && !currentMoveState.prevStepUp && isCrouching && isMoving && grounded ) {
			var posInMoveDirection = rootPosition + normalizedMoveDir * 0.2f;
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
					var stepPosition = rootPosition + safeDirection.normalized * 0.2f;
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
		var offsetExtent = this.moveData.colliderGroundOffset / 2;
		this.currentCharacterHeight = isCrouching ? standingCharacterHeight * moveData.crouchHeightMultiplier : standingCharacterHeight;
		characterHalfExtents = new Vector3(moveData.characterRadius,  this.currentCharacterHeight/2f - offsetExtent,moveData.characterRadius);
		var collider = this.GetCollider();
		collider.transform.localScale = characterHalfExtents*2;
		collider.transform.localPosition = new Vector3(0,this.currentCharacterHeight/2f+offsetExtent,0);
#endregion

#region FLYING

		//Flying movement
		if (currentMoveState.isFlying) {
			if (md.jump) {
				newVelocity.y += moveData.verticalFlySpeed;
			}

			if (md.crouch) {
				newVelocity.y -= moveData.verticalFlySpeed;
			}

			newVelocity.y *= Mathf.Clamp(.98f - deltaTime, 0, 1);
		}

#endregion

#region FRICTION_DRAG
		var flatMagnitude = new Vector3(newVelocity.x, 0, newVelocity.z).magnitude;
		// Calculate drag:
		var dragForce = physics.CalculateDrag(currentVelocity,  currentMoveState.isFlying ? .5f: 
			(moveData.drag * (inAir ? moveData.airDragMultiplier : 1)));
		if(!currentMoveState.isFlying){
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
		newVelocity += impulseVelocity;
		currentMoveState.airborneFromImpulse = !grounded || impulseVelocity.y > .01f;
		impulseVelocity = Vector3.zero;
		if(useExtraLogging){
			print(" isImpulsing: " + isImpulsing + " impulse force: " + impulseVelocity + "New Vel: " + newVelocity);
		}
	}
#endregion

#region MOVEMENT
		// Find speed
		//Adding 1 to offset the drag force so actual movement aligns with the values people enter in moveData
		var currentAcc = 0f;
		if (tryingToSprint) {
			currentSpeed = moveData.sprintSpeed;
			currentAcc = moveData.sprintAccelerationForce;
		} else {
			currentSpeed = moveData.speed;
			currentAcc = moveData.accelerationForce;
		}

		if (currentMoveState.state == CharacterState.Crouching) {
			currentSpeed *= moveData.crouchSpeedMultiplier;
			currentAcc *= moveData.crouchSpeedMultiplier;
		}

		if (currentMoveState.isFlying) {
			currentSpeed *= moveData.flySpeedMultiplier;
		} else if(inAir){
			currentSpeed *= moveData.airSpeedMultiplier;
		}

		//Apply speed
		if(moveData.useAccelerationMovement){
			characterMoveVelocity *= currentAcc;
		}else{
			characterMoveVelocity *= currentSpeed;
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
				if(drawDebugGizmos_STEPUP){
					GizmoUtils.DrawLine(rootPosition, rootPosition + characterMoveVelocity * 2, Color.red);
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
		var forwardDistance = (characterMoveVelocity.magnitude + newVelocity.magnitude) * deltaTime + (this.characterRadius+forwardMargin);
		var forwardVector = (characterMoveVelocity + newVelocity).normalized * forwardDistance;

#region MOVE_FORCE
		//Clamp directional movement to not add forces if you are already moving in that direction
		var flatVelocity = new Vector3(newVelocity.x, 0, newVelocity.z);
		var tryingToMove = normalizedMoveDir.sqrMagnitude > .1f;
		var rawMoveDot = Vector3.Dot(flatVelocity.normalized, normalizedMoveDir);
		//print("Directional Influence: " + (characterMoveVector - newVelocity) + " mag: " + (characterMoveVector - currentVelocity).magnitude);
		
		//Don't drift if you are turning the character
		if(moveData.accelerationTurnFriction > 0 && moveData.useAccelerationMovement && !isImpulsing && grounded && tryingToMove){
			var parallelDot = 1-Mathf.Abs(Mathf.Clamp01(rawMoveDot));
			//print("DOT: " + parallelDot);
			newVelocity += -Vector3.ClampMagnitude(flatVelocity, currentSpeed) * parallelDot * moveData.accelerationTurnFriction;
		}			

		//Stop character from moveing into colliders (Helps prevent axis aligned box colliders from colliding when they shoudln't like jumping in a voxel world)
		if(moveData.preventWallClipping){
			//Do raycasting after we have claculated our move direction
			(bool didHitForward, RaycastHit forwardHit)  = physics.CheckForwardHit(rootPosition, forwardVector, true, true);

			if(!currentMoveState.prevStepUp && didHitForward){
				var isVerticalWall = 1-Mathf.Max(0, Vector3.Dot(forwardHit.normal, Vector3.up)) >= moveData.maxSlopeDelta;
				var isKinematic = forwardHit.collider?.attachedRigidbody == null || forwardHit.collider.attachedRigidbody.isKinematic;

				//print("Avoiding wall: " + forwardHit.collider.gameObject.name + " distance: " + forwardHit.distance + " isVerticalWall: " + isVerticalWall + " isKinematic: " + isKinematic);
				//Stop character from walking into walls but Let character push into rigidbodies	
				if(isVerticalWall && isKinematic){
					//Stop movement into this surface
					var colliderDot = 1-Mathf.Max(0,-Vector3.Dot(forwardHit.normal, characterMoveVelocity.normalized));
					// var tempMagnitude = characterMoveVector.magnitude;
					// characterMoveVector -= forwardHit.normal * tempMagnitude * colliderDot;
					characterMoveVelocity = Vector3.ProjectOnPlane(characterMoveVelocity, forwardHit.normal);
					characterMoveVelocity.y = 0;
					characterMoveVelocity *= colliderDot;
					//print("Collider Dot: " + colliderDot + " moveVector: " + characterMoveVelocity);
				}

				//Push the character out of any colliders
				flatVelocity = Vector3.ClampMagnitude(newVelocity, forwardHit.distance-characterRadius-forwardMargin);
				newVelocity.x = flatVelocity.x;
				newVelocity.z = flatVelocity.z;
			}

			if(!grounded && detectedGround){
				//Hit ground but its not valid ground, push away from it
				//print("PUSHING AWAY FROM: " + groundHit.normal);
				newVelocity += groundHit.normal * physics.GetFlatDistance(rootPosition, groundHit.point) * .25f / deltaTime;
			}
		}
		
		//Instantly move at the desired speed
		var moveMagnitude = characterMoveVelocity.magnitude;
		var velMagnitude = flatVelocity.magnitude;
		
		//Don't move character in direction its already moveing
		//Positive dot means we are already moving in this direction. Negative dot means we are moving opposite of velocity.
		//Multipy by 2 so perpendicular movement is still fully applied rather than half applied
		var dirDot = Mathf.Max(moveData.minAccelerationDelta, Mathf.Clamp01((1-rawMoveDot)*2));
		
		if(useExtraLogging){
			print("old vel: " + currentVelocity + " new vel: " + newVelocity + " move dir: " + characterMoveVelocity + " Dir dot: " + dirDot + " currentSpeed: " + currentSpeed + " grounded: " + grounded + " canJump: " + canJump + " didJump: " + didJump);
		}

		if(currentMoveState.isFlying){
			newVelocity.x = md.moveDir.x * currentSpeed;
			newVelocity.z = md.moveDir.z * currentSpeed;
		}else if(!isImpulsing && !currentMoveState.airborneFromImpulse && //Not impulsing AND under our max speed
					(velMagnitude < (moveData.useAccelerationMovement?currentSpeed:Mathf.Max(moveData.sprintSpeed, currentSpeed) + 1))){
			if(moveData.useAccelerationMovement){
				newVelocity += Vector3.ClampMagnitude(characterMoveVelocity, currentSpeed-velMagnitude);
			}else{
				// if(Mathf.Abs(characterMoveVelocity.x) > Mathf.Abs(newVelocity.x)){
				// 	newVelocity.x = characterMoveVelocity.x;
				// }
				// if(Mathf.Abs(characterMoveVelocity.z) > Mathf.Abs(newVelocity.z)){
				// 	newVelocity.z = characterMoveVelocity.z;
				// }
				if(moveMagnitude+.5f >= velMagnitude){
					newVelocity.x = characterMoveVelocity.x;
					newVelocity.z = characterMoveVelocity.z;
				}
			}
		} else {
			//Moving faster than max speed or using acceleration mode
			newVelocity += normalizedMoveDir * (dirDot * dirDot / 2) * 
				(groundedState == CharacterState.Sprinting ? this.moveData.sprintAccelerationForce : moveData.accelerationForce);
		}

		//print("isreplay: " + replaying + " didHitForward: " + didHitForward + " moveVec: " + characterMoveVector + " colliderDot: " + colliderDot  + " for: " + forwardHit.collider?.gameObject.name + " point: " + forwardHit.point);
#endregion
#endregion

#region STEP_UP
	//Step up as the last step so we have the most up to date velocity to work from
	var didStepUp = false;
	if(moveData.detectStepUps && //Want to check step ups
		(!md.crouch || !moveData.preventStepUpWhileCrouching) && //Not blocked by crouch
		(moveData.assistedLedgeJump || currentMoveState.timeSinceBecameGrounded > .05) && //Grounded
		(Mathf.Abs(newVelocity.x)+Mathf.Abs(newVelocity.z)) > .05f) { //Moveing
		(bool hitStepUp, bool onRamp, Vector3 pointOnRamp, Vector3 stepUpVel) = physics.StepUp(rootPosition, newVelocity, deltaTime, detectedGround ? groundHit.normal: Vector3.up);
		if(hitStepUp){
			didStepUp = hitStepUp;
			var oldPos = rootPosition;
			if(pointOnRamp.y > oldPos.y){
				SnapToY(pointOnRamp.y, true);
				//airshipTransform.position = Vector3.MoveTowards(oldPos, transform.position, deltaTime);
			}
			//print("STEPPED UP. Vel before: " + newVelocity);
			newVelocity = Vector3.ClampMagnitude(new Vector3(stepUpVel.x, Mathf.Max(stepUpVel.y, newVelocity.y), stepUpVel.z), newVelocity.magnitude);
			//print("PointOnRamp: " + pointOnRamp + " position: " + rootPosition + " vel: " + newVelocity);
			
			if(drawDebugGizmos_STEPUP){
				GizmoUtils.DrawSphere(oldPos, .01f, Color.red, 4, 4);
				GizmoUtils.DrawSphere(rootPosition + newVelocity, .03f, new Color(1,.5f,.5f), 4, 4);
			}
			currentMoveState.state = groundedState;//Force grounded state since we are in the air for the step up
			grounded = true;
		}
	}
#endregion
		
#region APPLY FORCES

		//Clamp the velocity
		newVelocity = Vector3.ClampMagnitude(newVelocity, moveData.terminalVelocity);
		var magnitude = new Vector3(newVelocity.x, 0, newVelocity.z).magnitude;
		var canStopVel = !currentMoveState.airborneFromImpulse && (!inAir || moveData.useMinimumVelocityInAir) && !isImpulsing;
		var underMin = magnitude <= moveData.minimumVelocity && magnitude > .01f;
		//print("currentMoveState.airborneFromImpulse: " + currentMoveState.airborneFromImpulse + " unerMin: " +underMin + " notTryingToMove: " + notTryingToMove);
		if(canStopVel && !tryingToMove && underMin){
			//Not intending to move so snap to zero (Fake Dynamic Friction)
			//print("STOPPING VELOCITY. CanStop: " + canStopVel + " tryingtoMove: " + tryingToMove + " underMin: " + underMin);
			newVelocity.x = 0;
			newVelocity.z = 0;
		}

		
		//print($"<b>JUMP STATE</b> {md.GetTick()}. <b>isReplaying</b>: {replaying}    <b>mdJump </b>: {md.jump}    <b>canJump</b>: {canJump}    <b>didJump</b>: {didJump}    <b>currentPos</b>: {rootPosition}    <b>currentVel</b>: {currentVelocity}    <b>newVel</b>: {newVelocity}    <b>grounded</b>: {grounded}    <b>currentState</b>: {state}    <b>currentMoveState.prevState</b>: {currentMoveState.prevState}    <b>mdMove</b>: {md.moveDir}    <b>characterMoveVector</b>: {characterMoveVector}");
		
		//Execute the forces onto the rigidbody
		this.GetRigidbody().velocity = newVelocity;
#endregion

		
#region SAVE STATE
		// if(currentMoveState.timeSinceBecameGrounded < .1){
		// 	print("LANDED! prevVel: " + currentVelocity + " newVel: " + newVelocity);
		// }

		//Replicate the look vector
		SetLookVector(md.lookVector);

		//Fire state change event
		TrySetState(new CharacterStateSyncData() {
			state = currentMoveState.state,
			grounded = !inAir || didStepUp,
			sprinting = sprinting,
			crouching = isCrouching,
		});

		if (didJump){
			//Fire on the server
			TriggerJump();
			if(isClientOnly){
				//Fire locally immediately
				this.animationHelper.TriggerJump();
			}
		}

		// Handle OnMoveDirectionChanged event
		if (currentMoveState.prevMoveDir != md.moveDir) {
			OnMoveDirectionChanged?.Invoke(md.moveDir);
		}
		currentMoveState.prevState = currentMoveState.state;
		currentMoveState.prevCrouch = md.crouch;
		currentMoveState.prevMoveDir = md.moveDir;
		currentMoveState.prevGrounded = grounded;
		currentMoveState.prevStepUp = didStepUp;
#endregion

		//Track speed based on position
		if(useExtraLogging){
			print("Speed: " + currentSpeed + " Actual Movement Per Second: " + (physics.GetFlatDistance(rootPosition, lastPos) / deltaTime));
		}
		lastPos = rootPosition;
	}
#region MOVE END
#endregion

	public void Teleport(Vector3 position) {
		TeleportAndLook(position, isOwned ? lookVector : lookVector);
	}

	public void TeleportAndLook(Vector3 position, Vector3 lookVector) {
		if (useExtraLogging) {
			print("Teleporting to: " + position);
		}

		if(hasMovementAuth){
			//Teleport Locally
			TeleportInternal(position, lookVector);
		} else if(!isServerAuth && isServerOnly){
			//Tell client to teleport
			RpcTeleport(base.connectionToClient, position, lookVector);
		}
	}

	[TargetRpc]
	private void RpcTeleport(NetworkConnection conn, Vector3 pos, Vector3 lookVector) {
		this.TeleportInternal(pos, lookVector);
	}

	private void TeleportInternal(Vector3 pos, Vector3 lookVector){
		this.GetRigidbody().position = pos;
		this.lookVector = lookVector;
	}

	public void SetVelocity(Vector3 velocity) {
		if (hasMovementAuth) {
			SetVelocityInternal(velocity);
		} else if(!isServerAuth && isServerOnly){
			if (netId == 0) return;
			RpcSetVelocity(base.connectionToClient, velocity);
		}
	}

	private void SetVelocityInternal(Vector3 velocity) {
		if(useExtraLogging){
			print("Setting velocity: " + velocity);
		}

		this.GetRigidbody().velocity = velocity;
	}

	[TargetRpc]
	private void RpcSetVelocity(NetworkConnection conn, Vector3 velocity) {
		SetVelocityInternal(velocity);
	}

#region TS_ACCESS
	public void DisableMovement() {
		SetVelocity(Vector3.zero);
		currentMoveState.disableInput = true;
	}

	public void EnableMovement() {
		currentMoveState.disableInput = false;
	}

	
	public bool IsGrounded() {
		return grounded;
	}

	public bool IsSprinting() {
		return sprinting;
	}
	public void SetReplicatedState(CharacterStateSyncData oldData, CharacterStateSyncData newData) {
		animationHelper.SetState(newData);
		if(oldData.state != newData.state){
			this.stateChanged?.Invoke((int)newData.state);
		}
	}

	public Vector3 GetLookVector() {
		return this.lookVector;
	}

	public void SetMoveInput(Vector3 moveDir, bool jump, bool sprinting, bool crouch, bool moveDirWorldSpace) {
		if (moveDirWorldSpace) {
			moveDirInput = moveDir;
		} else {
			moveDirInput = this.graphicTransform.TransformDirection(moveDir);
		}
		crouchInput = crouch;
		sprintInput = sprinting;
		jumpInput = jump;
	}

	public void AddImpulse(Vector3 impulse){
		if(useExtraLogging){
			print("Adding impulse: " + impulse);
		}
		SetImpulse(this.impulseVelocity + impulse);
	}

	public void SetImpulse(Vector3 impulse){
		if (hasMovementAuth) {
			//Locally
			SetImpulseInternal(impulse);
		} else if(!isServerAuth && isServerOnly){
			//Tell client
			RpcSetImpulse(base.connectionToClient, impulse);
		}
	}

	[TargetRpc]
	private void RpcSetImpulse(NetworkConnection conn, Vector3 impulse) {
		SetImpulseInternal(impulse);
	}

	public void SetImpulseInternal(Vector3 impulse){
		if(useExtraLogging){
			print("Setting impulse: " + impulse);
		}
		impulseVelocity = impulse;
	}

	/// <summary>
	/// Manually force the look direction of the character. Triggers the OnNewLookVector event.
	/// </summary>
	/// <param name="lookVector"></param>
	public void SetLookVector(Vector3 lookVector) {
		OnNewLookVector?.Invoke(lookVector);
		SetLookVectorRecurring(lookVector);
	}

	/// <summary>
	/// Manually force the look direction of the character without triggering the OnNewLookVector event. 
	/// Useful for something that is updating the lookVector frequently and needs to listen for other scripts modifying the lookVector. 
	/// </summary>
	/// <param name="lookVector"></param>
	public void SetLookVectorRecurring(Vector3 lookVector){
		this.lookVector = lookVector;
	}

	public void SetCustomData(BinaryBlob customData) {
		queuedCustomData = customData;
	}

	public int GetState() {
		if (isOwned) {
			return (int)this.currentMoveState.state;
		}
		return (int)stateSyncData.state;
	}

	public bool IsFlying() {
		return this.currentMoveState.isFlying;
	}

	public Vector3 GetVelocity() {
		return this.GetRigidbody().velocity;
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

	public MoveInputData GetCurrentMoveInputData(){
		return currentMoveState.currentMoveInput;
	}

#endregion

	public void SetDebugFlying(bool flying){
		if (!this.moveData.allowDebugFlying) {
			// Debug.LogError("Unable to fly from console when allowFlying is false. Set this characters CharacterMovementData to allow flying if needed");
			return;
		}
		SetFlying(flying);
	}

	public void SetFlying(bool flyModeEnabled) {
		this.currentMoveState.isFlying = flyModeEnabled;
		if(isClient && hasMovementAuth){
			CommandSetFlying(flyModeEnabled);
		}else if(!isServerAuth && isServerOnly){
			RpcSetFlying(flyModeEnabled);
		}
	}

	[ClientRpc(includeOwner = false)]
	private void RpcSetFlying(bool flyModeEnabled) {
		this.currentMoveState.isFlying = flyModeEnabled;
	}

	[Command]
	private void CommandSetFlying(bool flyModeEnabled) {
		this.currentMoveState.isFlying = flyModeEnabled;
	}

	private void TrySetState(CharacterStateSyncData newStateData) {
		bool isNewState = newStateData.state != this.stateSyncData.state;
		bool isNewData = !newStateData.Equals(this.stateSyncData);

		// If new value in the state
		if (isNewData) {
			this.stateSyncData = newStateData;
			if(isClientOnly && hasMovementAuth){
				CommandSetStateData(newStateData);
			} else if (isServerOnly && hasMovementAuth){
				RpcSetStateData(newStateData);
			}
		}

		// If the character state is different
		if (isNewState) {
			if(drawDebugGizmos_STATES && newStateData.state == CharacterState.Airborne){
				GizmoUtils.DrawSphere(transform.position, .05f, Color.green,4,1);
			}
			stateChanged?.Invoke((int)newStateData.state);
		}

		animationHelper.SetState(newStateData);
	}
	
	// Called by owner to update the state data. This is then sent to all observers
	[Command] private void CommandSetStateData(CharacterStateSyncData data){
		this.stateSyncData = data;
		if (playAnimationOnServer) {
			ApplyNonLocalStateData(data);
		}
		this.RpcSetStateData(data);
	}

	[ClientRpc(includeOwner = false)]
	private void RpcSetStateData(CharacterStateSyncData data) {
		ApplyNonLocalStateData(data);
	}

	private void ApplyNonLocalStateData(CharacterStateSyncData data) {
		var oldState = this.stateSyncData;
		this.stateSyncData = data;

		if (oldState.state != data.state) {
			stateChanged?.Invoke((int)data.state);
		}
		animationHelper.SetState(data);
	}
	
	//Create a ServerRpc to allow owner to update the value on the server in the ClientAuthoritative mode
	[Command] private void SetServerLookVector(Vector3 value) {
		this.lookVector = value;
	}

	private void TriggerJump(){
		if(isClientOnly){
			CommandTriggerJump();
		}else if (isServer){
			if (playAnimationOnServer) animationHelper.TriggerJump();
			if(isServerOnly){
				RpcTriggerJump();
			}
		}
	}

	[Command]
	private void CommandTriggerJump(){
		if (playAnimationOnServer) animationHelper.TriggerJump();
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
