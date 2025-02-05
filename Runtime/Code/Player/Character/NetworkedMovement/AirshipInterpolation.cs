using System;
using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    public class AirshipInterpolation<State> : MonoBehaviour where State : StateSnapshot
    {
        public int snapshotBufferSize = 3;
        public State[] stateBuffer;

        public (State oldState, State newState) GetInterpolationStates(double time)
        {
            return (null, null);
        }

        public void PushState(State state)
        {
            if (stateBuffer[this.stateBuffer.Length - 1]?.time > state.time)
            {
                Debug.Log("Received state out of order.");
                return;
            }
            
            for (int i = 0; i < this.stateBuffer.Length - 1; i++)
            {
                stateBuffer[i] = stateBuffer[i + 1];
            }

            stateBuffer[this.stateBuffer.Length - 1] = state;
        }
    }
}