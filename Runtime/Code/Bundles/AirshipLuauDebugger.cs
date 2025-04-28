using System;
using System.Collections.Generic;
using System.Text;
using Code.Bundles;
using Luau;
using Mirror;
using UnityEngine;

public class AirshipLuauDebugger : NetworkBehaviour {
	[NonSerialized] [SyncVar] public string luauPluginVersion = LuauPlugin.LuauGetLuauPluginVersion();
	[NonSerialized] [SyncVar] public LuauPlugin.LuauBytecodeVersion bytecodeVersion = LuauPlugin.LuauGetBytecodeVersion();
	[NonSerialized] [SyncVar] public string serverPlayerVersion = "";
	[NonSerialized] [SyncVar(hook = nameof(OnLuauObjectsDebugStringChanged))] public string luauObjectsDebugString = "";

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

	[Command(requiresAuthority = false)]
	public void FetchServerLuauInstanceIds(LuauContext context) {
		luauObjectsDebugString = "SERVER " + FetchLuauUnityInstanceIds(context);
	}
	
	public static string FetchLuauUnityInstanceIds(LuauContext context) {
		var instanceIds = LuauPlugin.LuauDebugGetAllTrackedInstanceIds(context);
		
		var sb = new StringBuilder("OBJECTS:\n");
			
		// Count objects by unique name/type:
		var countByName = new Dictionary<string, int>();
		foreach (var instanceId in instanceIds) {
			var obj = ThreadDataManager.GetObjectReference(IntPtr.Zero, instanceId, true, true);
			if (obj is UnityEngine.Object unityObj) {
				var t = unityObj.GetType();
				var n = "(Destroyed)";
				if (unityObj != null) {
					n = unityObj.name;
				} else {
					var cachedName = ThreadDataManager.GetObjectReferenceName_TEMP_DEBUG(instanceId);
					if (cachedName != null) {
						n = cachedName + " (Destroyed)";
					}
				}
				var objName = $"[{t.Name}] {n}";
				if (!countByName.TryAdd(objName, 1)) {
					countByName[objName]++;
				}
			}
		}
			
		// Include top 20:
		for (var i = 0; i < 20; i++) {
			if (countByName.Count == 0) break;
				
			// Find top item:
			var topKey = "";
			var topCount = 0;
			foreach (var (key, count) in countByName) {
				if (count > topCount) {
					topKey = key;
					topCount = count;
				}
			}
				
			sb.AppendLine($"{topKey}: {topCount}");
			countByName.Remove(topKey);
		}

		return sb.ToString();
	}

	private void OnLuauObjectsDebugStringChanged(string oldStr, string newStr) {
		// Log on the client when the value changes:
		Debug.Log(newStr);
	}
}
