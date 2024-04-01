using System;
using System.Collections;
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
using UnityEngine.Serialization;
using VoxelWorldStuff;

namespace Code.Player.Character {
	[LuauAPI]
	public class CharacterMovement : NetworkBehaviour {
		[SerializeField] private CharacterMovementData moveData;
		public CharacterAnimationHelper animationHelper;

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

		[SyncVar(WritePermissions = WritePermission.ClientUnsynchronized, ReadPermissions = ReadPermission.ExcludeOwner)]
		[NonSerialized]
		public ushort groundedBlockId;
		[NonSerialized] public Vector3 groundedBlockPos;
		[NonSerialized] public RaycastHit groundedRaycastHit;

		private CharacterController characterController;
		private float characterControllerHeight;
		private Vector3 characterControllerCenter;
		private Collider characterCollider;

		// Controls
		private bool _jump;
		private float _lastJumpTime;
		private Vector3 _moveDir;
		private bool _sprint;
		private bool _crouchOrSlide;
		private Vector3 _lookVector;
		private bool _flying;
		private bool _allowFlight;

		// State
		private Vector3 velocity = Vector3.zero;//Networked velocity force
		private Vector3 lastWorldVel = Vector3.zero;//Literal last move of gameobject in scene
		private Vector3 slideVelocity;
		private float stepUp;
		private Vector3 impulse = Vector3.zero;
		private bool impulseIgnoreYIfInAir = false;
		private readonly Dictionary<int, CharacterMoveModifier> moveModifiers = new();
		private bool grounded;
		private Vector3 lastMove = Vector3.zero;

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
		private float timeTempInterpolationEnds;

		private CharacterMoveModifier prevCharacterMoveModifier = new CharacterMoveModifier()
		{
			speedMultiplier = 1,
		};

		private Vector3 trackedPosition = Vector3.zero;
		private float timeSinceSlideStart;
		private bool serverControlled = false;

		private Coroutine _resetOverlapRecoveryCo = null;

