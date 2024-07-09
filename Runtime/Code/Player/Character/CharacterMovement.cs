using System;
using System.Collections.Generic;
using Assets.Luau;
using Code.Player.Character.API;
using Code.Player.Human.Net;
using FishNet;
using FishNet.Component.Prediction;
using FishNet.Component.Transforming;
using FishNet.Connection;
using FishNet.Managing.Transporting;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Player.Entity;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;

namespace Code.Player.Character {
	public enum ServerAuthority{
		CLIENT_AUTH, //Client auth
		SERVER_AUTH, //Server auth with client prediction
		SERVER_ONLY //Don't predict, let the server control
	}
	[LuauAPI]
	[RequireComponent(typeof(Rigidbody))]
	public class CharacterMovement : NetworkBehaviour {

		
		[Header("References")]
		public Transform rootTransform; //The true position transform
		public Transform networkTransform; //The interpolated network transform
		public Transform graphicTransform; //A transform we can animate
		public CharacterMovementData moveData;
		public CharacterAnimationHelper animationHelper;
		public BoxCollider mainCollider;
		public Transform slopeVisualizer;

		[Header("Variables")]
		public ServerAuthority authorityMode = ServerAuthority.CLIENT_AUTH;
		[Tooltip("How many ticks before another reconcile is sent from server to clients")]
		public int ticksUntilReconcile = 1;
		public float observerRotationLerpMod = 1;

		[Header("Debug")]
		public bool drawDebugGizmos = false;
		public bool useExtraLogging = false;


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
		/// Params: boolean isReplay, MoveInputData moveData
		/// </summary>
		public event Action<object, object> OnBeginMove;
		/// <summary>
		/// Called at the end of a Move function.
		/// Params: boolean isReplay, MoveInputData moveData
		/// </summary>
		public event Action<object, object> OnEndMove;

		/// <summary>
		/// Params: MoveModifier
		/// </summary>
		public event Action<object> OnAdjustMove;

		/// <summary>
		/// Params: Vector3 velocity, ushort blockId
		/// </summary>
		public event Action<object, object> OnImpactWithGround;

		public event Action<object> OnMoveDirectionChanged;

		// [SyncVar(WritePermissions = WritePermission.ClientUnsynchronized, ReadPermissions = ReadPermission.ExcludeOwner)]
		[NonSerialized]
		public readonly SyncVar<ushort> groundedBlockId = new SyncVar<ushort>(new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));
		[NonSerialized] public Vector3 groundedBlockPos;
		[NonSerialized] public RaycastHit groundedRaycastHit;


		public float standingCharacterHeight => moveData.characterHeight;
		public float characterRadius => moveData.characterRadius;
		public Vector3 characterHalfExtents {get; private set;}

		public float currentCharacterHeight {get; private set;}

		private bool disableInput = false;
		// Controls
		private bool _jump;
		private Vector3 _moveDir;
		private Vector3 _impulseForce;
		private bool _sprint;
		private bool _crouchOrSlide;
		private bool _flying;
		private bool _allowFlight;

		// State
		private PredictionRigidbody predictionRigidbody = new PredictionRigidbody();
		private Vector3 externalForceVelocity = Vector3.zero;//Networked velocity force in m/s (Does not contain input velocities)
		private Vector3 lastWorldVel = Vector3.zero;//Literal last move of gameobject in scene
		private Vector3 trackedVelocity;
		private Vector3 slideVelocity;
		private float voxelStepUp;
		private readonly Dictionary<int, CharacterMoveModifier> moveModifiers = new();
		public bool grounded {get; private set;}
		public bool sprinting {get; private set;}

		/// <summary>
		/// Key: tick
		/// Value: the MoveModifier received from <c>adjustMoveEvent</c>
		/// </summary>
		private readonly Dictionary<uint, CharacterMoveModifier> moveModifierFromEventHistory = new();

		// History
		private bool prevCrouchOrSlide;
		private bool prevSprint;
		private bool prevJump;
		private int jumpCount = 0;
		private bool prevStepUp;
		private Vector3 prevMoveFinalizedDir;
		private Vector2 prevMoveVector;
		private Vector3 prevMoveDir;
		private Vector3 prevLookVector;
		private uint prevTick;
		private int prevGroundId;
		private bool prevGrounded;
		private float timeSinceBecameGrounded;
		private float timeSinceWasGrounded;
		private float stepUpStartTime;
		private float timeSinceJump;
		private float timeElapsed;
		private Vector3 prevJumpStartPos;
		private float lastServerUpdateTime = 0;
		private float serverUpdateRefreshDelay = .1f;

		private CharacterMoveModifier prevCharacterMoveModifier = new CharacterMoveModifier()
		{
			speedMultiplier = 1,
		};

		private Vector3 trackedPosition = Vector3.zero;
		private float timeSinceSlideStart;

		// [SyncVar (OnChange = nameof(ExposedState_OnChange), ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		[NonSerialized]
		public readonly SyncVar<CharacterAnimationHelper.CharacterAnimationSyncData> replicatedState = new SyncVar<CharacterAnimationHelper.CharacterAnimationSyncData>(new CharacterAnimationHelper.CharacterAnimationSyncData(), new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));

		// [SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		[NonSerialized]
		public readonly SyncVar<Vector3> replicatedLookVector = new SyncVar<Vector3>(new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));

		private CharacterState state = CharacterState.Idle;
		private CharacterState prevState = CharacterState.Idle;

		private BinaryBlob queuedCustomData = null;

		[HideInInspector]
		public VoxelWorld voxelWorld;
		private VoxelRollbackManager voxelRollbackManager;
		
		//[SerializeField] private PredictedObject predictedObject;

		private int overlappingCollidersCount = 0;
		private readonly Collider[] overlappingColliders = new Collider[256];
		private readonly List<Collider> ignoredColliders = new List<Collider>(256);


		// [Description("When great than this value you can sprint -1 is inputing fully backwards, 1 is inputing fully forwards")]
		// public float sprintForwardThreshold = .25f;

		private bool _forceReconcile;
		private int moveModifierIdCounter = 0;
		private Vector3 lastPos = Vector3.zero;
		private CharacterPhysics physics;

