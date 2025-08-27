public static class AirshipRuntimePath {
    public const string ServerExtension = ".lua.server~";
    public const string ClientExtension = ".lua.client~";
    public const string LuaExtension = ".lua";
}

public enum AirshipRuntimeHint {
    /// <summary>
    /// Is considered a "shared" script (both)
    /// </summary>
    None,
    /// <summary>
    /// Is a server script
    /// </summary>
    Server,
    /// <summary>
    /// Is a client script
    /// </summary>
    Client,
}
