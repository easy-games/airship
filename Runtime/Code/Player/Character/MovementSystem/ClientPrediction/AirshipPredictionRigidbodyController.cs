using UnityEngine;

/// <summary>
/// Add this component to any rigidbodies that need to exist in a scene with Server Side prediction
/// It handles smoothing the rigidbody and stopping its collisions during replays
/// TODO: In the future we may add support for replaying these rigidbodies during replays
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AirshipPredictionRigidbodyController : MonoBehaviour, IPredictedReplay {
    public Rigidbody rigid;
    [Tooltip("The visual component of the object that will be smoothed inbetween physics steps")]
    public Transform graphicsHolder;

    public string friendlyName => "Rigidbody_" + gameObject.name;

    public float guid => this.GetInstanceID();

    private bool waiting = false;
    private bool wasKinematic = false;
    private bool wasColliding = false;
    private CollisionDetectionMode pastDetectionMode = CollisionDetectionMode.Discrete;
    private Vector3 pastVel;
    private Vector3 pastAngularVel; 

    private void Awake() {
        if(!rigid) {
            rigid = gameObject.GetComponent<Rigidbody>();
        }
        if(!graphicsHolder) {
            Debug.LogWarning("Rigidbody is missing graphics holder and won't be smoothed or handled by client side prediction. GameObject: " + gameObject.name);
        }
    }
    
    private void OnEnable() {
        if(rigid && graphicsHolder){
            //Register for rigidbody smoothing
            AirshipPredictionManager.instance.RegisterRigidbody(rigid, graphicsHolder);

            //Listen to replays
            AirshipPredictionManager.instance.RegisterPredictedObject(this);
        }
    }

    private void OnDisable() {
        AirshipPredictionManager.instance.UnRegisterRigidbody(rigid);
        AirshipPredictionManager.instance.UnRegisterPredictedObject(this);
    }

    public void OnReplayStarted(AirshipPredictedState initialState, int historyIndex) {
        throw new System.NotImplementedException();
    }

    public void OnReplayTickStarted(int tick) {
        throw new System.NotImplementedException();
    }

    public void OnReplayTickFinished(int tick) {
        throw new System.NotImplementedException();
    }

    public void OnReplayFinished(AirshipPredictedState initialState) {
        throw new System.NotImplementedException();
    }

    public void OnReplayingOthersStarted() {
        if(waiting){
            return;
        }
        waiting = true;
        this.wasColliding = this.rigid.detectCollisions;
        this.wasKinematic = this.rigid.isKinematic;
        this.pastDetectionMode = this.rigid.collisionDetectionMode;
        this.pastVel = this.rigid.velocity;
        this.pastAngularVel = this.rigid.angularVelocity;
        
        this.rigid.collisionDetectionMode = CollisionDetectionMode.Discrete;
        this.rigid.isKinematic = true;
        this.rigid.detectCollisions = false;
    }

    public void OnReplayingOthersFinished() {
        if(!waiting){
            return;
        }
        waiting = false;
        this.rigid.isKinematic = this.wasKinematic;
        this.rigid.detectCollisions = this.wasColliding;
        this.rigid.collisionDetectionMode = this.pastDetectionMode;
        this.rigid.velocity = this.pastVel;
        this.rigid.angularVelocity = this.pastAngularVel;
    }
}
