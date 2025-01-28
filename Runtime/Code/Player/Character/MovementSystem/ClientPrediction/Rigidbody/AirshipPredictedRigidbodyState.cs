using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

public class AirshipPredictedRigidbodyState : AirshipPredictedState {
    public Quaternion rotation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] protected set; }
    public Vector3 angularVelocity { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] protected set; }

    public AirshipPredictedRigidbodyState(){
    }

    public AirshipPredictedRigidbodyState(int tick, Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angularVel){
        this.position = pos;
        this.velocity = vel;
        this.tick = tick;
        this.rotation = rot;
        this.angularVelocity = angularVel;
    }

    // public override AirshipPredictedState Interpolate(AirshipPredictedState other, float delta) {
    //     var otherRigid = (AirshipPredictedRigidbodyState)other;
    //     if(otherRigid != null){
    //         this.tick = math.lerp(this.tick, otherRigid.tick, delta);
    //         this.position = Vector3.Lerp(this.position, otherRigid.position, delta);
    //         this.velocity = Vector3.Lerp(this.velocity, otherRigid.velocity, delta);
    //         this.angularVelocity = Vector3.Lerp(this.angularVelocity, otherRigid.angularVelocity, delta);
    //         this.rotation = Quaternion.Lerp(this.rotation, otherRigid.rotation, delta);
    //         return this;
    //     }
    //     return other;
    // }
}