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
        /**
        * The time the input was created. This time is local to the client that created it. The server corrects this
         * time to it's local timeline before using it in the server tick processing.
        */
        public double time;
        
        public virtual object Clone()
        {
            throw new NotImplementedException("Subclasses should implement this method.");
        }
    }
}