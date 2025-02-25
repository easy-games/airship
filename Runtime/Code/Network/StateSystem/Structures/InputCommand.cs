using System;
using JetBrains.Annotations;

namespace Code.Player.Character.Net
{
    /**
     * Base class for input commands when using a networked state system.
     */
    public class InputCommand : ICloneable
    {
        /** The number this command is in the clients stream of commands. */
        public int commandNumber;
        
        public virtual object Clone()
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }
    }
}