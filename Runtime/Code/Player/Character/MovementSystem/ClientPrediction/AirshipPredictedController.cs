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
[RequireComponent(typeof(NetworkIdentity), typeof(AirshipPredictionRPC))]
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

    [Tooltip("Optimization. How long in seconds to wait between server state sends to the owning client. <= 1 to update the client as often as possible.")]
    public int serverToClientSendDelay = 0;

    [Tooltip("Optimization. How long in seconds to wait between server state sends to all observers. <= 1 to update the observers as often as possible.")]
    public int serverToObserversSendDelay = 0;
    public int replayTickOffset = 0;

    [Tooltip("Correction threshold in meters. For example, 0.1 means that if the client is off by more than 10cm, it gets corrected.")]
    public float positionCorrectionThreshold = 0.10f;
    [Tooltip("How much can velocity be off before being corrected. In meters per second")]
    public float velocityCorrectionThreshold = 0.5f;

    [Tooltip("Snap to the server state directly when velocity is < threshold. This is useful to reduce jitter/fighting effects before coming to rest.\nNote this applies position, rotation and velocity(!) so it's still smooth.")]
    public float velocitySnapThreshold = 2; // 0.5 has too much fighting-at-rest, 2 seems ideal.


    [Header("Bandwidth")]

    [Tooltip("Reduce sends while velocity==0. Client's objects may slightly move due to gravity/physics, so we still want to send corrections occasionally even if an object is idle on the server the whole time.")]
    public bool reduceSendsWhileIdle = true;
    

    [Header("Debugging")]
    public bool smoothRigidbody = true;
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
    protected int serverTick = 0;
    protected int predictedTick = 0;

    //State History
    protected readonly SortedList<int, T> stateHistory = new SortedList<int, T>();
    // manually store last recorded so we can easily check against this
    // without traversing the SortedList.
    protected T lastRecorded {get; private set;}
    protected double lastRecordedTime {get; private set;}
    private int lastRecordedTick = 0;

    //Mothion Smoothing
    protected float velocityCorrectionThresholdSqr; // ² cached in Awake
    protected float velocitySnapThresholdSqr; // ² cached in Awake
    protected float positionCorrectionThresholdSqr; // ² cached in Awake

    //Serialization
    private float lastSendTimeClient = 0;
    private float lastSendTimeObservers = 0;
    private int newestTick = 0;

	//Prediction Observers
	private Vector3 prevObserverPosition;
	private Vector3 nextObserverPosition;
	private float lastObserverTime;

    private AirshipPredictionRPC rpcCalls;
    
    public override int GetHashCode() {
        print("PredictedController Hash: " + base.GetHashCode());
        return 1337;
    }
#endregion