		[SyncVar (OnChange = nameof(ExposedState_OnChange), ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		[NonSerialized]
		public CharacterState replicatedState = CharacterState.Idle;

		[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
		[NonSerialized]
		public Vector3 replicatedLookVector;

		private CharacterState state = CharacterState.Idle;
		private CharacterState prevState = CharacterState.Idle;

		private BinaryBlob queuedCustomData = null;

		private VoxelWorld voxelWorld;
		private VoxelRollbackManager voxelRollbackManager;
		[FormerlySerializedAs("_predictedObject")]
		[SerializeField] private PredictedObject predictedObject;

		private int overlappingCollidersCount = 0;
		private readonly Collider[] overlappingColliders = new Collider[256];
		private readonly List<Collider> ignoredColliders = new List<Collider>(256);

		[Header("Variables")]
		public bool disableInput = false;

		// [Description("When great than this value you can sprint -1 is inputing fully backwards, 1 is inputing fully forwards")]
		// public float sprintForwardThreshold = .25f;

		private bool _forceReconcile;
		private int moveModifierIdCounter = 0;

		private void Awake() {
			characterController = GetComponent<CharacterController>();
			characterControllerHeight = characterController.height;
			characterControllerCenter = characterController.center;
			characterCollider = characterController.GetComponent<Collider>();
		}

		private void OnEnable() {
			this.disableInput = false;
			this._allowFlight = false;
			this._flying = false;
			characterController.enabled = true;
			this._lookVector = Vector3.zero;
			this.velocity = Vector3.zero;

			if (!voxelWorld) {
				voxelWorld = VoxelWorld.Instance;
			}
			if (voxelWorld != null) {
				voxelRollbackManager = voxelWorld.gameObject.GetComponent<VoxelRollbackManager>();
				voxelWorld.BeforeVoxelPlaced += OnBeforeVoxelPlaced;
				voxelWorld.VoxelChunkUpdated += VoxelWorld_VoxelChunkUpdated;
				voxelWorld.BeforeVoxelChunkUpdated += VoxelWorld_OnBeforeVoxelChunkUpdated;
			}
			if (voxelRollbackManager != null)
			{
				voxelRollbackManager.ReplayPreVoxelCollisionUpdate += OnReplayPreVoxelCollisionUpdate;
			}

			// EntityManager.Instance.AddEntity(this);
		}

		private void OnDisable() {
			// EntityManager.Instance.RemoveEntity(this);
			characterController.enabled = false;

			if (voxelWorld) {
				voxelWorld.BeforeVoxelPlaced -= OnBeforeVoxelPlaced;
				voxelWorld.VoxelChunkUpdated -= VoxelWorld_VoxelChunkUpdated;
				voxelWorld.BeforeVoxelChunkUpdated -= VoxelWorld_OnBeforeVoxelChunkUpdated;
			}

			if (voxelRollbackManager) {
				voxelRollbackManager.ReplayPreVoxelCollisionUpdate -= OnReplayPreVoxelCollisionUpdate;
			}
		}

		public Vector3 GetLookVector()
		{
			if (IsOwner)
			{
				return _lookVector;
			} else
			{
				return this.replicatedLookVector;
			}
		}

		public int AddMoveModifier(CharacterMoveModifier characterMoveModifier)
		{
			int id = this.moveModifierIdCounter;
			this.moveModifierIdCounter++;

			this.moveModifiers.Add(id, characterMoveModifier);

			return id;
		}

		public void RemoveMoveModifier(int id)
		{
			this.moveModifiers.Remove(id);
		}

		public void ClearMoveModifiers()
		{
			this.moveModifiers.Clear();
		}

		public override void OnStartClient()
		{
			base.OnStartClient();
			if (IsOwner) {
				characterCollider.hasModifiableContacts = true;
			}
		}

		public override void OnStartNetwork() {
			base.OnStartNetwork();
			TimeManager.OnTick += OnTick;
		}

		public override void OnStopNetwork() {
			base.OnStopNetwork();
			if (TimeManager != null) {
				TimeManager.OnTick -= OnTick;
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
			if (base.IsOwner && base.IsClient) {
				var entityChunkPos = VoxelWorld.WorldPosToChunkKey(transform.position);
				var diff = (entityChunkPos - chunk.chunkKey).magnitude;
				if (diff > 1) {
					return;
				}
				voxelRollbackManager.AddChunkSnapshot(TimeManager.LocalTick - 1, chunk);
			}
		}

		private void VoxelWorld_VoxelChunkUpdated(Chunk chunk) {
			if (!(base.IsClient && base.IsOwner)) return;

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
			if (base.TimeManager && ((base.IsClient && base.IsOwner) || (IsServer && !IsOwner))) {
				HandleBeforeVoxelPlaced(voxel, voxelPos, false);
			}
		}

		private void OnReplayPreVoxelCollisionUpdate(ushort voxel, Vector3Int voxelPos)
		{
			// Server doesn't do replays, so we don't need to pass it along.
			if (base.IsOwner && base.IsClient) {
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
			if (characterCollider.bounds.Intersects(voxelBounds)) {
				// print($"Triggering stepUp tick={TimeManager.LocalTick} time={Time.time}");
				stepUp = 1.01f;
			}
		}

		private void OnTick() {
			if (!enabled) {
				return;
			}

			if (IsOwner) {
				Reconcile(default, false);
				BuildActions(out var md);

				if (!IsClient && md.customData != null) {
					dispatchCustomData?.Invoke(TimeManager.Tick, md.customData);
				}

				MoveReplicate(md, false);
			}

			if (IsServer) {
				var t = transform;

				if (serverControlled) {
					// e.g. Bots/NPCs are server-controlled.
					BuildActions(out var md);
					Move(md, true);
				} else {
					// Client-controlled; call MoveReplicate with defaults,
					// which does Fish-Net magic to keep things in sync.
					MoveReplicate(default, true);
				}

				if (TimeManager.Tick % 3 == 0 || _forceReconcile)
				{
					_forceReconcile = false;
					var rd = new ReconcileData()
					{
						Position = t.position,
						Rotation = t.rotation,
						Velocity = velocity,
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
						// TimeSinceStepUp = this.timeSinceStepUp,
						// MoveModifiers = _moveModifiers,
						// MoveModifierFromEventHistory = _moveModifierFromEventHistory,
					};
					Reconcile(rd,  true);
				}
			}

			if (IsClient)
			{
				var currentPos = transform.position;
				var worldVel = (currentPos - trackedPosition) * (1 / (float)InstanceFinder.TimeManager.TickDelta);
				trackedPosition = currentPos;
				if (worldVel != lastWorldVel) {
					lastWorldVel = worldVel;
					animationHelper.SetVelocity(lastWorldVel);
				}
			}
		}

		[ObserversRpc(ExcludeOwner = true)]
		private void ObserverOnImpactWithGround(Vector3 velocity, ushort blockId)
		{
			this.OnImpactWithGround?.Invoke(velocity, blockId);
		}

		[Reconcile]
		private void Reconcile(ReconcileData rd, bool asServer, Channel channel = Channel.Unreliable) {
			var t = transform;

			// ReSharper disable once ReplaceWithSingleAssignment.False
			bool ignore = false;
			if (
				(t.position - rd.Position).magnitude <= 0.1f
			) {
				// print("Ignoring reconcile.");
				// ignore = true;
			}
			if (!ignore)
			{
				t.position = rd.Position;
				t.rotation = rd.Rotation;
				velocity = rd.Velocity;
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
				// timeSinceStepUp = rd.TimeSinceStepUp;
				// _moveModifiers = rd.MoveModifiers;
				// _moveModifierFromEventHistory = rd.MoveModifierFromEventHistory;
			}

			if (!asServer && base.IsOwner) {
				if (voxelRollbackManager) {
					voxelRollbackManager.DiscardSnapshotsBehindTick(rd.GetTick());
				}

				// Clear old move modifier history
				var keys = this.moveModifierFromEventHistory.Keys;
				var toRemove = new List<uint>();
				foreach (var key in keys)
				{
					if (key < rd.GetTick())
					{
						toRemove.Add(key);
					}
				}

				foreach (var key in toRemove)
				{
					this.moveModifierFromEventHistory.Remove(key);
				}
			}
		}

		[Replicate]
		private void MoveReplicate(MoveInputData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false) {
			if (asServer && !IsOwner) {
				if (md.customData != null) {
					dispatchCustomData?.Invoke(TimeManager.Tick, md.customData);
				}
			}
			Move(md, asServer, channel, replaying);
		}

		[ObserversRpc(RunLocally = true, ExcludeOwner = true)]
		private void ObserverPerformHumanActionExcludeOwner(CharacterAction action) {
			PerformHumanAction(action);
		}

		private void PerformHumanAction(CharacterAction action) {
			if (action == CharacterAction.Jump) {
				animationHelper.TriggerJump();
			}
		}

		private bool IsSprinting(MoveInputData md) {
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

		private bool VoxelIsSolid(ushort voxel) {
			return voxelWorld.GetCollisionType(voxel) != VoxelBlocks.CollisionType.None;
		}

		private (bool isGrounded, ushort blockId, Vector3Int blockPos, RaycastHit hit) CheckIfGrounded(Vector3 pos) {
			var radius = characterController.radius;

			const float tolerance = 0.03f;
			var offset = new Vector3(-0.5f, -0.5f - tolerance, -0.5f);

			// Check four corners to see if there's a block beneath player:
			if (voxelWorld) {
				var pos00 = Vector3Int.RoundToInt(pos + offset + new Vector3(-radius, 0, -radius));
				ushort voxel00 = voxelWorld.ReadVoxelAt(pos00);
				if (
					VoxelIsSolid(voxel00) &&
					!VoxelIsSolid(voxelWorld.ReadVoxelAt(pos00 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel00), blockPos: pos00, default);
				}

				var pos10 = Vector3Int.RoundToInt(pos + offset + new Vector3(radius, 0, -radius));
				ushort voxel10 = voxelWorld.ReadVoxelAt(pos10);
				if (
					VoxelIsSolid(voxel10) &&
					!VoxelIsSolid(voxelWorld.ReadVoxelAt(pos10 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel10), pos10, default);
				}

				var pos01 = Vector3Int.RoundToInt(pos + offset + new Vector3(-radius, 0, radius));
				ushort voxel01 = voxelWorld.ReadVoxelAt(pos01);
				if (
					VoxelIsSolid(voxel01) &&
					!VoxelIsSolid(voxelWorld.ReadVoxelAt(pos01 + new Vector3Int(0, 1, 0)))
				)
				{
					return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel01), pos01, default);
				}

				var pos11 = Vector3Int.RoundToInt(pos + offset + new Vector3(radius, 0, radius));
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
			var layerMask = LayerMask.GetMask("Default");
			var halfHeight = characterController.height / 1.9f;
			var centerPosition = pos + characterController.center;
			var rotation = transform.rotation;
			var distance = (halfHeight - radius) + 0.1f;

			if (Physics.BoxCast(centerPosition, new Vector3(radius, radius, radius), Vector3.down, out var hit, rotation, distance, layerMask, QueryTriggerInteraction.Ignore)) {
				var isKindaUpwards = Vector3.Dot(hit.normal, Vector3.up) > 0.1f;
				return (isGrounded: isKindaUpwards, blockId: 0, Vector3Int.zero, hit);
			}

			return (isGrounded: false, blockId: 0, Vector3Int.zero, default);
		}

		private void Move(MoveInputData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false) {
			var currentTime = TimeManager.TicksToTime(TickType.LocalTick);

			// if ((IsClient && IsOwner) || (IsServer && !IsOwner)) {
			// 	print("Move tick=" + md.GetTick() + (replaying ? " (replay)" : ""));
			// }

			// print($"Move isOwner={IsOwner} asServer={asServer}");

			if (!asServer && IsOwner && voxelRollbackManager) {
				if (replaying) {
					Profiler.BeginSample("Load Snapshot " + md.GetTick());
					voxelRollbackManager.LoadSnapshot(md.GetTick(), Vector3Int.RoundToInt(transform.position));
					Profiler.EndSample();
				} else {
					voxelRollbackManager.RevertBackToRealTime();
				}
			}

			if (IsOwner && IsClient && voxelWorld) {
				voxelWorld.focusPosition = this.transform.position;
			}

			var isDefaultMoveData = object.Equals(md, default(MoveInputData));

			var isIntersecting = IsIntersectingWithBlock();
			var delta = (float)TimeManager.TickDelta;
			var characterMoveVector = Vector3.zero;
			var (grounded, groundedBlockId, groundedBlockPos, hit) = CheckIfGrounded(transform.position);
			this.grounded = grounded;
			if (IsOwner || asServer) {
				this.groundedBlockId = groundedBlockId;
			}

			this.groundedRaycastHit = hit;
			this.groundedBlockPos = groundedBlockPos;

			if (isIntersecting)
			{
				grounded = true;
			}

			if (grounded && !prevGrounded)
			{
				timeSinceBecameGrounded = 0f;
			} else
			{
				timeSinceBecameGrounded = Math.Min(timeSinceBecameGrounded + delta, 100f);
			}

			if (isDefaultMoveData)
			{
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

			// Fall impact
			if (grounded && !prevGrounded && !replaying) {
				this.OnImpactWithGround?.Invoke(velocity, groundedBlockId);
				if (asServer)
				{
					ObserverOnImpactWithGround(velocity, groundedBlockId);
				}
			}

			// ********************* //
			// *** Move Modifier *** //
			// ********************* //
			CharacterMoveModifier characterMoveModifier = new CharacterMoveModifier()
			{
				speedMultiplier = 1,
			};
			if (!replaying)
			{
				CharacterMoveModifier modifierFromEvent = new CharacterMoveModifier()
				{
					speedMultiplier = 1,
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

			// todo: mix-in all from _moveModifiers
			if (characterMoveModifier.blockJump)
			{
				md.jump = false;
			}

			// ********************* //
			// ******* Jump ******** //
			// ********************* //
			var requestJump = md.jump;
			var didJump = false;
			if (requestJump) {
				var canJump = false;

				if (grounded) {
					canJump = true;
				}
				// coyote jump
				else if (prevMoveVector.y <= 0.02f && timeSinceWasGrounded <= moveData.jumpCoyoteTime && velocity.y <= 0 && timeSinceJump > moveData.jumpCoyoteTime) {
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
				if (velocity.y > 0f) {
					canJump = false;
				}
				if (canJump) {
					// Jump
					didJump = true;
					velocity.y = moveData.jumpSpeed;
					prevJumpStartPos = transform.position;

					if (!replaying) {
						if (asServer)
						{
							ObserverPerformHumanActionExcludeOwner(CharacterAction.Jump);
						} else if (IsOwner)
						{
							PerformHumanAction(CharacterAction.Jump);
						}
					}
				}
			}

			var isMoving = md.moveDir.sqrMagnitude > 0.1f;

			/*
         * Determine entity state state.
         * md.State MUST be set in all cases below.
         * We CANNOT read md.State at this point. Only md.PrevState.
         */
			var isJumping = !grounded || didJump;
			var shouldSlide = prevState is (CharacterState.Sprinting or CharacterState.Jumping) && timeSinceSlideStart >= moveData.slideCooldown;

			// if (md.crouchOrSlide && prevState is not (CharacterState.Crouching or CharacterState.Sliding) && grounded && shouldSlide && !md.jump)
			// {
			// 	// Slide if already sprinting & last slide wasn't too recent:
			// 	state = CharacterState.Sliding;
			// 	slideVelocity = GetSlideVelocity();
			// 	velocity = Vector3.ClampMagnitude(velocity, configuration.sprintSpeed * 1.1f);
			// 	timeSinceSlideStart = 0f;
			// }
			// else if (md.crouchOrSlide && prevState == CharacterState.Sliding && !didJump)
			// {
			// 	if (slideVelocity.magnitude <= configuration.crouchSpeedMultiplier * configuration.speed * 1.1)
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
				if (IsSprinting(md) && !characterMoveModifier.blockSprint) {
					state = CharacterState.Sprinting;
				} else {
					state = CharacterState.Running;
				}
			} else {
				state = CharacterState.Idle;
			}

			/*
	         * Update Time Since:
	         */
			if (state != CharacterState.Sliding) {
				timeSinceSlideStart = Math.Min(timeSinceSlideStart + delta, 100f);
			}

			if (didJump) {
				timeSinceJump = 0f;
			} else
			{
				timeSinceJump = Math.Min(timeSinceJump + delta, 100f);
			}

			if (grounded) {
				timeSinceWasGrounded = 0f;
			} else {
				timeSinceWasGrounded = Math.Min(timeSinceWasGrounded + delta, 100f);
			}

			/*
	         * md.State has been set. We can use it now.
	         */
			if (state != CharacterState.Sliding) {
				var norm = md.moveDir.normalized;
				characterMoveVector.x = norm.x;
				characterMoveVector.z = norm.z;
			}

			// Prevent falling off blocks while crouching
			if (!didJump && grounded && isMoving && md.crouchOrSlide && prevState != CharacterState.Sliding) {
				var posInMoveDirection = transform.position + md.moveDir.normalized * 0.2f;
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

			// Character height:
			switch (state)
			{
				case CharacterState.Crouching:
					characterController.height = characterControllerHeight * moveData.crouchHeightMultiplier;
					characterController.center = characterControllerCenter + new Vector3(0, -(characterControllerHeight - characterController.height) * 0.5f, 0);
					break;
				case CharacterState.Sliding:
					characterController.height = characterControllerHeight * moveData.slideHeightMultiplier;
					characterController.center = characterControllerCenter + new Vector3(0, -(characterControllerHeight - characterController.height) * 0.5f, 0);
					break;
				default:
					characterController.height = characterControllerHeight;
					characterController.center = characterControllerCenter;
					break;
			}

			// Gravity:
			if ((!grounded || velocity.y > 0) && !_flying) {
				velocity.y += Physics.gravity.y * delta;
			} else {
				// _velocity.y = -1f;
				velocity.y = 0f;
			}

			// Calculate drag:
			// var dragForce = EntityPhysics.CalculateDrag(_velocity * delta, configuration.airDensity, configuration.drag, _frontalArea);
			var dragForce = Vector3.zero; // Disable drag

			var isImpulsing = this.impulse != Vector3.zero;

			// Calculate friction:
			var frictionForce = Vector3.zero;
			if (grounded && !isImpulsing) {
				var flatVelocity = new Vector3(velocity.x, 0, velocity.z);
				if (flatVelocity.sqrMagnitude < 1f) {
					velocity.x = 0f;
					velocity.z = 0f;
					frictionForce = Vector3.zero;
				} else {
					frictionForce = CharacterPhysics.CalculateFriction(velocity, -Physics.gravity.y, moveData.mass, moveData.friction);
				}
			}

			// Apply impulse
			if (isImpulsing) {
				var impulseDrag = CharacterPhysics.CalculateDrag(this.impulse * delta, moveData.airDensity, moveData.drag, characterController.height * (characterController.radius * 2f));
				var impulseFriction = Vector3.zero;
				if (grounded) {
					var flatImpulseVelocity = new Vector3(this.impulse.x, 0, this.impulse.z);
					if (flatImpulseVelocity.sqrMagnitude < 1f) {
						this.impulse.x = 0;
						this.impulse.z = 0;
					} else {
						impulseFriction = CharacterPhysics.CalculateFriction(this.impulse, Physics.gravity.y, moveData.mass, moveData.friction) * 0.1f;
					}
				}
				this.impulse += Vector3.ClampMagnitude(impulseDrag + impulseFriction, this.impulse.magnitude);

				if (this.impulseIgnoreYIfInAir && !grounded) {
					this.impulse.y = 0f;
				}

				// if (grounded && this.impulse.sqrMagnitude < 1f) {
				//  this.impulse = Vector3.zero;
				// } else {
				characterMoveVector.x = 0;
				characterMoveVector.z = 0;
				dragForce = Vector3.zero;
				frictionForce = Vector3.zero;
				velocity += this.impulse;
				this.impulse = Vector3.zero;
				this.impulseIgnoreYIfInAir = false;
				// }
			}

			velocity += Vector3.ClampMagnitude(dragForce + frictionForce, new Vector3(velocity.x, 0, velocity.z).magnitude);
			// if (OwnerId != -1) {
			//  print($"tick={md.GetTick()} state={_state}, velocity={_velocity}, pos={transform.position}, name={gameObject.name}, ownerId={OwnerId}");
			// }

			// Bleed off slide velocity:
			if (state == CharacterState.Sliding && slideVelocity.sqrMagnitude > 0) {
				if (grounded)
				{
					slideVelocity = Vector3.Lerp(slideVelocity, Vector3.zero, Mathf.Min(1f, 4f * delta));
				}
				else
				{
					slideVelocity = Vector3.Lerp(slideVelocity, Vector3.zero, Mathf.Min(1f, 1f * delta));
				}

				if (slideVelocity.sqrMagnitude < 1)
				{
					slideVelocity = Vector3.zero;
				}
			}

			// Fly mode:
			if (_flying)
			{
				if (md.jump)
				{
					velocity.y += 14;
				}

				if (md.crouchOrSlide) {
					velocity.y -= 14;
				}
			}

			// Apply speed:
			float speed;
			if (state is CharacterState.Crouching or CharacterState.Sliding)
			{
				speed = moveData.crouchSpeedMultiplier * moveData.speed;
			} else if (IsSprinting(md) && !characterMoveModifier.blockSprint)
			{
				speed = moveData.sprintSpeed;
			} else
			{
				speed = moveData.speed;
			}

			if (_flying) {
				speed *= 3.5f;
			}

			characterMoveVector *= speed;
			characterMoveVector *= characterMoveModifier.speedMultiplier;

			var velocityMoveVector = Vector3.zero;

			// if (isImpulsing && impulseTickDuration <= Math.Round(configuration.impulseMoveDisableTime / TimeManager.TickDelta)) {
			//  move *= configuration.impulseMoveDisabledScalar;
			// }

			// Rotate the character:
			if (!isDefaultMoveData)
			{
				transform.LookAt(transform.position + new Vector3(md.lookVector.x, 0, md.lookVector.z));
				if (!replaying)
				{
					this.replicatedLookVector = md.lookVector;
				}
			}
			
			// Slopes
			if (grounded && !_flying && velocity.y == 0) {
				characterMoveVector -= new Vector3(0, 10, 0);
			}
			
			// Fix step offset not working on slopes
			if (grounded) {
				characterController.slopeLimit = 90;
			} else {
				characterController.slopeLimit = 70;
			}

			// Apply velocity to speed:
			velocityMoveVector += velocity;
			if (state == CharacterState.Sliding) {
				velocityMoveVector += slideVelocity;
			}

			if (isIntersecting && stepUp == 0)
			{
				// Prevent movement while stuck in block
				velocityMoveVector *= 0;
			}
			
			var velocityMoveVectorWithDelta = velocityMoveVector * delta;
			if (stepUp != 0) {
				// print($"Performing stepUp tick={md.GetTick()} time={Time.time}");
				const float maxStepUp = 2f;
				if (stepUp > maxStepUp) {
					stepUp -= maxStepUp;
					velocityMoveVectorWithDelta.y += maxStepUp;
				} else {
					velocityMoveVectorWithDelta.y += stepUp;
					stepUp = 0f;
				}
			}

			//     if (!replaying && IsOwner) {
			//      if (Time.time < this.timeTempInterpolationEnds) {
			// _predictedObject.GetOwnerSmoother()?.SetInterpolation(this.tempInterpolation);
			//      } else {
			//       _predictedObject.GetOwnerSmoother()?.SetInterpolation(this.ownerInterpolation);
			//      }
			//     }
			
			
			// Send movement to character controller
			characterController.Move(characterMoveVector * delta);
			var beforeVelMovement = transform.position;
			characterController.Move(velocityMoveVectorWithDelta);
			var velocityMoveDelta = transform.position - beforeVelMovement;

			// Check if difference between expected movement by velocity matches actual movement by velocity
			var differenceInVelocity = velocityMoveDelta - velocityMoveVectorWithDelta;
			if (differenceInVelocity.magnitude > 0.01f) {
				// if not, align velocity with the actual result (aka bounce off surface)
				velocity = Vector3.Project(velocity, velocityMoveDelta);
			}
			
			if (!replaying) {
				lastMove = velocityMoveVector + characterMoveVector;
			}

			// Effects
			if (!replaying)
			{
				if (replicatedState != state) {
					TrySetState(state);
				}
			}

			// Handle OnMoveDirectionChanged event
			if (prevMoveDir != md.moveDir) {
				OnMoveDirectionChanged?.Invoke(md.moveDir);
			}

			// Update History
			prevState = state;
			prevSprint = md.sprint;
			prevJump = md.jump;
			prevCrouchOrSlide = md.crouchOrSlide;
			prevMoveFinalizedDir = velocityMoveVectorWithDelta.normalized;
			prevMoveDir = md.moveDir;
			prevGrounded = grounded;
			prevTick = md.GetTick();
			prevCharacterMoveModifier = characterMoveModifier;
			prevLookVector = md.lookVector;

			PostCharacterControllerMove();
		}

		private void BuildActions(out MoveInputData moveData)
		{
			var customData = queuedCustomData;
			queuedCustomData = null;

			moveData = new MoveInputData()
			{
				moveDir = _moveDir,
				jump = _jump,
				crouchOrSlide = _crouchOrSlide,
				sprint = _sprint,
				lookVector = _lookVector,
				customData = customData
			};

			if (customData != null) {
				customDataFlushed?.Invoke();
			}
		}

		private IEnumerator ResetOverlapRecovery() {
			yield return new WaitForSeconds(0.4f);
			_resetOverlapRecoveryCo = null;
			characterController.enableOverlapRecovery = true;
		}

		private void SetNoOverlapRecoveryTemporarily() {
			if (_resetOverlapRecoveryCo != null) {
				StopCoroutine(_resetOverlapRecoveryCo);
				_resetOverlapRecoveryCo = null;
			}
			_resetOverlapRecoveryCo = StartCoroutine(ResetOverlapRecovery());
			characterController.enableOverlapRecovery = false;
		}

		[Server]
		public void Teleport(Vector3 position) {
			RpcTeleport(Owner, position);
		}

		[TargetRpc(RunLocally = true)]
		private void RpcTeleport(NetworkConnection conn, Vector3 pos) {
			characterController.enabled = false;
			velocity = Vector3.zero;
			transform.position = pos;
			// ReSharper disable once Unity.InefficientPropertyAccess
			characterController.enabled = true;
			// _predictedObject.InitializeSmoother(IsOwner);
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
			this.impulse = impulse;
			this.impulseIgnoreYIfInAir = ignoreYIfInAir;
			_forceReconcile = true;
		}

		[TargetRpc]
		private void RpcApplyImpulse(NetworkConnection conn, Vector3 impulse) {
			ApplyImpulse(impulse);
		}

		private void SetVelocityInternal(Vector3 velocity) {
			this.velocity = velocity;
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
			return (int)replicatedState;
		}

		public bool IsFlying() {
			return this._flying;
		}

		public bool IsAllowFlight() {
			return this._allowFlight;
		}

		public Vector3 GetVelocity() {
			return lastMove;
		}

		[ServerRpc]
		private void RpcSetFlying(bool flyModeEnabled)
		{
			this._flying = flyModeEnabled;
		}

		public void SetFlying(bool flying)
		{
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
			if (state != replicatedState)
			{
				replicatedState = state;
				stateChanged?.Invoke((int)state);
			}
		}

		/**
	 * Checks for colliders that intersect with the character.
	 * Returns true if character is colliding with any colliders.
	 */
		public bool IsIntersectingWithBlock()
		{
			float radius = characterController.radius;
			Vector3 center = transform.TransformPoint(characterController.center);
			Vector3 delta = (0.5f * characterController.height - radius) * Vector3.up;
			Vector3 bottom = center - delta;
			Vector3 top = bottom + delta;

			overlappingCollidersCount = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlappingColliders, LayerMask.GetMask("Block"));

			for (int i = 0; i < overlappingCollidersCount; i++) {
				Collider overlappingCollider = overlappingColliders[i];

				if (overlappingCollider.gameObject.isStatic) {
					continue;
				}

				ignoredColliders.Add(overlappingCollider);
				Physics.IgnoreCollision(characterController, overlappingCollider, true);
			}

			return overlappingCollidersCount > 0;
		}

		private void PostCharacterControllerMove() {
			for (int i = 0; i < ignoredColliders.Count; i++) {
				Collider ignoredCollider = ignoredColliders[i];
				Physics.IgnoreCollision(characterController, ignoredCollider, false);
			}

			ignoredColliders.Clear();
		}
	}
}
