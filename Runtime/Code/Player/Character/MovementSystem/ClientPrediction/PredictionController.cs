using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

// PredictedCharacterMovement is based off of:
// PredictedRigidbody which stores & indidvidually rewinds history per Rigidbody.
// Instead of syncing position and velocity we sync the movement state and inputs for replays.
// This will be slower as we have to resimulate physics steps. Perhaps a future optimization
// would be to seperate the scene into multiple physics scenes and only resimulate the once the player is in

/// <summary>
/// Base functionality for a client predicted object
/// Has code for storeing history for type T data
/// and handeling generic server client syncronization
/// </summary>
/// <typeparam name="T">The synced data needed for replays</typeparam>
public abstract class PredictionController<T> : NetworkBehaviour where T: PredictionState{

#region INSPECTOR
    // client keeps state history for correction & reconciliation.
    // this needs to be a SortedList because we need to be able to insert inbetween.
    // => RingBuffer: see prediction_ringbuffer_2 branch, but it's slower!
    [Header("State History")]
    public int stateHistoryLimit = 32; // 32 x 50 ms = 1.6 seconds is definitely enough

    public float recordInterval = 0.050f;

    [Tooltip("(Optional) performance optimization where FixedUpdate.RecordState() only inserts state into history if the state actually changed.\nThis is generally a good idea.")]
    public bool onlyRecordChanges = true;

    [Tooltip("(Optional) performance optimization where received state is compared to the LAST recorded state first, before sampling the whole history.\n\nThis can save significant traversal overhead for idle objects with a tiny chance of missing corrections for objects which revisisted the same position in the recent history twice.")]
    public bool compareLastFirst = true;



    //Correcting clint state when out of sync with server
    [Header("Reconciliation")]

    [Tooltip("Optimization. Reduce the number of reconciles by only reconciling every N frames")]
    public int checkReconcileEveryNthFrame = 4;

    [Tooltip("Applying server corrections one frame ahead gives much better results. We don't know why yet, so this is an option for now.")]
    public bool oneFrameAhead = true;

    [Tooltip("Correction threshold in meters. For example, 0.1 means that if the client is off by more than 10cm, it gets corrected.")]
    public double positionCorrectionThreshold = 0.10;


    [Header("Bandwidth")]

    [Tooltip("Reduce sends while velocity==0. Client's objects may slightly move due to gravity/physics, so we still want to send corrections occasionally even if an object is idle on the server the whole time.")]
    public bool reduceSendsWhileIdle = true;

    // Smooth the visual object over time between syncs
    // this only starts at a given velocity and ends when stopped moving.
    // to avoid constant on/off/on effects, it also stays on for a minimum time.
    [Header("Motion Smoothing")]

    [Tooltip("Smoothing via a graphics holder only happens on demand, while moving with a minimum velocity.")]
    public float motionSmoothingVelocityThreshold = 0.1f;
    public float motionSmoothingTimeTolerance = 0.5f;

    [Tooltip("Snap to the server state directly when velocity is < threshold. This is useful to reduce jitter/fighting effects before coming to rest.\nNote this applies position, rotation and velocity(!) so it's still smooth.")]
    public float velocitySnapThreshold = 2; // 0.5 has too much fighting-at-rest, 2 seems ideal.

    [Tooltip("Teleport if we are further than 'multiplier x collider size' behind.")]
    public float teleportDistanceMultiplier = 10;

    [Tooltip("How fast to interpolate to the target position, relative to how far we are away from it.\nHigher value will be more jitter but sharper moves, lower value will be less jitter but a little too smooth / rounded moves.")]
    public float positionInterpolationSpeed = 15; // 10 is a little too low for billiards at least
    public float rotationInterpolationSpeed = 10;
#endregion


#region PRIVATE

    //State History
    private readonly SortedList<double, T> stateHistory = new SortedList<double, T>();
    // manually store last recorded so we can easily check against this
    // without traversing the SortedList.
    T lastRecorded;
    double lastRecordTime;

    //Reconcile
    private bool wasMovingLastReconcile = false;

    //Mothion Smoothing
    private float motionSmoothingVelocityThresholdSqr; // ² cached in Awake
    private double motionSmoothingLastMovedTime;
    private float smoothFollowThreshold; // caching to avoid calculation in LateUpdate
    private float smoothFollowThresholdSqr; // caching to avoid calculation in LateUpdate
    private double positionCorrectionThresholdSqr; // ² cached in Awake
    
#endregion


#region EVENTS
    private Action OnBeginPrediction;
    private Action OnEndPrediction;

#endregion


#region ABSTRACT

