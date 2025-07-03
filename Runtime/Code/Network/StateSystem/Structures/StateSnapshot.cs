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
         * The unscaled time the snapshot was created. This time is local to the client/server that created it. In server
         * authoritative mode, this time is what is used to render observed characters. This should _not_ be converted
         * to ticks! Ticks use scaled time and will not always map 1 to 1 with a real time value.
         */
        public double time;
        public uint tick;

        /**
         * Compares two snapshots.
         */
        // Note: this should only be called to reconcile client predicted state with server authoritative state (ex. predicted.Compare(authoritative)). If we change this in the future
        // our custom command system will not be able to know if a command was authoritatively cancelled by the server on the client (see PredictedCommandManager.ts in Core)
        public virtual bool Compare<TSystem, TState, TDiff, TInput>(NetworkedStateSystem<TSystem, TState, TDiff, TInput> system, TState snapshot) where TState : StateSnapshot
            where TDiff : StateDiff
            where TInput : InputCommand
            where TSystem : NetworkedStateSystem<TSystem, TState, TDiff, TInput>
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }

        /// <summary>
        /// Creates a diff that will generate the passed snapshot paramter when applied to the base state using ApplyDiff().
        /// </summary>
        /// <param name="snapshot"></param>
        /// <typeparam name="TState"></typeparam>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual StateDiff CreateDiff<TState>(TState snapshot) where TState : StateSnapshot {
            throw new NotImplementedException("Subclasses should implement this method.");
        }

        /// <summary>
        /// Applies a diff to the given state snapshot and returns the new resulting state.
        /// </summary>
        /// <param name="diff"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual StateSnapshot ApplyDiff(StateDiff diff) {
            throw new NotImplementedException("Subclasses should implement this method.");
        }

        public virtual object Clone()
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }
    }
}