using System;
using Luau;
using UnityEngine;

public class AirshipComponentV2 : MonoBehaviour {
	public AirshipScript script;

	public IntPtr thread;
	[HideInInspector] public LuauContext context = LuauContext.Game;
	
	private void Awake() {
		// Load the component onto the thread:
		thread = LuauScript.LoadAndExecuteScript(gameObject, context, LuauScriptCacheMode.Cached, script);
		if (thread == IntPtr.Zero) {
			thread = LuauScript.LoadAndExecuteScript(gameObject, context, LuauScriptCacheMode.NotCached, script);
		}

		if (thread == IntPtr.Zero) {
			// Failed to load the component
			Debug.LogError($"Component failed to load: {script.m_path}");
			return;
		}
		
		print("AirshipComponentV2::Awake");
	}
}
