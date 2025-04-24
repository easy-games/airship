using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Assets.Code.Luau {
	public class ScriptingEntryPoint : MonoBehaviour {
		public static bool IsLoaded = false;
		public static event Action OnScriptBindingRun;

		public static void InvokeOnLuauStartup() {
			OnScriptBindingRun?.Invoke();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void OnLoad() {
			IsLoaded = false;
		}

		private const string CoreEntryScript = "assets/airshippackages/@easy/core/shared/corebootstrap.ts";
		private const string MainMenuEntryScript = "assets/airshippackages/@easy/core/shared/mainmenuingame.ts";
		
		private void Awake() {
			if (IsLoaded) return;

			OnScriptBindingRun += StartCoreScripts;
		}

		private void OnDestroy() {
			OnScriptBindingRun -= StartCoreScripts;
		}

		private void StartCoreScripts() {
			if (IsLoaded) return;

			IsLoaded = true;
			DontDestroyOnLoad(this);

			var coreCamera = GameObject.Find("AirshipCoreSceneCamera");
			if (coreCamera && coreCamera.scene.name == "CoreScene") {
				Destroy(coreCamera);
			}
			
			// Main Menu
			var stopwatch = Stopwatch.StartNew();
			{
				var go = new GameObject("MainMenuInGame");
				LuauScript.Create(go, MainMenuEntryScript, LuauContext.Protected, true);
			}

			// Core
			{
				var go = new GameObject("@Easy/Core");
				LuauScript.Create(go, CoreEntryScript, LuauContext.Game, true);
			}
			stopwatch.Stop();
			// Debug.Log($"ScriptingEntryPoint elapsed time: {stopwatch.ElapsedMilliseconds}ms");
		}
	}
}