    protected abstract Vector3 currentPosition {get; set;}
    protected abstract Vector3 currentVelocity {get; set;}
    protected abstract void MoveTo(Vector3 newPosition, Vector3 newVelocity);
    protected abstract bool CanReceiveServerState(T serverState);
    protected abstract bool NeedsCorrection(T serverState, T interpolatedState);
    protected abstract T CreateCurrentState();

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual bool IsMoving() =>
        currentVelocity.sqrMagnitude >= motionSmoothingVelocityThresholdSqr;
#endregion

#region INIT
    protected virtual void Awake() {
        // cache some threshold to avoid calculating them in LateUpdate
        float colliderSize = GetComponentInChildren<Collider>().bounds.size.magnitude;
        smoothFollowThreshold = colliderSize * teleportDistanceMultiplier;
        smoothFollowThresholdSqr = smoothFollowThreshold * smoothFollowThreshold;

        // cache ² computations
        motionSmoothingVelocityThresholdSqr = motionSmoothingVelocityThreshold * motionSmoothingVelocityThreshold;
        positionCorrectionThresholdSqr = positionCorrectionThreshold * positionCorrectionThreshold;
    }

    protected virtual void Update() {
        if (isServer) UpdateServer();
        if (isClientOnly) UpdateClient();
    }

    protected override void OnValidate() {
        base.OnValidate();

        // force syncDirection to be ServerToClient
        syncDirection = SyncDirection.ServerToClient;

        // state should be synced immediately for now.
        // later when we have prediction fully dialed in,
        // then we can maybe relax this a bit.
        syncInterval = 0;
    }
#endregion

#region SERVER
        private void UpdateServer()
        {
            // bandwidth optimization while idle.
            if (reduceSendsWhileIdle) {
                // while moving, always sync every frame for immediate corrections.
                // while idle, only sync once per second.
                //
                // we still need to sync occasionally because objects on client
                // may still slide or move slightly due to gravity, physics etc.
                // and those still need to get corrected if not moving on server.
                //
                // TODO
                // next round of optimizations: if client received nothing for 1s,
                // force correct to last received state. then server doesn't need
                // to send once per second anymore.
                syncInterval = IsMoving() ? 0 : 1;
            }

            // always set dirty to always serialize in next sync interval.
            SetDirty();
        }
#endregion
#region CLIENT
        private void UpdateClient(){
            // perf: enough to check reconcile every few frames.
            // PredictionBenchmark: only checking every 4th frame: 770 => 800 FPS
            if (Time.frameCount % checkReconcileEveryNthFrame != 0) return;

            bool moving = IsMoving();

            // started moving?
            if (moving && !wasMovingLastReconcile) {
                OnBeginPrediction?.Invoke();
                wasMovingLastReconcile = true;
            }
            // stopped moving?
            else if (!moving && wasMovingLastReconcile) {
                // ensure a minimum time since starting to move, to avoid on/off/on effects.
                if (NetworkTime.time >= motionSmoothingLastMovedTime + motionSmoothingTimeTolerance) {
                    OnEndPrediction?.Invoke();
                    wasMovingLastReconcile = false;
                }
            }
        }

        void LateUpdate(){
            // only follow on client-only, not in server or host mode
            if (isClientOnly) SmoothFollowPhysicsCopy();
        }
        
        void FixedUpdate() {
            // on clients (not host) we record the current state every FixedUpdate.
            // this is cheap, and allows us to keep a dense history.
            if (!isClientOnly) return;

            // OPTIMIZATION: RecordState() is expensive because it inserts into a SortedList.
            // only record if state actually changed!
            // risks not having up to date states when correcting,
            // but it doesn't matter since we'll always compare with the 'newest' anyway.
            //
            // we check in here instead of in RecordState() because RecordState() should definitely record if we call it!
            if (onlyRecordChanges) {
                // TODO maybe don't reuse the correction thresholds?
                if ((lastRecorded.position - currentPosition).sqrMagnitude < positionCorrectionThresholdSqr) {
                    // Debug.Log($"FixedUpdate for {name}: taking optimized early return instead of recording state.");
                    return;
                }
            }

            // instead of recording every fixedupdate, let's record in an interval.
            // we don't want to record every tiny move and correct too hard.
            if (NetworkTime.time < lastRecordTime + recordInterval) return;

            RecordState();
        }
#endregion



#region PREDICTION
    //If we are in a new state then insert the current state into the history at this specific time
    void RecordState() {
        lastRecordTime = NetworkTime.time;

        // NetworkTime.time is always behind by bufferTime.
        // prediction aims to be on the exact same server time (immediately).
        // use predictedTime to record state, otherwise we would record in the past.
        double predictedTime = NetworkTime.predictedTime;

        // FixedUpdate may run twice in the same frame / NetworkTime.time.
        // for now, simply don't record if already recorded there.
        if (predictedTime == lastRecorded.timestamp) return;

        // keep state history within limit
        if (stateHistory.Count >= stateHistoryLimit)
            stateHistory.RemoveAt(0);

        var newState = CreateCurrentState();
        // add state to history
        stateHistory.Add(predictedTime, newState);

        // manually remember last inserted state for faster .Last comparisons
        lastRecorded = newState;
    }

