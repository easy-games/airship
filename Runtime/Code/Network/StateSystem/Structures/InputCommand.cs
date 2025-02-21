using JetBrains.Annotations;

namespace Code.Player.Character.Net
{
    /**
     * Base class for input commands when using a networked state system.
     */
    public class InputCommand
    {
        public int commandNumber;
        /** The time the input was recorded */
        public double time;
    }
}