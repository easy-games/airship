using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class AirshipPredictionManager : MonoBehaviour {
    public static double PhysicsTime {get; private set;} = 0;
    public static bool SmoothRigidbodies = true;

    public static Action OnPhysicsTick;

    private static AirshipPredictionManager _instance = null;

    public static AirshipPredictionManager instance {
        get{
            if(!_instance){
                Debug.Log("Creating Prediction Singleton");
                var go = new GameObject("PredictionManager");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AirshipPredictionManager>();
                _instance.StartPrediction();
            }
            return _instance;
        }
    }


#region INTERNAL

    internal class ReplayData{
        public IPredictedReplay replayController;
        public AirshipPredictedState initialState;
        public int endingTick;
        public int afterIndex;
        public ReplayData(IPredictedReplay replayController, AirshipPredictedState initialState, int endingTick, int afterIndex){
            this.replayController = replayController;
            this.initialState = initialState;
            this.endingTick = endingTick;
            this.afterIndex = afterIndex;
        }
    }

    internal class RigidbodyState{
        public Rigidbody rigid;
        public Transform graphicsHolder;
        public Vector3 currentPosition;
        public Quaternion currentRotation;
        public Vector3 lastPosition;
        public Quaternion lastRotation;

        public RigidbodyState(Rigidbody rigid, Transform graphicsHolder){
            this.rigid = rigid;
            this.graphicsHolder = graphicsHolder;
            this.currentPosition = rigid.position;
            this.currentRotation = rigid.rotation;
        }
    }
#endregion

    private bool debugging = false;
    private bool readyToTick = false;
    private float physicsTimer;
    private SortedList<double, ReplayData> pendingReplays = new SortedList<double, ReplayData>();
    private Dictionary<float, IPredictedReplay> replayObjects = new Dictionary<float, IPredictedReplay>();
    private Dictionary<float, RigidbodyState> currentTrackedRigidbodies = new Dictionary<float, RigidbodyState>();
    private float lastSimulationTime = 0;
    private float lastSimulationDuration = 0;
    private float currentSimulationTime = 0;
    private int simI = 0;
    private int simFrames = 4;

#region PUBLIC API
    public void StartPrediction(){
        Physics.simulationMode = SimulationMode.Script;
        debugging = false;
        this.physicsTimer = 0;
    }

    public void StopPrediction(){
        Physics.simulationMode = SimulationMode.FixedUpdate;
    }

    public void EnabledDebugMode(){
        debugging = true;
        readyToTick = false;
    }

    public void DisableDebugMode(){
        debugging = false;
    }

    public void StepDebugPhysics(){
        readyToTick = true;
    }

    public void RegisterPredictedObject(IPredictedReplay replayObject) {
        this.replayObjects.Add(replayObject.guid, replayObject);
    }
    
    public void UnRegisterPredictedObject(IPredictedReplay replayObject) {
        this.replayObjects.Remove(replayObject.guid);
    }

    public void RegisterRigidbody(Rigidbody rigid, Transform graphicsHolder) {
        this.currentTrackedRigidbodies.Add(rigid.GetInstanceID(), new RigidbodyState(rigid, graphicsHolder));
    }
    
    public void UnRegisterRigidbody(Rigidbody rigid) {
        this.currentTrackedRigidbodies.Remove(rigid.GetInstanceID());
    }
#endregion

#region UPDATE
    private void Update() {

        if(Physics.simulationMode != SimulationMode.Script){
            return;
        }

        // if(pendingReplays.Count > 0){
        //     StartReplays();
        // }


        if(debugging && !readyToTick){
            //Don't step time 
            return;
        }
        readyToTick = false;

        // Catch up with the game time.
        // Advance the physics simulation in portions of Time.fixedDeltaTime
        // Note that generally, we don't want to pass variable delta to Simulate as that leads to unstable results.
        physicsTimer += Time.deltaTime;
        while (physicsTimer >= Time.fixedDeltaTime) {

            //Simulate the physics
            physicsTimer -= Time.fixedDeltaTime;
            Physics.Simulate(Time.fixedDeltaTime);
            PhysicsTime += Time.fixedDeltaTime;

            OnPhysicsTick?.Invoke();

            if(!SmoothRigidbodies){
                continue;
            }

            //RIGID SMOOTHING: Save timeing data every X frames
            simI++;
            if(simI >= simFrames){
                simI = 0;
                lastSimulationTime = currentSimulationTime;
                currentSimulationTime = Time.time;
                lastSimulationDuration = Time.time - lastSimulationTime;
                lastSimulationTime = Time.time;

                //Update rigidbody state data
                foreach(var kvp in currentTrackedRigidbodies){
                    kvp.Value.lastPosition = kvp.Value.currentPosition;
                    kvp.Value.lastRotation = kvp.Value.currentRotation;
                    kvp.Value.currentPosition = kvp.Value.rigid.position;
                    kvp.Value.currentRotation = kvp.Value.rigid.rotation;
                }
            }
        }

        if(NetworkClient.active){
            //Smooth out rigidbody movement
            InterpolateBodies();
        }
    }

#endregion


#region SMOOTHING

    /*
    Based on Unity Engine Modules -> PhysicsManager.cpp
    void PhysicsManager::InterpolateBodies(PhysicsSceneHandle handle)
    */
    public void InterpolateBodies(){
        if(!SmoothRigidbodies || lastSimulationDuration == 0){
            return;
        }
        float interpolationTime = Mathf.Clamp01((Time.time - lastSimulationTime) / lastSimulationDuration);
        //print("interpolationTime: " + interpolationTime + " lastTime: " + lastSimulationTime + " lastDuration: " + lastSimulationDuration + " time: " + Time.time);
        //TODO: Sort the rigidbodies by depth (how deep in heirarchy?) so that we update nested rigidbodies in the correct order
        foreach(var kvp in currentTrackedRigidbodies){
            var rigidData = kvp.Value;
            // rigidData.graphicsHolder.SetPositionAndRotation(
            //     Vector3.Lerp(rigidData.lastPosition, rigidData.currentPosition, interpolationTime), 
            //     Quaternion.Lerp(rigidData.lastRotation, rigidData.currentRotation, interpolationTime)
            //     );
            rigidData.graphicsHolder.position = Vector3.Lerp(rigidData.lastPosition, rigidData.currentPosition, interpolationTime);

            // GizmoUtils.DrawSphere(
            //     Vector3.Lerp(rigidData.lastPosition, rigidData.currentPosition, interpolationTime),
            //     .25f, new Color(interpolationTime,.2f,1-interpolationTime), 4, 4);
        }
    }

#endregion

#region REPLAYING

    public void QueueReplay(IPredictedReplay replayController, AirshipPredictedState initialState, int endingTick, int afterIndex) {
        if(replayController == null){
            Debug.LogError("Trying to queue replay without a controller");
            return;
        }
        if(initialState == null){
            Debug.LogError("Trying to queue replay without an initial state");
            return;
        }
        if(endingTick < initialState.tick){
            Debug.LogError("Trying to queue a replay with a negative duration");
            return;
        }
        if(pendingReplays.ContainsKey(initialState.tick)){
            Debug.LogError("Trying to queue a timestamp that already exists");
            return;
        }

        //Debug.Log("Queue replay: " + replayController.friendlyName);

        //TODO let predicted objects queue replay data and then do the replay simulations all together
        //So if you have 10 predicted rigidbodies they can share replay simulations
        //pendingReplays.Add(initialState.timestamp, new ReplayData(replayController, initialState, duration));

        //Clear all pending replays
        //pendingReplays.Clear();

        //Just replay this
        Replay(new ReplayData(replayController, initialState, endingTick, afterIndex));
    }

    private void StartReplays(){
        // //Let any other objects disable while this replays
        foreach(var kvp in replayObjects){
            kvp.Value.OnReplayingOthersStarted();
        }
        
        // //Replay all pending replays
        foreach(var kvp in pendingReplays){
            //Make sure this object isn't disabled
            kvp.Value.replayController.OnReplayingOthersFinished();

            Debug.Log("Starting replay for: " + kvp.Value.replayController.friendlyName);
            //Replay Logic
            Replay(kvp.Value);

            //Reset for other replays
            kvp.Value.replayController.OnReplayingOthersStarted();
        }
        
        //Let any other objects reset after this replay
        foreach(var kvp in replayObjects){
            kvp.Value.OnReplayingOthersFinished();
        }
    }

    private void Replay(ReplayData replayData){
        //Let any other objects disable while this replays
        foreach(var kvp in replayObjects){
            if(kvp.Key == replayData.replayController.guid){
                continue;
            }
            kvp.Value.OnReplayingOthersStarted();
        }

        //Replay started callback
        replayData.replayController.OnReplayStarted(replayData.initialState, replayData.afterIndex);

        int tick = replayData.initialState.tick;
        int finalTick = replayData.endingTick;

        Debug.Log("Replaying " + replayData.replayController.friendlyName + " from: " + replayData.initialState.tick + " to: " + replayData.endingTick);

        //Simulate physics for the duration of the replay
        while(tick <= finalTick) {
            //TODO
            //Move all other dynamic rigidbodies to their saved states at this time
            //TODO maybe make a bool so this is optional?

            //Simulate 1 physics step
            tick ++;

            //Replay ticked callback
            replayData.replayController.OnReplayTickStarted(tick);

            //Run the simulation in the scene
            Physics.Simulate((float)Time.fixedDeltaTime);

            //Replay ticked callback
            replayData.replayController.OnReplayTickFinished(tick);
        }

        print("Replay finished");
        //Done replaying callback
        replayData.replayController.OnReplayFinished(replayData.initialState);
        
        //Let any other objects reset after this replay
        foreach(var kvp in replayObjects){
            if(kvp.Key == replayData.replayController.guid){
                continue;
            }
            kvp.Value.OnReplayingOthersFinished();
        }
    }
#endregion
}


public interface IPredictedReplay {
    public abstract string friendlyName{get;}
    public abstract float guid {get;}
    public abstract void OnReplayStarted(AirshipPredictedState initialState, int historyIndex);
    public abstract void OnReplayTickStarted(int tick);
    public abstract void OnReplayTickFinished(int tick);
    public abstract void OnReplayFinished(AirshipPredictedState initialState);

    public abstract void OnReplayingOthersStarted();
    public abstract void OnReplayingOthersFinished();
} 