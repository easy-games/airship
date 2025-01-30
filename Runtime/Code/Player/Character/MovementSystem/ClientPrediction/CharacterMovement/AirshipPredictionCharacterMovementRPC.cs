using System;
using Mirror;
using UnityEngine;

public class AirshipPredictionCharacterMovementRPC : AirshipPredictionRPC {
    /// <summary>
    /// Make an RPC call to observers giving them state information so they can visually replicate the server
    /// </summary>
    public override void SendServerStateToObservers(AirshipPredictedState serverState){
        //Send state to OBSERVERS
        RpcObserverRecieveServerStateMovement(serverState as CharacterMovementState);
        //Child classes can send Rpc calls with their own state information
    }

    [ClientRpc(includeOwner = false)]
    private void RpcObserverRecieveServerStateMovement(CharacterMovementState serverState){   
        OnObserverRecievedServerState?.Invoke(serverState);
    }
}
