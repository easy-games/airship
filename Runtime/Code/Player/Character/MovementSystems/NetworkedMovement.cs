using Code.Player.Character.Net;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class NetworkedMovement<State, Input> : NetworkBehaviour where State: StateSnapshot where Input : InputCommand
    {
       /**
        * Sets the current state to base movement ticks off of. Updates the associated
        * rigidbody to match the state provided. It is important that the implementation
        * if this method hard set any physics fields to match those in the provided state.
        */
        public abstract void SetCurrentState(State state);

        /**
         * Gets the current state of the movement system.
         */
        public abstract State GetCurrentState(int commandNumber, double time);

        /**
         * Gets the latest command retrieved during the update loop. This command will be sent to the server and predicted.
         */
        public abstract Input GetCommand(int commandNumber, double time);

        /**
         * Ticks the predictable movement and advances the current movement state based on the move input data provided.
         */
        public abstract void Tick(Input command, bool replay);

        /**
         * Set the state to be the interpolated state between these two snapshots.
         */
        public abstract void Interpolate(float delta, State stateOld, State stateNew);
    }
}