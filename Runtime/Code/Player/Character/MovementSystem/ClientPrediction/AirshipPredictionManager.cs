using System.Collections.Generic;
using UnityEngine;

public class AirshipPredictionManager : MonoBehaviour {
    private static AirshipPredictionManager _instance;

    public static AirshipPredictionManager instance {
        get{
            if(!_instance){
                var go = new GameObject("PredictionManager");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<AirshipPredictionManager>();
                _instance.StartPrediction();
            }
            return _instance;
        }
    }

    internal class ReplayData{
        public IPredictionReplay replayController;
        public AirshipPredictionState initialState;
        public double duration;
        public double maxTickDuration = 1;
        public ReplayData(IPredictionReplay replayController, AirshipPredictionState initialState, double duration, double maxTickDuration){
            this.replayController = replayController;
            this.initialState = initialState;
            this.duration = duration;
            this.maxTickDuration = maxTickDuration;
        }
    }

    private bool debugging = false;
    private bool readyToTick = false;
    private float timer;
    private SortedList<double, ReplayData> pendingReplays = new SortedList<double, ReplayData>();

    public void StartPrediction(){
        Physics.simulationMode = SimulationMode.Script;
        debugging = false;
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

    public void StepPhysics(){
        readyToTick = true;
    }

    private void Update() {
        if(Physics.simulationMode != SimulationMode.Script){
            return;
        }

        if(pendingReplays.Count > 0){
            StartReplays();
        }


        if(debugging && !readyToTick){
            //Don't step time 
            return;
        }
        readyToTick = false;
        
        timer += Time.deltaTime;

        // Catch up with the game time.
        // Advance the physics simulation in portions of Time.fixedDeltaTime
        // Note that generally, we don't want to pass variable delta to Simulate as that leads to unstable results.
        while (timer >= Time.fixedDeltaTime) {
            timer -= Time.fixedDeltaTime;
            Physics.Simulate(Time.fixedDeltaTime);
        }
    }

    public void QueueReplay(IPredictionReplay replayController, AirshipPredictionState initialState, double duration, double maxTickDuration){
        if(replayController == null){
            Debug.LogError("Trying to queue replay without a controller");
            return;
        }
        if(initialState == null){
            Debug.LogError("Trying to queue replay without an initial state");
            return;
        }
        if(duration <= 0){
            Debug.LogError("Trying to queue a replay with a negative duration");
        }

        Debug.Log("Queue replay: " + replayController.friendlyName);
        //TODO queue for mass processing of many replays at once
        //pendingReplays.Add(initialState.timestamp, new ReplayData(replayController, initialState, duration));

        //Replay this instantly
        Replay(new ReplayData(replayController, initialState, duration, maxTickDuration));
    }

    private void StartReplays(){
        //TODO let predicted objects queue replay data and then do the replay simulations all together
        //So if you have 10 predicted rigidbodies they can share replay simulations
        foreach(var kvp in pendingReplays){
            Debug.Log("Starting replay for: " + kvp.Value.replayController.friendlyName);
            Replay(kvp.Value);
        }

        //Dont processing at all replays
        pendingReplays.Clear();
    }

    private void Replay(ReplayData replayData){
        //print("Replaying A: " + replayController.friendlyName);
        //Replay started callback
        replayData.replayController.OnReplayStart(replayData.initialState);
        //print("replaying B");

        double time = replayData.initialState.timestamp;
        double simulationDuration;
        double finalTime = time+replayData.duration;

        //print("replaying C");
        //Simulate physics for the duration of the replay
        while(time < finalTime) {
            //Move the rigidbody based on the saved inputs (impulses)
            //If no inputs then just resimulate with its current velocity
            //TODO

            //Move all dynamic rigidbodies to their saved states at this time
            //TODO
            //TODO maybe make a bool so this is optional?

            //Simulate physics until the next input state
            //TODO

            //For now just simulate all the way to the final time
            simulationDuration = finalTime - time;
            if(simulationDuration > replayData.maxTickDuration){
                simulationDuration = replayData.maxTickDuration;
            }
            //print("replaying D");
            if(simulationDuration < 0){
                Debug.LogError("NEGATIVE DURATION");
                time = finalTime+1;
                continue;
            }
            time += simulationDuration;

            //Don't simulate the last tiny bit of time
            if(simulationDuration < Time.fixedDeltaTime){
                continue;
            }

            //Replay ticked callback
            replayData.replayController.OnReplayTickStarted(time);
            //print("replaying E");

            //Run the simulation in the scene
            Physics.Simulate((float)simulationDuration);

            //print("replaying F");
            //Replay ticked callback
            replayData.replayController.OnReplayTickFinished(time);
            //print("replaying G");
        }

        //print("replaying H");
        //Done replaying callback
        replayData.replayController.OnReplayFinished(replayData.initialState);
        //print("replaying I");
    }
}


public interface IPredictionReplay {
    public abstract string friendlyName{get;}
    public abstract void OnReplayStart(AirshipPredictionState initialState);
    public abstract void OnReplayTickStarted(double time);
    public abstract void OnReplayTickFinished(double time);
    public abstract void OnReplayFinished(AirshipPredictionState initialState);
} 