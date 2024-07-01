using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Timing;
using UnityEngine;

public class RigidbodyLogging : MonoBehaviour
{    
    public bool logLateUpdate = true;
    public bool logFixedUpdate = false;
    public bool logTickUpdate = false;
    public bool logVel = false;
    public Vector3 continuousForce = Vector3.zero;
    public Rigidbody rigid;

    private Vector3 lastLateUpdatePos = Vector3.zero;
    private Vector3 lastFixedUpdatePos = Vector3.zero;
    private Vector3 lastTickUpdatePos = Vector3.zero;

    private void Start(){
        if(!rigid){
            this.rigid.GetComponent<Rigidbody>();
        }
        InstanceFinder.TimeManager.OnTick += TickUpdate;
    }

    private void OnDisable(){
        InstanceFinder.TimeManager.OnTick -= TickUpdate;
    }

    private void LateUpdate() {
        if(!logLateUpdate){
            return;
        }
        var diff = (this.transform.position - this.lastLateUpdatePos).magnitude;
        Debug.Log(gameObject.name + " LateUpdate Speed: " + (diff / Time.deltaTime));
        this.lastLateUpdatePos = this.transform.position;
    }

    private void FixedUpdate() {
        if(logFixedUpdate){
            var diff = (this.transform.position - this.lastFixedUpdatePos).magnitude;
            Debug.Log(gameObject.name + " FixedUpdate Speed: " + (diff / Time.fixedDeltaTime));
            this.lastFixedUpdatePos = this.transform.position;
        }

        if(rigid){
            rigid.AddForce(continuousForce, ForceMode.Acceleration);
            if(logVel){
                Debug.Log(gameObject.name + " Velocity: " + rigid.velocity);
            }
        }
    }

    private void TickUpdate() {
        if(!logTickUpdate){
            return;
        }
        var diff = (this.transform.position - this.lastTickUpdatePos).magnitude;
        Debug.Log(gameObject.name + " TickUpdate Speed: " + (diff / InstanceFinder.TimeManager.TickDelta));
        this.lastTickUpdatePos = this.transform.position;
    }
}
