using System;
using Luau;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Assets.Code.Luau {
	public class ScriptingEntryPoint : MonoBehaviour {
		public static bool IsLoaded = false;
		public static event Action onLuauStartup;

		public static void InvokeOnLuauStartup() {
			onLuauStartup?.Invoke();
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void OnLoad() {
			IsLoaded = false;
		}

		private const string CoreEntryScript = "assets/airshippackages/@easy/core/shared/corebootstrap.ts";
		private const string MainMenuEntryScript = "assets/airshippackages/@easy/core/shared/mainmenuingame.ts";
		
		private void Awake() {
			LuauCore.CoreInstance.CheckSetup();
			if (IsLoaded) return;

			// SceneManager.activeSceneChanged += SceneManager_ActiveSceneChanged;
			onLuauStartup += StartCoreScripts;
		}

		private void OnDestroy() {
			// SceneManager.activeSceneChanged -= SceneManager_ActiveSceneChanged;
			onLuauStartup -= StartCoreScripts;
		}

		private void StartCoreScripts() {
			if (IsLoaded) return;

			IsLoaded = true;
			DontDestroyOnLoad(this);

			var coreCamera = GameObject.Find("AirshipCoreSceneCamera");
			if (coreCamera && coreCamera.scene.name == "CoreScene") {
				Object.Destroy(coreCamera);
			}

			print($"Loading scripts. Active scene: {SceneManager.GetActiveScene().name}");

			// Main Menu
			{
				var go = new GameObject("MainMenuInGame");
				var binding = go.AddComponent<ScriptBinding>();

				binding.SetScriptFromPath(MainMenuEntryScript, LuauContext.Protected);
				binding.contextOverwritten = true;
				binding.InitEarly();
			}

			// Core
			{
				var go = new GameObject("@Easy/Core");
				var binding = go.AddComponent<ScriptBinding>();

				binding.SetScriptFromPath(CoreEntryScript, LuauContext.Game);
				binding.contextOverwritten = true;
				binding.InitEarly();
			}
		}

		private void SceneManager_ActiveSceneChanged(Scene oldScene, Scene newScene) {
			if (oldScene.name != "CoreScene") return;

			this.StartCoreScripts();
		}
	}
}