    //Update our rigidbody to match a new snapshot state
    void ApplyState(T snapshotState){
        // Hard snap to the position below a threshold velocity.
        // this is fine because the visual object still smoothly interpolates to it.
        if (currentVelocity.magnitude <= velocitySnapThreshold) {
            // Debug.Log($"Prediction: snapped {name} into place because velocity {predictedRigidbody.velocity.magnitude:F3} <= {snapThreshold:F3}");

            // apply server state immediately.
            // important to apply velocity as well, instead of Vector3.zero.
            // in case an object is still slightly moving, we don't want it
            // to stop and start moving again on client - slide as well here.
            currentPosition = snapshotState.position;
            currentVelocity = snapshotState.velocity;

            // clear history and insert the exact state we just applied.
            // this makes future corrections more accurate.
            stateHistory.Clear();
            stateHistory.Add(snapshotState.timestamp, snapshotState);
            return;
        }

        //Smoothly move towards the new state
        MoveTo(snapshotState.position, snapshotState.velocity);
    }
#endregion

#region STATES 

    // process a received server state.
    // compares it against our history and applies corrections if needed.
    protected void OnReceivedState(double timestamp, T serverState) {

        // correction requires at least 2 existing states for 'before' and 'after'.
        // if we don't have two yet, drop this state and try again next time once we recorded more.
        if (stateHistory.Count < 2) return;
        

        // DO NOT SYNC SLEEPING! this cuts benchmark performance in half(!!!)
        // color code remote sleeping objects to debug objects coming to rest
        // if (showRemoteSleeping)
        // {
        //     rend.material.color = sleeping ? Color.gray : originalColor;
        // }

        // OPTIONAL performance optimization when comparing idle objects.
        // even idle objects will have a history of ~32 entries.
        // sampling & traversing through them is unnecessarily costly.
        // instead, compare directly against the current rigidbody position!
        // => this is technically not 100% correct if an object runs in
        //    circles where it may revisit the same position twice.
        // => but practically, objects that didn't move will have their
        //    whole history look like the last inserted state.
        // => comparing against that is free and gives us a significant
        //    performance saving vs. a tiny chance of incorrect results due
        //    to objects running in circles.
        // => the RecordState() call below is expensive too, so we want to
        //    do this before even recording the latest state. the only way
        //    to do this (in case last recorded state is too old), is to
        //    compare against live rigidbody.position without any recording.
        //    this is as fast as it gets for skipping idle objects.
        //
        // if this ever causes issues, feel free to disable it.
        if (Vector3.SqrMagnitude(serverState.position - currentPosition) < positionCorrectionThresholdSqr) {
            // Debug.Log($"OnReceivedState for {name}: taking optimized early return!");
            return;
        }

        //Does the subclass think we can recieve a new server state?
        if(!CanReceiveServerState(serverState)){
            return;
        }

        // we only capture state every 'interval' milliseconds.
        // so the newest entry in 'history' may be up to 'interval' behind 'now'.
        // if there's no latency, we may receive a server state for 'now'.
        // sampling would fail, if we haven't recorded anything in a while.
        // to solve this, always record the current state when receiving a server state.
        RecordState();

        T oldest = stateHistory.Values[0];
        T newest = stateHistory.Values[stateHistory.Count - 1];

        // edge case: is the state older than the oldest state in history?
        // this can happen if the client gets so far behind the server
        // that it doesn't have a recored history to sample from.
        // in that case, we should hard correct the client.
        // otherwise it could be out of sync as long as it's too far behind.
        if (serverState.timestamp < oldest.timestamp) {
            // when starting, client may only have 2-3 states in history.
            // it's expected that server states would be behind those 2-3.
            // only show a warning if it's behind the full history limit!
            if (stateHistory.Count >= stateHistoryLimit)
                Debug.LogWarning($"Hard correcting client object {name} because the client is too far behind the server. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This would cause the client to be out of sync as long as it's behind.");

            // force apply the state
            ApplyState(serverState);
            return;
        }

        // edge case: is it newer than the newest state in history?
        // this can happen if client's predictedTime predicts too far ahead of the server.
        // in that case, log a warning for now but still apply the correction.
        // otherwise it could be out of sync as long as it's too far ahead.
        //
        // for example, when running prediction on the same machine with near zero latency.
        // when applying corrections here, this looks just fine on the local machine.
        if (newest.timestamp < serverState.timestamp) {
            // the correction is for a state in the future.
            // we clamp it to 'now'.
            // TODO maybe we should interpolate this back to 'now'?
            // this can happen a lot when latency is ~0. logging all the time allocates too much and is too slow.
            // double ahead = state.timestamp - newest.timestamp;
            // Debug.Log($"Hard correction because the client is ahead of the server by {(ahead*1000):F1}ms. History of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This can happen when latency is near zero, and is fine unless it shows jitter.");
            ApplyState(serverState);
            return;
        }

        // find the two closest client states between timestamp
        if (!Sample(stateHistory, timestamp, out T before, out T after, out int afterIndex, out double t))
        {
            // something went very wrong. sampling should've worked.
            // hard correct to recover the error.
            Debug.LogError($"Failed to sample history of size={stateHistory.Count} @ t={timestamp:F3} oldest={oldest.timestamp:F3} newest={newest.timestamp:F3}. This should never happen because the timestamp is within history.");
            ApplyState(serverState);
            return;
        }

        // interpolate between them to get the best approximation
        T interpolatedState = (T)before.Interpolate(after, (float)t);
        // Debug.Log($"CORRECTION NEEDED FOR {name} @ {timestamp:F3}: client={interpolated.position} server={state.position} difference={difference:F3}");

        // too far off? then correct it
        if (Vector3.SqrMagnitude(serverState.position - interpolatedState.position) >= positionCorrectionThresholdSqr
                || NeedsCorrection(serverState, interpolatedState)) {
            // show the received correction position + velocity for debugging.
            // helps to compare with the interpolated/applied correction locally.
            //Debug.DrawLine(state.position, state.position + state.velocity * 0.1f, Color.white, lineTime);

            // insert the correction and correct the history on top of it.
            // returns the final recomputed state after rewinding.
            T recomputed = CorrectHistory(stateHistory, stateHistoryLimit, serverState, before, after, afterIndex);

            // log, draw & apply the final position.
            // always do this here, not when iterating above, in case we aren't iterating.
            // for example, on same machine with near zero latency.
            // int correctedAmount = stateHistory.Count - afterIndex;
            // Debug.Log($"Correcting {name}: {correctedAmount} / {stateHistory.Count} states to final position from: {rb.position} to: {last.position}");
            //Debug.DrawLine(physicsCopyRigidbody.position, recomputed.position, Color.green, lineTime);
            ApplyState(recomputed);

            // user callback
            OnCorrected();
        }
    }
#endregion


#region HISTORY
        // get the two states closest to a given timestamp.
        // those can be used to interpolate the exact state at that time.
        // => RingBuffer: see prediction_ringbuffer_2 branch, but it's slower!
        public bool Sample<T>(
            SortedList<double, T> history,
            double timestamp, // current server time
            out T before,
            out T after,
            out int afterIndex,
            out double t)     // interpolation factor
        {
            before = default;
            after  = default;
            t = 0;
            afterIndex = -1;

            // can't sample an empty history
            // interpolation needs at least two entries.
            //   can't Lerp(A, A, 1.5). dist(A, A) * 1.5 is always 0.
            if (history.Count < 2) {
                return false;
            }

            // older than oldest
            if (timestamp < history.Keys[0]) {
                return false;
            }

            // iterate through the history
            // TODO this needs to be faster than O(N)
            //      search around that area.
            //      should be O(1) most of the time, unless sampling was off.
            int index = 0; // manually count when iterating. easier than for-int loop.
            KeyValuePair<double, T> prev = new KeyValuePair<double, T>();

            // SortedList foreach iteration allocates a LOT. use for-int instead.
            // foreach (KeyValuePair<double, T> entry in history) {
            for (int i = 0; i < history.Count; ++i)
            {
                double key = history.Keys[i];
                T value = history.Values[i];

                // exact match?
                if (timestamp == key)
                {
                    before = value;
                    after = value;
                    afterIndex = index;
                    t = Mathd.InverseLerp(key, key, timestamp);
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (key > timestamp)
                {
                    before = prev.Value;
                    after = value;
                    afterIndex = index;
                    t = Mathd.InverseLerp(prev.Key, key, timestamp);
                    return true;
                }

                // remember the last
                prev = new KeyValuePair<double, T>(key, value);
                index += 1;
            }

            return false;
        }

        // inserts a server state into the client's history.
        // readjust the deltas of the states after the inserted one.
        // returns the corrected final position.
        // => RingBuffer: see prediction_ringbuffer_2 branch, but it's slower!
        public static T CorrectHistory<T>(
            SortedList<double, T> history,
            int stateHistoryLimit,
            T corrected,     // corrected state with timestamp
            T before,        // state in history before the correction
            T after,         // state in history after the correction
            int afterIndex)  // index of the 'after' value so we don't need to find it again here
            where T: PredictedState
        {
            // respect the limit
            // TODO unit test to check if it respects max size
            if (history.Count >= stateHistoryLimit)
            {
                history.RemoveAt(0);
                afterIndex -= 1; // we removed the first value so all indices are off by one now
            }

            // PERFORMANCE OPTIMIZATION: avoid O(N) insertion, only readjust all values after.
            // the end result is the same since after.delta and after.position are both recalculated.
            // it's technically not correct if we were to reconstruct final position from 0..after..end but
            // we never do, we only ever iterate from after..end!
            //
            //   insert the corrected state into the history, or overwrite if already exists
            //   SortedList insertions are O(N)!
            //     history[corrected.timestamp] = corrected;
            //     afterIndex += 1; // we inserted the corrected value before the previous index

            // the entry behind the inserted one still has the delta from (before, after).
            // we need to correct it to (corrected, after).
            //
            // for example:
            //   before:    (t=1.0, delta=10, position=10)
            //   after:     (t=3.0, delta=20, position=30)
            //
            // then we insert:
            //   corrected: (t=2.5, delta=__, position=25)
            //
            // previous delta was from t=1.0 to t=3.0 => 2.0
            // inserted delta is from t=2.5 to t=3.0 => 0.5
            // multiplier is 0.5 / 2.0 = 0.25
            // multiply 'after.delta(20)' by 0.25 to get the new 'after.delta(5)
            //
            // so the new history is:
            //   before:    (t=1.0, delta=10, position=10)
            //   corrected: (t=2.5, delta=__, position=25)
            //   after:     (t=3.0, delta= 5, position=__)
            //
            // so when we apply the correction, the new after.position would be:
            //   corrected.position(25) + after.delta(5) = 30
            //
            double previousDeltaTime = after.timestamp - before.timestamp;     // 3.0 - 1.0 = 2.0
            double correctedDeltaTime = after.timestamp - corrected.timestamp; // 3.0 - 2.5 = 0.5

            // fix multiplier becoming NaN if previousDeltaTime is 0:
            // double multiplier = correctedDeltaTime / previousDeltaTime;
            double multiplier = previousDeltaTime != 0 ? correctedDeltaTime / previousDeltaTime : 0; // 0.5 / 2.0 = 0.25

            // recalculate 'after.delta' with the multiplier
            after.positionDelta        = Vector3.Lerp(Vector3.zero, after.positionDelta, (float)multiplier);
            after.velocityDelta        = Vector3.Lerp(Vector3.zero, after.velocityDelta, (float)multiplier);
            after.angularVelocityDelta = Vector3.Lerp(Vector3.zero, after.angularVelocityDelta, (float)multiplier);
            // Quaternions always need to be normalized in order to be a valid rotation after operations
            after.rotationDelta        = Quaternion.Slerp(Quaternion.identity, after.rotationDelta, (float)multiplier).normalized;

            // changes aren't saved until we overwrite them in the history
            history[after.timestamp] = after;

            // second step: readjust all absolute values by rewinding client's delta moves on top of it.
            T last = corrected;
            for (int i = afterIndex; i < history.Count; ++i)
            {
                double key = history.Keys[i];
                T value = history.Values[i];

                // correct absolute position based on last + delta.
                value.position        = last.position + value.positionDelta;
                value.velocity        = last.velocity + value.velocityDelta;
                value.angularVelocity = last.angularVelocity + value.angularVelocityDelta;
                // Quaternions always need to be normalized in order to be a valid rotation after operations
                value.rotation        = (value.rotationDelta * last.rotation).normalized; // quaternions add delta by multiplying in this order

                // save the corrected entry into history.
                history[key] = value;

                // save last
                last = value;
            }

            // third step: return the final recomputed state.
            return last;
        }
#endregion

}