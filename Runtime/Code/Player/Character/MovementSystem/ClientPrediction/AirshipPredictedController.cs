using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror;
using Unity.Mathematics;
using UnityEngine;

// PredictedController is based off of:
// PredictedRigidbody which stores & indidvidually rewinds history per Rigidbody.
// Keeping this layer abstract so child classes can impliment the details of the states

/// <summary>
/// Base functionality for a client predicted object
/// Has code for storeing history for type T data
/// and handeling generic server client syncronization
/// </summary>
/// <typeparam name="T">The data type stored in the history that is needed for replays</typeparam>
[RequireComponent(typeof(NetworkIdentity))]
public abstract class AirshipPredictedController<T> : NetworkBehaviour, IPredictedReplay where T: AirshipPredictedState{

#region INSPECTOR
    // client keeps state history for correction & reconciliation.
    // this needs to be a SortedList because we need to be able to insert inbetween.
    // => RingBuffer: see prediction_ringbuffer_2 branch, but it's slower!
    [Header("State History")]
    public int stateHistoryLimit = 32; // 32 x 50 ms = 1.6 seconds is definitely enough

    [Tooltip("How many seconds between each call to record the history state of the object.")]
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
    [Tooltip("How much can velocity be off before being corrected. In meters per second")]
    public float velocityCorrectionThreshold = 0.5f;

    [Tooltip("Snap to the server state directly when velocity is < threshold. This is useful to reduce jitter/fighting effects before coming to rest.\nNote this applies position, rotation and velocity(!) so it's still smooth.")]
    public float velocitySnapThreshold = 2; // 0.5 has too much fighting-at-rest, 2 seems ideal.


    [Header("Bandwidth")]

    [Tooltip("Reduce sends while velocity==0. Client's objects may slightly move due to gravity/physics, so we still want to send corrections occasionally even if an object is idle on the server the whole time.")]
    public bool reduceSendsWhileIdle = true;
    

    [Header("Debugging")]
    [Tooltip("Draw gizmos. Shows server position and client position")]
    public bool showLogs = false;

    [Tooltip("Draw gizmos. Shows server position and client position")]
    public bool showGizmos = false;
    public float gizmoDuration = 1;

    [Tooltip("Physics components are moved onto a ghost object beyond this threshold. Main object visually interpolates to it.")]
    public float gizmoVelocityThreshold = 0.1f;

    [Tooltip("Performance optimization: only draw gizmos at an interval.")]
    public int drawGizmosEveryNthFrame = 4;
    public Color serverColor = Color.red;
    public Color clientColor = Color.blue;
#endregion


#region PRIVATE

    //State History
    protected readonly SortedList<double, T> stateHistory = new SortedList<double, T>();
    // manually store last recorded so we can easily check against this
    // without traversing the SortedList.
    protected T lastRecorded {get; private set;}
    double lastRecordTime;

    //Mothion Smoothing
    private float velocityCorrectionThresholdSqr; // ² cached in Awake
    private float velocitySnapThresholdSqr; // ² cached in Awake
    private double positionCorrectionThresholdSqr; // ² cached in Awake
    
#endregion


#region EVENTS
    private Action OnBeginPrediction;
    private Action OnEndPrediction;

#endregion


#region ABSTRACT
    
    public abstract Vector3 currentPosition {get;}
    public abstract Vector3 currentVelocity {get;}
    public abstract void SnapTo(T newState);
    public abstract T CreateCurrentState(double currentTime);
    public abstract void SerializeState(NetworkWriter writer);
    public abstract T DeserializeState(NetworkReader reader, double timestamp);

    public abstract void OnReplayStarted(AirshipPredictedState initialState, int historyIndex);
    public abstract void OnReplayTickStarted(double time);
    public abstract void OnReplayTickFinished(double time);
    public abstract void OnReplayFinished(AirshipPredictedState initialState);
    public abstract void OnReplayingOthersStarted();
    public abstract void OnReplayingOthersFinished();

#endregion

#region VIRTUAL
    public virtual string friendlyName => "Prediction: " + gameObject.GetInstanceID();
    public virtual float guid => gameObject.GetInstanceID();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual bool IsMoving() =>
        currentVelocity.sqrMagnitude >= velocityCorrectionThresholdSqr;

