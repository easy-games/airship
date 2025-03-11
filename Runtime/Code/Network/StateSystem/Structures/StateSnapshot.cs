using System;
using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Network.StateSystem.Structures
{
    /**
     * Base class for state snapshots when using a networked state system.
     */
    public class StateSnapshot: ICloneable
    {
        public int lastProcessedCommand;
        /**
         * The time the snapshot was created. This time is local to the client/server that created it.
         */
        public double time;

        /**
         * Compares two snapshots.
         */
        // Note: this should only be called to reconcile client predicted state with server authoritative state (ex. predicted.Compare(authoritative)). If we change this in the future
        // our custom command system will not be able to know if a command was authoritatively cancelled by the server on the client (see PredictedCommandManager.ts in Core)
        public virtual bool Compare<TSystem, TState, TInput>(NetworkedStateSystem<TSystem, TState, TInput> system, TState snapshot) where TState : StateSnapshot
            where TInput : InputCommand
            where TSystem : NetworkedStateSystem<TSystem, TState, TInput>
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }

        public virtual object Clone()
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }
    }
}