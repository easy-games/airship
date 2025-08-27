public static class AirshipRuntimePath {
    public const string ServerExtension = ".server-lua";
    public const string ClientExtension = ".client-lua";
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