#region INIT

		private void Awake(){
			var nob = gameObject.GetComponent<NetworkObject>();
			var network = gameObject.GetComponent<NetworkTransform>();
			nob._enablePrediction = authorityMode == ServerAuthority.SERVER_AUTH;
			network._clientAuthoritative = authorityMode == ServerAuthority.CLIENT_AUTH;
			if(authorityMode != ServerAuthority.CLIENT_AUTH){
				var smoother = gameObject.GetComponent<NetworkTickSmoother>();
				if(smoother){
					Destroy(smoother);
				}
			}
		}

		private void OnEnable() {
			this.physics= new CharacterPhysics(this);
			this.disableInput = false;
			this._allowFlight = false;
			this._flying = false;
			this.mainCollider.enabled = true;
			this.externalForceVelocity = Vector3.zero;
    		this.predictionRigidbody.Initialize(gameObject.GetComponent<Rigidbody>());

			if (!voxelWorld) {
				voxelWorld = VoxelWorld.Instance;
			}
			if (voxelWorld != null) {
				voxelRollbackManager = voxelWorld.gameObject.GetComponent<VoxelRollbackManager>();
				voxelWorld.BeforeVoxelPlaced += OnBeforeVoxelPlaced;
				voxelWorld.VoxelChunkUpdated += VoxelWorld_VoxelChunkUpdated;
				voxelWorld.BeforeVoxelChunkUpdated += VoxelWorld_OnBeforeVoxelChunkUpdated;
			}
			if (voxelRollbackManager != null) {
				voxelRollbackManager.ReplayPreVoxelCollisionUpdate += OnReplayPreVoxelCollisionUpdate;
			}

			this.replicatedState.OnChange += ExposedState_OnChange;

			// EntityManager.Instance.AddEntity(this);
		}

		private void OnDisable() {
			// EntityManager.Instance.RemoveEntity(this);
			mainCollider.enabled = false;

			if (voxelWorld) {
				voxelWorld.BeforeVoxelPlaced -= OnBeforeVoxelPlaced;
				voxelWorld.VoxelChunkUpdated -= VoxelWorld_VoxelChunkUpdated;
				voxelWorld.BeforeVoxelChunkUpdated -= VoxelWorld_OnBeforeVoxelChunkUpdated;
			}

			if (voxelRollbackManager) {
				voxelRollbackManager.ReplayPreVoxelCollisionUpdate -= OnReplayPreVoxelCollisionUpdate;
			}

			this.replicatedState.OnChange -= ExposedState_OnChange;
		}
#endregion

		private void LateUpdate(){
			var lookTarget = new Vector3(replicatedLookVector.Value.x, 0, replicatedLookVector.Value.z);
			if(lookTarget != Vector3.zero){
				if(IsClientStarted && IsOwner){
					//Instantly rotate for owner
					graphicTransform.rotation = Quaternion.LookRotation(lookTarget);
					//Notify the server of the new rotation periodically
					if(authorityMode == ServerAuthority.CLIENT_AUTH && Time.time - lastServerUpdateTime > serverUpdateRefreshDelay){
						lastServerUpdateTime = Time.time;
						SetServerLookVector(replicatedLookVector.Value);
					}
				}else{
					//Tween to rotation
					graphicTransform.rotation = Quaternion.Lerp(
						graphicTransform.rotation, 
						Quaternion.LookRotation(lookTarget),
						observerRotationLerpMod * Time.deltaTime);
				}
			}
		}

		public Vector3 GetLookVector() {
			return this.replicatedLookVector.Value;
		}

		public int AddMoveModifier(CharacterMoveModifier characterMoveModifier) {
			int id = this.moveModifierIdCounter;
			this.moveModifierIdCounter++;

			this.moveModifiers.Add(id, characterMoveModifier);

			return id;
		}

		public void RemoveMoveModifier(int id) {
			this.moveModifiers.Remove(id);
		}

		public void ClearMoveModifiers() {
			this.moveModifiers.Clear();
		}

		public override void OnStartClient() {
			base.OnStartClient();
			// if (IsOwner)
			// {
			// 	mainCollider.hasModifiableContacts = true;
			// }
		}

		public override void OnStartNetwork() {
			base.OnStartNetwork();
			TimeManager.OnTick += OnTick;
			TimeManager.OnPostTick += OnPostTick;
			//Set our own kinematic state since we are disabeling the NetworkTransforms configuration
			bool shouldBeKinematic = this.IsClientInitialized && !this.Owner.IsLocalClient;
			if (shouldBeKinematic)
			{
				//switch this so Unity doesn't throw a needless error
				predictionRigidbody.Rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
			}
			predictionRigidbody.Rigidbody.isKinematic = shouldBeKinematic;
		}

		public override void OnStopNetwork() {
			base.OnStopNetwork();
			if (TimeManager != null) {
				TimeManager.OnTick -= OnTick;
				TimeManager.OnPostTick -= OnPostTick;
			}
		}

		private void ExposedState_OnChange(CharacterAnimationHelper.CharacterAnimationSyncData prev, CharacterAnimationHelper.CharacterAnimationSyncData next, bool asServer) {
			animationHelper.SetState(next);
			if(prev.state != next.state){
				this.stateChanged?.Invoke((int)next.state);
			}
		}