    //Do we need to correct this state?
    protected virtual bool NeedsCorrection(T serverState, T interpolatedState){
        print("Correction distance: " + Vector3.SqrMagnitude(serverState.position - interpolatedState.position));
        return Vector3.SqrMagnitude(serverState.position - interpolatedState.position) >= positionCorrectionThresholdSqr;
    }
#endregion

protected void Log(string message){
    if(!showLogs){
        return;
    }
    Debug.Log(gameObject.name + " Prediction: " + message);
}

#region INIT
    protected virtual void Awake() {
        // cache some threshold to avoid calculating them in LateUpdate
        float colliderSize = GetComponentInChildren<Collider>().bounds.size.magnitude;

        // cache ² computations
        velocitySnapThresholdSqr = velocitySnapThreshold * velocitySnapThreshold;
        velocityCorrectionThresholdSqr = velocityCorrectionThreshold * velocityCorrectionThreshold;
        positionCorrectionThresholdSqr = positionCorrectionThreshold * positionCorrectionThreshold;
    }

    protected virtual void OnEnable() {
        AirshipPredictionManager.instance.RegisterPredictedObject(this);
    }

    protected virtual void OnDisable() {
        AirshipPredictionManager.instance.UnRegisterPredictedObject(this);
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
        protected virtual void Update() {
            if (!isServer){
                return;
            }

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
        
        //Record history states
        protected virtual void FixedUpdate() {
            // on clients (not host) we record the current state every FixedUpdate.
            // this is cheap, and allows us to keep a dense history.
            if (!isClientOnly) return;

            // OPTIMIZATION: RecordState() is expensive because it inserts into a SortedList.
            // only record if state actually changed!
            // risks not having up to date states when correcting,
            // but it doesn't matter since we'll always compare with the 'newest' anyway.
            //
            // we check in here instead of in RecordState() because RecordState() should definitely record if we call it!
            if (onlyRecordChanges && lastRecorded != null) {
                // TODO maybe don't reuse the correction thresholds?
                if ((lastRecorded.position - currentPosition).sqrMagnitude < positionCorrectionThresholdSqr) {
                    // Log($"FixedUpdate for {name}: taking optimized early return instead of recording state.");
                    return;
                }
            }

            // instead of recording every fixedupdate, let's record in an interval.
            // we don't want to record every tiny move and correct too hard.
            if (NetworkTime.time < lastRecordTime + recordInterval) return;

            // NetworkTime.time is always behind by bufferTime.
            // prediction aims to be on the exact same server time (immediately).
            // use predictedTime to record state, otherwise we would record in the past.
            // Adding a buffer margin so that our stored history always remains in the future of recieved server states
            RecordState(NetworkTime.predictedTime + .5);
        }

        protected void AttemptToRecord(){
            
        }
#endregion



#region PREDICTION
    //If we are in a new state then insert the current state into the history at this specific time
    protected void RecordState(double stateTime) {
        print("Recording: " + stateTime);
        lastRecordTime = NetworkTime.time;

        // FixedUpdate may run twice in the same frame / NetworkTime.time.
        // for now, simply don't record if already recorded there.
        if (lastRecorded != null && stateTime == lastRecorded.timestamp) return;

        // keep state history within limit
        if (stateHistory.Count >= stateHistoryLimit)
            stateHistory.RemoveAt(0);

        var newState = CreateCurrentState(stateTime);
        // add state to history
        stateHistory.Add(stateTime, newState);

        // manually remember last inserted state for faster .Last comparisons
        lastRecorded = newState;
    }

    //Update our rigidbody to match a new snapshot state
    void ApplyState(T snapshotState){
        // Hard snap to the state
        //Log($"Prediction: snapped {name} into place because velocity {currentVelocity.magnitude:F3} <= {velocitySnapThreshold:F3}");

        // apply server state immediately.
        // important to apply velocity as well, instead of Vector3.zero.
        // in case an object is still slightly moving, we don't want it
        // to stop and start moving again on client - slide as well here.

        //Set the position and velocity immediatly
        SnapTo(snapshotState);

        // clear history and insert the exact state we just applied.
        // this makes future corrections more accurate.
        stateHistory.Clear();
        stateHistory.Add(snapshotState.timestamp, snapshotState);
    }

    // process a received server state.
    // compares it against our history and applies corrections if needed.
    protected void OnReceivedState(double serverTimestamp, T serverState) {

        // correction requires at least 2 existing states for 'before' and 'after'.
        // if we don't have two yet, drop this state and try again next time once we recorded more.
        if (stateHistory.Count < 2) return;
        
        //print("RECIEVED STATE: " + timestamp);

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
        if (Vector3.SqrMagnitude(serverState.position - currentPosition) < positionCorrectionThresholdSqr &&
            Vector3.SqrMagnitude(serverState.velocity - currentVelocity) < velocityCorrectionThresholdSqr) {
            //Log($"OnReceivedState for {name}: taking is idle optimized early return!");
            return;
        }

        //Render server state
        if(this.showGizmos){
            //Recieved server position
            GizmoUtils.DrawBox(serverState.position, Quaternion.identity, 
                new Vector3(.1f, .1f, .1f), serverColor, gizmoDuration);
        }

        // we only capture state every 'interval' milliseconds.
        // so the newest entry in 'history' may be up to 'interval' behind 'now'.
        // if there's no latency, we may receive a server state for 'now'.
        // sampling would fail, if we haven't recorded anything in a while.
        // to solve this, always record the current state when receiving a server state.
        RecordState(NetworkTime.predictedTime);

        double oldestTime = stateHistory.Values[0].timestamp;
        double newestTime = stateHistory.Values[stateHistory.Count - 1].timestamp;

        // edge case: is the server state newer than the newest state in history?
        // this can happen if client's predictedTime predicts too far ahead of the server.
        // in that case, log a warning for now but still apply the correction.
        // otherwise it could be out of sync as long as it's too far ahead.
        //
        // for example, when running prediction on the same machine with near zero latency.
        // when applying corrections here, this looks just fine on the local machine.
        if (newestTime < serverTimestamp) {
            // the correction is for a state in the future.
            // force the state to the server
            // TODO maybe we should interpolate this back to 'now'?
            // this can happen a lot when latency is ~0. logging all the time allocates too much and is too slow.
            double ahead = serverTimestamp - newestTime;
            Log($"Hard correction because the server is ahead of the client by {ahead.ToString("#.####"):F1} seconds. \n" +
                $"serverTime={serverTimestamp:F3} localNewestTime={newestTime:F3} predictionTime: {NetworkTime.predictedTime}\n" +
                $"local oldestTime={oldestTime:F3} History of size={stateHistory.Count}. \n" +
                $"This can happen when latency is near zero, and is fine unless it shows jitter.");
            
            //Only force if the difference is significant
            if(ahead > .2){
                var log = "ALL HISTORY: ";
                for(int i=0; i<stateHistory.Count; i++){
                    log += "\n state " + i + ": " + stateHistory.Keys[i];
                }
                print(log);
                ApplyState(serverState);
                return;
            }
        }

        T interpolatedState;
        int afterIndex;

        // edge case: is the state older than the oldest state in history?
        // this can happen if the client gets so far behind the server
        // that it doesn't have a recored history to sample from.
        // in that case, we should hard correct the client.
        // otherwise it could be out of sync as long as it's too far behind.
        if (serverTimestamp < oldestTime) {
            // when starting, client may only have 2-3 states in history.
            // it's expected that server states would be behind those 2-3.
            // only show a warning if it's behind the full history limit!
            if (stateHistory.Count >= stateHistoryLimit)
                Debug.LogWarning($"Hard correcting client object {name} because the client is too far behind the server. History of size={stateHistory.Count} @ t={serverTimestamp:F3} oldest={oldestTime:F3} newest={newestTime:F3}. This would cause the client to be out of sync as long as it's behind.");

            Log("serverState is older than our history");
            //Add a history element before everything to replay from
            stateHistory.Add(serverTimestamp, serverState);
            interpolatedState = serverState;
            afterIndex = 1;
            //Continue to the replay below
        }
        // find the two closest client states between timestamp
        else if (!Sample(stateHistory, serverTimestamp, out T before, out T after, out afterIndex, out double t))
        {
            // something went very wrong. sampling should've worked.
            // hard correct to recover the error.
            Debug.LogError($"Failed to sample history of size={stateHistory.Count} @ t={serverTimestamp:F3} oldest={oldestTime:F3} newest={newestTime:F3}. This should never happen because the timestamp is within history.");
            ApplyState(serverState);
            return;
        }else{
            // interpolate between them to get the best approximation
            interpolatedState = (T)before.Interpolate(after, (float)t);
        }


        // too far off? then correct it
        if (NeedsCorrection(serverState, interpolatedState)) {
            Log($"CORRECTION NEEDED FOR {name} @ {serverTimestamp:F3}: client= {interpolatedState.timestamp} + pos: {interpolatedState.position} server= {interpolatedState.timestamp} + pos: {serverState.position}");
            if(showGizmos){
                // show the received correction position + velocity for debugging.
                // helps to compare with the interpolated/applied correction locally.
                GizmoUtils.DrawLine(serverState.position, serverState.position + serverState.velocity * 0.1f, Color.white, gizmoDuration);
            }


            //Simulate until the end of our history or however long we think we are ahead of the server whicher is longer
            double finalTime = lastRecorded.timestamp > NetworkTime.predictedTime ? lastRecorded.timestamp : NetworkTime.predictedTime;

            print("Replaying until: " + finalTime + " which is " + (finalTime - serverTimestamp) + " seconds away");

            //Replay States
            AirshipPredictionManager.instance.QueueReplay(this, serverState, finalTime - serverTimestamp, afterIndex);
        }
    }
#endregion


#region SERIALIZATION

    // send state to clients every sendInterval.
    // reliable for now.
    // TODO we should use the one from FixedUpdate
    public override void OnSerialize(NetworkWriter writer, bool initialState) {
        // Time.time was at the beginning of this frame.
        // NetworkLateUpdate->Broadcast->OnSerialize is at the end of the frame.
        // as result, client should use this to correct the _next_ frame.
        // otherwise we see noticeable resets that seem off by one frame.
        //
        // to solve this, we can send the current deltaTime.
        // server is technically supposed to be at a fixed frame rate, but this can vary.
        // sending server's current deltaTime is the safest option.
        // client then applies it on top of remoteTimestamp.
        //Saves just the time changed this frame rather than the double timestamp
        writer.WriteFloat(Time.deltaTime);
        //TODO: Why can't I just pass the timestamp??? Instead of this delta offset thing ^

        //Let the child class serialize its data
        SerializeState(writer);
    }

    // read the server's state, compare with client state & correct if necessary.
    public override void OnDeserialize(NetworkReader reader, bool initialState) {        
        // deserialize data
        // we want to know the time on the server when this was sent, which is remoteTimestamp.
        double timestamp = NetworkClient.connection.remoteTimeStamp;
        double serverDeltaTime = reader.ReadFloat();

        // server sends state at the end of the frame.
        // parse and apply the server's delta time to our timestamp.
        // otherwise we see noticeable resets that seem off by one frame.
        timestamp += serverDeltaTime;

        // however, adding yet one more frame delay gives much(!) better results.
        // we don't know why yet, so keep this as an option for now.
        // possibly because client captures at the beginning of the frame,
        // with physics happening at the end of the frame?
        if (oneFrameAhead) timestamp += serverDeltaTime;

        //Let the child class deserialize its data
        T newState = DeserializeState(reader, timestamp);

        print("Deserialize time: " + timestamp + " readerTime: " + serverDeltaTime);
        // process received state
        try{
            OnReceivedState(timestamp, newState);
        }catch(Exception e){
            Debug.LogError("Error on recieved state: " + e.Message + " source: " + e.Source);
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
            for (int i = 0; i < history.Count; ++i) {
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
        // clears all entries after the states time
        public void ClearHistoryAfterState(
            T correctedState,     // corrected state with timestamp
            int afterIndex)  // index of the 'after' value so we don't need to find it again here
        {
            //Add the corrected state to the history and remove all following states
            for(int i=afterIndex; i < stateHistory.Count; i++){
                stateHistory.Remove(afterIndex);
            }
            stateHistory.Add(correctedState.timestamp, correctedState);
            // var log = "Cleared states after: " + correctedState.timestamp;
            // foreach(var state in stateHistory){
            //     log += "\n State: " + state.Value.timestamp + " pos: " + state.Value.position;
            // }
            //print(log);
        }
#endregion

#region UTIL
    
    // simple and slow version with MoveTowards, which recalculates delta and delta.sqrMagnitude:
    //   Vector3 newPosition = Vector3.MoveTowards(currentPosition, physicsPosition, positionStep * deltaTime);
    // faster version copied from MoveTowards:
    // this increases Prediction Benchmark Client's FPS from 615 -> 640.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static Vector3 MoveTowardsCustom(
        Vector3 current,
        Vector3 target,
        Vector3 _delta,     // pass this in since we already calculated it
        float _sqrDistance, // pass this in since we already calculated it
        float _distance,    // pass this in since we already calculated it
        float maxDistanceDelta) {
        if (_sqrDistance == 0.0 || maxDistanceDelta >= 0.0 && _sqrDistance <= maxDistanceDelta * maxDistanceDelta)
            return target;

        float distFactor = maxDistanceDelta / _distance; // unlike Vector3.MoveTowards, we only calculate this once
        return new Vector3(
            // current.x + (_delta.x / _distance) * maxDistanceDelta,
            // current.y + (_delta.y / _distance) * maxDistanceDelta,
            // current.z + (_delta.z / _distance) * maxDistanceDelta);
            current.x + _delta.x * distFactor,
            current.y + _delta.y * distFactor,
            current.z + _delta.z * distFactor);
    }

#endregion
}