using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Assets.Luau;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;
using Player.Entity;
using Tayx.Graphy;

[LuauAPI]
public class EntityDriver : NetworkBehaviour {
	[SerializeField] private EntityConfig configuration;
	[SerializeField] private EntityAnimator anim;

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

	public ushort groundedBlockId;
	public Vector3 groundedBlockPos;

	private CharacterController _characterController;
	private float _characterControllerHeight;
	private Vector3 _characterControllerCenter;
	private Collider _characterCollider;

	// Controls
	private bool _jump;
	private float _lastJumpTime;
	private Vector3 _moveDir;
	private bool _sprint;
	private bool _crouchOrSlide;
	private Vector3 _lookVector;

	// State
	private Vector3 _velocity = Vector3.zero;
	private Vector3 _slideVelocity;
	private float _stepUp;
	private Vector3 _impulseVelocity = Vector3.zero;
	private float _impulseDuration;
	private Dictionary<int, MoveModifier> _moveModifiers = new();
	private bool _grounded;

	/// <summary>
	/// Key: tick
	/// Value: the MoveModifier received from <c>adjustMoveEvent</c>
	/// </summary>
	private Dictionary<uint, MoveModifier> _moveModifierFromEventHistory = new();

	// History
	private bool _prevCrouchOrSlide;
	private bool _prevSprint;
	private bool _prevJump;
	private Vector3 _prevMoveFinalizedDir;
	private Vector2 _prevMoveVector;
	private Vector3 _prevMoveDir;
	private Vector3 _prevLookVector;
	private uint _prevTick;
	private bool _prevGrounded;
	private float _timeSinceBecameGrounded;
	private float _timeSinceWasGrounded;
	private float _timeSinceJump;
	private Vector3 _prevJumpStartPos;
	private float _timeSinceImpulse;

	private MoveModifier _prevMoveModifier = new MoveModifier()
	{
		speedMultiplier = 1,
	};

	private Vector3 _trackedPosition = Vector3.zero;

	private float _timeSinceSlideStart;

	private float _frontalArea;

	private bool _serverControlled = true;
	
	private Coroutine _resetOverlapRecoveryCo = null;
	private float _lastReplicateTime;
	
