using System;
using Code.Bundles;
using Mirror;

public class AirshipVersionFetcher : NetworkBehaviour {
	[NonSerialized] [SyncVar] public string luauPluginVersion = LuauPlugin.LuauGetLuauPluginVersion();
	[NonSerialized] [SyncVar] public LuauPlugin.LuauBytecodeVersion bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
	[NonSerialized] [SyncVar] public string serverPlayerVersion = "";

	private void OnServerInitialized() {
		luauPluginVersion = LuauPlugin.LuauGetLuauPluginVersion();
		bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
		serverPlayerVersion = AirshipVersion.GetVersionHash();;
	}
}
