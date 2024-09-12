/// Identifiers for the various Luau states. Should match up with the Luau plugin "LuauContext.h" enum
[System.Flags]
public enum LuauContext {
    /// Game context exist per-game. It is spun up and tore down between games.
    Game = 1 << 0,
    
    /// Protected context always exists and runs the duration of the main application.
    Protected = 1 << 1,
    
    /// <summary>
    /// Rendering-based context per-game. 
    /// </summary>
    RenderPass = 1 << 2,
}
