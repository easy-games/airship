using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Managing.Timing;
using FishNet.Transporting;
using GameKit.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FishNet.Component.ColliderRollback
{
    public class RollbackManager : MonoBehaviour
    {
        #region Types.
        [System.Serializable, System.Flags] //Remove on 2024/01/01, replace with PhysicsType that is not part of RollbackManager.
        public enum PhysicsType : byte
        {
            TwoDimensional = 1,
            ThreeDimensional = 2,
            Both = 4
        }
        #endregion

        #region Internal.
        /// <summary>
        /// Cached value for bounding box layermask.
        /// </summary>
        internal int? BoundingBoxLayerNumber
        {
            get
            {
                if (_boundingBoxLayerNumber == null)
                {
                    for (int i = 0; i < 32; i++)
                    {
                        if ((1 << i) == BoundingBoxLayer.value)
                        {
                            _boundingBoxLayerNumber = i;
                            break;
                        }
                    }
                }

                return _boundingBoxLayerNumber;
            }
        }
        private int? _boundingBoxLayerNumber;
        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Layer to use when creating and checking against bounding boxes. This should be different from any layer used.")]
        [SerializeField]
        private LayerMask _boundingBoxLayer = 0;
        /// <summary>
        /// Layer to use when creating and checking against bounding boxes. This should be different from any layer used.
        /// </summary>
        internal LayerMask BoundingBoxLayer => _boundingBoxLayer;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Maximum time in the past colliders can be rolled back to.")]
        [SerializeField]
        private float _maximumRollbackTime = 1.25f;
        /// <summary>
        /// Maximum time in the past colliders can be rolled back to.
        /// </summary>
        internal float MaximumRollbackTime => _maximumRollbackTime;
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("Interpolation value for the NetworkTransforms or objects being rolled back.")]
        [Range(0, 250)]
        [SerializeField]
        internal ushort Interpolation = 2;
        #endregion

        //PROSTART
        #region Private Pro.
        /// <summary>
        /// Physics used when rolling back.
        /// </summary>
        private RollbackPhysicsType _rollbackPhysics;
        /// <summary>
        /// NetworkManager on the same object as this script.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// All active ColliderRollback scripts.
        /// </summary>
        private List<ColliderRollback> _allRollbacks = new List<ColliderRollback>();
        /// <summary>
        /// Cache for raycast non-alloc hits.
        /// </summary>
        RaycastHit[] _hitsCache = new RaycastHit[50];
        /// <summary>
        /// Cache for raycast2d non-alloc hits.
        /// </summary>
        RaycastHit2D[] _hitsCache2d = new RaycastHit2D[50];
        #endregion
        //PROEND		

        //PROSTART        
        private void TimeManager_OnPostTick()
        {
            CreateSnapshots();
        }
        //PROEND

        /// <summary>
        /// Initializes this script for use.
        /// </summary>
        /// <param name="manager"></param>
        internal void InitializeOnce_Internal(NetworkManager manager)
        {
            //PROSTART
            _networkManager = manager;
            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            //PROEND
        }

        //PROSTART
        private bool IsBoundingBoxLayerSet(bool warn)
        {
            bool set = (BoundingBoxLayerNumber != null);
            if (!set && warn)
                _networkManager?.LogWarning($"RollbackManager BoundingBoxLayer is unset or mixed. Bounding box rollbacks will not function. This value must be changed outside of play mode.");

            return set;
        }
        //PROEND


        //PROSTART
        /// <summary>
        /// Called when server connection state changes.
        /// </summary>
        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            //Listen just before ticks.
            if (obj.ConnectionState == LocalConnectionState.Started)
            {
                //If the server invoking this event is the only one started subscribe.
                if (_networkManager.ServerManager.OneServerStarted())
                    _networkManager.TimeManager.OnPostTick += TimeManager_OnPostTick;
            }
            else
            {
                //If no servers are started then unsubscribe.
                if (!_networkManager.ServerManager.AnyServerStarted())
                    _networkManager.TimeManager.OnPostTick -= TimeManager_OnPostTick;
            }
        }

        /// <summary>
        /// Registers a ColliderRollback.
        /// </summary>
        /// <param name="cr"></param>
        internal void RegisterColliderRollback(ColliderRollback cr)
        {
            _allRollbacks.Add(cr);
        }

        /// <summary>
        /// Unregisters a ColliderRollback.
        /// </summary>
        /// <param name="oldSceneHandle">If not 0 then ColliderRollback will be removed from scene rollbacks using the sceneHandle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnregisterColliderRollback(ColliderRollback cr)
        {
            _allRollbacks.Remove(cr);
        }

        /// <summary>
        /// Creates snapshots for colliders.
        /// </summary>
        private void CreateSnapshots()
        {
            List<ColliderRollback> lst = _allRollbacks;
            int count = lst.Count;
            for (int i = 0; i < count; i++)
                lst[i].CreateSnapshot();
        }
        //PROEND


        /// <summary>
        /// Rolls back all colliders.
        /// </summary>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        [Obsolete("Use Rollback(PreciseTick, RollbackPhysicsType, bool)")] //Remove on 2024/01/01.
        public void Rollback(PreciseTick pt, PhysicsType physicsType, bool asOwner = false)
        {
            //PROSTART
            if (physicsType == PhysicsType.Both)
            {
                _networkManager.LogError($"PhysicsType of Both cannot be used. This has been removed for performance reasons. Rollback and return the physics type as you need to perform casts.");
                return;
            }
            else if (physicsType == PhysicsType.ThreeDimensional)
            {
                Rollback(0, pt, RollbackPhysicsType.Physics, asOwner);
            }
            else if (physicsType == PhysicsType.TwoDimensional)
            {
                Rollback(0, pt, RollbackPhysicsType.Physics2D, asOwner);
            }
            //PROEND
        }


        /// <summary>
        /// Rolls back all colliders.
        /// </summary>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(PreciseTick pt, RollbackPhysicsType physicsType, bool asOwner = false)
        {
            //PROSTART
            Rollback(0, pt, physicsType, asOwner);
            //PROEND
        }


        /// <summary>
        /// Rolls back all colliders.
        /// </summary>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rollback(Scene scene, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwner = false)
        {
            //PROSTART
            Rollback(scene.handle, pt, physicsType, asOwner);
            //PROEND
        }
        /// <summary>
        /// Rolls back all colliders.
        /// </summary>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="physicsType">Type of physics to rollback; this is often what your casts will use.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(int sceneHandle, PreciseTick pt, RollbackPhysicsType physicsType, bool asOwner = false)
        {
            //PROSTART
            float time = GetRollbackTime(pt, asOwner);
            List<ColliderRollback> lst = _allRollbacks;

            //If a handle is specified perform a different loop.
            if (sceneHandle != 0)
            {
                foreach (ColliderRollback item in lst)
                {
                    if (item.gameObject.scene.handle != sceneHandle)
                        continue;
                    item.Rollback(time);
                }
            }
            else
            {
                foreach (ColliderRollback item in lst)
                    item.Rollback(time);
            }

            _rollbackPhysics = physicsType;
            SyncTransforms(physicsType);
            //PROEND
        }


        /// <summary>
        /// Rolls back all 3d colliders hit by a test cast against bounding boxes.
        /// </summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="normalizedDirection">Direction to cast.</param>
        /// <param name="distance">Distance of cast.</param>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwner = false)
        {
            //PROSTART
            Rollback(0, origin, normalizedDirection, distance, pt, asOwner);
            //PROEND
        }

        /// <summary>
        /// Rolls back all 3d colliders hit by a test cast against bounding boxes.
        /// </summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="normalizedDirection">Direction to cast.</param>
        /// <param name="distance">Distance of cast.</param>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Rollback(Scene scene, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwner = false)
        {
            //PROSTART
            Rollback(scene.handle, origin, normalizedDirection, distance, pt, asOwner);
            //PROEND
        }
        /// <summary>
        /// Rolls back all 3d colliders hit by a test cast against bounding boxes.
        /// </summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="normalizedDirection">Direction to cast.</param>
        /// <param name="distance">Distance of cast.</param>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(int sceneHandle, Vector3 origin, Vector3 normalizedDirection, float distance, PreciseTick pt, bool asOwner = false)
        {
            //PROSTART
            if (!IsBoundingBoxLayerSet(true))
                return;

            float time = GetRollbackTime(pt, asOwner);

            int hitCount = Physics.RaycastNonAlloc(origin, normalizedDirection, _hitsCache, distance, _boundingBoxLayer);

            //If a handle is specified perform a different loop.
            if (sceneHandle != 0)
            {
                for (int i = 0; i < hitCount; i++)
                {
                    if (_hitsCache[i].transform.gameObject.scene.handle != sceneHandle)
                        continue;
                    if (_hitsCache[i].transform.TryGetComponent<ColliderRollback>(out ColliderRollback cr))
                        cr.Rollback(time);
                }

            }
            else
            {
                for (int i = 0; i < hitCount; i++)
                {
                    if (_hitsCache[i].transform.TryGetComponent<ColliderRollback>(out ColliderRollback cr))
                        cr.Rollback(time);
                }
            }

            //If maxed hits resize cache.
            if (hitCount == _hitsCache.Length)
                Array.Resize(ref _hitsCache, hitCount * 3);

            RollbackPhysicsType physicsType = RollbackPhysicsType.Physics;
            _rollbackPhysics |= physicsType;
            SyncTransforms(physicsType);
            //PROEND
        }

        /// <summary>
        /// Rolls back all 3d colliders hit by a test cast against bounding boxes.
        /// </summary>
        /// <param name="origin">Ray origin.</param>
        /// <param name="normalizedDirection">Direction to cast.</param>
        /// <param name="distance">Distance of cast.</param>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="asOwner">True if IsOwner of the object the raycast is for. This can be ignored and only provides more accurate results for clientHost.</param>
        public void Rollback(Vector2 origin, Vector2 normalizedDirection, float distance, PreciseTick pt, bool asOwner = false)
        {
            //PROSTART
            if (!IsBoundingBoxLayerSet(true))
                return;

            float time = GetRollbackTime(pt, asOwner);
            int hitCount = Physics2D.RaycastNonAlloc(origin, normalizedDirection, _hitsCache2d, distance, BoundingBoxLayer);
            for (int i = 0; i < hitCount; i++)
            {
                if (_hitsCache2d[i].transform.TryGetComponent<ColliderRollback>(out ColliderRollback cr))
                    cr.Rollback(time);
            }

            //If maxed hits resize cache.
            if (hitCount == _hitsCache2d.Length)
                Array.Resize(ref _hitsCache2d, hitCount * 3);

            RollbackPhysicsType physicsType = RollbackPhysicsType.Physics2D;
            _rollbackPhysics |= physicsType;
            SyncTransforms(physicsType);
            //PROEND
        }

        /// <summary>
        /// Returns all ColliderRollback objects back to their original position.
        /// </summary>
        public void Return()
        {
            //PROSTART
            List<ColliderRollback> lst = _allRollbacks;
            int count = lst.Count;
            for (int i = 0; i < count; i++)
                lst[i].Return();

            SyncTransforms(_rollbackPhysics);
            //PROEND
        }

        //PROSTART
        /// <summary>
        /// Calculates rollback time based on a precise tick.
        /// </summary>
        /// <param name="pt">Precise tick received from the client.</param>
        /// <param name="asOwner">True if IsOwner of the object. This can be ignored and only provides more accurate results for clientHost.</param>
        private float GetRollbackTime(PreciseTick pt, bool asOwner = false)
        {
            if (_networkManager == null)
                return 0.0f;

            TimeManager timeManager = _networkManager.TimeManager;
            //How much time to rollback.
            float time = 0f;
            float tickDelta = (float)timeManager.TickDelta;
            //Rolling back not as host.
            if (!asOwner)
            {
                ulong pastTicks = (timeManager.Tick - pt.Tick) + Interpolation;
                if (pastTicks >= 0)
                {
                    //They should never get this high, ever. This is to prevent overflows.
                    if (pastTicks > ushort.MaxValue)
                        pastTicks = ushort.MaxValue;

                    //Add past ticks time.
                    time = (pastTicks * tickDelta);

                    //More protection.
                    if (pt.Percent > 100)
                        pt.Percent = 100;
                    else if (pt.Percent < 0)
                        pt.Percent = 0;

                    int percentWhole;
                    float percent;
                    //Add client percent time.
                    percentWhole = (sbyte)(100 - pt.Percent);
                    percent = Mathf.Max(0f, percentWhole / 100f);
                    time += (percent * tickDelta);
                    time -= tickDelta;
                }
            }
            //Rolling back as owner (client host firing).
            else
            {
                ulong pastTicks = (timeManager.Tick - pt.Tick);
                if (pastTicks >= 0)
                {
                    time = (pastTicks * tickDelta * 0.5f);
                    float percent = (float)(1f - (timeManager.GetTickPercent() / 100f));
                    time -= (percent * tickDelta);
                }
            }
            return time;
        }

        /// <summary>
        /// Applies transforms for the specified physics type.
        /// </summary>
        /// <param name="physicsType"></param>
        private void SyncTransforms(RollbackPhysicsType physicsType)
        {
            if (physicsType ==  RollbackPhysicsType.Physics)
                Physics.SyncTransforms();
            else if (physicsType == RollbackPhysicsType.Physics2D)
                Physics2D.SyncTransforms();
        }
        //PROEND
    }

}