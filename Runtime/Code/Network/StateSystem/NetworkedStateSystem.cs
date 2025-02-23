using System;
using Code.Network.StateSystem.Structures;
using Code.Player.Character.Net;
using JetBrains.Annotations;
using Mirror;
using UnityEngine;

namespace Code.Network.StateSystem
{
    public enum NetworkedStateSystemMode
    {
        /** The system will be used as a way to generate commands to send to an authoritative server. */
        Input,

        /** The system is the authority. It will process commands either from itself, or a remote client. */
        Authority,

        /**
         * The system is an observer. It will only be provided snapshots and will need to interpolate between
         * them to display what has already happened.
         */
        Observer,
    }

    [RequireComponent(typeof(NetworkIdentity))]
    public abstract class NetworkedStateSystem<State, Input> : NetworkBehaviour
        where State : StateSnapshot where Input : InputCommand
    {
        [NonSerialized] public NetworkedStateSystemMode mode;

        /**
        * This function is called to update the system on how it will be used.
        * For example, if the mode is Observer, you may wish to set rigidbodies to kinematic
        * or permanently disable certain effects when this function is called.
        */
        public abstract void OnSetMode(NetworkedStateSystemMode mode);

        /**
         * Sets the current state to base ticks off of. Updates the associated
         * rigidbody to match the state provided. It is important that the implementation
         * if this method hard set any physics fields to match those in the provided state.
         *
         * The time in the state value is the local time the state was captured (Time.unscaledTimeAsDouble)
         */
        public abstract void SetCurrentState(State state);

        /**
         * Gets the current state of the system. The time value provided is the local time for the tick (Time.unscaledTimeAsDouble)
         */
        public abstract State GetCurrentState(int commandNumber, double time);

        /**
         * Gets the latest command retrieved during the update loop. This command will be sent to the server and predicted.
         * This is called generally at the rate of FixedUpdate, but may not be called in situations where we are waiting
         * for commands from the server.
         */
        public abstract Input GetCommand(int commandNumber);

        /**
         * Ticks the system and advances the current state based on the move input data provided.
         * Tick will be called with a null command if a tick should occur but no command was available for that tick.
         * This function is called at least as often as FixedUpdate, but may be called more often during re-simulations
         * or on the server when there is a backup of commands.
         */
        public abstract void Tick([CanBeNull] Input command, bool replay);

        /**
         * Set the state to be the interpolated state between these two snapshots. This is called every frame
         * during LateUpdate.
         *
         * The time value of the states is the time on the server when the state was generated (NetworkTime.time).
         */
        public abstract void Interpolate(float delta, State stateOld, State stateNew);

        /**
         * This function is called when the interpolation on an observing client passes the provided state.
         * It will be called at a similar frequency to FixedUpdate (maybe more or less depending on simulation timing)
         * and can be used for things like playing effects or updating animation state.
         *
         * The time value of the state is the time on the server when the state was generated (NetworkTime.time).
         */
        public abstract void InterpolateReachedState(State state);
    }
}