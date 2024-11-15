using Mirror;

public class LuauVersionFetcher : NetworkBehaviour {
	[SyncVar] public string version = LuauPlugin.LuauGetLuauPluginVersion();
	[SyncVar] public LuauPlugin.LuauBytecodeVersion bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
}
