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

    private float timer;

    private void Update() {
        if(Physics.simulationMode != SimulationMode.Script){
            return;
        }

        timer += Time.deltaTime;

        // Catch up with the game time.
        // Advance the physics simulation in portions of Time.fixedDeltaTime
        // Note that generally, we don't want to pass variable delta to Simulate as that leads to unstable results.
        while (timer >= Time.fixedDeltaTime) {
            timer -= Time.fixedDeltaTime;
            Physics.Simulate(Time.fixedDeltaTime);
        }
    }

    private void FixedUpdate(){
        //TODO let predicted objects queue replay data and then do the replay simulations all together
        //So if you have 10 predicted rigidbodies they can share replay simulations
    }

    public void StartPrediction(){
        Physics.simulationMode = SimulationMode.Script;
    }

    public void StopPrediction(){
        Physics.simulationMode = SimulationMode.FixedUpdate;
    }
}