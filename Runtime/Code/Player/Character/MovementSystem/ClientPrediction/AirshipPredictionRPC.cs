using System;
using Mirror;
using UnityEngine;

public class AirshipPredictionRPC : NetworkBehaviour {

    /// <summary>
    /// AirshipPredictedController listens to Rpc results
    /// </summary>
    public Action<int, bool, Vector3, Vector3> OnClientRecievedServerState;
    public Action<AirshipPredictedState> OnObserverRecievedServerState;

    /// <summary>
    /// Make an RPC call to the owning client giving them state information so they can reconcile
    /// </summary>
    public void SendServerStateToClient(int tick, bool forceReplay, Vector3 position, Vector3 velocity){
        //Send position and velcoity to CLIENT
        RpcClientRecieveServerState(tick, forceReplay, position, velocity);
    }

    [TargetRpc()]
    private void RpcClientRecieveServerState(int tick, bool forceReplay, Vector3 position, Vector3 velocity){
        OnClientRecievedServerState?.Invoke(tick,forceReplay, position, velocity);
    }

    /// <summary>
    /// Make an RPC call to observers giving them state information so they can visually replicate the server
    /// </summary>
    public virtual void SendServerStateToObservers(AirshipPredictedState serverState){
        //Send state to OBSERVERS
        RpcObserverRecieveServerState(serverState);
        //Child classes can send Rpc calls with their own state information
    }

    [TargetRpc()]
    private void RpcObserverRecieveServerState(AirshipPredictedState serverState){
        //new AirshipPredictedState() { tick = serverTick, position = serverState.position, velocity = serverState.velocity}
        OnObserverRecievedServerState?.Invoke(serverState);
    }
}