	[SyncVar (OnChange = nameof(ExposedState_OnChange), ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
	private EntityState _exposedState = EntityState.Idle;

	[SyncVar (ReadPermissions = ReadPermission.ExcludeOwner, WritePermissions = WritePermission.ClientUnsynchronized)]
	public Vector3 replicatedLookVector;

	private EntityState _state = EntityState.Idle;
	private EntityState _prevState = EntityState.Idle;

	private BinaryBlob _queuedCustomData = null;
	private uint _queuedSyncTick = 0;

	private VoxelWorld _voxelWorld;
	private VoxelRollbackManager _voxelRollbackManager;
	
	private int _overlappingCollidersCount = 0;
	private Collider[] _overlappingColliders = new Collider[256];
	private List<Collider> _ignoredColliders = new List<Collider>(256);

	[Header("Variables")]
	[Description("When great than this value you can sprint -1 is inputing fully backwards, 1 is inputing fully forwards")]
	public float sprintForwardThreshold = .25f;

	private bool _forceReconcile;
	private int moveModifierIdCounter = 0;

	private void Awake() {
		_characterController = GetComponent<CharacterController>();
		_characterControllerHeight = _characterController.height;
		_characterControllerCenter = _characterController.center;
		_characterCollider = _characterController.GetComponent<Collider>();
		_frontalArea = _characterController.height * (_characterController.radius * 2f);
		var voxelWorldObj = GameObject.Find("VoxelWorld");
		if (voxelWorldObj != null) {
			_voxelWorld = voxelWorldObj.GetComponent<VoxelWorld>();
			if (_voxelWorld != null) {
				_voxelWorld.VoxelPlaced += OnVoxelPlaced;
				_voxelWorld.PreVoxelCollisionUpdate += OnPreVoxelCollisionUpdate;
			}
			_voxelRollbackManager = voxelWorldObj.GetComponent<VoxelRollbackManager>();
			if (_voxelRollbackManager != null)
			{
				_voxelRollbackManager.ReplayPreVoxelCollisionUpdate += OnReplayPreVoxelCollisionUpdate;
			}
			var fps = GraphyManager.Instance.AverageFPS;
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

	private void Start()
	{
		_characterController.enabled = true;
	}

	private void OnDestroy() {
		if (_voxelWorld != null) {
			_voxelWorld.VoxelPlaced -= OnVoxelPlaced;
			_voxelWorld.PreVoxelCollisionUpdate -= OnPreVoxelCollisionUpdate;
		}
		if (_voxelRollbackManager != null)
		{
			_voxelRollbackManager.ReplayPreVoxelCollisionUpdate -= OnReplayPreVoxelCollisionUpdate;
		}
	}

	private void LateUpdate()
	{
		//Keep track of this transforms velocity
		// if (RunCore.IsClient())
		// {
		// 	var currentPos = transform.position;
		// 	var velocity = (currentPos - _trackedPosition) * (1 / Time.deltaTime);
		// 	_trackedPosition = currentPos;
		// 	anim.SetVelocity(velocity);
		// }
	}

	public int AddMoveModifier(MoveModifier moveModifier)
	{
		int id = this.moveModifierIdCounter;
		this.moveModifierIdCounter++;

		this._moveModifiers.Add(id, moveModifier);

		return id;
	}

	public void RemoveMoveModifier(int id)
	{
		this._moveModifiers.Remove(id);
	}

	public void ClearMoveModifiers()
	{
		this._moveModifiers.Clear();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		if (IsOwner) {
			_characterCollider.hasModifiableContacts = true;
		}
	}

	public override void OnStartNetwork() {
		base.OnStartNetwork();
		TimeManager.OnTick += OnTick;
		// if (RunCore.IsClient()) {
		// 	TimeManager.OnPostTick += OnPostTick;
		// }
	}

	public override void OnStopNetwork() {
		base.OnStopNetwork();
		if (TimeManager != null) {
			TimeManager.OnTick -= OnTick;
			// if (RunCore.IsClient()) {
			// 	TimeManager.OnPostTick -= OnPostTick;
			// }
		}
	}

	public override void OnOwnershipServer(NetworkConnection prevOwner) {
		base.OnOwnershipServer(prevOwner);
		_serverControlled = Owner == null || !Owner.IsValid;
	}

	private void ExposedState_OnChange(EntityState prev, EntityState next, bool asServer)
	{
		anim.SetState(next);
	}

	private void OnVoxelPlaced(object voxel, object x, object y, object z)
	{
		if (!base.IsOwner) return;

		var voxelPos = new Vector3Int((int)x, (int)y, (int)z);
		
		var t = transform;
		var entityPosition = t.position;
		var voxelCenter = voxelPos + (Vector3.one / 2f);
		
		if (Vector3.Distance(voxelCenter, entityPosition) <= 16f) {
			// TODO: Save chunk collider state
			_voxelRollbackManager.AddChunkSnapshotsNearVoxelPos(TimeManager.LocalTick, Vector3Int.RoundToInt(entityPosition), false);
		}
	}

	private void OnPreVoxelCollisionUpdate(ushort voxel, Vector3Int voxelPos)
	{
		HandlePreVoxelCollisionUpdate(voxel, voxelPos, false);
	}

	private void OnReplayPreVoxelCollisionUpdate(ushort voxel, Vector3Int voxelPos)
	{
		HandlePreVoxelCollisionUpdate(voxel, voxelPos, true);
	}

	private void HandlePreVoxelCollisionUpdate(ushort voxel, Vector3Int voxelPos, bool replay)
	{
		var t = transform;
		var entityPosition = t.position;
		var voxelCenter = voxelPos + (Vector3.one / 2f);

		if (base.IsOwner && base.IsClient && !replay)
		{
			if (Vector3.Distance(voxelCenter, entityPosition) <= 16f) {
				// TODO: Save chunk collider state
				_voxelRollbackManager.AddChunkSnapshotsNearVoxelPos(TimeManager.LocalTick, Vector3Int.RoundToInt(entityPosition), true);
			}
		}

		if (voxel != 0)
		{
			// Check for intersection of entity and the newly-placed voxel:
			var radius = _characterController.radius;
			var height = _characterController.height;
			var entityPos = entityPosition + (t.up * (height / 2));
			// var entityBounds = new Bounds(entityPos, new Vector3(radius, height, radius));
			var voxelBounds = new Bounds(voxelCenter, Vector3.one);

			// If entity intersects with new voxel, bump the entity upwards (by default, the physics will push it to
			// to the side, which is bad for vertical stacking).
			if (_characterCollider.bounds.Intersects(voxelBounds)) {
				// var bumpAmount = voxelBounds.max.y - entityBounds.min.y;
				// var posY = entityPosition.y;
				// _bumpToY = posY + bumpAmount;
				// _entityLerp = new EntityLerp(posY, _bumpToY, InstanceFinder.TimeManager.GetPreciseTick(TickType.Tick), 0.1f);
				//
				// SetNoOverlapRecoveryTemporarily();
				_stepUp = 1.01f;
			}
		}
	}

	// private void OnPostTick() {
	// 	// _voxelRollbackManager.RevertBackToRealTime();
	// }

	private void OnTick() {
		if (IsOwner) {
			Reconcile(default,false);
			BuildActions(out var md);
			MoveReplicate(md, false);
		}

		if (IsServer) {
			var t = transform;
			
			if (_serverControlled) {
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
					Velocity = _velocity,
					SlideVelocity = _slideVelocity,
					PrevMoveFinalizedDir = _prevMoveFinalizedDir,
					EntityState = _state,
					PrevEntityState = _prevState,
					PrevMoveVector = _prevMoveVector,
					PrevSprint = _prevSprint,
					PrevJump = _prevJump,
					PrevMoveDir = _prevMoveDir,
					PrevGrounded = _prevGrounded,
					PrevJumpStartPos = _prevJumpStartPos,
					TimeSinceSlideStart = _timeSinceSlideStart,
					TimeSinceBecameGrounded = _timeSinceBecameGrounded,
					TimeSinceWasGrounded = _timeSinceWasGrounded,
					TimeSinceJump = _timeSinceJump,
					TimeSinceImpulse = _timeSinceImpulse,
					ImpulseVelocity = _impulseVelocity,
					ImpulseDuration = _impulseDuration,
					PrevMoveModifier = _prevMoveModifier,
					PrevLookVector = _prevLookVector,
					// MoveModifiers = _moveModifiers,
					// MoveModifierFromEventHistory = _moveModifierFromEventHistory,
				};
				Reconcile(rd,  true);	
			}
		}

		if (IsClient)
		{
			var currentPos = transform.position;
			var velocity = (currentPos - _trackedPosition) * (1 / (float)InstanceFinder.TimeManager.TickDelta);
			_trackedPosition = currentPos;
			anim.SetVelocity(velocity);
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

		string miss = "";
		if ((rd.SlideVelocity - _slideVelocity).magnitude > 0.1)
		{
			miss += " slideVelocity=" + rd.SlideVelocity;
		}
		bool ignore = false;
		if (!ignore)
		{
			t.position = rd.Position;
			t.rotation = rd.Rotation;
			_velocity = rd.Velocity;
			_slideVelocity = rd.SlideVelocity;
			_prevMoveFinalizedDir = rd.PrevMoveFinalizedDir;
			_state = rd.EntityState;
			_prevState = rd.PrevEntityState;
			_prevMoveVector = rd.PrevMoveVector;
			_prevSprint = rd.PrevSprint;
			_prevJump = rd.PrevJump;
			_prevGrounded = rd.PrevGrounded;
			_prevMoveDir = rd.PrevMoveDir;
			_prevJumpStartPos = rd.PrevJumpStartPos;
			_prevTick = rd.GetTick() - 1;
			_timeSinceSlideStart = rd.TimeSinceSlideStart;
			_timeSinceBecameGrounded = rd.TimeSinceBecameGrounded;
			_timeSinceWasGrounded = rd.TimeSinceWasGrounded;
			_timeSinceJump = rd.TimeSinceJump;
			_timeSinceImpulse = rd.TimeSinceImpulse;
			_impulseDuration = rd.ImpulseDuration;
			_impulseVelocity = rd.ImpulseVelocity;
			_prevMoveModifier = rd.PrevMoveModifier;
			// _moveModifiers = rd.MoveModifiers;
			// _moveModifierFromEventHistory = rd.MoveModifierFromEventHistory;
		}

		if (!asServer && base.IsOwner) {
			_voxelRollbackManager.DiscardSnapshotsBehindTick(rd.GetTick());

			// Clear old move modifier history
			var keys = this._moveModifierFromEventHistory.Keys;
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
				this._moveModifierFromEventHistory.Remove(key);
			}
		}
	}
	
	[Replicate]
	private void MoveReplicate(MoveInputData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false) {
		if (asServer && md.CustomData != null)
		{
			long ticksPassed = (TimeManager.Tick - (long)md.SyncTick);
			if (ticksPassed < 0) ticksPassed = 0;
			dispatchCustomData?.Invoke(TimeManager.Tick, md.CustomData);
		}

		if (asServer)
		{
			_lastReplicateTime = Time.unscaledTime;
		}

		Move(md, asServer, channel, replaying);
	}

	public void UpdateSyncTick()
	{
		_queuedSyncTick = base.TimeManager.Tick;
	}

	[ObserversRpc(RunLocally = true, ExcludeOwner = true)]
	private void ObserverPerformEntityActionExcludeOwner(EntityAction action) {
		PerformEntityAction(action);
	}

	private void PerformEntityAction(EntityAction action) {
		if (action == EntityAction.Jump) {
			anim.StartJump();
		}
	}

	private bool IsSprinting(MoveInputData md) {
		//Only sprint if you are moving forward
		// return md.Sprint && md.MoveInput.y > sprintForwardThreshold;
		return md.Sprint && md.MoveDir.magnitude > 0.1f;
	}

	private Vector3 GetSlideVelocity()
	{
		var flatMoveDir = new Vector3(_prevMoveFinalizedDir.x, 0, _prevMoveFinalizedDir.z).normalized;
		return flatMoveDir * (configuration.sprintSpeed * configuration.slideSpeedMultiplier);
	}

	public bool IsGrounded() {
		return _grounded;
	}

	private (bool isGrounded, ushort blockId, Vector3Int blockPos) CheckIfGrounded() {
		var radius = _characterController.radius;
		var pos = transform.position;
		
		const float tolerance = 0.03f;
		var offset = new Vector3(-0.5f, -0.5f - tolerance, -0.5f);
		
		// Check four corners to see if there's a block beneath player:
		var pos00 = Vector3Int.RoundToInt(pos + offset + new Vector3(-radius, 0, -radius));
		ushort voxel00 = _voxelWorld.ReadVoxelAt(pos00);
		if (
			VoxelWorld.VoxelIsSolid(voxel00) &&
			!VoxelWorld.VoxelIsSolid(_voxelWorld.ReadVoxelAt(pos00 + new Vector3Int(0, 1, 0)))
			)
		{
			return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel00), blockPos: pos00);
		}
		
		var pos10 = Vector3Int.RoundToInt(pos + offset + new Vector3(radius, 0, -radius));
		ushort voxel10 = _voxelWorld.ReadVoxelAt(pos10);
		if (
			VoxelWorld.VoxelIsSolid(voxel10) &&
			!VoxelWorld.VoxelIsSolid(_voxelWorld.ReadVoxelAt(pos10 + new Vector3Int(0, 1, 0)))
		)
		{
			return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel10), pos10);
		}
		
