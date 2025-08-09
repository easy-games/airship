using System;
using System.IO;
using Luau;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum LuauScriptCacheMode {
	NotCached,
	Cached,
}

[LuauAPI(LuauContext.Protected)]
public class LuauScript : MonoBehaviour {
	private const bool ElevateToProtectedWithinCoreScene = true;
	
	public static AwakeData QueuedAwakeData = null;
    
	// Injected from LuauHelper
	public static IAssetBridge AssetBridge;
	
	public AirshipScript script;

	public IntPtr thread;
	[NonSerialized] public LuauContext context = LuauContext.Game;
	[HideInInspector] public bool forceContext = false;
	
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void OnReload() {
		QueuedAwakeData = null;
	}
	
	private static string CleanupFilePath(string path) {
		var extension = Path.GetExtension(path);
		if (extension == string.Empty) {
			path += ".lua";
		}

		if (path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) {
			path = path.Substring("assets/".Length);
		}
		
		return path;
	}

	public static AirshipScript LoadAirshipScriptFromPath(string fullFilePath) {
		var cleanPath = CleanupFilePath(fullFilePath);
#if UNITY_EDITOR && !AIRSHIP_PLAYER
		return AssetDatabase.LoadAssetAtPath<AirshipScript>("Assets/" + cleanPath.Replace(".lua", ".ts")) 
		       ?? AssetDatabase.LoadAssetAtPath<AirshipScript>("Assets/" + cleanPath);
#else
		if (AssetBridge != null && AssetBridge.IsLoaded()) {
			var luaPath = cleanPath.Replace(".ts", ".lua");
			return AssetBridge.LoadAssetInternal<AirshipScript>(luaPath);
		}

		throw new Exception("AssetBridge not loaded");
#endif
	}

	public static LuauScript Create(GameObject go, string scriptPath, LuauContext context, bool forceContext) {
		var script = LoadAirshipScriptFromPath(scriptPath);
		if (script == null) {
			throw new Exception($"Failed to load script from file: {scriptPath}");
		}

		return Create(go, script, context, forceContext);
	}

	public static LuauScript Create(GameObject go, AirshipScript script, LuauContext context, bool forceContext) {
		var awakeData = new AwakeData() {
			Script = script,
			Context = context,
			ForceContext = forceContext,
		};
		QueuedAwakeData = awakeData;
		
		var luauScript = go.AddComponent<LuauScript>();

		// If QueuedAwakeData is still set, then the LuauScript didn't awake right away.
		// In this case, just set it ourselves. This would happen if the GameObject was
		// in an inactive state.
		if (QueuedAwakeData == awakeData) {
			QueuedAwakeData = null;
			luauScript.script = script;
			luauScript.context = context;
			luauScript.forceContext = forceContext;
		}
		
		return luauScript;
	}

	private void Awake() {
		if (QueuedAwakeData != null) {
			script = QueuedAwakeData.Script;
			context = QueuedAwakeData.Context;
			forceContext = QueuedAwakeData.ForceContext;
			QueuedAwakeData = null;
		}

		// Assume protected context for bindings within CoreScene
		// if (!forceContext && ((gameObject.scene.name is "CoreScene" or "MainMenu") || (SceneManager.GetActiveScene().name is "CoreScene" or "MainMenu")) && ElevateToProtectedWithinCoreScene) {
		if (!forceContext && gameObject.scene.name is "CoreScene" or "MainMenu" && ElevateToProtectedWithinCoreScene) {
			context = LuauContext.Protected;
		}
		
		thread = LoadAndExecuteScript(gameObject, context, LuauScriptCacheMode.NotCached, script, out var status);
	}

	/// <summary>
	/// Creates a new Luau thread from the given script. The thread is not yet executed. Returns a nullptr if the Luau
	/// fails to create the new thread.
	/// </summary>
	public static IntPtr LoadScript(GameObject obj, LuauContext context, LuauScriptCacheMode cacheMode, AirshipScript script) {
		if (ReferenceEquals(script, null)) {
			throw new Exception("[LuauScript]: Script reference is null");
		}

		if (!script.m_compiled) {
			throw new Exception($"[LuauScript]: Script reference ({script.name}) cannot run due to compilation error: {script.m_compilationError}");
		}
		
		var cleanPath = CleanupFilePath(script.m_path);
		var id = ThreadDataManager.GetOrCreateObjectId(obj);
		var nativeCodegen = script.HasDirective("native");
		
		// Tell Luau to load the bytecode onto a new Luau thread:
		switch (cacheMode) {
			case LuauScriptCacheMode.NotCached:
				return LuauPlugin.LuauCreateThread(context, script.m_bytes, cleanPath, id, nativeCodegen);
			case LuauScriptCacheMode.Cached:
				var requirePath = LuauCore.GetRequirePath(script.m_path, cleanPath);
				return LuauPlugin.LuauCreateThreadWithCachedModule(context, requirePath, id);
			default:
				throw new Exception($"[LuauScript]: Unhandled mode: {cacheMode}");
		}
	}

	/// <summary>
	/// Execute a thread. The thread must first be created with the LoadScript function.
	/// </summary>
	public static int ExecuteScript(IntPtr thread) {
		// Execute the new thread. We don't need to do anything after this. If the thread errors, the error will be
		// outputted. If the thread yields, whoever yielded it owns responsibility for resuming it (e.g. the task
		// scheduler):
		return LuauPlugin.LuauRunThread(thread);
	}

	/// <summary>
	/// Loads and executes the given script. The executed Luau thread is returned. If the thread is a nullptr, then
	/// that indicates that Luau failed to load the script.
	/// </summary>
	public static IntPtr LoadAndExecuteScript(GameObject obj, LuauContext context, LuauScriptCacheMode cacheMode, AirshipScript script, out int status) {
		Profiler.BeginSample("LoadAndExecuteScript");
		status = -1;
		
		var thread = LoadScript(obj, context, cacheMode, script);

		var shouldCacheValue = thread == IntPtr.Zero && cacheMode == LuauScriptCacheMode.Cached;
		if (shouldCacheValue) {
			thread = LoadScript(obj, context, LuauScriptCacheMode.NotCached, script);
		}
		
		// A nullptr indicates that Luau failed to load the bytecode. Luau will write the error to the output, and we
		// need to check for the nullptr and stop here:
		if (thread == IntPtr.Zero) {
			Debug.LogError($"[LuauScript] Failed to create Luau thread for script: {script.m_path}");
			Profiler.EndSample();
			return thread;
		}

		if (cacheMode == LuauScriptCacheMode.Cached && !shouldCacheValue) {
			status = 0;
		} else {
			status = ExecuteScript(thread);
		}

		if (shouldCacheValue && status == 0) {
			var requirePath = LuauCore.GetRequirePath(script.m_path, CleanupFilePath(script.m_path));
			LuauPlugin.LuauCacheModuleOnThread(thread, requirePath);
		}

		Profiler.EndSample();
		return thread;
	}

	public class AwakeData {
		public AirshipScript Script;
		public LuauContext Context;
		public bool ForceContext;
	}
}