#region VOXEL_WORLD
		private void VoxelWorld_OnBeforeVoxelChunkUpdated(Chunk chunk) {
			if (base.IsOwner && base.IsClientInitialized) {
				var entityChunkPos = VoxelWorld.WorldPosToChunkKey(transform.position);
				var diff = (entityChunkPos - chunk.chunkKey).magnitude;
				if (diff > 1) {
					return;
				}
				voxelRollbackManager.AddChunkSnapshot(TimeManager.LocalTick - 1, chunk);
			}
		}


		private void VoxelWorld_VoxelChunkUpdated(Chunk chunk) {
			if (!(base.IsClientInitialized && base.IsOwner)) return;

			var voxelPos = VoxelWorld.ChunkKeyToWorldPos(chunk.chunkKey);
			var t = transform;
			var entityPosition = t.position;
			var voxelCenter = voxelPos + (Vector3.one / 2f);

			if (Vector3.Distance(voxelCenter, entityPosition) <= 16f) {
				// TODO: Save chunk collider state
				voxelRollbackManager.AddChunkSnapshot(TimeManager.LocalTick, chunk);
			}
		}

		private void OnBeforeVoxelPlaced(ushort voxel, Vector3Int voxelPos)
		{
			if (base.TimeManager && ((base.IsClientInitialized && base.IsOwner) || (base.IsServerInitialized && !IsOwner))) {
				HandleBeforeVoxelPlaced(voxel, voxelPos, false);
			}
		}

		private void OnReplayPreVoxelCollisionUpdate(ushort voxel, Vector3Int voxelPos)
		{
			// Server doesn't do replays, so we don't need to pass it along.
			if (base.IsOwner && base.IsClientInitialized) {
				HandleBeforeVoxelPlaced(voxel, voxelPos, true);
			}
		}

		private void HandleBeforeVoxelPlaced(ushort voxel, Vector3Int voxelPos, bool replay) {
			if (voxel == 0) return; // air placement

			// Check for intersection of entity and the newly-placed voxel:
			var voxelCenter = voxelPos + (Vector3.one / 2f);
			var voxelBounds = new Bounds(voxelCenter, Vector3.one);

			// If entity intersects with new voxel, bump the entity upwards (by default, the physics will push it to
			// to the side, which is bad for vertical stacking).
			if (mainCollider.bounds.Intersects(voxelBounds)) {
				// print($"Triggering stepUp tick={TimeManager.LocalTick} time={Time.time}");
				voxelStepUp = 1.01f;
			}
		}
#endregion

		private void OnTick() {
			if (!enabled) {
				return;
			}

			//Update the movement state of the character
			MoveReplicate(BuildMoveData());

			if (base.IsClientStarted) {
				//Update visual state of client character
				var currentPos = rootTransform.position;
				var worldVel = (currentPos - trackedPosition) * (1 / (float)InstanceFinder.TimeManager.TickDelta);
				trackedPosition = currentPos;
				if (worldVel != lastWorldVel) {
					lastWorldVel = worldVel;
					animationHelper.SetVelocity(graphicTransform.InverseTransformDirection(worldVel));
				}
			}
		}

		private void OnPostTick() {
			if(authorityMode == ServerAuthority.CLIENT_AUTH){
				return;
			}
			
			//Have to reconcile rigidbodies in post tick
			if (TimeManager.Tick % ticksUntilReconcile == 0 || _forceReconcile) {
				CreateReconcile();
			}
		}

		[ObserversRpc(ExcludeOwner = true)]
		private void ObserverOnImpactWithGround(Vector3 velocity, ushort blockId) {
			this.OnImpactWithGround?.Invoke(velocity, blockId);
		}

#region RECONCILE
		public override void CreateReconcile() {
			if (base.IsServerInitialized) {
				var t = this.transform;
				ReconcileData rd = new ReconcileData() {
					trackedVelocity = trackedVelocity,
					SlideVelocity = slideVelocity,
					PrevMoveFinalizedDir = prevMoveFinalizedDir,
					characterState = state,
					prevCharacterState = prevState,
					PrevMoveVector = prevMoveVector,
					PrevSprint = prevSprint,
					PrevJump = prevJump,
					PrevCrouch = prevCrouchOrSlide,
					prevStepUp = prevStepUp,
					PrevMoveDir = prevMoveDir,
					PrevGrounded = prevGrounded,
					prevGroundId = prevGroundId,
					PrevJumpStartPos = prevJumpStartPos,
					TimeSinceSlideStart = timeSinceSlideStart,
					TimeSinceBecameGrounded = timeSinceBecameGrounded,
					TimeSinceWasGrounded = timeSinceWasGrounded,
					TimeSinceJump = timeSinceJump,
					jumpCount = jumpCount,
					prevCharacterMoveModifier = prevCharacterMoveModifier,
					PrevLookVector = prevLookVector,
					stepUpStartTime = stepUpStartTime
				};

				rd.SetRigidbody(predictionRigidbody);
				Reconciliation(rd);
			}
		}

		[Reconcile]
		private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable) {
			if (object.Equals(rd, default(ReconcileData))) return;

			//Sets state of transform and rigidbody.
			Rigidbody rb = predictionRigidbody.Rigidbody;
			//Debug.Log("SETTING RIGIBODY: " + rb.velocity + " tracked vel: " + rd.trackedVelocity + " literal move: " + lastWorldVel);
			rb.SetState(rd.RigidbodyState);

			//Applies reconcile information from predictionrigidbody.
			predictionRigidbody.Reconcile(rd.PredictionRigidbody);
			trackedVelocity = rd.trackedVelocity;
			slideVelocity = rd.SlideVelocity;
			prevMoveFinalizedDir = rd.PrevMoveFinalizedDir;
			state = rd.characterState;
			prevState = rd.prevCharacterState;
			prevMoveVector = rd.PrevMoveVector;
			prevSprint = rd.PrevSprint;
			prevJump = rd.PrevJump;
			prevCrouchOrSlide = rd.PrevCrouch;
			prevStepUp = rd.prevStepUp;
			prevGrounded = rd.PrevGrounded;
			prevGroundId = rd.prevGroundId;
			prevMoveDir = rd.PrevMoveDir;
			prevJumpStartPos = rd.PrevJumpStartPos;
			prevTick = rd.GetTick() - 1;
			timeSinceSlideStart = rd.TimeSinceSlideStart;
			timeSinceBecameGrounded = rd.TimeSinceBecameGrounded;
			timeSinceWasGrounded = rd.TimeSinceWasGrounded;
			stepUpStartTime = rd.stepUpStartTime;
			timeSinceJump = rd.TimeSinceJump;
			jumpCount = rd.jumpCount;
			prevCharacterMoveModifier = rd.prevCharacterMoveModifier;

			if (!base.IsServerInitialized && base.IsOwner) {
				if (voxelRollbackManager) {
					voxelRollbackManager.DiscardSnapshotsBehindTick(rd.GetTick());
				}

				// Clear old move modifier history
				var keys = this.moveModifierFromEventHistory.Keys;
				var toRemove = new List<uint>();
				foreach (var key in keys) {
					if (key < rd.GetTick())
					{
						toRemove.Add(key);
					}
				}
				foreach (var key in toRemove) {
					this.moveModifierFromEventHistory.Remove(key);
				}
			}
		}
