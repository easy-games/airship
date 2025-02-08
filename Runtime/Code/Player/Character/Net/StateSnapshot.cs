using System;
using UnityEngine;

namespace Code.Player.Character.Net
{
    public class StateSnapshot
    {
        public int lastProcessedCommand;
        /** The time the snapshot was created */
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