		var pos01 = Vector3Int.RoundToInt(pos + offset + new Vector3(-radius, 0, radius));
		ushort voxel01 = _voxelWorld.ReadVoxelAt(pos01);
		if (
			VoxelWorld.VoxelIsSolid(voxel01) &&
			!VoxelWorld.VoxelIsSolid(_voxelWorld.ReadVoxelAt(pos01 + new Vector3Int(0, 1, 0)))
		)
		{
			return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel01), pos01);
		}
		
		var pos11 = Vector3Int.RoundToInt(pos + offset + new Vector3(radius, 0, radius));
		ushort voxel11 = _voxelWorld.ReadVoxelAt(pos11);
		if (
			VoxelWorld.VoxelIsSolid(voxel11) &&
			!VoxelWorld.VoxelIsSolid(_voxelWorld.ReadVoxelAt(pos11 + new Vector3Int(0, 1, 0)))
		)
		{
			return (isGrounded: true, blockId: VoxelWorld.VoxelDataToBlockId(voxel11), pos11);
		}

		// Fallthrough - do raycast to check for PrefabBlock object below:
		var layerMask = LayerMask.GetMask("Default");
		var halfHeight = _characterController.height / 1.9f;
		var centerPosition = pos + _characterController.center;
		var rotation = transform.rotation;
		var distance = (halfHeight - radius) + 0.1f;
		
		if (Physics.BoxCast(centerPosition, new Vector3(radius, radius, radius), Vector3.down, out var hit, rotation, distance, layerMask)) {
			var isKindaUpwards = Vector3.Dot(hit.normal, Vector3.up) > 0.1f;
			return (isGrounded: isKindaUpwards, blockId: 0, Vector3Int.zero);
		}
		
		return (isGrounded: false, blockId: 0, Vector3Int.zero);
	}

	private void Move(MoveInputData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false)
	{
		var currentTime = TimeManager.TicksToTime(TickType.LocalTick);

		if (!asServer && IsOwner) {
			if (replaying) {
				_voxelRollbackManager.LoadSnapshot(md.GetTick(), Vector3Int.RoundToInt(transform.position));
			} else {
				_voxelRollbackManager.RevertBackToRealTime();
			}
		}

		var isDefaultMoveData = object.Equals(md, default(MoveInputData));

		var isIntersecting = IsIntersectingWithBlock();
		var delta = (float)TimeManager.TickDelta;
		var move = Vector3.zero;
		var (grounded, groundedBlockId, groundedBlockPos) = CheckIfGrounded();
		_grounded = grounded;
		this.groundedBlockId = groundedBlockId;
		this.groundedBlockPos = groundedBlockPos;

		if (isIntersecting)
		{
			grounded = true;
		}

		if (grounded && !_prevGrounded)
		{
			_timeSinceBecameGrounded = 0f;
		} else
		{
			_timeSinceBecameGrounded = Math.Min(_timeSinceBecameGrounded + delta, 100f);
		}

		if (isDefaultMoveData)
        {
	        // Predictions.
	        // This is where we guess the movement of the client.
	        // Note: default move data only happens on the server.
	        // There will never be a replay that happens with default data. Because replays are client only.
	        md.CrouchOrSlide = _prevCrouchOrSlide;
	        md.Sprint = _prevSprint;
	        md.Jump = _prevJump;
	        md.MoveDir = _prevMoveDir;
	        md.LookVector = _prevLookVector;
        }

		// Fall impact
		if (grounded && !_prevGrounded && !replaying) {
			print("impact with ground!");
			this.OnImpactWithGround?.Invoke(_velocity, groundedBlockId);
			if (IsServer)
			{
				ObserverOnImpactWithGround(_velocity, groundedBlockId);
			}
		}

		// ********************* //
		// *** Move Modifier *** //
		// ********************* //
		MoveModifier moveModifier = new MoveModifier()
		{
			speedMultiplier = 1,
		};
		if (!replaying)
		{
			MoveModifier modifierFromEvent = new MoveModifier()
			{
				speedMultiplier = 1,
				blockSprint = false,
				blockJump = false,
			};
			OnAdjustMove?.Invoke(modifierFromEvent);
			_moveModifierFromEventHistory.TryAdd(md.GetTick(), modifierFromEvent);
			moveModifier = modifierFromEvent;
		} else
		{
			if (_moveModifierFromEventHistory.TryGetValue(md.GetTick(), out MoveModifier value))
			{
				moveModifier = value;
			} else
			{
				moveModifier = _prevMoveModifier;
			}
		}

		// todo: mix-in all from _moveModifiers
		if (moveModifier.blockJump)
		{
			md.Jump = false;
		}

		// ********************* //
		// ******* Jump ******** //
		// ********************* //
        var requestJump = md.Jump;
        var didJump = false;
        if (requestJump)
        {
	        var canJump = false;

	        if (grounded)
	        {
		        canJump = true;
	        }
	        // coyote jump
	        else if (_prevMoveVector.y <= 0.02f && _timeSinceWasGrounded <= configuration.jumpCoyoteTime && _velocity.y <= 0)
	        {

	        }

	        // extra cooldown if jumping up blocks
	        if (transform.position.y - _prevJumpStartPos.y > 0.2)
	        {
		        if (_timeSinceBecameGrounded < configuration.jumpCooldown)
		        {
			        canJump = false;
		        }
	        }
	        if (canJump)
	        {
		        // Jump
		        didJump = true;
		        _velocity.y = configuration.jumpSpeed;
		        _prevJumpStartPos = transform.position;

		        if (!replaying) {
			        if (asServer)
			        {
				        ObserverPerformEntityActionExcludeOwner(EntityAction.Jump);
			        } else if (IsOwner)
			        {
				        PerformEntityAction(EntityAction.Jump);
			        }
		        }
	        }
        }

        var isMoving = md.MoveDir.sqrMagnitude > 0.1f;

        /*
         * Determine entity state state.
         * md.State MUST be set in all cases below.
         * We CANNOT read md.State at this point. Only md.PrevState.
         */
        var isJumping = !grounded || didJump;
        var shouldSlide = _prevState is (EntityState.Sprinting or EntityState.Jumping) && _timeSinceSlideStart >= configuration.slideCooldown;

        var tempPrev = _state;
        if (md.CrouchOrSlide && _prevState is not (EntityState.Crouching or EntityState.Sliding) && grounded && shouldSlide)
        {
	        // Slide if already sprinting & last slide wasn't too recent:
	        _state = EntityState.Sliding;
	        _slideVelocity = GetSlideVelocity();
	        _velocity = Vector3.ClampMagnitude(_velocity, configuration.sprintSpeed * 1.1f);
	        _timeSinceSlideStart = 0f;
	        md.DebugStartedSliding = true;
        }
        else if (md.CrouchOrSlide && _prevState == EntityState.Sliding)
        {
	        if (_slideVelocity.magnitude <= configuration.crouchSpeedMultiplier * configuration.speed * 1.1)
	        {
		        _state = EntityState.Crouching;
		        _slideVelocity = Vector3.zero;
	        } else
	        {
		        _state = EntityState.Sliding;
	        }
        } else if (isJumping) {
	        _state = EntityState.Jumping;
        } else if (md.CrouchOrSlide && grounded)
        {
	        _state = EntityState.Crouching;
        } else if (isMoving) {
	        if (IsSprinting(md))
	        {
		        _state = EntityState.Sprinting;
	        } else
	        {
		        _state = EntityState.Running;
	        }
        } else {
	        _state = EntityState.Idle;
        }

        /*
         * Update Time Since:
         */
        if (_state != EntityState.Sliding)
        {
	        _timeSinceSlideStart = Math.Min(_timeSinceSlideStart + delta, 100f);
        }

        if (didJump)
        {
	        _timeSinceJump = 0f;
        } else
        {
	        _timeSinceJump = Math.Min(_timeSinceJump + delta, 100f);
        }

        if (grounded)
        {
	        _timeSinceWasGrounded = 0f;
        } else
        {
	        _timeSinceWasGrounded = Math.Min(_timeSinceWasGrounded + delta, 100f);
        }

        _timeSinceImpulse = Math.Min(_timeSinceImpulse + delta, 100f);

        /*
         * md.State has been set. We can use it now.
         */
        if (_state != EntityState.Sliding) {
	        var norm = md.MoveDir.normalized;
	        move.x = norm.x;
	        move.z = norm.z;
        }

        // Character height:
        switch (_state)
        {
	        case EntityState.Crouching:
		        _characterController.height = _characterControllerHeight * configuration.crouchHeightMultiplier;
		        _characterController.center = _characterControllerCenter + new Vector3(0, -(_characterControllerHeight - _characterController.height) * 0.5f, 0);
		        break;
	        case EntityState.Sliding:
		        _characterController.height = _characterControllerHeight * configuration.slideHeightMultiplier;
		        _characterController.center = _characterControllerCenter + new Vector3(0, -(_characterControllerHeight - _characterController.height) * 0.5f, 0);
		        break;
	        default:
		        _characterController.height = _characterControllerHeight;
		        _characterController.center = _characterControllerCenter;
		        break;
        }

        // Gravity:
        if (!grounded || _velocity.y > 0) {
	        _velocity.y += Physics.gravity.y * delta;
        } else {
	        // _velocity.y = -1f;
	        _velocity.y = 0f;
        }

        // Calculate drag:
        // var dragForce = EntityPhysics.CalculateDrag(_velocity * delta, configuration.airDensity, configuration.drag, _frontalArea);
        var dragForce = Vector3.zero; // Disable drag

        var isImpulsing = _timeSinceImpulse < _impulseDuration && _impulseVelocity.sqrMagnitude > 0;

        // Calculate friction:
        var frictionForce = Vector3.zero;
        if (grounded && !isImpulsing) {
            var flatVelocity = new Vector3(_velocity.x, 0, _velocity.z);
            if (flatVelocity.sqrMagnitude < 1f) {
                _velocity.x = 0f;
                _velocity.z = 0f;
                frictionForce = Vector3.zero;
            } else {
                frictionForce = EntityPhysics.CalculateFriction(_velocity, -Physics.gravity.y, configuration.mass, configuration.friction);
            }
        }
        
        // Apply impulse:
        if (isImpulsing) {
	        var impulseCompletionRatio = Math.Min(_timeSinceImpulse / _impulseDuration, 1);
	        _velocity = Vector3.Lerp(_velocity, _impulseVelocity, impulseCompletionRatio);

	        dragForce = Vector3.zero;
	        frictionForce = Vector3.zero;
        }

        _velocity += Vector3.ClampMagnitude(dragForce + frictionForce, new Vector3(_velocity.x, 0, _velocity.z).magnitude);
        if (!replaying && ((IsOwner && IsClient) || (!IsOwner && IsServer))) {
	        // print($"tick={md.GetTick()} state={_state}, velocity={_velocity}, dragForce={dragForce}, frictionForce={frictionForce} md.Jump={md.Jump} didJump={didJump} grounded={grounded}");
        }

        // Bleed off slide velocity:
        if (_state == EntityState.Sliding && _slideVelocity.sqrMagnitude > 0) {
	        if (grounded)
	        {
		        _slideVelocity = Vector3.Lerp(_slideVelocity, Vector3.zero, Mathf.Min(1f, 4f * delta));
	        }
	        else
	        {
		        _slideVelocity = Vector3.Lerp(_slideVelocity, Vector3.zero, Mathf.Min(1f, 1f * delta));
	        }

	        if (_slideVelocity.sqrMagnitude < 1)
	        {
		        _slideVelocity = Vector3.zero;
	        }
        }

        // Apply speed:
        float speed;
        if (_state is EntityState.Crouching or EntityState.Sliding)
        {
	        speed = configuration.crouchSpeedMultiplier * configuration.speed;
        } else if (IsSprinting(md) && !moveModifier.blockSprint)
        {
	        speed = configuration.sprintSpeed;
        } else
        {
	        speed = configuration.speed;
        }

        move *= speed;
        move *= moveModifier.speedMultiplier;

        if (_timeSinceImpulse <= configuration.impulseMoveDisableTime)
        {
	        // move *= 0.1f;
        }

        // Rotate the character:
        if (!isDefaultMoveData)
        {
	        transform.LookAt(transform.position + new Vector3(md.LookVector.x, 0, md.LookVector.z));
	        if (!replaying)
	        {
		        this.replicatedLookVector = md.LookVector;
	        }
        }

        // Apply velocity to speed:
        move += _velocity;
        if (_state == EntityState.Sliding) {
	        move += _slideVelocity;
        }
        
        if (isIntersecting && _stepUp == 0)
        {
	        // Prevent movement while stuck in block
	        move *= 0;
        }

        var moveWithDelta = move * delta;
        if (_stepUp != 0)
        {
	        moveWithDelta.y += _stepUp;
	        _stepUp = 0;
        }
        _characterController.Move(moveWithDelta);

        // Effects
        if (!replaying)
        {
	        if (_exposedState != _state) {
		        TrySetState(_state);
	        }
        }

        // Update History
        _prevState = _state;
        _prevSprint = md.Sprint;
        _prevJump = md.Jump;
        _prevCrouchOrSlide = md.CrouchOrSlide;
        _prevMoveFinalizedDir = moveWithDelta.normalized;
        _prevMoveDir = md.MoveDir;
        _prevGrounded = grounded;
        _prevTick = md.GetTick();
        _prevMoveModifier = moveModifier;
        _prevLookVector = md.LookVector;

        PostCharacterControllerMove();
	}

	private void BuildActions(out MoveInputData moveData)
	{
		var customData = _queuedCustomData;
		_queuedCustomData = null;

		moveData = new MoveInputData()
		{
			MoveDir = _moveDir,
			Jump = _jump,
			CrouchOrSlide = _crouchOrSlide,
			Sprint = _sprint,
			LookVector = _lookVector,
			SyncTick = _queuedSyncTick,
			CustomData = customData
		};

		if (customData != null) {
			customDataFlushed?.Invoke();
		}
	}

	private IEnumerator ResetOverlapRecovery() {
		yield return new WaitForSeconds(0.4f);
		_resetOverlapRecoveryCo = null;
		_characterController.enableOverlapRecovery = true;
	}

	private void SetNoOverlapRecoveryTemporarily() {
		if (_resetOverlapRecoveryCo != null) {
			StopCoroutine(_resetOverlapRecoveryCo);
			_resetOverlapRecoveryCo = null;
		}
		_resetOverlapRecoveryCo = StartCoroutine(ResetOverlapRecovery());
		_characterController.enableOverlapRecovery = false;
	}
	
	[Server]
	public void Teleport(Vector3 position) {
		RpcTeleport(Owner, position);
	}

	[TargetRpc(RunLocally = true)]
	private void RpcTeleport(NetworkConnection conn, Vector3 pos) {
		_characterController.enabled = false;
		_velocity = Vector3.zero;
		transform.position = pos;
		// ReSharper disable once Unity.InefficientPropertyAccess
		_characterController.enabled = true;
	}

	[Server]
	public void ApplyVelocityOverTime(Vector3 velocity, float duration) {
		ApplyVelocityOverTimeInternal(velocity, duration);
		if (Owner.ClientId != -1) {
			RpcApplyVelocityOverTime(Owner, velocity, duration);
		}
	}

	private void SetVelocityInternal(Vector3 velocity) {
		_velocity = velocity;
		_timeSinceImpulse = 0f;
		_forceReconcile = true;
	}

	private void ApplyVelocityOverTimeInternal(Vector3 impulse, float duration) {
		print($"ApplyImpulseOverTimeInternal. tick={TimeManager.LocalTick}");
		// _velocity = Vector3.zero;
		_impulseVelocity = impulse;
		_impulseDuration = duration;
		_timeSinceImpulse = 0f;
		_forceReconcile = true;
	}
    
	[TargetRpc]
	private void RpcApplyVelocityOverTime(NetworkConnection conn, Vector3 impulse, float duration) {
		ApplyVelocityOverTimeInternal(impulse, duration);
	}

	/**
	 * Called by TS.
	 */
	public void SetMoveInput(Vector3 moveDir, bool jump, bool sprinting, bool crouchOrSlide)
	{
		var dir = this.transform.TransformDirection(moveDir);
		_moveDir = dir;
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
		_queuedCustomData = customData;
	}

	public int GetState() {
		return (int)_exposedState;
	}

	private void TrySetState(EntityState state) {
		if (state != _exposedState)
		{
			_exposedState = state;
			stateChanged?.Invoke((int)state);
		}
	}

	/**
	 * Checks for colliders that intersect with the character.
	 * Returns true if character is colliding with any colliders.
	 */
	public bool IsIntersectingWithBlock()
	{
		float radius = _characterController.radius;
		Vector3 center = transform.TransformPoint(_characterController.center);
		Vector3 delta = (0.5f * _characterController.height - radius) * Vector3.up;
		Vector3 bottom = center - delta;
		Vector3 top = bottom + delta;

		_overlappingCollidersCount = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _overlappingColliders, LayerMask.GetMask("Block"));
 
		for (int i = 0; i < _overlappingCollidersCount; i++) {
			Collider overlappingCollider = _overlappingColliders[i];
 
			if (overlappingCollider.gameObject.isStatic) {
				continue;
			}
			
			_ignoredColliders.Add(overlappingCollider);
			Physics.IgnoreCollision(_characterController, overlappingCollider, true);
		}

		return _overlappingCollidersCount > 0;
	}

	private void PostCharacterControllerMove() {
		for (int i = 0; i < _ignoredColliders.Count; i++) {
			Collider ignoredCollider = _ignoredColliders[i];
			Physics.IgnoreCollision(_characterController, ignoredCollider, false);
		}
 
		_ignoredColliders.Clear();
	}
}
