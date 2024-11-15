using System;
using Mirror;

public class LuauVersionFetcher : NetworkBehaviour {
	[NonSerialized] [SyncVar] public string version = LuauPlugin.LuauGetLuauPluginVersion();
	[NonSerialized] [SyncVar] public LuauPlugin.LuauBytecodeVersion bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();

	private void OnServerInitialized() {
		version = LuauPlugin.LuauGetLuauPluginVersion();
		bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
	}
}
