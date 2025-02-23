using JetBrains.Annotations;

namespace Code.Player.Character.Net
{
    /**
     * Base class for input commands when using a networked state system.
     */
    public class InputCommand
    {
        /** The number this command is in the clients stream of commands. */
        public int commandNumber;
    }
}