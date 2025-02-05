


using System.Collections.Generic;
using Code.Player.Character.Net;
using Code.Player.Character.NetworkedMovement;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Code.Player.Character.MovementSystem.NetworkedMovement
{
    public class AirshipInputPrediction: MonoBehaviour
    {
        public StateSnapshot lastConfirmedState;
        // input history
        public Queue<InputCommand> history = new Queue<InputCommand>();

        private void FixedUpdate()
        {   
            // check if last confirmed state has been updated
            // handle reconciles here somehow?
            
            // iterate over history and replay on top of last confirmed state
            
            // Store the result of the history?
            
            // send the command to the server?
        }

        // reconcile history with snapshot data

        // send inputs on an interval

        // figure how how this plays with rollbacks... probably doesn't need to care at all?
    }
}