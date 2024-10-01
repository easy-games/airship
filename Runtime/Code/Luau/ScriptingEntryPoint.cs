using System;
using Luau;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Assets.Code.Luau {
	public class ScriptingEntryPoint : MonoBehaviour {
		public static bool IsLoaded = false;
		public static event Action onScriptBindingRun;

		public static void InvokeOnLuauStartup() {
			onScriptBindingRun?.Invoke();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void OnLoad() {
			IsLoaded = false;
		}

		private const string CoreEntryScript = "assets/airshippackages/@easy/core/shared/corebootstrap.ts";
		private const string MainMenuEntryScript = "assets/airshippackages/@easy/core/shared/mainmenuingame.ts";
		
		private void Awake() {
			if (IsLoaded) return;

			onScriptBindingRun += StartCoreScripts;
		}

		private void OnDestroy() {
			onScriptBindingRun -= StartCoreScripts;
		}

		private void StartCoreScripts() {
			if (IsLoaded) return;

			IsLoaded = true;
			DontDestroyOnLoad(this);

			var coreCamera = GameObject.Find("AirshipCoreSceneCamera");
			if (coreCamera && coreCamera.scene.name == "CoreScene") {
				Object.Destroy(coreCamera);
			}

			LuauCore.CoreInstance.CheckSetup();

			// Main Menu
			{
				var go = new GameObject("MainMenuInGame");
				var binding = go.AddComponent<AirshipRuntimeScript>();
				binding.SetScriptFromPath(MainMenuEntryScript, LuauContext.Protected, true);
				// binding.contextOverwritten = true;
				// binding.InitEarly();
			}

			// Core
			{
				var go = new GameObject("@Easy/Core");
				var binding = go.AddComponent<AirshipRuntimeScript>();
				binding.SetScriptFromPath(CoreEntryScript, LuauContext.Game, true);
				// binding.contextOverwritten = true;
				// binding.InitEarly();
			}
		}
	}
}
