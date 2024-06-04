using Luau;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Code.Luau {
	public class ScriptingEntryPoint : MonoBehaviour {
		public static bool IsLoaded = false;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		public static void OnLoad() {
			IsLoaded = false;
		}

		private const string CoreEntryScript = "airshippackages/@easy/core/shared/corebootstrap.ts";
		private const string MainMenuEntryScript = "airshippackages/@easy/core/shared/mainmenuingame.ts";
		
		private void Awake() {
			LuauCore.CoreInstance.CheckSetup();

			if (IsLoaded) return;
			IsLoaded = true;
			DontDestroyOnLoad(this);
			
			var coreCamera = GameObject.Find("AirshipCoreSceneCamera");
			if (coreCamera && coreCamera.scene.name == "CoreScene") {
				Object.Destroy(coreCamera);
			}

			var gameBindings = GetComponentsInChildren<ScriptBinding>();

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

			foreach (var binding in gameBindings) {
				binding.context = LuauContext.Game;
				binding.contextOverwritten = true;
				binding.InitEarly();
			}
		}
	}
}
