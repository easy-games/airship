namespace Luau {
    /// <summary>
    /// Identifiers for the various Luau states. Should match up with the Luau plugin "LuauContext.h" enum
    /// </summary>
    public enum LuauContext {
        /// <summary>
        /// Game context exist per-game. It is spun up and tore down between games.
        /// </summary>
        Game,
        
        /// <summary>
        /// Protected context always exists and runs the duration of the main application.
        /// </summary>
        Protected,
    }
}
