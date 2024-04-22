using System;
using System.Collections.Generic;
using Assets.Luau;
using Code.Player.Character.API;
using Code.Player.Human.Net;
using FishNet;
using FishNet.Component.Prediction;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Player.Entity;
using UnityEngine;
using UnityEngine.Profiling;
using VoxelWorldStuff;

namespace Code.Player.Character {
	[LuauAPI]
	[RequireComponent(typeof(Rigidbody))]
	public class CharacterMovement : NetworkBehaviour {
		[SerializeField] private CharacterMovementData moveData;
		public CharacterAnimationHelper animationHelper;
		public CapsuleCollider mainCollider;

		public delegate void StateChanged(object state);
		public event StateChanged stateChanged;

		public delegate void CustomDataFlushed();
		public event CustomDataFlushed customDataFlushed;

		public delegate void DispatchCustomData(object tick, BinaryBlob customData);
		public event DispatchCustomData dispatchCustomData;

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


		public float standingCharacterHeight {get; private set;} 
		public float standingCharacterRadius {get; private set;} 
		public Vector3 standingCharacterCenter {get; private set;} 

		public float currentCharacterHeight => mainCollider.height;
		public float currentCharacterRadius => mainCollider.radius;
		public Vector3 currentCharacterCenter => mainCollider.center;

		// Controls
		private bool _jump;
		private Vector3 _moveDir;
		private bool _sprint;
		private bool _crouchOrSlide;
		private Vector3 _lookVector;
		private bool _flying;
		private bool _allowFlight;

		// State
		private PredictionRigidbody predictionRigidbody = new PredictionRigidbody();
		private Vector3 externalForceVelocity = Vector3.zero;//Networked velocity force in m/s (Does not contain input velocities)
		private Vector3 lastWorldVel = Vector3.zero;//Literal last move of gameobject in scene
		private Vector3 slideVelocity;
		private float stepUp;
		private Vector3 impulse = Vector3.zero;
		private bool impulseIgnoreYIfInAir = false;
		private readonly Dictionary<int, CharacterMoveModifier> moveModifiers = new();
		private bool grounded;
		private bool sprinting;

		/// <summary>
		/// Key: tick
		/// Value: the MoveModifier received from <c>adjustMoveEvent</c>
		/// </summary>
		private readonly Dictionary<uint, CharacterMoveModifier> moveModifierFromEventHistory = new();

		// History
		private bool prevCrouchOrSlide;
		private bool prevSprint;
		private bool prevJump;
		private Vector3 prevMoveFinalizedDir;
		private Vector2 prevMoveVector;
		private Vector3 prevMoveDir;
		private Vector3 prevLookVector;
		private uint prevTick;
		private bool prevGrounded;
		private float timeSinceBecameGrounded;
		private float timeSinceWasGrounded;
		private float timeSinceJump;
		private Vector3 prevJumpStartPos;

		private CharacterMoveModifier prevCharacterMoveModifier = new CharacterMoveModifier()
		{
			speedMultiplier = 1,
		};

		private Vector3 trackedPosition = Vector3.zero;
		private float timeSinceSlideStart;
		private bool serverControlled = false;

		// [SyncVar (OnChange = nameof(ExposedState_OnChange), ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		[NonSerialized]
		public readonly SyncVar<CharacterState> replicatedState = new SyncVar<CharacterState>(CharacterState.Idle, new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));