#region ABSTRACT
    
    public abstract Vector3 currentPosition {get;}
    public abstract Vector3 currentVelocity {get;}
    public abstract void SnapTo(T newState);
    public abstract T CreateCurrentState(int currentTick);
    public abstract T DeserializeState(int tick, Vector3 position, Vector3 velocity);

    public abstract void OnReplayStarted(AirshipPredictedState initialState, int historyIndex);
    public abstract void OnReplayTickStarted(int tick);
    public abstract void OnReplayTickFinished(int tick);
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
        bool needsCorrection = Vector3.SqrMagnitude(serverState.position - interpolatedState.position) >= positionCorrectionThresholdSqr;
        if(showLogs && needsCorrection){
            print("Correction distance: " + Vector3.Magnitude(serverState.position - interpolatedState.position));
        }
        return needsCorrection;
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
        rpcCalls = gameObject.GetComponent<AirshipPredictionRPC>();
        if(!rpcCalls){
            Debug.LogError("Missing AirshipPredictionRPC component on predicted controller");
        }
        rpcCalls.OnClientRecievedServerState += OnClientRecievedServerState;

        // cache ² computations
        velocitySnapThresholdSqr = velocitySnapThreshold * velocitySnapThreshold;
        velocityCorrectionThresholdSqr = velocityCorrectionThreshold * velocityCorrectionThreshold;
        positionCorrectionThresholdSqr = positionCorrectionThreshold * positionCorrectionThreshold;
    }

    protected virtual void OnEnable() {
        AirshipPredictionManager.instance.RegisterPredictedObject(this);
        AirshipPredictionManager.OnPhysicsTick += OnPhysicsTick;
    }

    protected virtual void OnDisable() {
        AirshipPredictionManager.instance.UnRegisterPredictedObject(this);
        AirshipPredictionManager.OnPhysicsTick -= OnPhysicsTick;
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

    protected bool IsObserver(){
        return isClient && !isOwned;
    }
#endregion


#region PREDICTION
    private bool forceNextReplay = false;
    public void ForceReplay(){
        if(isServerOnly){
            forceNextReplay = true;
        }
    }

    protected virtual bool ShouldRecordState(){
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
                    return false;
                }
            }

            // instead of recording every fixedupdate, let's record in an interval.
            // we don't want to record every tiny move and correct too hard.
            if (NetworkTime.time < lastRecordedTime + recordInterval) return false;

            // FixedUpdate may run twice in the same frame / NetworkTime.time.
            // for now, simply don't record if already recorded there.
            if (NetworkTime.time == lastRecordedTime) return false;

            return true;
    }

    //If we are in a new state then insert the current state into the history at this specific time
    protected void RecordState(int stateTick) {
        lastRecordedTime = NetworkTime.time;

        //PrintHistory("Recording: " + stateTime);

        // keep state history within limit
        if (stateHistory.Count >= stateHistoryLimit)
            stateHistory.RemoveAt(0);

        //Debug.Log("Record State: " + stateTime + " tick: " + tick);

        var newState = CreateCurrentState(stateTick);

        if(stateHistory.ContainsKey(stateTick)){
            Debug.LogWarning("Trying to Record a state that has already been recorded: " + stateTick);
        } else{
            // add state to history
            stateHistory.Add(stateTick, newState);
        }

        // manually remember last inserted state for faster .Last comparisons
        lastRecorded = newState;
        lastRecordedTick = stateTick;

        //print("Recording State: " + stateTime + " vel: " + newState.velocity);
    }

    //Update our rigidbody to match a new snapshot state
    void ApplyState(T snapshotState){
        // Hard snap to the state
        Log("Applying State: " + snapshotState.tick);
        SetServerTick(snapshotState.tick);

        // apply server state immediately.
        // important to apply velocity as well, instead of Vector3.zero.
        // in case an object is still slightly moving, we don't want it
        // to stop and start moving again on client - slide as well here.

        //Set the position and velocity immediatly
        SnapTo(snapshotState);

        // clear history and insert the exact state we just applied.
        // this makes future corrections more accurate.
        stateHistory.Clear();
        stateHistory.Add(snapshotState.tick, snapshotState);
    }

    // process a received server state.
    // compares it against our history and applies corrections if needed.
    // serverTimestamp is the same value as serverState.timestamp
    protected virtual void OnClientRecievedServerState(int tick, bool forceReplay, Vector3 position, Vector3 velocity){
        //Only use if its the latest information
        if(!IsLatestTick(tick)){
            return;
        }

        print("Recieved Server state: " + netId + "  server tick: " + serverTick);
        // process received state
        try{
            var serverState = DeserializeState(tick, position, velocity);
            if(forceReplay){
                int forcedIndex;
                if(stateHistory.Count <= 2){
                    //Shouldn't this be apply state so it actually saves into the history?
                    //ApplyState(serverState);
                    SnapTo(serverState);
                    return;
                } else if (Sample(stateHistory, serverTick, out T before, out forcedIndex)) {
                    AirshipPredictionManager.instance.QueueReplay(this, serverState, lastRecorded.tick - 1 + replayTickOffset, forcedIndex);
                    return;
                } else {
                    // something went very wrong. sampling should've worked.
                    Debug.LogError("Unable to sample with a forced replay at tick: " + serverTick);
                    PrintHistory();
                }
            }

            // correction requires at least 2 existing states for 'before' and 'after'.
            // if we don't have two yet, drop this state and try again next time once we recorded more.
            if (stateHistory.Count <= 2) return;
            
            //print("RECIEVED STATE: " + serverTimestamp + " stateTime: " + serverState.timestamp);

            if(lastRecordedTick > 0 && lastRecordedTick < serverTick){
                GizmoUtils.DrawBox(serverState.position, Quaternion.identity, new Vector3(.04f, .04f, .04f), Color.white, gizmoDuration);
            }
            

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
                //Close enought to not need any adjustments
                //Log($"OnReceivedState for {name}: taking is idle optimized early return!");
                return;
            }

            // we only capture state every 'interval' milliseconds.
            // so the newest entry in 'history' may be up to 'interval' behind 'now'.
            // if there's no latency, we may receive a server state for 'now'.
            // sampling would fail, if we haven't recorded anything in a while.
            // to solve this, always record the current state when receiving a server state.

            //This shouldn't be need in Airship because we are controlling the FixedUpdate loop and recording again will just record redundant data
            // if(predictedTime > lastRecorded.timestamp + recordInterval/2 
            //     && !this.stateHistory.ContainsKey(predictedTime)){
            //     RecordState(predictedTime);
            // }

            int oldestTick = stateHistory.Values[0].tick;

            // edge case: is the server state newer than the newest state in history?
            // this can happen if client's predictedTime predicts too far ahead of the server.
            // in that case, log a warning for now but still apply the correction.
            // otherwise it could be out of sync as long as it's too far ahead.
            //
            // for example, when running prediction on the same machine with near zero latency.
            // when applying corrections here, this looks just fine on the local machine.
            // Only force if the difference is significant
            if (serverTick > lastRecordedTick) {
                // the correction is for a state in the future.
                // force the state to the server
                // TODO maybe we should interpolate this back to 'now'?
                // this can happen a lot when latency is ~0. logging all the time allocates too much and is too slow.
                Log($"Hard correction because the server is ahead of the client\n" +
                    $"serverTime={serverTick:F3} locallastRecordTick={lastRecordedTick:F3} predictionTime: {predictedTick}\n" +
                    $"local oldestTick={oldestTick:F3} History of size={stateHistory.Count}. \n" +
                    $"This can happen when latency is near zero, and is fine unless it shows jitter.");
                PrintHistory();
                ApplyState(serverState);
                return;
            }

            T clientState;
            int afterIndex;

            // edge case: is the state older than the oldest state in history?
            // this can happen if the client gets so far behind the server
            // that it doesn't have a recored history to sample from.
            // in that case, we should hard correct the client.
            // otherwise it could be out of sync as long as it's too far behind.
            if (serverTick < oldestTick) {
                // when starting, client may only have 2-3 states in history.
                // it's expected that server states would be behind those 2-3.
                // only show a warning if it's behind the full history limit!
                if (stateHistory.Count >= stateHistoryLimit)
                    Debug.LogWarning($"Hard correcting client object {name} because the client is too far behind the server. History of size={stateHistory.Count} oldest={oldestTick:F3} newest={lastRecordedTick}. This would cause the client to be out of sync as long as it's behind.");

                Log("serverState is older than our history. Server: " + serverTick + " oldest tick: " + stateHistory.Keys[0]);
                //Add a history element before everything to replay from
                stateHistory.Add(serverTick, serverState);
                clientState = serverState;
                afterIndex = 1;
                //Continue to the replay below
            }
            // find the two closest client states between timestamp
            else if (Sample(stateHistory, serverTick, out T before, out afterIndex)) {
                clientState = before;
            }else {
                // something went very wrong. sampling should've worked.
                // hard correct to recover the error.
                Debug.LogError($"Failed to sample history of size={stateHistory.Count} @ oldest={oldestTick:F3} newest={lastRecordedTick}. This should never happen because the timestamp is within history.");
                PrintHistory();
                ApplyState(serverState);
                return;
            }


            // too far off? then correct it
            if (NeedsCorrection(serverState, clientState)) {
                Log($"CORRECTION NEEDED FOR {name} client= {clientState.tick} + pos: {clientState.position} server= {serverState.tick} + pos: {serverState.position}");
                if(showGizmos){
                    // show the received correction position + velocity for debugging.
                    // helps to compare with the interpolated/applied correction locally.
                    GizmoUtils.DrawLine(serverState.position, serverState.position + serverState.velocity * 0.1f, Color.white, gizmoDuration);

                    //Recieved server position
                    GizmoUtils.DrawBox(serverState.position, Quaternion.identity, 
                        new Vector3(.1f, .1f, .1f), serverColor, gizmoDuration);

                    //Current client position
                    GizmoUtils.DrawSphere(currentPosition, .08f, Color.red, 4, gizmoDuration);
                    GizmoUtils.DrawLine(currentPosition, currentPosition + currentVelocity * 0.1f, Color.red, gizmoDuration);

                    //Client position at the this timestamp
                    GizmoUtils.DrawSphere(clientState.position, .08f, Color.red, 4, gizmoDuration);
                    GizmoUtils.DrawLine(clientState.position, clientState.position + clientState.velocity * 0.1f, Color.red, gizmoDuration);
                }


                //Simulate until the end of our history
                if(lastRecordedTick > serverTick){
                    if(showLogs){
                        print("Replaying until tick: " + lastRecordedTick + " which is " + ((serverTick - lastRecordedTick) * syncInterval) + " seconds away");
                    }

                    //Replay States
                    AirshipPredictionManager.instance.QueueReplay(this, serverState, lastRecorded.tick - 1 + replayTickOffset, afterIndex);
                }else{
                    //Snap because there isn't a time difference (should just be in shared mode)
                    ApplyState(serverState);
                }
            }
        } catch(Exception e){
            Debug.LogError("Error on recieved state: " + e.Message + " trace: " + e.StackTrace);
        }
        
    }