#endregion

		private bool CheckIfSprinting(MoveInputData md) {
			//Only sprint if you are moving forward
			// return md.Sprint && md.MoveInput.y > sprintForwardThreshold;
			return md.sprint && md.moveDir.magnitude > 0.1f;
		}

		private Vector3 GetSlideVelocity()
		{
			var flatMoveDir = new Vector3(prevMoveFinalizedDir.x, 0, prevMoveFinalizedDir.z).normalized;
			return flatMoveDir * (moveData.sprintSpeed * moveData.slideSpeedMultiplier);
		}

		public bool IsGrounded() {
			return grounded;
		}

		public bool IsSprinting() {
			return sprinting;
		}

		[Replicate]
		private void MoveReplicate(MoveInputData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable) {
			if (state == ReplicateState.CurrentFuture) return;

			if(IsClientInitialized || authorityMode != ServerAuthority.CLIENT_AUTH){
				OnBeginMove?.Invoke(base.PredictionManager.IsReconciling, md);
				Move(md, base.IsServerInitialized, channel, base.PredictionManager.IsReconciling);
				OnEndMove?.Invoke(base.PredictionManager.IsReconciling, md);
			}
		}

#region MOVE START
		private void Move(MoveInputData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false) {
			//print("MOVE tick: " + md.GetTick() + " replay: " + replaying);
			// if(authority == ServerAuthority.SERVER_ONLY && !IsServerStarted){
			// 	return;
			// }
			// var currentTime = TimeManager.TicksToTime(TickType.LocalTick);

			//if ((IsClient && IsOwner) || (IsServer && !IsOwner)) {
				//print("Move tick=" + md.GetTick() + (replaying ? " (replay)" : ""));
			//}

			 //print($"Move isOwner={IsOwner} asServer={asServer}");

#region VOXELS
			if (!asServer && IsOwner && voxelRollbackManager) {
				if (replaying) {
					Profiler.BeginSample("Load Snapshot " + md.GetTick());
					voxelRollbackManager.LoadSnapshot(md.GetTick(), Vector3Int.RoundToInt(transform.position));
					Profiler.EndSample();
				} else {
					voxelRollbackManager.RevertBackToRealTime();
				}
			}

			if (IsOwner && base.IsClientInitialized && voxelWorld) {
				voxelWorld.focusPosition = this.transform.position;
			}
#endregion

#region INIT VARIABLES
			var characterMoveVelocity = Vector3.zero;
			var currentVelocity = predictionRigidbody.Rigidbody.velocity;// trackedVelocity;
			var newVelocity = currentVelocity;
			var isDefaultMoveData = object.Equals(md, default(MoveInputData));
			var isIntersecting = IsIntersectingWithBlock();
			var deltaTime = (float)TimeManager.TickDelta;
			timeElapsed = (float)TimeManager.TicksToTime(TimeManager.Tick);

#region GROUNDED
			//Ground checks
			var (grounded, groundedBlockId, groundedBlockPos, groundHit, detectedGround) = physics.CheckIfGrounded(transform.position, newVelocity * deltaTime, md.moveDir);
			if (isIntersecting) {
				grounded = true;
			}
			this.grounded = grounded;
			if (IsOwner || asServer) {
				this.groundedBlockId.Value = groundedBlockId;
			}
			this.groundedRaycastHit = groundHit;
			this.groundedBlockPos = groundedBlockPos;

			//Lock to ground
			//if(grounded && newVelocity.y < .01f){
				//newVelocity.y = 0;
				//bool forceSnap = prevGrounded && groundHit.collider.GetInstanceID() == prevGroundId;
				//SnapToY(groundHit.point.y, forceSnap);
			//}

			if (grounded && !prevGrounded) {
				jumpCount = 0;
				timeSinceBecameGrounded = 0f;
			} else {
				timeSinceBecameGrounded = Math.Min(timeSinceBecameGrounded + deltaTime, 100f);
			}
			var groundSlopeDir = detectedGround ? Vector3.Cross(Vector3.Cross(groundHit.normal, Vector3.down), groundHit.normal).normalized : transform.forward;
			var slopeDot = 1-Mathf.Max(0, Vector3.Dot(groundHit.normal, Vector3.up));
#endregion

			if (isDefaultMoveData) {
				// Predictions.
				// This is where we guess the movement of the client.
				// Note: default move data only happens on the server.
				// There will never be a replay that happens with default data. Because replays are client only.
				md.crouchOrSlide = prevCrouchOrSlide;
				md.sprint = prevSprint;
				md.jump = false;//prevJump; //Don't predict more jumps
				md.moveDir = prevMoveDir;
				md.lookVector = prevLookVector;
			}

			if (this.disableInput) {
				md.moveDir = Vector3.zero;
				md.crouchOrSlide = false;
				md.jump = false;
				md.lookVector = prevLookVector;
				md.sprint = false;
			}
#endregion

			// Fall impact
			if (grounded && !prevGrounded && !replaying) {
				this.OnImpactWithGround?.Invoke(currentVelocity, groundedBlockId);
				if (asServer){
					ObserverOnImpactWithGround(currentVelocity, groundedBlockId);
				}
			}

#region MODIFIERS
			CharacterMoveModifier characterMoveModifier = new CharacterMoveModifier()
			{
				speedMultiplier = 1,
				jumpMultiplier = 1,
				blockSprint = false,
				blockJump = false,
			};
			if (!replaying)
			{
				CharacterMoveModifier modifierFromEvent = new CharacterMoveModifier()
				{
					speedMultiplier = 1,
					jumpMultiplier = 1,
					blockSprint = false,
					blockJump = false,
				};
				OnAdjustMove?.Invoke(modifierFromEvent);
				moveModifierFromEventHistory.TryAdd(md.GetTick(), modifierFromEvent);
				characterMoveModifier = modifierFromEvent;
			} else
			{
				if (moveModifierFromEventHistory.TryGetValue(md.GetTick(), out CharacterMoveModifier value))
				{
					characterMoveModifier = value;
				} else
				{
					characterMoveModifier = prevCharacterMoveModifier;
				}
			}

			// // todo: mix-in all from _moveModifiers
			if (characterMoveModifier.blockJump)
			{
				md.jump = false;
			}
#endregion

#region GRAVITY
			if(moveData.useGravity){                
				///if () {
				if(!_flying && 
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
			var didJump = false;
			var canJump = false;
			if (requestJump) {
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
				if (transform.position.y - prevJumpStartPos.y > 0.01) {
					if (timeSinceJump < moveData.jumpUpBlockCooldown)
					{
						canJump = false;
					}
				}
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
					jumpCount++;
					newVelocity.y = moveData.jumpSpeed * characterMoveModifier.jumpMultiplier;
					prevJumpStartPos = transform.position;
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
			var shouldSlide = prevState is (CharacterState.Sprinting or CharacterState.Jumping) && timeSinceSlideStart >= moveData.slideCooldown;
			var inAir = didJump || (!detectedGround && !prevStepUp);
			// if (md.crouchOrSlide && prevState is not (CharacterState.Crouching or CharacterState.Sliding) && grounded && shouldSlide && !md.jump)
			// {
			// 	// Slide if already sprinting & last slide wasn't too recent:
			// 	state = CharacterState.Sliding;
			// 	slideVelocity = GetSlideVelocity();
			// 	slideVelocity = Vector3.ClampMagnitude(slideVelocity, moveData.sprintSpeed * 1.1f);
			// 	timeSinceSlideStart = 0f;
			// }
			// else if (md.crouchOrSlide && prevState == CharacterState.Sliding && !didJump)
			// {
			// 	if (slideVelocity.magnitude <= moveData.crouchSpeedMultiplier * moveData.speed * 1.1)
			// 	{
			// 		state = CharacterState.Crouching;
			// 		slideVelocity = Vector3.zero;
			// 	} else
			// 	{
			// 		state = CharacterState.Sliding;
			// 	}
			// }

			//Check to see if we can stand up from a crouch
			if (inAir) {
				state = CharacterState.Jumping;
			} else if((moveData.autoCrouch || prevState == CharacterState.Crouching) && !physics.CanStand()){
				state = CharacterState.Crouching;
			}else if (md.crouchOrSlide && grounded) {
				state = CharacterState.Crouching;
			} else if (isMoving) {
				if (CheckIfSprinting(md) && !characterMoveModifier.blockSprint) {
					state = CharacterState.Sprinting;
					sprinting = true;
				} else {
					state = CharacterState.Running;
				}
			} else {
				state = CharacterState.Idle;
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
			if (state != CharacterState.Sliding) {
				timeSinceSlideStart = Math.Min(timeSinceSlideStart + deltaTime, 100f);
			}

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
			if (state != CharacterState.Sliding) {
				characterMoveVelocity.x = normalizedMoveDir.x;
				characterMoveVelocity.z = normalizedMoveDir.z;
			}
	#region CROUCH
			// Prevent falling off blocks while crouching
			var isCrouching = !didJump && md.crouchOrSlide && prevState != CharacterState.Sliding;
			if (moveData.preventFallingWhileCrouching && !prevStepUp && isCrouching && isMoving && grounded ) {
				var posInMoveDirection = transform.position + normalizedMoveDir * 0.2f;
				var (groundedInMoveDirection, blockId, blockPos, _, _) = physics.CheckIfGrounded(posInMoveDirection, newVelocity, normalizedMoveDir);
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
						(foundGroundedDir, _, _, _, _) = physics.CheckIfGrounded(stepPosition, newVelocity, normalizedMoveDir);
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
			switch (state)
			{
				case CharacterState.Crouching:
					this.currentCharacterHeight = standingCharacterHeight * moveData.crouchHeightMultiplier;
					break;
				case CharacterState.Sliding:
					this.currentCharacterHeight = standingCharacterHeight * moveData.slideHeightMultiplier;
					break;
				default:
					this.currentCharacterHeight = standingCharacterHeight;
					break;
			}

			characterHalfExtents = new Vector3(moveData.characterRadius,  this.currentCharacterHeight/2f,moveData.characterRadius);
			mainCollider.transform.localScale = characterHalfExtents*2;
			mainCollider.transform.localPosition = new Vector3(0,this.currentCharacterHeight/2f,0);
#endregion


#region IMPULSE
		var isImpulsing = _impulseForce != Vector3.zero;
		if (isImpulsing) {
			if(useExtraLogging){
				print("isImpulsing	: " + isImpulsing + " impulse force: " +_impulseForce);
			}
			newVelocity += _impulseForce;
			_impulseForce = Vector3.zero;

			//Apply the impulse over multiple frames to push against drag in a more expected way
			///_impulseForce *= .95f-deltaTime;
			//characterMoveVelocity *= .95f-deltaTime;
			//Stop the y impulse instantly since its not using air resistance atm
			// _impulseForce.y = 0; 
			// if(_impulseForce.sqrMagnitude < .5f){
			// 	_impulseForce = Vector3.zero;
			// }
		}
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
			


#region MOVEMENT
			// Find speed
			float currentSpeed;
			//Adding 1 to offset the drag force so actual movement aligns with the values people enter in moveData
			if (state is CharacterState.Crouching or CharacterState.Sliding) {
				currentSpeed = moveData.crouchSpeedMultiplier * moveData.speed + 1;
			} else if (CheckIfSprinting(md) && !characterMoveModifier.blockSprint) {
				currentSpeed = moveData.sprintSpeed+1;
			} else {
				currentSpeed = moveData.speed+1;
			}

			if (_flying) {
				currentSpeed *= 3.5f;
			}

			currentSpeed *= characterMoveModifier.speedMultiplier;

			//Apply speed
			characterMoveVelocity *= currentSpeed;


			// Bleed off slide velocity:
			//Sliding is disabled for now
			// if (state == CharacterState.Sliding && slideVelocity.sqrMagnitude > 0) {
			// 	print("sliding: " + slideVelocity);
			// 	if (grounded)
			// 	{
			// 		slideVelocity = Vector3.Lerp(slideVelocity, Vector3.zero, Mathf.Min(1f, 4f * deltaTime));
			// 	}
			// 	else
			// 	{
			// 		slideVelocity = Vector3.Lerp(slideVelocity, Vector3.zero, Mathf.Min(1f, 1f * deltaTime));
			// 	}

			// 	if (slideVelocity.sqrMagnitude < 1)
			// 	{
			// 		slideVelocity = Vector3.zero;
			// 	}

			// 	newVelocity += slideVelocity;
			// }

			//Flying movement
			if (_flying) {
				if (md.jump) {
					newVelocity.y += 14;
				}

				if (md.crouchOrSlide) {
					newVelocity.y -= 14;
				}
			}

#region SLOPE			
			if (moveData.detectSlopes && detectedGround){
				//On Ground and detecting slopes

				//print("SLOPE DOT: " + slopeDot + " slope dir: " + groundSlopeDir.normalized);
				//print("Move Vector Before: " + characterMoveVector);
				//Add slope forces
				// var slopeDir = Vector3.ProjectOnPlane(characterMoveVector.normalized, hit.normal);
				// characterMoveVector.y = slopeDir.y * -moveData.slopeForce; 
				// groundSlopeDir *= strength;

				//Slideing down slopes
				//print("slopDot: " + slopeDot);

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
				//Don't move character in direction its already moveing
				//Positive dot means we are already moving in this direction. Negative dot means we are moving opposite of velocity.
				var dirDot = Vector3.Dot(flatVelocity.normalized, characterMoveVelocity.normalized) / currentSpeed;
				if(!replaying && useExtraLogging){
					print("old vel: " + currentVelocity + " new vel: " + newVelocity + " move dir: " + characterMoveVelocity + " Dir dot: " + dirDot + " grounded: " + grounded + " canJump: " + canJump + " didJump: " + didJump);
				}
				characterMoveVelocity *= -Mathf.Min(0, dirDot-1);
			
				if (inAir){
					characterMoveVelocity *= moveData.airSpeedMultiplier;
				}

				//Instantly move at the desired speed
				if(Mathf.Abs(newVelocity.x) < Mathf.Abs(characterMoveVelocity.x)){
					newVelocity.x = characterMoveVelocity.x;
				}
				if(Mathf.Abs(newVelocity.y) < Mathf.Abs(characterMoveVelocity.y)){
					newVelocity.y = characterMoveVelocity.y;
				}
				if(Mathf.Abs(newVelocity.z) < Mathf.Abs(characterMoveVelocity.z)){
					newVelocity.z = characterMoveVelocity.z;
				}
			}
			//print("isreplay: " + replaying + " didHitForward: " + didHitForward + " moveVec: " + characterMoveVector + " colliderDot: " + colliderDot  + " for: " + forwardHit.collider?.gameObject.name + " point: " + forwardHit.point);
#endregion
#endregion

#region STEP_UP
		//Step up as the last step so we have the most up to date velocity to work from
		var didStepUp = false;
		if(moveData.detectStepUps && !md.crouchOrSlide){
			(bool hitStepUp, bool onRamp, Vector3 pointOnRamp, Vector3 stepUpVel) = physics.StepUp(rootTransform.position, newVelocity + characterMoveVelocity, deltaTime, detectedGround ? groundHit.normal: Vector3.up);
			if(hitStepUp){
				didStepUp = onRamp;
				SnapToY(pointOnRamp.y, true);
				newVelocity = Vector3.ClampMagnitude(new Vector3(stepUpVel.x, Mathf.Max(stepUpVel.y, newVelocity.y), stepUpVel.z), newVelocity.magnitude);
			}

			// Prevent movement while stuck in block
			if (isIntersecting && voxelStepUp == 0) {
				if(useExtraLogging){
					print("STOPPING VELOCITY!");
				}
				newVelocity *= 0;
			}

			//Stepping up voxel blocks
			// if (voxelStepUp != 0) {
			// 	// print($"Performing stepUp tick={md.GetTick()} time={Time.time}");
			// 	const float maxStepUp = 2f;
			// 	if (voxelStepUp > maxStepUp) {
			// 		voxelStepUp -= maxStepUp;
			// 		characterMoveVector.y += maxStepUp;
			// 	} else {
			// 		characterMoveVector.y += voxelStepUp;
			// 		voxelStepUp = 0f;
			// 	}
			// }

			//     if (!replaying && IsOwner) {
			//      if (Time.time < this.timeTempInterpolationEnds) {
			// _predictedObject.GetOwnerSmoother()?.SetInterpolation(this.tempInterpolation);
			//      } else {
			//       _predictedObject.GetOwnerSmoother()?.SetInterpolation(this.ownerInterpolation);
			//      }
			//     }
		}
#endregion
			
#region APPLY FORCES
			if(moveData.useAccelerationMovement){
				newVelocity += characterMoveVelocity;
			}

			//Clamp the velocity
			newVelocity = Vector3.ClampMagnitude(newVelocity, moveData.terminalVelocity);
			if(!inAir && !isImpulsing
				&& normalizedMoveDir.sqrMagnitude < .1f 
				&& Mathf.Abs(newVelocity.x + newVelocity.z) < moveData.minimumVelocity
				){
				//Zero out flat velocity
				newVelocity.x = 0;
				newVelocity.z = 0;
			}
			
			//print($"<b>JUMP STATE</b> {md.GetTick()}. <b>isReplaying</b>: {replaying}    <b>mdJump </b>: {md.jump}    <b>canJump</b>: {canJump}    <b>didJump</b>: {didJump}    <b>currentPos</b>: {transform.position}    <b>currentVel</b>: {currentVelocity}    <b>newVel</b>: {newVelocity}    <b>grounded</b>: {grounded}    <b>currentState</b>: {state}    <b>prevState</b>: {prevState}    <b>mdMove</b>: {md.moveDir}    <b>characterMoveVector</b>: {characterMoveVector}");
			
			//Execute the forces onto the rigidbody
			predictionRigidbody.Velocity(newVelocity);
			predictionRigidbody.Simulate();
			trackedVelocity = newVelocity;
#endregion

			
#region SAVE STATE
			if (!replaying && this.IsClientStarted) {
				//Replicate the look vector
				if (!isDefaultMoveData) {
					SetLookVector(md.lookVector);
				}
				
				//Fire state change event
				TrySetState(new CharacterAnimationHelper.CharacterAnimationSyncData(){
					state = state,
					grounded = !inAir || didStepUp,
					sprinting = sprinting,
					crouching = isCrouching,
				});

				if(didJump){
					RpcTriggerJump();
					//Fire locally immediately
					this.animationHelper.TriggerJump();
				}
			}

			// Handle OnMoveDirectionChanged event
			if (prevMoveDir != md.moveDir) {
				OnMoveDirectionChanged?.Invoke(md.moveDir);
			}
			prevState = state;
			prevSprint = md.sprint;
			prevJump = md.jump;
			prevCrouchOrSlide = md.crouchOrSlide;
			prevMoveVector = characterMoveVelocity;
			prevMoveFinalizedDir = md.moveDir;//TODO: we aren't modifying the dir so this isn't needed anymore?
			prevMoveDir = md.moveDir;
			prevGrounded = grounded;
			prevTick = md.GetTick();
			prevCharacterMoveModifier = characterMoveModifier;
			prevLookVector = md.lookVector;
			prevGroundId = groundHit.collider?groundHit.collider.GetInstanceID():0;
			prevStepUp = didStepUp;
			///prevJumpStartPos is set when you actually jump
#endregion

			PostCharacterControllerMove();

			if(!replaying){
				if(useExtraLogging){
					print("Speed: " + currentSpeed + " Actual Movement Per Second: " + (physics.GetFlatDistance(rootTransform.position, lastPos) / deltaTime));
				}
				lastPos = transform.position;
			}
		}
#endregion
#region MOVE END
#endregion

		private MoveInputData BuildMoveData() {

			if (!base.IsOwner && !base.IsServerInitialized) {
				MoveInputData data = default;
				data.customData = queuedCustomData;
				queuedCustomData = null;
				return data;
			}
			
			//Let TS apply custom data
			OnSetCustomData?.Invoke();

			var customData = queuedCustomData;
			queuedCustomData = null;

			MoveInputData moveData = new MoveInputData(_moveDir, _jump, _crouchOrSlide, _sprint, replicatedLookVector.Value, customData);

			return moveData;
		}

		private void SnapToY(float newY, bool forceSnap){
			if(useExtraLogging){
				print("Snapping to Y: " + newY);
			}
			var newPos = this.predictionRigidbody.Rigidbody.transform.position;
			newPos.y = newY;
			ForcePosition(newPos);
		}

		private void ForcePosition(Vector3 newPos){
			if(this.predictionRigidbody.Rigidbody.isKinematic){
				this.transform.position = newPos;
			}else{
				this.predictionRigidbody.Rigidbody.MovePosition(newPos);
			}
		}

		[Server]
		public void Teleport(Vector3 position) {
			TeleportAndLook(position, replicatedLookVector.Value);
		}

		[Server]
		public void TeleportAndLook(Vector3 position, Vector3 lookVector) {
			if(useExtraLogging){
				print("Teleporting to: " + position);
			}
			_forceReconcile = true;
			RpcTeleport(Owner, position, lookVector);
		}



		[TargetRpc(RunLocally = true)]
		private void RpcTeleport(NetworkConnection conn, Vector3 pos, Vector3 lookVector) {
			mainCollider.enabled = false;
			//predictionRigidbody.Velocity(Vector3.zero);
			rootTransform.position = pos;
			replicatedLookVector.Value = lookVector;
			mainCollider.enabled = true;
		}

		[Server]
		public void SetVelocity(Vector3 velocity) {
			SetVelocityInternal(velocity);
			if (Owner.ClientId != -1) {
				RpcSetVelocity(Owner, velocity);
			}
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
			predictionRigidbody.Velocity(velocity);
			_forceReconcile = true;
		}

		[TargetRpc]
		private void RpcSetVelocity(NetworkConnection conn, Vector3 velocity) {
			SetVelocityInternal(velocity);
		}

#region TS_ACCESS
		public void SetMoveInput(Vector3 moveDir, bool jump, bool sprinting, bool crouchOrSlide, bool moveDirWorldSpace) {
			if (moveDirWorldSpace) {
				_moveDir = moveDir;
			} else {
				_moveDir = this.graphicTransform.TransformDirection(moveDir);
			}
			_crouchOrSlide = crouchOrSlide;
			_sprint = sprinting;
			_jump = jump;
		}

		public void AddImpulse(Vector3 impulse){
			if(useExtraLogging){
				print("Adding impulse: " + impulse);
			}
			_impulseForce += impulse;
		}

		public void SetImpulse(Vector3 impulse){
			if(useExtraLogging){
				print("Setting impulse: " + impulse);
			}
			_impulseForce = impulse;
		}

		public void SetLookVector(Vector3 lookVector){
			this.replicatedLookVector.Value = lookVector;
		}

		public void SetCustomData(BinaryBlob customData) {
			queuedCustomData = customData;
		}

		public int GetState() {
			return (int)replicatedState.Value.state;
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
#endregion

		[ServerRpc]
		private void RpcSetFlying(bool flyModeEnabled) {
			this._flying = flyModeEnabled;
		}

		public void SetFlying(bool flying) {
			if (flying && !this._allowFlight) {
				Debug.LogError("Unable to fly when allow flight is false. Call entity.SetAllowFlight(true) first.");
				return;
			}
			this._flying = flying;
			RpcSetFlying(flying);
		}

		[Server]
		public void SetAllowFlight(bool allowFlight) {
			TargetAllowFlight(base.Owner, allowFlight);
		}

		[TargetRpc(RunLocally = true)]
		private void TargetAllowFlight(NetworkConnection conn, bool allowFlight) {
			this._allowFlight = allowFlight;
			if (this._flying && !this._allowFlight) {
				this._flying = false;
			}
		}

		private void TrySetState(CharacterAnimationHelper.CharacterAnimationSyncData syncedState) {
			this.replicatedState.Value = syncedState;
			if(authorityMode == ServerAuthority.CLIENT_AUTH){
				SetServerState(syncedState);
			}
			if(syncedState.state != this.replicatedState.Value.state){
				stateChanged?.Invoke((int)syncedState.state);
			}
			animationHelper.SetState(syncedState);
		}
		
		//Create a ServerRpc to allow owner to update the value on the server in the ClientAuthoritative mode
		[ServerRpc] private void SetServerState(CharacterAnimationHelper.CharacterAnimationSyncData value){
			this.replicatedState.Value = value;
		}
		
		//Create a ServerRpc to allow owner to update the value on the server in the ClientAuthoritative mode
		[ServerRpc] private void SetServerLookVector(Vector3 value){
			this.replicatedLookVector.Value = value;
		}

		[ServerRpc]
		private void RpcTriggerJump(){
			TriggerJump();
		}
		
		[ObserversRpc(RunLocally = false, ExcludeOwner = true)]
		private void TriggerJump(){
			this.animationHelper.TriggerJump();
		}

		/**
		 * Checks for colliders that intersect with the character.
		 * Returns true if character is colliding with any colliders.
		 */
		public bool IsIntersectingWithBlock() {
			// Vector3 center = transform.TransformPoint(currentCharacterCenter);
			// Vector3 delta = (0.5f * currentCharacterHeight - currentCharacterRadius) * Vector3.up;
			// Vector3 bottom = center - delta;
			// Vector3 top = bottom + delta;

			// overlappingCollidersCount = Physics.OverlapCapsuleNonAlloc(bottom, top, currentCharacterRadius, overlappingColliders, LayerMask.GetMask("Block"));

			// for (int i = 0; i < overlappingCollidersCount; i++) {
			// 	Collider overlappingCollider = overlappingColliders[i];

			// 	if (overlappingCollider.gameObject.isStatic) {
			// 		continue;
			// 	}

			// 	ignoredColliders.Add(overlappingCollider);
			// 	Physics.IgnoreCollision(mainCollider, overlappingCollider, true);
			// }

			//return overlappingCollidersCount > 0;
			return false;
		}

		private void PostCharacterControllerMove() {
			// for (int i = 0; i < ignoredColliders.Count; i++) {
			// 	Collider ignoredCollider = ignoredColliders[i];
			// 	Physics.IgnoreCollision(mainCollider, ignoredCollider, false);
			// }

			// ignoredColliders.Clear();
		}
	}
}