		// [SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		[NonSerialized]
		public readonly SyncVar<Vector3> replicatedLookVector = new SyncVar<Vector3>(new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));

		private CharacterState state = CharacterState.Idle;
		private CharacterState prevState = CharacterState.Idle;

		private BinaryBlob queuedCustomData = null;

		private VoxelWorld voxelWorld;
		private VoxelRollbackManager voxelRollbackManager;
		
		//[SerializeField] private PredictedObject predictedObject;

		private int overlappingCollidersCount = 0;
		private readonly Collider[] overlappingColliders = new Collider[256];
		private readonly List<Collider> ignoredColliders = new List<Collider>(256);

		[Header("Variables")]
		public bool disableInput = false;
		public bool useGravity = true;

		[Tooltip("Auto detect slopes to create a downward drag. Disable as an optimization to skip raycast checks")]
		public bool detectSlopes = true;
		public LayerMask groundCollisionLayerMask;

		// [Description("When great than this value you can sprint -1 is inputing fully backwards, 1 is inputing fully forwards")]
		// public float sprintForwardThreshold = .25f;

		private bool _forceReconcile;
		private int moveModifierIdCounter = 0;

		private void OnEnable() {
			this.disableInput = false;
			this._allowFlight = false;
			this._flying = false;
			this.mainCollider.enabled = true;
			this._lookVector = Vector3.zero;
			this.externalForceVelocity = Vector3.zero;
    		this.predictionRigidbody.Initialize(gameObject.GetComponent<Rigidbody>());

			this.standingCharacterCenter = mainCollider.center;
			this.standingCharacterHeight = mainCollider.height;
			this.standingCharacterRadius = mainCollider.radius;

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

		public Vector3 GetLookVector() {
			if (IsOwner) {
				return _lookVector;
			} else {
				return this.replicatedLookVector.Value;
			}
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
			if (IsOwner) {
				mainCollider.hasModifiableContacts = true;
			}
		}

		public override void OnStartNetwork() {
			base.OnStartNetwork();
			TimeManager.OnTick += OnTick;
			TimeManager.OnPostTick += OnPostTick;
		}

		public override void OnStopNetwork() {
			base.OnStopNetwork();
			if (TimeManager != null) {
				TimeManager.OnTick -= OnTick;
				TimeManager.OnPostTick -= OnPostTick;
			}
		}

		public override void OnOwnershipServer(NetworkConnection prevOwner) {
			base.OnOwnershipServer(prevOwner);
			serverControlled = Owner == null || !Owner.IsValid;
		}

		private void ExposedState_OnChange(CharacterState prev, CharacterState next, bool asServer) {
			animationHelper.SetState(next);
			this.stateChanged?.Invoke((int)next);
		}

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
				stepUp = 1.01f;
			}
		}

		private void OnTick() {
			if (!enabled) {
				return;
			}

            //Update the movement state of the character
			MoveReplicate(BuildMoveData());

			if (base.IsClientStarted) {
				//Update visual state of client character
				var currentPos = transform.position;
				var worldVel = (currentPos - trackedPosition) * (1 / (float)InstanceFinder.TimeManager.TickDelta);
				trackedPosition = currentPos;
				if (worldVel != lastWorldVel) {
					lastWorldVel = worldVel;
					animationHelper.SetVelocity(lastWorldVel);
				}
			}
		}

		private void OnPostTick() {
			//Have to reconcile rigidbodies in post tick
			if (base.IsServerStarted) {
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
					SlideVelocity = slideVelocity,
					PrevMoveFinalizedDir = prevMoveFinalizedDir,
					characterState = state,
					prevCharacterState = prevState,
					PrevMoveVector = prevMoveVector,
					PrevSprint = prevSprint,
					PrevJump = prevJump,
					PrevMoveDir = prevMoveDir,
					PrevGrounded = prevGrounded,
					PrevJumpStartPos = prevJumpStartPos,
					TimeSinceSlideStart = timeSinceSlideStart,
					TimeSinceBecameGrounded = timeSinceBecameGrounded,
					TimeSinceWasGrounded = timeSinceWasGrounded,
					TimeSinceJump = timeSinceJump,
					prevCharacterMoveModifier = prevCharacterMoveModifier,
					PrevLookVector = prevLookVector,
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
			Debug.Log("SETTING RIGIBODY: " + rb.velocity);
			rb.SetState(rd.RigidbodyState);

			//Applies reconcile information from predictionrigidbody.
			predictionRigidbody.Reconcile(rd.PredictionRigidbody);

			slideVelocity = rd.SlideVelocity;
			prevMoveFinalizedDir = rd.PrevMoveFinalizedDir;
			state = rd.characterState;
			prevState = rd.prevCharacterState;
			prevMoveVector = rd.PrevMoveVector;
			prevSprint = rd.PrevSprint;
			prevJump = rd.PrevJump;
			prevGrounded = rd.PrevGrounded;
			prevMoveDir = rd.PrevMoveDir;
			prevJumpStartPos = rd.PrevJumpStartPos;
			prevTick = rd.GetTick() - 1;
			timeSinceSlideStart = rd.TimeSinceSlideStart;
			timeSinceBecameGrounded = rd.TimeSinceBecameGrounded;
			timeSinceWasGrounded = rd.TimeSinceWasGrounded;
			timeSinceJump = rd.TimeSinceJump;
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

		[ObserversRpc(RunLocally = true, ExcludeOwner = true)]
		private void ObserverPerformHumanActionExcludeOwner(CharacterAction action) {
			PerformHumanAction(action);
		}

		private void PerformHumanAction(CharacterAction action) {
			if (action == CharacterAction.Jump) {
				animationHelper.TriggerJump();
			}
		}

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

		private bool VoxelIsSolid(ushort voxel) {
			return voxelWorld.GetCollisionType(voxel) != VoxelBlocks.CollisionType.None;
		}

#region GROUNDED
		private (bool isGrounded, ushort blockId, Vector3Int blockPos, RaycastHit hit) CheckIfGrounded(Vector3 pos) {
			const float tolerance = 0.03f;
			var offset = new Vector3(-0.5f, -0.5f - tolerance, -0.5f);

			// Check four corners to see if there's a block beneath player:
			if (voxelWorld) {
				var pos00 = Vector3Int.RoundToInt(pos + offset + new Vector3(-standingCharacterRadius, 0, -standingCharacterRadius));
				ushort voxel00 = voxelWorld.ReadVoxelAt(pos00);
				if (
					VoxelIsSolid(voxel00) &&
					!VoxelIsSolid(voxelWorld.ReadVoxelAt(pos00 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel00), blockPos: pos00, default);
				}

				var pos10 = Vector3Int.RoundToInt(pos + offset + new Vector3(standingCharacterRadius, 0, -standingCharacterRadius));
				ushort voxel10 = voxelWorld.ReadVoxelAt(pos10);
				if (
					VoxelIsSolid(voxel10) &&
					!VoxelIsSolid(voxelWorld.ReadVoxelAt(pos10 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel10), pos10, default);
				}

				var pos01 = Vector3Int.RoundToInt(pos + offset + new Vector3(-standingCharacterRadius, 0, standingCharacterRadius));
				ushort voxel01 = voxelWorld.ReadVoxelAt(pos01);
				if (
					VoxelIsSolid(voxel01) &&
					!VoxelIsSolid(voxelWorld.ReadVoxelAt(pos01 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel01), pos01, default);
				}

				var pos11 = Vector3Int.RoundToInt(pos + offset + new Vector3(standingCharacterRadius, 0, standingCharacterRadius));
				ushort voxel11 = voxelWorld.ReadVoxelAt(pos11);
				if (
					VoxelIsSolid(voxel11) &&
					!VoxelIsSolid(voxelWorld.ReadVoxelAt(pos11 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel11), pos11, default);
				}
			}


			// Fallthrough - do raycast to check for PrefabBlock object below:
			var layerMask = groundCollisionLayerMask;
			var centerPosition = pos;
			centerPosition.y += standingCharacterRadius*2;
			var rotation = transform.rotation;
			var distance = standingCharacterRadius + 0.1f;

			if (Physics.BoxCast(centerPosition, new Vector3(standingCharacterRadius, standingCharacterRadius, standingCharacterRadius), Vector3.down, out var hit, rotation, distance, layerMask, QueryTriggerInteraction.Ignore)) {
				var isKindaUpwards = Vector3.Dot(hit.normal, Vector3.up) > 0.1f;
				//print("HIT GROUND. Start: " + centerPosition + " distance: " + distance + " hit point: " + hit.collider.gameObject.name + " at: " + hit.point);
				return (isGrounded: isKindaUpwards, blockId: 0, Vector3Int.zero, hit);
			}

			return (isGrounded: false, blockId: 0, Vector3Int.zero, default);
		}
#endregion


[Replicate]
private void MoveReplicate(MoveInputData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable) {
	if (state == ReplicateState.CurrentFuture) return;

	if (base.IsServerInitialized && !IsOwner) {
		if (md.customData != null) {
			dispatchCustomData?.Invoke(TimeManager.Tick, md.customData);
		}
	}
	Move(md, base.IsServerInitialized, channel, base.PredictionManager.IsReconciling);
}

#region MOVE START
		private void Move(MoveInputData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false) {
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
			var characterMoveVector = Vector3.zero;
			var currentVelocity = predictionRigidbody.Rigidbody.velocity;
			var newVelocity = currentVelocity;
			var isDefaultMoveData = object.Equals(md, default(MoveInputData));
			var isIntersecting = IsIntersectingWithBlock();
			var deltaTime = (float)TimeManager.TickDelta;
			var (grounded, groundedBlockId, groundedBlockPos, hit) = CheckIfGrounded(transform.position);
			if (isIntersecting) {
				grounded = true;
			}
			this.grounded = grounded;
			if (IsOwner || asServer) {
				this.groundedBlockId.Value = groundedBlockId;
			}
			this.groundedRaycastHit = hit;
			this.groundedBlockPos = groundedBlockPos;

			if (grounded && !prevGrounded) {
				timeSinceBecameGrounded = 0f;
				//Just hit ground
				// print("ZEROING OUT Y VEL");
				// newVelocity.y = 0;
			} else {
				timeSinceBecameGrounded = Math.Min(timeSinceBecameGrounded + deltaTime, 100f);
			}

			if (isDefaultMoveData) {
				// Predictions.
				// This is where we guess the movement of the client.
				// Note: default move data only happens on the server.
				// There will never be a replay that happens with default data. Because replays are client only.
				md.crouchOrSlide = prevCrouchOrSlide;
				md.sprint = prevSprint;
				md.jump = prevJump;
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
				print("blocking jump");
				md.jump = false;
			}
#endregion

#region GRAVITY
			if(useGravity){                
				if ((!grounded || currentVelocity.y > 0) && !_flying) {
					//apply gravity
					var verticalGravMod = currentVelocity.y > 0 ? moveData.upwardsGravityMod : 1;
					newVelocity.y += Physics.gravity.y * moveData.gravityMod * verticalGravMod * deltaTime;
					//print("Applying grav: " + newVelocity);
				}
				//Clamp downward speed (simple terminal vel)
				newVelocity.y = Mathf.Max(-moveData.terminalVelocity, newVelocity.y);
			}
			//print("gravity force: " + Physics.gravity.y + " vel: " + velocity.y);
#endregion

#region JUMPING
			var requestJump = md.jump;
			var didJump = false;
			var canJump = false;
			if (requestJump) {

				if (grounded) {
					canJump = true;
				}
				// coyote jump
				else if (prevMoveVector.y <= 0.02f && timeSinceWasGrounded <= moveData.jumpCoyoteTime && currentVelocity.y <= 0 && timeSinceJump > moveData.jumpCoyoteTime) {
					canJump = true;
				}

				// extra cooldown if jumping up blocks
				if (transform.position.y - prevJumpStartPos.y > 0.01) {
					if (timeSinceJump < moveData.jumpUpBlockCooldown)
					{
						canJump = false;
					}
				}
				// dont allow jumping when travelling up
				if (currentVelocity.y > 0f) {
					canJump = false;
				}

				// dont jump if we already processed the jump
				// if(prevState == CharacterState.Jumping){
				// 	canJump = false;
				// }

				if (canJump) {
					// Jump
					didJump = true;
					newVelocity.y = moveData.jumpSpeed * characterMoveModifier.jumpMultiplier;
					prevJumpStartPos = transform.position;

					if (!replaying) {
						if (asServer) {
							ObserverPerformHumanActionExcludeOwner(CharacterAction.Jump);
						} else if (IsOwner) {
							PerformHumanAction(CharacterAction.Jump);
						}
					}
				}
			}

#endregion

#region STATE
			/*
         * Determine entity state state.
         * md.State MUST be set in all cases below.
         * We CANNOT read md.State at this point. Only md.PrevState.
         */
			var isMoving = md.moveDir.sqrMagnitude > 0.1f;
			var isJumping = !grounded || didJump;
			var shouldSlide = prevState is (CharacterState.Sprinting or CharacterState.Jumping) && timeSinceSlideStart >= moveData.slideCooldown;

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

			if (isJumping) {
				state = CharacterState.Jumping;
			} else if (md.crouchOrSlide && grounded) {
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
				characterMoveVector.x = normalizedMoveDir.x;
				characterMoveVector.z = normalizedMoveDir.z;
			}

	#region CROUCH
			// Prevent falling off blocks while crouching
			if (!didJump && grounded && isMoving && md.crouchOrSlide && prevState != CharacterState.Sliding) {
				var posInMoveDirection = transform.position + normalizedMoveDir * 0.2f;
				var (groundedInMoveDirection, blockId, blockPos, _) = this.CheckIfGrounded(posInMoveDirection);
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
						(foundGroundedDir, _, _, _) = this.CheckIfGrounded(stepPosition);
						if (foundGroundedDir)
						{
							characterMoveVector = safeDirection;
							break;
						}
					}

					// Only if we didn't find a safe direction set move to 0
					if (!foundGroundedDir) characterMoveVector = Vector3.zero;
				}
			}
	#endregion

			// Modify colliders size based on movement state
			switch (state)
			{
				case CharacterState.Crouching:
					mainCollider.height = standingCharacterHeight * moveData.crouchHeightMultiplier;
					mainCollider.center = standingCharacterCenter + new Vector3(0, -(standingCharacterHeight - mainCollider.height) * 0.5f, 0);
					break;
				case CharacterState.Sliding:
					mainCollider.height = standingCharacterHeight * moveData.slideHeightMultiplier;
					mainCollider.center = standingCharacterCenter + new Vector3(0, -(standingCharacterHeight - mainCollider.height) * 0.5f, 0);
					break;
				default:
					mainCollider.height = standingCharacterHeight;
					mainCollider.center = standingCharacterCenter;
					break;
			}
#endregion


#region IMPULSE
		var isImpulsing = this.impulse != Vector3.zero;
		if (isImpulsing) {
			// var impulseDrag = CharacterPhysics.CalculateDrag(this.impulse * deltaTime, moveData.airDensity, moveData.drag, currentCharacterHeight * (currentCharacterRadius * 2f));
			// var impulseFriction = Vector3.zero;
			// if (grounded) {
			// 	var flatImpulseVelocity = new Vector3(this.impulse.x, 0, this.impulse.z);
			// 	if (flatImpulseVelocity.sqrMagnitude < 1f) {
			// 		this.impulse.x = 0;
			// 		this.impulse.z = 0;
			// 	} else {
			// 		impulseFriction = CharacterPhysics.CalculateFriction(this.impulse, Physics.gravity.y, moveData.mass, moveData.friction) * 0.1f;
			// 	}
			// }
			// this.impulse += Vector3.ClampMagnitude(impulseDrag + impulseFriction, this.impulse.magnitude);

			// if (this.impulseIgnoreYIfInAir && !grounded) {
			// 	this.impulse.y = 0f;
			// }

			// if (grounded && this.impulse.sqrMagnitude < 1f) {
			//  this.impulse = Vector3.zero;
			// } else {
			print("Impulse force: "+ this.impulse);
			newVelocity += this.impulse;
			characterMoveVector.x = 0;
			characterMoveVector.z = 0;
			//dragForce = Vector3.zero;
			//frictionForce = Vector3.zero;
			//this.impulse = Vector3.zero;
			//Apply the impulse over multiple frames to push against drag in a more expected way
			this.impulse *= .95f-deltaTime;
			//Stop the y impulse instantly since its not using air resistance atm
			this.impulse.y = 0; 
			if(this.impulse.sqrMagnitude < .5f){
				this.impulse = Vector3.zero;
			}
			this.impulseIgnoreYIfInAir = false;
			// }
		}
#endregion

#region FRICTION_DRAG
			// Calculate drag:
			var dragForce = CharacterPhysics.CalculateDrag(currentVelocity, moveData.airDensity, moveData.drag, currentCharacterRadius);
			//Ignore vertical drag so we have full control over jumping and falling
			dragForce.y = 0;
			//var dragForce = Vector3.zero; // Disable drag


			// Calculate friction:
			var frictionForce = Vector3.zero;
			if (grounded && !isImpulsing) {
				//Clamp to 0 if vel is below threshold
				if (new Vector3(newVelocity.x, 0, newVelocity.z).sqrMagnitude < .5f) {
					frictionForce = Vector3.zero;
				} else {
					frictionForce = CharacterPhysics.CalculateFriction(newVelocity, -Physics.gravity.y, moveData.mass, moveData.friction);
				}
			}

			//Slow down velocity based on drag and friction
			newVelocity += Vector3.ClampMagnitude(dragForce + frictionForce, new Vector3(newVelocity.x, 0, newVelocity.z).magnitude);
			
#endregion
			
			// if (OwnerId != -1) {
			//  print($"tick={md.GetTick()} state={_state}, velocity={_velocity}, pos={transform.position}, name={gameObject.name}, ownerId={OwnerId}");
			// }
			
#region STEP_UP
			//TODO: Need to auto step up with rigidbody setup
			// Fix step offset not working on slopes
			// if (grounded) {
			// 	characterController.slopeLimit = 90;
			// } else {
			// 	characterController.slopeLimit = 70;
			// }

			if (isIntersecting && stepUp == 0) {
				// Prevent movement while stuck in block
				print("STOPPING VELOCITY!");
				newVelocity *= 0;
			}

			// if (stepUp != 0) {
			// 	// print($"Performing stepUp tick={md.GetTick()} time={Time.time}");
			// 	const float maxStepUp = 2f;
			// 	if (stepUp > maxStepUp) {
			// 		stepUp -= maxStepUp;
			// 		velocityMoveVectorWithDelta.y += maxStepUp;
			// 	} else {
			// 		velocityMoveVectorWithDelta.y += stepUp;
			// 		stepUp = 0f;
			// 	}
			// }

			//     if (!replaying && IsOwner) {
			//      if (Time.time < this.timeTempInterpolationEnds) {
			// _predictedObject.GetOwnerSmoother()?.SetInterpolation(this.tempInterpolation);
			//      } else {
			//       _predictedObject.GetOwnerSmoother()?.SetInterpolation(this.ownerInterpolation);
			//      }
			//     }
#endregion

#region MOVEMENT
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
			if (_flying)
			{
				if (md.jump)
				{
					newVelocity.y += 14;
				}

				if (md.crouchOrSlide) {
					newVelocity.y -= 14;
				}
			}

			// Apply speed:
			float currentSpeed;
			if (state is CharacterState.Crouching or CharacterState.Sliding) {
				currentSpeed = moveData.crouchSpeedMultiplier * moveData.speed;
			} else if (CheckIfSprinting(md) && !characterMoveModifier.blockSprint) {
				currentSpeed = moveData.sprintSpeed;
			} else {
				currentSpeed = moveData.speed;
			}

			if (_flying) {
				currentSpeed *= 3.5f;
			}

			characterMoveVector *= currentSpeed;
			characterMoveVector *= characterMoveModifier.speedMultiplier;
			
			//Clamp directional movement to not add forces if you are already moving in that direction
			var flatVelocity = new Vector3(newVelocity.x, 0, newVelocity.z);
			//print("Directional Influence: " + (characterMoveVector - newVelocity) + " mag: " + (characterMoveVector - currentVelocity).magnitude);
			
			//Positive dot means we are already moving in this direction. Negative dot means we are moving opposite of velocity.
			var dirDot = Vector3.Dot(flatVelocity.normalized, characterMoveVector.normalized) / currentSpeed;
			//print("old vel: " + currentVelocity + " new vel: " + newVelocity + " move dir: " + characterMoveVector + " Dir dot: " + dirDot);
			characterMoveVector *= -Mathf.Min(0, dirDot-1);

			if(grounded){
				//Adjust movement based on the slope of the ground you are on
				//characterMoveVector = Vector3.ProjectOnPlane(characterMoveVector, hit.normal);
			}

			// Rotate the character:
			if (!isDefaultMoveData) {
				transform.LookAt(transform.position + new Vector3(md.lookVector.x, 0, md.lookVector.z));
				if (!replaying) {
					this.replicatedLookVector.Value = md.lookVector;
				}
			}
#endregion
			
#region APPLY FORCES
			

			
			//Execute the forces onto the rigidbody
			newVelocity += characterMoveVector;
			//Update the predicted rigidbody
			predictionRigidbody.Velocity(newVelocity);
			
			print($"<b>JUMP STATE</b> {md.GetTick()}. <b>isReplaying</b>: {replaying} <b>mdJump </b>: {md.jump} <b>canJump</b>: {canJump} <b>didJump</b>: {didJump} <b>currentPos</b>: {transform.position} <b>currentVel</b>: {currentVelocity} <b>newVel</b>: {newVelocity} <b>grounded</b>: {grounded} <b>currentState</b>: {state} <b>prevState</b>: {prevState} <b>mdMove</b>: {md.moveDir} <b>characterMoveVector</b>: {characterMoveVector}");
			//print("Final vel: " + newVelocity);
			//predictionRigidbody.AddForce(newForceVector, ForceMode.Force);
			//predictionRigidbody.AddForce(characterMoveVector, ForceMode.Impulse);
			predictionRigidbody.Simulate();
#endregion

			
#region SAVE STATE
			if (!replaying) {
				//Fire state change event
				if (replicatedState.Value != state) {
					TrySetState(state);
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
			prevMoveVector = characterMoveVector;
			prevMoveFinalizedDir = md.moveDir;//TODO: we aren't modifying the dir so this isn't needed anymore?
			prevMoveDir = md.moveDir;
			prevGrounded = grounded;
			prevTick = md.GetTick();
			prevCharacterMoveModifier = characterMoveModifier;
			prevLookVector = md.lookVector;
			///prevJumpStartPos is set when you actually jump
#endregion

			PostCharacterControllerMove();
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

			var customData = queuedCustomData;
			queuedCustomData = null;

			MoveInputData moveData = new MoveInputData(_moveDir, _jump, _crouchOrSlide, _sprint, _lookVector, customData);

			if (customData != null) {
				customDataFlushed?.Invoke();
			}

			return moveData;
		}

		[Server]
		public void Teleport(Vector3 position, Quaternion rotation) {
			print("Teleporting to: " + position);
			_forceReconcile = true;
			RpcTeleport(Owner, position, rotation);
		}

		[TargetRpc(RunLocally = true)]
		private void RpcTeleport(NetworkConnection conn, Vector3 pos, Quaternion rot) {
			print("TELEPORT");
			mainCollider.enabled = false;
			//predictionRigidbody.Velocity(Vector3.zero);
			predictionRigidbody.Rigidbody.transform.SetPositionAndRotation(pos, rot);
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

		[Server]
		public void ApplyImpulse(Vector3 impulse) {
			this.ApplyImpulse(impulse, false);
		}

		[Server]
		public void ApplyImpulse(Vector3 impulse, bool ignoreYIfInAir) {
			print("Adding impulse: " + impulse);
			this.impulse = impulse;
			this.impulseIgnoreYIfInAir = ignoreYIfInAir;
			_forceReconcile = true;
		}

		[TargetRpc]
		private void RpcApplyImpulse(NetworkConnection conn, Vector3 impulse) {
			ApplyImpulse(impulse);
		}

		private void SetVelocityInternal(Vector3 velocity) {
			print("Setting velocity: " + velocity);
			predictionRigidbody.Velocity(velocity);
			_forceReconcile = true;
		}

		[TargetRpc]
		private void RpcSetVelocity(NetworkConnection conn, Vector3 velocity) {
			SetVelocityInternal(velocity);
		}

		/**
	 * Called by TS.
	 */
		public void SetMoveInput(Vector3 moveDir, bool jump, bool sprinting, bool crouchOrSlide, bool moveDirWorldSpace) {
			if (moveDirWorldSpace) {
				_moveDir = moveDir;
			} else {
				_moveDir = this.transform.TransformDirection(moveDir);
			}
			_crouchOrSlide = crouchOrSlide;
			_sprint = sprinting;
			_jump = jump;
		}

		/**
	 * Called by TS.
	 */
		public void SetLookVector(Vector3 lookVector)
		{
			this._lookVector = lookVector;
		}

		public void SetCustomData(BinaryBlob customData) {
			queuedCustomData = customData;
		}

		public int GetState() {
			return (int)replicatedState.Value;
		}

		public bool IsFlying() {
			return this._flying;
		}

		public bool IsAllowFlight() {
			return this._allowFlight;
		}

		public Vector3 GetVelocity() {
			return predictionRigidbody.Rigidbody.velocity;
		}

		[ServerRpc]
		private void RpcSetFlying(bool flyModeEnabled)
		{
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

		private void TrySetState(CharacterState state) {
			if (state != this.replicatedState.Value) {
				this.replicatedState.Value = state;
				stateChanged?.Invoke((int)state);
			}
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