#endregion


#region SERIALIZATION

        protected virtual void Update() {
            if(IsObserver()){
                InterpolateObserverPosition();
            }
            
            if (!isServer){
                return;
            }

            int optimizationDuration = 0;
            // bandwidth optimization while idle.
            if (reduceSendsWhileIdle && !IsMoving()) {
                // while moving, always sync every frame for immediate corrections.
                // while idle, only sync once per second.
                optimizationDuration = 1;

                // TODO
                // next round of optimizations: if client received nothing for 1s,
                // force correct to last received state. then server doesn't need
                // to send once per second anymore.
            }

            
            if(Time.time - lastSendTimeClient > this.serverToClientSendDelay + optimizationDuration){
                this.lastSendTimeClient = Time.time;
                print("Sending tp client RPC: " + this.netId + ": " + serverTick);
                rpcCalls.SendServerStateToClient(serverTick, forceNextReplay, currentPosition, currentVelocity);
                forceNextReplay = false;
            }

            
            if(Time.time - lastSendTimeObservers > this.serverToObserversSendDelay + optimizationDuration){
                this.lastSendTimeObservers = Time.time;
                print("Sending to observers RPC: " + serverTick);

                //Send entire state data to OBSERVERS
                rpcCalls.SendServerStateToObservers();
            }
            

            // always set dirty to always serialize in next sync interval.
            SetDirty();
        }


        protected bool IsLatestTick(int tick){
            if(tick < newestTick){
                return false;
            }
            newestTick = tick;
            SetServerTick(tick);
            return true;
        }

        private AirshipPredictedState[] observedStates = new AirshipPredictedState[2] {null, null};

        protected virtual void OnObserverRecievedServerState(AirshipPredictedState serverState){
		    //In server auth we don't sync position with the NetworkTransform so I am doing it here

            //Store prev and next positions so we can lerp between the two to account for network lag
            prevObserverPosition = transform.position; //Start lerping from where we currently are so we don't get popping
            nextObserverPosition = serverState.position; //End at the latest server position

            observedStates[0] = observedStates[1];
            observedStates[1] = serverState;

            //Track how long we should interpolate
            lastObserverTime = Time.time;
        }
        
        private void InterpolateObserverPosition(){
            var prevState = observedStates[0];
            var nextState = observedStates[1];

            if(prevState == null){
                return;
            }
            
            //Find where we are in time since the last observer update
            var delta = Mathf.Clamp(
                (Time.time - lastObserverTime) / (GetTime(nextState.tick - prevState.tick) + .1f), //Get duration from the ticks they were sent on the server +100ms margin
                0, 10); //Let it extrapolate but not too crazy (10 is an arbitrary guess at what is too crazy)
            //Interpolate between prev position and the lateset server position
            transform.position = Vector3.Lerp(
                prevObserverPosition,
                nextObserverPosition, delta);
        }

        private void SetServerTick(int tick){
            serverTick = tick;
            //Calculate the estimated predicted tick. But don't go into the past if we have already predicted further
            var offset = NetworkTime.predictedTime - NetworkTime.time;
            predictedTick = Mathf.Max(serverTick + math.max(1, GetTick(offset)), predictedTick);
        }
