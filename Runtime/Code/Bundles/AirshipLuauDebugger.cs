using System;
using System.Collections.Generic;
using Code.Bundles;
using Mirror;
using UnityEngine;

public class AirshipLuauDebugger : NetworkBehaviour {
	[NonSerialized] [SyncVar] public string luauPluginVersion = LuauPlugin.LuauGetLuauPluginVersion();
	[NonSerialized] [SyncVar] public LuauPlugin.LuauBytecodeVersion bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
	[NonSerialized] [SyncVar] public string serverPlayerVersion = "";

	[NonSerialized]
	public readonly SyncDictionary<LuauContext, List<LuauPlugin.LuauMemoryCategoryDumpItem>> ServerMemDump = new();
	private readonly Dictionary<LuauContext, float> _lastServerMemDumpUpdate = new() {
		[LuauContext.Game] = 0,
		[LuauContext.Protected] = 0,
	};

	[NonSerialized] [SyncVar] public ulong ServerUnityObjects = LuauPlugin.LuauGetUnityObjectCount();
	
	private const float MinServerMemDumpUpdateInterval = 1; 

	private void OnServerInitialized() {
		luauPluginVersion = LuauPlugin.LuauGetLuauPluginVersion();
		bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
		serverPlayerVersion = AirshipVersion.GetVersionHash();
		ServerUnityObjects = LuauPlugin.LuauGetUnityObjectCount();
	}

	[Command(requiresAuthority = false)]
	public void FetchServerMemoryDump(LuauContext context) {
		var now = Time.unscaledTime;
		if (now - _lastServerMemDumpUpdate[context] < MinServerMemDumpUpdateInterval) {
			return;
		}
		_lastServerMemDumpUpdate[context] = now;
		
		if (!ServerMemDump.TryGetValue(context, out var dump)) {
			dump = new List<LuauPlugin.LuauMemoryCategoryDumpItem>();
			ServerMemDump.Add(context, dump);
		}
		
		LuauPlugin.LuauGetMemoryCategoryDump(context, dump);
		ServerMemDump[context] = dump; // Force update
		
		ServerUnityObjects = LuauPlugin.LuauGetUnityObjectCount();
	}
}
