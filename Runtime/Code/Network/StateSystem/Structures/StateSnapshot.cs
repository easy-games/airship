using System;
using UnityEngine;

namespace Code.Network.StateSystem.Structures
{
    /**
     * Base class for state snapshots when using a networked state system.
     */
    public class StateSnapshot
    {
        public int lastProcessedCommand;
        /**
         * The time the snapshot was created. This time is local to the client/server that created it.
         */
        public double time;

        /**
         * Compares two snapshots with a given % margin.
         */
        public virtual bool CompareWithMargin(float margin, StateSnapshot snapshot)
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }
    }
}