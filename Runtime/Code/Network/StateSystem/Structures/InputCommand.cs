using System;
using JetBrains.Annotations;
using UnityEngine;

namespace Code.Player.Character.Net
{
    /**
     * Base class for input commands when using a networked state system.
     */
    public class InputCommand : ICloneable
    {
        /** The number this command is in the clients stream of commands. */
        public int commandNumber;
        /**
         * The tick the input was created. This time is local to the client/server that created it.
         */
        public uint tick;
        
        public virtual object Clone()
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }
    }
}