#endregion

#region HISTORY
        //Record history states
        protected virtual void OnPhysicsTick() {
            serverTick++;
            if(this.isServer){
                predictedTick = serverTick;
            }else{
                predictedTick++;
            }

            // on clients (not host) we record the current state every FixedUpdate.
            // this is cheap, and allows us to keep a dense history.
            if (!isClientOnly || IsObserver()) return;

            if(ShouldRecordState()) {
                // NetworkTime.time is always behind by bufferTime.
                // prediction aims to be on the exact same server time (immediately).
                // use predictedTime to record state, otherwise we would record in the past.
                RecordState(serverTick);
            }
        }

        public void PrintHistory(string additionalMessage = ""){
            var log = additionalMessage + " CurrentHistory: ";
            foreach(var state in stateHistory){
                log += "\n State: " + state.Value.tick + " pos: " + state.Value.position + " vel: " + state.Value.velocity;
            }
            print(log);
        }

        // get the two states closest to a given timestamp.
        // those can be used to interpolate the exact state at that time.
        // => RingBuffer: see prediction_ringbuffer_2 branch, but it's slower!
        public bool Sample<T>(
            SortedList<int, T> history,
            int tick, // current server time
            out T before,
            out int afterIndex)
        {
            before = default;
            afterIndex = -1;

            // can't sample an empty history
            // interpolation needs at least two entries.
            //   can't Lerp(A, A, 1.5). dist(A, A) * 1.5 is always 0.
            if (history.Count <= 2) {
                this.Log("History is too short");
                return false;
            }

            // older than oldest
            if (tick < history.Keys[0]) {
                this.Log("Time is older than our history");
                return false;
            }

            var lastIndex = history.Keys.Count-1;
            if(tick >= history.Keys[lastIndex]){
                this.Log("Time is newer than our history");
                before = history.Values[lastIndex-1];
                afterIndex = lastIndex;
                return true;
            }

            // iterate through the history
            int index = 0; // manually count when iterating. easier than for-int loop.
            KeyValuePair<double, T> prev = new KeyValuePair<double, T>();

            // SortedList foreach iteration allocates a LOT. use for-int instead.
            // foreach (KeyValuePair<double, T> entry in history) {
            for (int i = 0; i < history.Count; ++i) {
                double key = history.Keys[i];
                T value = history.Values[i];

                // exact match?
                if (tick == key)
                {
                    before = value;
                    afterIndex = index;
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (key > tick)
                {
                    before = prev.Value;
                    afterIndex = index;
                    return true;
                }

                // remember the last
                prev = new KeyValuePair<double, T>(key, value);
                index += 1;
            }

            Debug.LogWarning("Unable to find valid time");
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
            stateHistory.Add(correctedState.tick, correctedState);
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

    public int GetTick(double time){
        //print("Getting tick. Time: " + time + " interval: " + this.recordInterval + " rounded: " + Mathf.RoundToInt((float)time / this.recordInterval));
        return Mathf.RoundToInt((float)time / this.recordInterval);
    }

    public int GetCurrentTick(){
        return predictedTick;
    }

    public float GetTime(int tick){
        return tick * this.recordInterval;
    }

#endregion
}