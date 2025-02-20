using System;
using Code.Player.Character.Net;
using JetBrains.Annotations;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement
{
    public enum MovementMode
    {
        /** The movement system will be used as a way to generate commands to send to an authoritative server. */
        Input,

        /** The movement system is the authority. It will process commands either from itself, or a remote client. */
        Authority,

        /**
         * The movement system is an observer. It will only be provided snapshots and will need to interpolate between
         * them to display what has already happened.
         */
        Observer,
    }

    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class NetworkedMovement<State, Input> : NetworkBehaviour
        where State : StateSnapshot where Input : InputCommand
    {
        [NonSerialized] public MovementMode mode;

        /**
        * This function is called to update the movement system on how it will be used.
        * For example, if the mode is Observer, you may wish to set rigidbodies to kinematic
        * or permanently disable certain effects when this function is called.
        */
        public abstract void OnSetMode(MovementMode mode);

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
         * This is called generally at the rate of FixedUpdate, but may not be called in situations where we are waiting
         * for commands from the server.
         */
        public abstract Input GetCommand(int commandNumber, double time);

        /**
         * Ticks the predictable movement and advances the current movement state based on the move input data provided.
         * Tick will be called with a null command if a tick should occur but no command was available for that tick.
         * This function is called at least as often as FixedUpdate, but may be called more often during re-simulations
         * or on the server when there is a backup of commands.
         */
        public abstract void Tick([CanBeNull] Input command, bool replay);

        /**
         * Set the state to be the interpolated state between these two snapshots. This is called every frame
         * during LateUpdate.
         */
        public abstract void Interpolate(float delta, State stateOld, State stateNew);

        /**
         * This function is called when the interpolation on an observing client passes the provided state.
         * It will be called at a similar frequency to FixedUpdate (maybe more or less depending on simulation timing)
         * and can be used for things like playing effects or updating animation state.
         */
        public abstract void InterpolateReachedState(State state);
    }
}