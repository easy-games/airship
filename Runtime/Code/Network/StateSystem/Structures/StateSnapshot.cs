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
         * Compares two snapshots with a given % margin.
         */
        public virtual bool Compare<TState, TInput>(NetworkedStateSystem<TState, TInput> system, TState snapshot) where TState : StateSnapshot
            where TInput : InputCommand
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }

        public virtual object Clone()
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }
    }
}